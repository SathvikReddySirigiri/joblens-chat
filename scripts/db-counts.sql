-- JobLens seed / runtime table counts (read-only).
-- Run against App_Data/joblens.db (local) or the Azure data path.

SELECT 'Companies' AS TableName, COUNT(*) AS RowCount FROM Companies
UNION ALL SELECT 'ScamReports', COUNT(*) FROM ScamReports
UNION ALL SELECT 'Recruiters', COUNT(*) FROM Recruiters
UNION ALL SELECT 'JobPostings', COUNT(*) FROM JobPostings
UNION ALL SELECT 'InterviewReviews', COUNT(*) FROM InterviewReviews
UNION ALL SELECT 'CheckLogs', COUNT(*) FROM CheckLogs
UNION ALL SELECT 'Users', COUNT(*) FROM Users
UNION ALL SELECT 'JobApplications', COUNT(*) FROM JobApplications
UNION ALL SELECT 'Resumes', COUNT(*) FROM Resumes
ORDER BY TableName;

SELECT Status, COUNT(*) AS RowCount
FROM ScamReports
GROUP BY Status
ORDER BY Status;
