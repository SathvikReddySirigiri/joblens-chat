using JobLens.Infrastructure.Security;
using JobLens.Application.Common;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Hosting;

using JobLens.Application.Interfaces;

namespace JobLens.Infrastructure.Persistence;

public static partial class DatabaseInitializer
{
    private static async Task EnsureDescriptionColumnAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var exists = false;
        await using (var pragma = connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA table_info(ScamReports);";
            await using var reader = await pragma.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                if (string.Equals(reader.GetString(1), "Description", StringComparison.OrdinalIgnoreCase))
                {
                    exists = true;
                    break;
                }
            }
        }

        if (exists)
        {
            return;
        }

        await using var alter = connection.CreateCommand();
        alter.CommandText = "ALTER TABLE ScamReports ADD COLUMN Description TEXT NOT NULL DEFAULT '';";
        await alter.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureOwnershipColumnsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        // Nullable owner so existing seed rows stay NULL until a signed-in user creates a record.
        await AddColumnIfMissingAsync(
            connection,
            "ScamReports",
            "CreatedByUserId",
            "ALTER TABLE ScamReports ADD COLUMN CreatedByUserId INTEGER NULL REFERENCES Users(UserId);",
            cancellationToken);
        await AddColumnIfMissingAsync(
            connection,
            "InterviewReviews",
            "CreatedByUserId",
            "ALTER TABLE InterviewReviews ADD COLUMN CreatedByUserId INTEGER NULL REFERENCES Users(UserId);",
            cancellationToken);
    }

    private static Task EnsureCheckLogUserIdAsync(SqliteConnection connection, CancellationToken cancellationToken) =>
        AddColumnIfMissingAsync(
            connection,
            "CheckLogs",
            "UserId",
            "ALTER TABLE CheckLogs ADD COLUMN UserId INTEGER NULL REFERENCES Users(UserId);",
            cancellationToken);

    private static async Task AddColumnIfMissingAsync(
        SqliteConnection connection,
        string table,
        string column,
        string alterSql,
        CancellationToken cancellationToken)
    {
        var exists = false;
        await using (var pragma = connection.CreateCommand())
        {
            pragma.CommandText = $"PRAGMA table_info({table});";
            await using var reader = await pragma.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                {
                    exists = true;
                    break;
                }
            }
        }

        if (exists)
        {
            return;
        }

        await using var alter = connection.CreateCommand();
        alter.CommandText = alterSql;
        await alter.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Resolves demo passwords. Development may fall back to documented defaults;
    /// non-Development must set Seed:ModeratorPassword and Seed:StudentPassword.
    /// </summary>
    internal static (string ModeratorPassword, string StudentPassword) DemoPasswords(
        IConfiguration configuration,
        IWebHostEnvironment environment,
        ILogger logger)
    {
        var moderator = configuration["Seed:ModeratorPassword"];
        var student = configuration["Seed:StudentPassword"];
        var missing = string.IsNullOrWhiteSpace(moderator) || string.IsNullOrWhiteSpace(student);

        if (missing
            && !string.Equals(
                environment.EnvironmentName,
                Environments.Development,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Set Seed__ModeratorPassword and Seed__StudentPassword in App Settings");
        }

        var usedFallback = false;
        if (string.IsNullOrWhiteSpace(moderator))
        {
            moderator = "Moderator#2026";
            usedFallback = true;
        }

        if (string.IsNullOrWhiteSpace(student))
        {
            student = "Student#2026";
            usedFallback = true;
        }

        if (usedFallback)
        {
            logger.LogWarning(
                "Demo user passwords are the documented class defaults. Set Seed:ModeratorPassword and Seed:StudentPassword before a real deployment.");
        }

        return (moderator, student);
    }

    private static Task SeedRecruitersIfEmptyAsync(SqliteConnection connection, CancellationToken cancellationToken) =>
        SeedIfEmptyAsync(connection, "SELECT COUNT(*) FROM Recruiters;", InsertRecruitersAsync, cancellationToken);

    private static async Task SeedUsersIfEmptyAsync(
        SqliteConnection connection,
        string moderatorPassword,
        string studentPassword,
        CancellationToken cancellationToken)
    {
        if (await ScalarAsync(connection, "SELECT COUNT(*) FROM Users;", cancellationToken) > 0)
        {
            return;
        }

        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await InsertUsersAsync(connection, transaction, moderatorPassword, studentPassword, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task BackfillDescriptionsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        // Fill only blank descriptions so existing report facts stay exactly as seeded.
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE ScamReports
            SET Description = CASE ScamType
                WHEN 'Fake check' THEN 'The recruiter mailed a check and asked the student to buy equipment, then wire back the difference. The check was not from the company.'
                WHEN 'Equipment purchase' THEN 'The message told the student to buy a laptop from a named seller before the start date. The seller was not the company''s vendor.'
                WHEN 'Upfront fee' THEN 'The sender required a training or badge fee before any interview. The company careers page lists no such fee.'
                WHEN 'Chat-only interview' THEN 'Every step stayed in a private chat and never moved to a company video call. No employee could be found on the official site.'
                WHEN 'Identity theft' THEN 'The sender asked for a government ID and bank details during the first screening. A real campus recruiter does not collect those up front.'
                WHEN 'Fake staffing agency' THEN 'A supposed agency used the company name but had no listing on the official careers page. The contact domain did not match the company.'
                ELSE Description
            END
            WHERE Description = '';
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task SeedJobPostingsIfEmptyAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await SeedIfEmptyAsync(connection, "SELECT COUNT(*) FROM JobPostings;", InsertPostingsAsync, cancellationToken);
    }

    private static async Task SeedReviewsIfEmptyAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await SeedIfEmptyAsync(connection, "SELECT COUNT(*) FROM InterviewReviews;", InsertReviewsAsync, cancellationToken);
    }

    private static async Task SeedCheckLogsIfEmptyAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await SeedIfEmptyAsync(connection, "SELECT COUNT(*) FROM CheckLogs;", InsertCheckLogsAsync, cancellationToken);
    }

    private static async Task SeedJobApplicationsIfEmptyAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        if (await ScalarAsync(connection, "SELECT COUNT(*) FROM JobApplications;", cancellationToken) > 0)
        {
            return;
        }

        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await InsertJobApplicationsAsync(connection, transaction, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task InsertJobApplicationsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        int? studentId = null;
        await using (var lookup = connection.CreateCommand())
        {
            lookup.Transaction = transaction;
            lookup.CommandText = "SELECT UserId FROM Users WHERE Email = 'student1@joblens.demo' LIMIT 1;";
            var value = await lookup.ExecuteScalarAsync(cancellationToken);
            if (value is not null and not DBNull)
            {
                studentId = Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        if (studentId is null)
        {
            return;
        }

        var companyIds = await LoadCompanyIdsAsync(connection, transaction, cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO JobApplications (
                UserId, CompanyName, CompanyId, RoleTitle, Source, JobUrl, RecruiterEmail,
                Status, AppliedDate, NextFollowUpDate, Notes, RiskVerdict, ClosedDate, CreatedAt, UpdatedAt)
            VALUES (
                $userId, $companyName, $companyId, $roleTitle, $source, $jobUrl, $recruiterEmail,
                $status, $appliedDate, $nextFollowUpDate, $notes, $riskVerdict, $closedDate, $createdAt, $updatedAt);
            """;
        var userId = command.Parameters.Add("$userId", SqliteType.Integer);
        var companyName = command.Parameters.Add("$companyName", SqliteType.Text);
        var companyId = command.Parameters.Add("$companyId", SqliteType.Integer);
        var roleTitle = command.Parameters.Add("$roleTitle", SqliteType.Text);
        var source = command.Parameters.Add("$source", SqliteType.Text);
        var jobUrl = command.Parameters.Add("$jobUrl", SqliteType.Text);
        var recruiterEmail = command.Parameters.Add("$recruiterEmail", SqliteType.Text);
        var status = command.Parameters.Add("$status", SqliteType.Text);
        var appliedDate = command.Parameters.Add("$appliedDate", SqliteType.Text);
        var nextFollowUpDate = command.Parameters.Add("$nextFollowUpDate", SqliteType.Text);
        var notes = command.Parameters.Add("$notes", SqliteType.Text);
        var riskVerdict = command.Parameters.Add("$riskVerdict", SqliteType.Text);
        var closedDate = command.Parameters.Add("$closedDate", SqliteType.Text);
        var createdAt = command.Parameters.Add("$createdAt", SqliteType.Text);
        var updatedAt = command.Parameters.Add("$updatedAt", SqliteType.Text);

        // Demo pipeline for student1: mixed statuses, two High-risk recruiter emails from seeded scam domains.
        var rows = new (string Company, string Role, string Source, string? Url, string? Email, string Status, string? Applied, string? FollowUp, string? Notes, string? Risk, string? Closed)[]
        {
            ("Amazon", "Backend Intern", "JobLensPosting", "https://amazon.jobs/intern", "talent@amazon.com", "Interviewing", "2026-10-12", "2026-11-05", "Phone screen scheduled.", "Low", null),
            ("Microsoft", "Software Engineer Intern", "LinkedIn", "https://careers.microsoft.com", "hr@amaz0n-careers.net", "Applied", "2026-10-20", "2026-11-01", "Recruiter email looked off — verify domain.", "High", null),
            ("Google", "STEP Intern", "Handshake", null, "campus@google.com", "Applied", "2026-10-28", "2026-11-10", null, "Low", null),
            ("Deloitte", "Technology Consultant Intern", "Referral", null, null, "Saved", null, "2026-11-08", "Ask alumni contact for intro.", null, null),
            ("JPMorgan Chase", "Summer Analyst", "CompanyWebsite", "https://careers.jpmorgan.com", "recruiting@jpmorgan.com", "Offer", "2026-09-15", null, "Offer letter received.", "Low", "2026-10-30"),
            ("Meta", "Data Science Intern", "LiveJobs", "https://www.metacareers.com", null, "NoResponse", "2026-09-01", "2026-10-15", "No reply after two follow-ups.", null, null),
            ("Apple", "Hardware Intern", "Other", null, "jobs@rnicrosoft-hiring.com", "Rejected", "2026-08-20", null, "Suspicious recruiter domain.", "High", "2026-09-12"),
            ("IBM", "Cloud Intern", "JobLensPosting", null, "campus@ibm.com", "Interviewing", "2026-10-05", "2026-11-12", "Final round next week.", "Low", null),
            ("Salesforce", "AMTS Intern", "LinkedIn", "https://salesforce.com/careers", null, "Withdrawn", "2026-09-22", null, "Withdrew after accepting Chase.", null, "2026-10-31"),
            ("Adobe", "Product Design Intern", "Handshake", null, "university@adobe.com", "Applied", "2026-11-02", "2026-11-16", null, "Low", null),
            ("Oracle", "Database Intern", "LiveJobs", null, null, "Saved", null, null, "Save for spring cycle.", null, null),
            ("Cisco", "Network Engineer Intern", "Referral", null, "internships@cisco.com", "Applied", "2026-10-18", "2026-11-06", "Follow up with referral.", "Low", null),
        };

        foreach (var row in rows)
        {
            userId.Value = studentId.Value;
            companyName.Value = row.Company;
            companyId.Value = companyIds.TryGetValue(row.Company, out var linkedId) ? linkedId : (object)DBNull.Value;
            roleTitle.Value = row.Role;
            source.Value = row.Source;
            jobUrl.Value = (object?)row.Url ?? DBNull.Value;
            recruiterEmail.Value = (object?)row.Email ?? DBNull.Value;
            status.Value = row.Status;
            appliedDate.Value = (object?)row.Applied ?? DBNull.Value;
            nextFollowUpDate.Value = (object?)row.FollowUp ?? DBNull.Value;
            notes.Value = (object?)row.Notes ?? DBNull.Value;
            riskVerdict.Value = (object?)row.Risk ?? DBNull.Value;
            closedDate.Value = (object?)row.Closed ?? DBNull.Value;
            createdAt.Value = "2026-10-01";
            updatedAt.Value = "2026-11-01";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task SeedIfEmptyAsync(
        SqliteConnection connection,
        string countSql,
        Func<SqliteConnection, SqliteTransaction, CancellationToken, Task> insert,
        CancellationToken cancellationToken)
    {
        if (await ScalarAsync(connection, countSql, cancellationToken) > 0)
        {
            return;
        }

        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await insert(connection, transaction, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task InsertPostingsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        var companyIds = await LoadCompanyIdsAsync(connection, transaction, cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO JobPostings (
                CompanyId, Title, Location, State, IsRemote, IsPaid, PayRatePerHour, Description, PostedDate)
            VALUES (
                $companyId, $title, $location, $state, $remote, $paid, $pay, $description, $postedDate);
            """;
        var companyId = command.Parameters.Add("$companyId", SqliteType.Integer);
        var title = command.Parameters.Add("$title", SqliteType.Text);
        var location = command.Parameters.Add("$location", SqliteType.Text);
        var state = command.Parameters.Add("$state", SqliteType.Text);
        var remote = command.Parameters.Add("$remote", SqliteType.Integer);
        var paid = command.Parameters.Add("$paid", SqliteType.Integer);
        var pay = command.Parameters.Add("$pay", SqliteType.Real);
        var description = command.Parameters.Add("$description", SqliteType.Text);
        var postedDate = command.Parameters.Add("$postedDate", SqliteType.Text);

        foreach (var row in SeedCatalog.Document.JobPostings)
        {
            companyId.Value = RequireCompany(companyIds, row.CompanyName);
            title.Value = row.Title;
            location.Value = row.Location;
            state.Value = row.State;
            remote.Value = row.IsRemote;
            paid.Value = row.IsPaid;
            pay.Value = (object?)row.PayRatePerHour ?? DBNull.Value;
            description.Value = row.Description;
            postedDate.Value = row.PostedDate;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task InsertReviewsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        var companyIds = await LoadCompanyIdsAsync(connection, transaction, cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO InterviewReviews (CompanyId, Role, Rating, Experience, WasLegit, ReviewDate)
            VALUES ($companyId, $role, $rating, $experience, $legit, $reviewDate);
            """;
        var companyId = command.Parameters.Add("$companyId", SqliteType.Integer);
        var role = command.Parameters.Add("$role", SqliteType.Text);
        var rating = command.Parameters.Add("$rating", SqliteType.Integer);
        var experience = command.Parameters.Add("$experience", SqliteType.Text);
        var legit = command.Parameters.Add("$legit", SqliteType.Integer);
        var reviewDate = command.Parameters.Add("$reviewDate", SqliteType.Text);

        foreach (var row in SeedCatalog.Document.InterviewReviews)
        {
            companyId.Value = RequireCompany(companyIds, row.CompanyName);
            role.Value = row.Role;
            rating.Value = row.Rating;
            experience.Value = row.Experience;
            legit.Value = row.WasLegit;
            reviewDate.Value = row.ReviewDate;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task InsertCheckLogsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO CheckLogs (CheckType, InputSummary, RiskScore, Verdict, CreatedAt)
            VALUES ($type, $summary, $score, $verdict, $createdAt);
            """;
        var type = command.Parameters.Add("$type", SqliteType.Text);
        var summary = command.Parameters.Add("$summary", SqliteType.Text);
        var score = command.Parameters.Add("$score", SqliteType.Integer);
        var verdict = command.Parameters.Add("$verdict", SqliteType.Text);
        var createdAt = command.Parameters.Add("$createdAt", SqliteType.Text);

        foreach (var row in SeedCatalog.Document.CheckLogs)
        {
            type.Value = row.CheckType;
            summary.Value = row.InputSummary;
            verdict.Value = row.Verdict;
            score.Value = row.RiskScore;
            createdAt.Value = row.CreatedAt;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task<Dictionary<string, int>> LoadCompanyIdsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        var companyIds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        await using var lookup = connection.CreateCommand();
        lookup.Transaction = transaction;
        lookup.CommandText = "SELECT CompanyId, Name FROM Companies;";
        await using var reader = await lookup.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            companyIds[reader.GetString(1)] = reader.GetInt32(0);
        }

        return companyIds;
    }

    private static async Task InsertRecruitersAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using (var fromReports = connection.CreateCommand())
        {
            fromReports.Transaction = transaction;
            fromReports.CommandText = """
                INSERT INTO Recruiters (Email, Domain, CompanyId, FirstSeen, Notes)
                SELECT LOWER(RecruiterEmail), LOWER(Domain), MIN(CompanyId), MIN(ReportedDate), ''
                FROM ScamReports
                GROUP BY LOWER(RecruiterEmail);
                """;
            await fromReports.ExecuteNonQueryAsync(cancellationToken);
        }

        var companyIds = await LoadCompanyIdsByDomainAsync(connection, transaction, cancellationToken);
        await using var official = connection.CreateCommand();
        official.Transaction = transaction;
        official.CommandText = """
            INSERT INTO Recruiters (Email, Domain, CompanyId, FirstSeen, Notes)
            VALUES ($email, $domain, $companyId, $firstSeen, $notes);
            """;
        var email = official.Parameters.Add("$email", SqliteType.Text);
        var domain = official.Parameters.Add("$domain", SqliteType.Text);
        var companyId = official.Parameters.Add("$companyId", SqliteType.Integer);
        var firstSeen = official.Parameters.Add("$firstSeen", SqliteType.Text);
        var notes = official.Parameters.Add("$notes", SqliteType.Text);

        foreach (var row in SeedCatalog.Document.OfficialRecruiters)
        {
            email.Value = row.Email;
            domain.Value = row.CompanyDomain;
            companyId.Value = RequireCompany(companyIds, row.CompanyDomain);
            firstSeen.Value = row.FirstSeen;
            notes.Value = row.Notes;
            await official.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task InsertUsersAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string moderatorPassword,
        string studentPassword,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO Users (Email, DisplayName, PasswordHash, Role, CreatedAt)
            VALUES ($email, $name, $hash, $role, $createdAt);
            """;
        var email = command.Parameters.Add("$email", SqliteType.Text);
        var name = command.Parameters.Add("$name", SqliteType.Text);
        var hash = command.Parameters.Add("$hash", SqliteType.Text);
        var role = command.Parameters.Add("$role", SqliteType.Text);
        var createdAt = command.Parameters.Add("$createdAt", SqliteType.Text);

        foreach (var account in SeedCatalog.Document.Users)
        {
            var password = account.PasswordSlot == "moderator" ? moderatorPassword : studentPassword;
            email.Value = account.Email;
            name.Value = account.DisplayName;
            hash.Value = UserPassword.Hash(account.Email, password);
            role.Value = account.Role;
            createdAt.Value = "2026-01-01";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task<Dictionary<string, int>> LoadCompanyIdsByDomainAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        var companyIds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        await using var lookup = connection.CreateCommand();
        lookup.Transaction = transaction;
        lookup.CommandText = "SELECT CompanyId, OfficialDomain FROM Companies;";
        await using var reader = await lookup.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            companyIds[reader.GetString(1)] = reader.GetInt32(0);
        }

        return companyIds;
    }

    private static int RequireCompany(Dictionary<string, int> companyIds, string name)
    {
        if (!companyIds.TryGetValue(name, out var id))
        {
            throw new InvalidOperationException($"Seed row names an unknown company '{name}'.");
        }

        return id;
    }
}
