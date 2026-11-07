using JobLens.Domain.Entities;
using JobLens.Application.Dtos;
using Microsoft.Data.Sqlite;

using JobLens.Application.Interfaces;

namespace JobLens.Infrastructure.Persistence;

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
        // Touch the embedded seed before any insert so a drifted file fails startup even when the database already exists.
        _ = SeedCatalog.Document;

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
        var (moderatorPassword, studentPassword) = DemoPasswords(configuration, environment, logger);
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

    public static async Task ResetAsync(
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
        var seed = SeedCatalog.Document;

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

            foreach (var company in seed.Companies)
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
                AmountRequestedUsd, DomainAgeDays, Status, ReportedDate, Description)
            VALUES (
                $companyId, $email, $domain, $scamType, $channel,
                $amount, $age, $status, $reportedDate, $description);
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
        var description = insertReport.Parameters.Add("$description", SqliteType.Text);

        foreach (var report in seed.Reports)
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
            description.Value = report.Description;
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

    private static string DomainOf(string email)
    {
        var at = email.LastIndexOf('@');
        return at < 0 ? "" : email[(at + 1)..].ToLowerInvariant();
    }
}
