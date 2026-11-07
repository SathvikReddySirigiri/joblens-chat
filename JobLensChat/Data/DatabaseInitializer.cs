using JobLensChat.Models;
using Microsoft.Data.Sqlite;

namespace JobLensChat.Data;

public static partial class DatabaseInitializer
{
    public static async Task InitializeAsync(
        IConfiguration configuration,
        IWebHostEnvironment environment,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var path = DatabasePath.Resolve(configuration, environment);
        var connectionString = $"Data Source={path};Foreign Keys=True";

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await CreateSchemaAsync(connection, cancellationToken);

        var companyCount = await ScalarAsync(connection, "SELECT COUNT(*) FROM Companies;", cancellationToken);
        if (companyCount == 0)
        {
            // One transaction so a failed seed cannot leave a half-written database.
            await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
            await SeedAsync(connection, transaction, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        // New tables seed on their own so an existing 10-company database still receives them.
        await BackfillDescriptionsAsync(connection, cancellationToken);
        await SeedJobPostingsIfEmptyAsync(connection, cancellationToken);
        await SeedReviewsIfEmptyAsync(connection, cancellationToken);
        await SeedCheckLogsIfEmptyAsync(connection, cancellationToken);
        await SeedRecruitersIfEmptyAsync(connection, cancellationToken);
        var (moderatorPassword, studentPassword) = DemoPasswords(configuration, logger);
        await SeedUsersIfEmptyAsync(connection, moderatorPassword, studentPassword, cancellationToken);

        var counts = await ReadCountsAsync(connection, cancellationToken);
        logger.LogInformation(
            "JobLens database ready at {DatabasePath}. Companies={Companies}, Recruiters={Recruiters}, Reports={Reports}, Approved={Approved}, Pending={Pending}, Rejected={Rejected}, JobPostings={JobPostings}, InterviewReviews={InterviewReviews}, CheckLogs={CheckLogs}, Users={Users}.",
            path,
            counts.Companies,
            counts.Recruiters,
            counts.ScamReports,
            counts.Approved,
            counts.Pending,
            counts.Rejected,
            counts.JobPostings,
            counts.InterviewReviews,
            counts.CheckLogs,
            counts.Users);
    }

    private static async Task CreateSchemaAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Companies (
                CompanyId INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL UNIQUE,
                OfficialDomain TEXT NOT NULL UNIQUE,
                Industry TEXT NOT NULL,
                HqState TEXT NOT NULL CHECK (length(HqState) = 2)
            );

            CREATE TABLE IF NOT EXISTS ScamReports (
                ReportId INTEGER PRIMARY KEY AUTOINCREMENT,
                CompanyId INTEGER NOT NULL REFERENCES Companies(CompanyId),
                RecruiterEmail TEXT NOT NULL,
                Domain TEXT NOT NULL,
                ScamType TEXT NOT NULL CHECK (ScamType IN ('Fake check','Equipment purchase','Upfront fee','Chat-only interview','Identity theft','Fake staffing agency')),
                Channel TEXT NOT NULL CHECK (Channel IN ('Email','Telegram','WhatsApp','LinkedIn','Text message')),
                AmountRequestedUsd REAL NOT NULL CHECK (AmountRequestedUsd >= 0),
                DomainAgeDays INTEGER NOT NULL CHECK (DomainAgeDays >= 0),
                Status TEXT NOT NULL CHECK (Status IN ('Pending','Approved','Rejected')),
                ReportedDate TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS IX_ScamReports_Domain ON ScamReports(Domain);
            CREATE INDEX IF NOT EXISTS IX_ScamReports_CompanyId ON ScamReports(CompanyId);
            CREATE INDEX IF NOT EXISTS IX_ScamReports_ReportedDate ON ScamReports(ReportedDate);

            CREATE TABLE IF NOT EXISTS JobPostings (
                PostingId INTEGER PRIMARY KEY AUTOINCREMENT,
                CompanyId INTEGER NOT NULL REFERENCES Companies(CompanyId) ON DELETE RESTRICT,
                Title TEXT NOT NULL,
                Location TEXT NOT NULL,
                State TEXT NOT NULL CHECK (length(State) = 2),
                IsRemote INTEGER NOT NULL CHECK (IsRemote IN (0, 1)),
                IsPaid INTEGER NOT NULL CHECK (IsPaid IN (0, 1)),
                PayRatePerHour REAL NULL CHECK (PayRatePerHour IS NULL OR PayRatePerHour >= 0),
                Description TEXT NOT NULL,
                PostedDate TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS InterviewReviews (
                ReviewId INTEGER PRIMARY KEY AUTOINCREMENT,
                CompanyId INTEGER NOT NULL REFERENCES Companies(CompanyId) ON DELETE RESTRICT,
                Role TEXT NOT NULL,
                Rating INTEGER NOT NULL CHECK (Rating BETWEEN 1 AND 5),
                Experience TEXT NOT NULL,
                WasLegit INTEGER NOT NULL CHECK (WasLegit IN (0, 1)),
                ReviewDate TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS CheckLogs (
                CheckId INTEGER PRIMARY KEY AUTOINCREMENT,
                CheckType TEXT NOT NULL CHECK (CheckType IN ('Recruiter', 'Offer')),
                InputSummary TEXT NOT NULL,
                RiskScore INTEGER NOT NULL CHECK (RiskScore BETWEEN 0 AND 100),
                Verdict TEXT NOT NULL CHECK (Verdict IN ('LOW', 'MEDIUM', 'HIGH')),
                CreatedAt TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS IX_JobPostings_CompanyId ON JobPostings(CompanyId);
            CREATE INDEX IF NOT EXISTS IX_JobPostings_PostedDate ON JobPostings(PostedDate);
            CREATE INDEX IF NOT EXISTS IX_InterviewReviews_CompanyId ON InterviewReviews(CompanyId);
            CREATE INDEX IF NOT EXISTS IX_InterviewReviews_ReviewDate ON InterviewReviews(ReviewDate);
            CREATE INDEX IF NOT EXISTS IX_CheckLogs_CreatedAt ON CheckLogs(CreatedAt);

            CREATE TABLE IF NOT EXISTS Recruiters (
                RecruiterId INTEGER PRIMARY KEY AUTOINCREMENT,
                Email TEXT NOT NULL UNIQUE,
                Domain TEXT NOT NULL,
                CompanyId INTEGER NULL REFERENCES Companies(CompanyId) ON DELETE SET NULL,
                FirstSeen TEXT NOT NULL,
                Notes TEXT NOT NULL DEFAULT ''
            );

            CREATE TABLE IF NOT EXISTS Users (
                UserId INTEGER PRIMARY KEY AUTOINCREMENT,
                Email TEXT NOT NULL UNIQUE,
                DisplayName TEXT NOT NULL,
                PasswordHash TEXT NOT NULL,
                Role TEXT NOT NULL CHECK (Role IN ('Student', 'Moderator')),
                CreatedAt TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS IX_Recruiters_CompanyId ON Recruiters(CompanyId);
            CREATE INDEX IF NOT EXISTS IX_Recruiters_Domain ON Recruiters(Domain);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
        await EnsureDescriptionColumnAsync(connection, cancellationToken);
        await EnsureOwnershipColumnsAsync(connection, cancellationToken);
    }

    internal static async Task CreateEmptyDatabaseAsync(string path, CancellationToken cancellationToken = default)
    {
        var folder = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(folder))
        {
            Directory.CreateDirectory(folder);
        }

        await using var connection = new SqliteConnection(SqliteConnections.ForPath(path));
        await connection.OpenAsync(cancellationToken);
        await CreateSchemaAsync(connection, cancellationToken);
    }

    internal static async Task ResetAsync(
        IConfiguration configuration,
        IWebHostEnvironment environment,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var path = DatabasePath.Resolve(configuration, environment);
        // Pool handles keep the file locked on Windows after the last request closes.
        SqliteConnection.ClearAllPools();
        foreach (var candidate in new[] { path, path + "-wal", path + "-shm" })
        {
            if (File.Exists(candidate))
            {
                File.Delete(candidate);
            }
        }

        await InitializeAsync(configuration, environment, logger, cancellationToken);
    }

    private static async Task SeedAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        EnsureSeedRules();

        await using (var insertCompany = connection.CreateCommand())
        {
            insertCompany.Transaction = transaction;
            insertCompany.CommandText = """
                INSERT INTO Companies (Name, OfficialDomain, Industry, HqState)
                VALUES ($name, $domain, $industry, $state);
                """;
            var name = insertCompany.Parameters.Add("$name", SqliteType.Text);
            var domain = insertCompany.Parameters.Add("$domain", SqliteType.Text);
            var industry = insertCompany.Parameters.Add("$industry", SqliteType.Text);
            var state = insertCompany.Parameters.Add("$state", SqliteType.Text);

            foreach (var company in CompanySeed)
            {
                name.Value = company.Name;
                domain.Value = company.OfficialDomain;
                industry.Value = company.Industry;
                state.Value = company.HqState;
                await insertCompany.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        var companyIds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        await using (var lookup = connection.CreateCommand())
        {
            lookup.Transaction = transaction;
            lookup.CommandText = "SELECT CompanyId, Name FROM Companies;";
            await using var reader = await lookup.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                companyIds[reader.GetString(1)] = reader.GetInt32(0);
            }
        }

        await using var insertReport = connection.CreateCommand();
        insertReport.Transaction = transaction;
        insertReport.CommandText = """
            INSERT INTO ScamReports (
                CompanyId, RecruiterEmail, Domain, ScamType, Channel,
                AmountRequestedUsd, DomainAgeDays, Status, ReportedDate)
            VALUES (
                $companyId, $email, $domain, $scamType, $channel,
                $amount, $age, $status, $reportedDate);
            """;
        var companyId = insertReport.Parameters.Add("$companyId", SqliteType.Integer);
        var email = insertReport.Parameters.Add("$email", SqliteType.Text);
        var reportDomain = insertReport.Parameters.Add("$domain", SqliteType.Text);
        var scamType = insertReport.Parameters.Add("$scamType", SqliteType.Text);
        var channel = insertReport.Parameters.Add("$channel", SqliteType.Text);
        var amount = insertReport.Parameters.Add("$amount", SqliteType.Real);
        var age = insertReport.Parameters.Add("$age", SqliteType.Integer);
        var status = insertReport.Parameters.Add("$status", SqliteType.Text);
        var reportedDate = insertReport.Parameters.Add("$reportedDate", SqliteType.Text);

        foreach (var report in ReportSeed)
        {
            if (!companyIds.TryGetValue(report.CompanyName, out var id))
            {
                throw new InvalidOperationException($"Seed report names an unknown company '{report.CompanyName}'.");
            }

            companyId.Value = id;
            email.Value = report.RecruiterEmail;
            // Store the host from the address so Domain and RecruiterEmail cannot drift apart.
            reportDomain.Value = DomainOf(report.RecruiterEmail);
            scamType.Value = report.ScamType;
            channel.Value = report.Channel;
            amount.Value = report.AmountRequestedUsd;
            age.Value = report.DomainAgeDays;
            status.Value = report.Status;
            reportedDate.Value = report.ReportedDate;
            await insertReport.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task<DataCounts> ReadCountsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                (SELECT COUNT(*) FROM Companies),
                (SELECT COUNT(*) FROM ScamReports),
                (SELECT COUNT(*) FROM ScamReports WHERE Status = 'Approved'),
                (SELECT COUNT(*) FROM ScamReports WHERE Status = 'Pending'),
                (SELECT COUNT(*) FROM ScamReports WHERE Status = 'Rejected'),
                (SELECT COUNT(*) FROM JobPostings),
                (SELECT COUNT(*) FROM InterviewReviews),
                (SELECT COUNT(*) FROM CheckLogs),
                (SELECT COUNT(*) FROM Recruiters),
                (SELECT COUNT(*) FROM Users);
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return new DataCounts(
            reader.GetInt32(0),
            reader.GetInt32(1),
            reader.GetInt32(2),
            reader.GetInt32(3),
            reader.GetInt32(4),
            reader.GetInt32(5),
            reader.GetInt32(6),
            reader.GetInt32(7),
            reader.GetInt32(8),
            reader.GetInt32(9));
    }

    private static async Task<int> ScalarAsync(SqliteConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static bool InRange(string value, string start, string end) =>
        string.CompareOrdinal(value, start) >= 0 && string.CompareOrdinal(value, end) <= 0;

    private static string DomainOf(string email)
    {
        var at = email.LastIndexOf('@');
        return at < 0 ? "" : email[(at + 1)..].ToLowerInvariant();
    }

    private static void EnsureSeedRules()
    {
        // Fail startup if the hardcoded rows drift from the known answers the tests rely on.
        var problems = new List<string>();
        if (CompanySeed.Length != 10)
        {
            problems.Add($"Expected 10 companies, found {CompanySeed.Length}.");
        }

        if (ReportSeed.Length != 60)
        {
            problems.Add($"Expected 60 reports, found {ReportSeed.Length}.");
        }

        var expected = new Dictionary<string, (int Approved, int Pending, int Rejected)>(StringComparer.OrdinalIgnoreCase)
        {
            ["Amazon"] = (9, 2, 1),
            ["Microsoft"] = (8, 1, 1),
            ["Deloitte"] = (6, 1, 1),
            ["Google"] = (5, 2, 0),
            ["JPMorgan Chase"] = (4, 1, 1),
            ["Walmart"] = (3, 1, 1),
            ["Accenture"] = (2, 2, 0),
            ["Raymond James"] = (2, 0, 1),
            ["Publix"] = (1, 1, 1),
            ["Moffitt Cancer Center"] = (1, 1, 0),
        };

        foreach (var company in expected)
        {
            var rows = ReportSeed.Where(report => report.CompanyName == company.Key).ToArray();
            var approved = rows.Count(report => report.Status == "Approved");
            var pending = rows.Count(report => report.Status == "Pending");
            var rejected = rows.Count(report => report.Status == "Rejected");
            if (approved != company.Value.Approved || pending != company.Value.Pending || rejected != company.Value.Rejected)
            {
                problems.Add($"{company.Key} status counts are {approved}/{pending}/{rejected}.");
            }
        }

        void ExpectDomain(string domain, int approved, int pending, int rejected)
        {
            var rows = ReportSeed.Where(report => DomainOf(report.RecruiterEmail) == domain).ToArray();
            if (rows.Count(report => report.Status == "Approved") != approved
                || rows.Count(report => report.Status == "Pending") != pending
                || rows.Count(report => report.Status == "Rejected") != rejected)
            {
                problems.Add($"Domain {domain} does not match {approved}/{pending}/{rejected}.");
            }
        }

        ExpectDomain("amaz0n-careers.net", 3, 1, 0);
        ExpectDomain("rnicrosoft-hiring.com", 3, 0, 0);
        ExpectDomain("deloitte-recruitment-hr.com", 2, 1, 0);
        ExpectDomain("google-careers-team.net", 2, 0, 0);

        var forbidden = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "amazon.com", "microsoft.com", "google.com", "safe-company-example.com",
        };
        if (ReportSeed.Any(report => forbidden.Contains(DomainOf(report.RecruiterEmail))))
        {
            problems.Add("A forbidden official or unused domain appears in the seed.");
        }

        var janMar = ReportSeed.Count(report => InRange(report.ReportedDate, "2026-01-01", "2026-03-31"));
        var aprJun = ReportSeed.Count(report => InRange(report.ReportedDate, "2026-04-01", "2026-06-30"));
        var july = ReportSeed.Count(report => InRange(report.ReportedDate, "2026-07-01", "2026-07-31"));
        var augOct = ReportSeed.Count(report => InRange(report.ReportedDate, "2026-08-01", "2026-10-31"));
        if (janMar != 4 || aprJun != 8 || july != 12 || augOct != 36)
        {
            problems.Add($"Date buckets are {janMar}/{aprJun}/{july}/{augOct}, expected 4/8/12/36.");
        }

        if (ReportSeed.Any(report => string.CompareOrdinal(report.ReportedDate, "2026-01-05") < 0
            || string.CompareOrdinal(report.ReportedDate, "2026-10-30") > 0))
        {
            problems.Add("A report date is outside 2026-01-05 through 2026-10-30.");
        }

        var equipment = ReportSeed.Count(report =>
            report.ScamType == "Equipment purchase"
            && report.Status == "Approved"
            && report.ReportedDate.CompareTo("2026-09-01") >= 0);
        if (equipment < 4)
        {
            problems.Add($"Only {equipment} approved equipment-purchase reports are dated on or after 2026-09-01.");
        }

        var deloitteTelegram = ReportSeed.Count(report => report.CompanyName == "Deloitte" && report.Channel == "Telegram");
        if (deloitteTelegram < 2)
        {
            problems.Add($"Only {deloitteTelegram} Deloitte Telegram reports.");
        }

        var freeProviders = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "gmail.com", "outlook.com", "yahoo.com" };
        foreach (var report in ReportSeed)
        {
            var domain = DomainOf(report.RecruiterEmail);
            var moneyType = report.ScamType is "Identity theft" or "Chat-only interview";
            if (moneyType && report.AmountRequestedUsd != 0)
            {
                problems.Add($"{report.RecruiterEmail} should request $0.");
            }

            if (!moneyType && (report.AmountRequestedUsd < 50 || report.AmountRequestedUsd > 3000))
            {
                problems.Add($"{report.RecruiterEmail} amount is outside 50–3000.");
            }

            if (freeProviders.Contains(domain))
            {
                if (report.DomainAgeDays is < 1000 or > 8000)
                {
                    problems.Add($"{domain} domain age {report.DomainAgeDays} is outside 1000–8000.");
                }
            }
            else if (report.DomainAgeDays is < 3 or > 60)
            {
                problems.Add($"{domain} domain age {report.DomainAgeDays} is outside 3–60.");
            }
        }

        foreach (var type in new[] { "Fake check", "Equipment purchase", "Upfront fee", "Chat-only interview", "Identity theft", "Fake staffing agency" })
        {
            var count = ReportSeed.Count(report => report.ScamType == type);
            if (count < 3)
            {
                problems.Add($"Scam type {type} appears {count} times.");
            }
        }

        foreach (var channel in new[] { "Email", "Telegram", "WhatsApp", "LinkedIn", "Text message" })
        {
            var count = ReportSeed.Count(report => report.Channel == channel);
            if (count < 3)
            {
                problems.Add($"Channel {channel} appears {count} times.");
            }
        }

        if (problems.Count > 0)
        {
            throw new InvalidOperationException("Scam report seed is inconsistent: " + string.Join(" ", problems));
        }
    }

    private readonly record struct CompanyRow(string Name, string OfficialDomain, string Industry, string HqState);

    private readonly record struct ReportRow(
        string CompanyName,
        string RecruiterEmail,
        string ScamType,
        string Channel,
        double AmountRequestedUsd,
        int DomainAgeDays,
        string Status,
        string ReportedDate);

    private static readonly CompanyRow[] CompanySeed =
    [
        new("Amazon", "amazon.com", "Technology", "WA"),
        new("Microsoft", "microsoft.com", "Technology", "WA"),
        new("Google", "google.com", "Technology", "CA"),
        new("Deloitte", "deloitte.com", "Consulting", "NY"),
        new("JPMorgan Chase", "jpmorgan.com", "Finance", "NY"),
        new("Walmart", "walmart.com", "Retail", "AR"),
        new("Accenture", "accenture.com", "Consulting", "IL"),
        new("Raymond James", "raymondjames.com", "Finance", "FL"),
        new("Publix", "publix.com", "Retail", "FL"),
        new("Moffitt Cancer Center", "moffitt.org", "Healthcare", "FL"),
    ];

    private static readonly ReportRow[] ReportSeed =
    [
        new("Amazon", "hr@amaz0n-careers.net", "Equipment purchase", "Email", 1200, 9, "Approved", "2026-09-01"),
        new("Amazon", "recruiting@amaz0n-careers.net", "Fake check", "Email", 850, 12, "Approved", "2026-09-08"),
        new("Amazon", "talent@amaz0n-careers.net", "Upfront fee", "LinkedIn", 400, 6, "Approved", "2026-10-03"),
        new("Amazon", "jobs@amaz0n-careers.net", "Fake staffing agency", "Telegram", 600, 15, "Pending", "2026-08-05"),
        new("Amazon", "recruiter@amazon-jobs-now.net", "Equipment purchase", "WhatsApp", 2100, 22, "Approved", "2026-09-15"),
        new("Amazon", "hr@amazon-hiring-desk.com", "Fake check", "Text message", 1500, 30, "Approved", "2026-10-10"),
        new("Amazon", "careers@amzn-opportunity.net", "Chat-only interview", "Telegram", 0, 18, "Approved", "2026-07-06"),
        new("Amazon", "apply@amazon-remote-roles.com", "Identity theft", "Email", 0, 45, "Approved", "2026-08-12"),
        new("Amazon", "staff@amazon-workforce.net", "Upfront fee", "LinkedIn", 250, 28, "Approved", "2026-10-17"),
        new("Amazon", "hello@amazon-talent-team.com", "Fake staffing agency", "WhatsApp", 900, 11, "Approved", "2026-08-21"),
        new("Amazon", "info@gmail.com", "Chat-only interview", "Email", 0, 2400, "Pending", "2026-07-16"),
        new("Amazon", "alerts@outlook.com", "Fake check", "Text message", 700, 1800, "Rejected", "2026-04-07"),
        new("Microsoft", "hr@rnicrosoft-hiring.com", "Equipment purchase", "Email", 1800, 8, "Approved", "2026-09-12"),
        new("Microsoft", "recruiting@rnicrosoft-hiring.com", "Fake check", "LinkedIn", 950, 14, "Approved", "2026-10-08"),
        new("Microsoft", "talent@rnicrosoft-hiring.com", "Upfront fee", "Telegram", 320, 5, "Approved", "2026-08-14"),
        new("Microsoft", "jobs@microsoft-careers-mail.com", "Chat-only interview", "WhatsApp", 0, 20, "Approved", "2026-07-21"),
        new("Microsoft", "hr@ms-hiring-portal.net", "Identity theft", "Email", 0, 40, "Approved", "2026-08-24"),
        new("Microsoft", "apply@microsoft-opportunity.com", "Fake staffing agency", "Text message", 1100, 33, "Approved", "2026-10-15"),
        new("Microsoft", "careers@outlook.com", "Equipment purchase", "Email", 640, 3200, "Approved", "2026-09-22"),
        new("Microsoft", "recruiter@microsoft-talent-now.net", "Fake check", "LinkedIn", 480, 26, "Approved", "2026-06-02"),
        new("Microsoft", "info@yahoo.com", "Upfront fee", "WhatsApp", 150, 4500, "Pending", "2026-05-06"),
        new("Microsoft", "alerts@gmail.com", "Chat-only interview", "Telegram", 0, 5100, "Rejected", "2026-03-09"),
        new("Deloitte", "hr@deloitte-recruitment-hr.com", "Equipment purchase", "Telegram", 1600, 7, "Approved", "2026-09-19"),
        new("Deloitte", "recruiting@deloitte-recruitment-hr.com", "Fake check", "Email", 880, 16, "Approved", "2026-10-20"),
        new("Deloitte", "talent@deloitte-recruitment-hr.com", "Upfront fee", "Telegram", 275, 10, "Pending", "2026-08-17"),
        new("Deloitte", "jobs@deloitte-careers-desk.com", "Fake staffing agency", "LinkedIn", 1400, 24, "Approved", "2026-10-01"),
        new("Deloitte", "hr@deloitte-hiring-team.net", "Chat-only interview", "WhatsApp", 0, 19, "Approved", "2026-07-25"),
        new("Deloitte", "apply@deloitte-opportunity.com", "Identity theft", "Email", 0, 38, "Approved", "2026-08-26"),
        new("Deloitte", "careers@deloitte-staffing.net", "Fake check", "Text message", 520, 42, "Approved", "2026-06-11"),
        new("Deloitte", "recruiter@yahoo.com", "Upfront fee", "LinkedIn", 90, 6200, "Rejected", "2026-02-18"),
        new("Google", "hr@google-careers-team.net", "Equipment purchase", "Email", 2400, 4, "Approved", "2026-09-26"),
        new("Google", "recruiting@google-careers-team.net", "Fake check", "LinkedIn", 1300, 13, "Approved", "2026-10-24"),
        new("Google", "jobs@google-hiring-mail.com", "Upfront fee", "WhatsApp", 360, 21, "Approved", "2026-08-03"),
        new("Google", "talent@g00gle-careers.net", "Chat-only interview", "Telegram", 0, 17, "Approved", "2026-07-29"),
        new("Google", "apply@google-opportunity-team.com", "Identity theft", "Email", 0, 50, "Approved", "2026-08-28"),
        new("Google", "careers@gmail.com", "Fake staffing agency", "Text message", 750, 2100, "Pending", "2026-07-09"),
        new("Google", "recruiter@outlook.com", "Fake check", "Email", 410, 3900, "Pending", "2026-04-22"),
        new("JPMorgan Chase", "hr@jpmorgan-talent-hub.com", "Equipment purchase", "Email", 1900, 11, "Approved", "2026-10-06"),
        new("JPMorgan Chase", "recruiting@jpmorgan-careers-mail.net", "Fake check", "LinkedIn", 720, 29, "Approved", "2026-09-03"),
        new("JPMorgan Chase", "talent@jpmchase-hiring.com", "Upfront fee", "WhatsApp", 210, 35, "Approved", "2026-08-07"),
        new("JPMorgan Chase", "jobs@jpmorgan-opportunity.net", "Chat-only interview", "Telegram", 0, 8, "Approved", "2026-07-13"),
        new("JPMorgan Chase", "apply@gmail.com", "Identity theft", "Email", 0, 7000, "Pending", "2026-06-20"),
        new("JPMorgan Chase", "careers@jpmorgan-staff.net", "Fake staffing agency", "Text message", 980, 44, "Rejected", "2026-05-19"),
        new("Walmart", "hr@walmart-hiring-center.net", "Equipment purchase", "Email", 540, 23, "Approved", "2026-09-05"),
        new("Walmart", "recruiting@walmart-careers-desk.com", "Fake check", "WhatsApp", 1100, 31, "Approved", "2026-10-13"),
        new("Walmart", "talent@walmart-jobs-now.com", "Upfront fee", "LinkedIn", 180, 12, "Approved", "2026-08-10"),
        new("Walmart", "jobs@outlook.com", "Chat-only interview", "Telegram", 0, 1500, "Pending", "2026-07-18"),
        new("Walmart", "apply@walmart-opportunity.net", "Fake staffing agency", "Text message", 860, 27, "Rejected", "2026-01-12"),
        new("Accenture", "hr@accenture-talent-mail.com", "Equipment purchase", "Email", 1700, 6, "Approved", "2026-09-10"),
        new("Accenture", "recruiting@accenture-hiring-team.net", "Fake check", "LinkedIn", 640, 36, "Approved", "2026-10-22"),
        new("Accenture", "talent@gmail.com", "Upfront fee", "WhatsApp", 125, 2800, "Pending", "2026-07-02"),
        new("Accenture", "jobs@accenture-careers-hub.com", "Chat-only interview", "Telegram", 0, 19, "Pending", "2026-08-19"),
        new("Raymond James", "hr@raymondjames-careers.net", "Identity theft", "Email", 0, 14, "Approved", "2026-06-28"),
        new("Raymond James", "recruiting@rj-hiring-desk.com", "Fake staffing agency", "LinkedIn", 1250, 41, "Approved", "2026-10-28"),
        new("Raymond James", "talent@yahoo.com", "Fake check", "Text message", 300, 3600, "Rejected", "2026-08-31"),
        new("Publix", "hr@publix-hiring-team.com", "Equipment purchase", "WhatsApp", 430, 25, "Approved", "2026-09-17"),
        new("Publix", "recruiting@outlook.com", "Upfront fee", "Email", 75, 4100, "Pending", "2026-07-23"),
        new("Publix", "jobs@publix-careers-mail.net", "Fake staffing agency", "Text message", 640, 48, "Rejected", "2026-07-27"),
        new("Moffitt Cancer Center", "hr@moffitt-careers-desk.org", "Chat-only interview", "Telegram", 0, 9, "Approved", "2026-07-31"),
        new("Moffitt Cancer Center", "recruiting@yahoo.com", "Identity theft", "LinkedIn", 0, 5500, "Pending", "2026-03-20"),
    ];
}
