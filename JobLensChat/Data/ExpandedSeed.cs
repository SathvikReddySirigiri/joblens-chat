namespace JobLensChat.Data;

internal static class ExpandedSeed
{
    internal readonly record struct PostingRow(
        string CompanyName,
        string Title,
        string Location,
        string State,
        int IsRemote,
        int IsPaid,
        double? PayRatePerHour,
        string PostedDate);

    internal readonly record struct ReviewRow(
        string CompanyName,
        string Role,
        int Rating,
        string Experience,
        int WasLegit,
        string ReviewDate);

    internal static readonly PostingRow[] Postings =
    [
        new("Amazon", "Data Analyst Intern", "Seattle, WA", "WA", 0, 1, 32, "2026-06-03"),
        new("Amazon", "Software Engineering Intern", "Austin, TX", "TX", 1, 1, 40, "2026-08-14"),
        new("Amazon", "Business Analyst", "Seattle, WA", "WA", 0, 1, 27, "2026-10-02"),
        new("Microsoft", "Cybersecurity Intern", "Redmond, WA", "WA", 0, 1, 38, "2026-06-18"),
        new("Microsoft", "Software Engineer", "Austin, TX", "TX", 1, 1, 45, "2026-09-09"),
        new("Microsoft", "Data Analyst Intern", "Redmond, WA", "WA", 0, 0, null, "2026-07-07"),
        new("Google", "Software Engineering Intern", "Mountain View, CA", "CA", 0, 1, 42, "2026-06-22"),
        new("Google", "Cybersecurity Intern", "Austin, TX", "TX", 1, 1, 36, "2026-09-15"),
        new("Google", "Business Analyst", "San Francisco, CA", "CA", 0, 1, 30, "2026-10-20"),
        new("Deloitte", "Business Analyst", "New York, NY", "NY", 0, 1, 29, "2026-06-11"),
        new("Deloitte", "Data Analyst Intern", "Chicago, IL", "IL", 1, 1, 24, "2026-08-03"),
        new("Deloitte", "Cybersecurity Intern", "New York, NY", "NY", 0, 1, 33, "2026-10-08"),
        new("JPMorgan Chase", "Business Analyst", "New York, NY", "NY", 0, 1, 34, "2026-07-01"),
        new("JPMorgan Chase", "Cybersecurity Intern", "Tampa, FL", "FL", 1, 1, 31, "2026-09-01"),
        new("JPMorgan Chase", "Data Analyst", "New York, NY", "NY", 0, 1, 37, "2026-10-16"),
        new("Walmart", "Data Analyst Intern", "Bentonville, AR", "AR", 0, 1, 22, "2026-06-25"),
        new("Walmart", "Business Analyst", "Dallas, TX", "TX", 1, 1, 21, "2026-08-19"),
        new("Walmart", "Software Engineering Intern", "Bentonville, AR", "AR", 0, 0, null, "2026-09-28"),
        new("Accenture", "Business Analyst", "Chicago, IL", "IL", 0, 1, 28, "2026-07-14"),
        new("Accenture", "Cybersecurity Intern", "Chicago, IL", "IL", 0, 1, 30, "2026-08-27"),
        new("Accenture", "Data Analyst Intern", "Austin, TX", "TX", 1, 1, 26, "2026-10-12"),
        new("Raymond James", "Business Analyst", "St. Petersburg, FL", "FL", 0, 1, 25, "2026-06-30"),
        new("Raymond James", "Data Analyst Intern", "Tampa, FL", "FL", 0, 1, 20, "2026-09-04"),
        new("Raymond James", "Cybersecurity Intern", "St. Petersburg, FL", "FL", 1, 1, 27, "2026-10-23"),
        new("Publix", "Data Analyst Intern", "Lakeland, FL", "FL", 0, 1, 19, "2026-07-20"),
        new("Publix", "Business Analyst", "Lakeland, FL", "FL", 0, 0, null, "2026-08-11"),
        new("Publix", "Software Engineering Intern", "Jacksonville, FL", "FL", 1, 1, 23, "2026-10-05"),
        new("Moffitt Cancer Center", "Data Analyst Intern", "Tampa, FL", "FL", 0, 1, 24, "2026-06-08"),
        new("Moffitt Cancer Center", "Cybersecurity Intern", "Tampa, FL", "FL", 0, 1, 26, "2026-09-18"),
        new("Moffitt Cancer Center", "Business Analyst", "Tampa, FL", "FL", 1, 0, null, "2026-10-27"),
    ];

    internal static readonly ReviewRow[] Reviews =
    [
        new("Amazon", "Data Analyst Intern", 5, "A structured loop with a real hiring manager and a written offer later.", 1, "2026-03-12"),
        new("Amazon", "Warehouse Associate", 2, "The chat never left Telegram and the sender avoided the official site.", 0, "2026-08-04"),
        new("Microsoft", "Software Engineering Intern", 4, "Two video rounds and a recruiter who used a microsoft.com address.", 1, "2026-04-16"),
        new("Microsoft", "Support Engineer", 5, "The panel matched the posting on the company careers page.", 1, "2026-09-02"),
        new("Google", "Business Analyst", 4, "A campus recruiter followed up from the official hiring portal.", 1, "2026-05-07"),
        new("Google", "Data Analyst Intern", 1, "They asked for a fee before scheduling any interview.", 0, "2026-07-19"),
        new("Deloitte", "Consulting Analyst", 5, "Case interview plus a call with a manager listed on the firm site.", 1, "2026-03-28"),
        new("Deloitte", "Cybersecurity Intern", 3, "The process was slow but the interviewers were real employees.", 1, "2026-10-06"),
        new("JPMorgan Chase", "Business Analyst", 4, "Superday in New York matched the role posted for summer 2026.", 1, "2026-04-02"),
        new("JPMorgan Chase", "Operations Analyst", 5, "Background check came only after a written offer letter.", 1, "2026-08-21"),
        new("Walmart", "Data Analyst Intern", 4, "On-site Bentonville interview with a team that matched the posting.", 1, "2026-06-11"),
        new("Walmart", "Software Engineering Intern", 2, "The offer PDF used a look-alike domain instead of walmart.com.", 0, "2026-09-14"),
        new("Accenture", "Business Analyst", 5, "Recruiter scheduled a video interview from the campus portal.", 1, "2026-05-22"),
        new("Accenture", "Cybersecurity Intern", 4, "Technical screen covered tools the job description named.", 1, "2026-10-09"),
        new("Raymond James", "Finance Intern", 4, "St. Petersburg office visit and a follow-up from raymondjames.com.", 1, "2026-03-18"),
        new("Raymond James", "Operations Analyst", 3, "Phone screen was legitimate, then the role was filled.", 1, "2026-07-30"),
        new("Publix", "Business Analyst", 5, "Store-support interview in Lakeland with a corporate recruiter.", 1, "2026-06-25"),
        new("Publix", "Data Analyst Intern", 4, "They confirmed the internship on the Publix careers site.", 1, "2026-09-29"),
        new("Moffitt Cancer Center", "Research Intern", 5, "Hospital volunteer office confirmed the interview slot.", 1, "2026-04-09"),
        new("Moffitt Cancer Center", "Data Analyst Intern", 4, "Panel asked about coursework and never requested payment.", 1, "2026-10-15"),
    ];

    internal static readonly string[] CheckDates =
    [
        "2026-05-04", "2026-05-12", "2026-05-21",
        "2026-06-02", "2026-06-09", "2026-06-16", "2026-06-25",
        "2026-07-03", "2026-07-10", "2026-07-17", "2026-07-24", "2026-07-30",
        "2026-08-04", "2026-08-08", "2026-08-12", "2026-08-16", "2026-08-20", "2026-08-25", "2026-08-29",
        "2026-09-02", "2026-09-05", "2026-09-08", "2026-09-11", "2026-09-15", "2026-09-18", "2026-09-22", "2026-09-26", "2026-09-29",
        "2026-10-01", "2026-10-03", "2026-10-06", "2026-10-08", "2026-10-10", "2026-10-13", "2026-10-15", "2026-10-18", "2026-10-21", "2026-10-24", "2026-10-27", "2026-10-30",
    ];

    internal static readonly string[] CheckDomains =
    [
        "amaz0n-careers.net",
        "gmail.com",
        "rnicrosoft-hiring.com",
        "outlook.com",
        "deloitte-recruitment-hr.com",
        "yahoo.com",
        "google-careers-team.net",
        "walmart-hiring-center.net",
        "jpmorgan-talent-hub.com",
        "publix-hiring-team.com",
    ];
}
