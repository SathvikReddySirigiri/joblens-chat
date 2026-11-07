namespace JobLens.Application.Dtos;

public sealed record DataCounts(
    int Companies,
    int ScamReports,
    int Approved,
    int Pending,
    int Rejected,
    int JobPostings = 0,
    int InterviewReviews = 0,
    int CheckLogs = 0,
    int Recruiters = 0,
    int Users = 0);
