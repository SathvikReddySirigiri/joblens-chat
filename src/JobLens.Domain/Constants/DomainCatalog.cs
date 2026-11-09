namespace JobLens.Domain.Constants;

public static class Paging
{
    public const int PageSize = 15;
}

public static class ScamTypes
{
    public static readonly string[] All =
    [
        "Fake check", "Equipment purchase", "Upfront fee", "Chat-only interview", "Identity theft", "Fake staffing agency",
    ];
}

public static class Channels
{
    public static readonly string[] All =
    [
        "Email", "Telegram", "WhatsApp", "LinkedIn", "Text message",
    ];
}

public static class ReportStatus
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";

    public static readonly string[] All = [Pending, Approved, Rejected];
}

public static class Verdicts
{
    public const string Low = "LOW";
    public const string Medium = "MEDIUM";
    public const string High = "HIGH";

    public static readonly string[] All = [Low, Medium, High];
}

public static class ApplicationSources
{
    public const string LiveJobs = "LiveJobs";
    public const string JobLensPosting = "JobLensPosting";
    public const string Handshake = "Handshake";
    public const string LinkedIn = "LinkedIn";
    public const string Referral = "Referral";
    public const string CompanyWebsite = "CompanyWebsite";
    public const string Other = "Other";

    public static readonly string[] All =
    [
        LiveJobs, JobLensPosting, Handshake, LinkedIn, Referral, CompanyWebsite, Other,
    ];
}

public static class ApplicationStatuses
{
    public const string Saved = "Saved";
    public const string Applied = "Applied";
    public const string Interviewing = "Interviewing";
    public const string Offer = "Offer";
    public const string Rejected = "Rejected";
    public const string Withdrawn = "Withdrawn";
    public const string NoResponse = "NoResponse";

    public static readonly string[] All =
    [
        Saved, Applied, Interviewing, Offer, Rejected, Withdrawn, NoResponse,
    ];

    public static readonly string[] Closed = [Offer, Rejected, Withdrawn];

    public static bool IsClosed(string status) =>
        Closed.Contains(status, StringComparer.OrdinalIgnoreCase);
}

public static class ApplicationRiskVerdicts
{
    public const string Low = "Low";
    public const string Medium = "Medium";
    public const string High = "High";

    public static readonly string[] All = [Low, Medium, High];

    public static string? FromRecruiterVerdict(string? verdict) =>
        string.Equals(verdict, Verdicts.Low, StringComparison.OrdinalIgnoreCase) ? Low
        : string.Equals(verdict, Verdicts.Medium, StringComparison.OrdinalIgnoreCase) ? Medium
        : string.Equals(verdict, Verdicts.High, StringComparison.OrdinalIgnoreCase) ? High
        : null;
}

public static class EmailDraftTypes
{
    public const string FollowUpAfterApplying = "FollowUpAfterApplying";
    public const string ThankYouAfterInterview = "ThankYouAfterInterview";
    public const string AskForInterviewDetails = "AskForInterviewDetails";
    public const string VerifyRecruiter = "VerifyRecruiter";
    public const string AcceptOffer = "AcceptOffer";
    public const string DeclineOffer = "DeclineOffer";
    public const string WithdrawApplication = "WithdrawApplication";
    public const string ReportScamToCareerCenter = "ReportScamToCareerCenter";

    public static readonly string[] All =
    [
        VerifyRecruiter,
        ReportScamToCareerCenter,
        FollowUpAfterApplying,
        ThankYouAfterInterview,
        AskForInterviewDetails,
        AcceptOffer,
        DeclineOffer,
        WithdrawApplication,
    ];

    public static string DisplayName(string type) => type switch
    {
        FollowUpAfterApplying => "Follow up after applying",
        ThankYouAfterInterview => "Thank you after interview",
        AskForInterviewDetails => "Ask for interview details",
        VerifyRecruiter => "Verify recruiter",
        AcceptOffer => "Accept offer",
        DeclineOffer => "Decline offer",
        WithdrawApplication => "Withdraw application",
        ReportScamToCareerCenter => "Report scam to career center",
        _ => type,
    };
}

public static class EmailDraftTones
{
    public const string Professional = "Professional";
    public const string Friendly = "Friendly";
    public const string Brief = "Brief";

    public static readonly string[] All = [Professional, Friendly, Brief];
}
