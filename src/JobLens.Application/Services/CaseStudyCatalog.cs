using JobLens.Application.Dtos;

namespace JobLens.Application.Services;

public static class CaseStudyCatalog
{
    public static IReadOnlyList<CaseStudy> All { get; } =
    [
        CreateCase1(),
        CreateCase2(),
        CreateCase3(),
        CreateCase4(),
    ];

    public static CaseStudy? Find(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        return All.FirstOrDefault(item => item.Id.Equals(id.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static CaseStudy CreateCase1()
    {
        const string bodyC =
            """
            Hi Jordan,
            We have mailed you a $2,800 check for your home-office setup.
            Deposit it today, then send $1,900 by Zelle to our approved vendor for your MacBook.
            Please complete this within 24 hours to keep your position.
            """;

        return new CaseStudy(
            "case1",
            "Fake check for a home-office setup",
            "Fake check",
            "A look-alike Amazon domain offers a high-paying remote internship, then mails a check and asks for money back.",
            [
                new CaseEmail(
                    "1 · First contact",
                    "case1a.png",
                    "Amazon HR Team",
                    "hr@amaz0n-careers.net",
                    "jordan.lee@university.edu",
                    "Mon 10/12/2026 9:14 AM",
                    "Congratulations – Remote Data Entry Internship",
                    """
                    Hi Jordan,
                    Great news! After reviewing your profile, you have been selected for our Remote Data Entry Internship.
                    Pay: $45/hour, flexible hours, no interview needed.
                    Reply YES today to secure your spot.
                    – Amazon HR Team
                    """,
                    false),
                new CaseEmail(
                    "2 · Student replies",
                    "case1b.png",
                    "Jordan Lee",
                    "jordan.lee@university.edu",
                    "hr@amaz0n-careers.net",
                    "Mon 10/12/2026 11:02 AM",
                    "RE: Congratulations – Remote Data Entry Internship",
                    """
                    Hi,
                    YES, I'm very interested! What are the next steps, and when can I start?
                    Thank you,
                    Jordan
                    """,
                    true),
                new CaseEmail(
                    "3 · The ask",
                    "case1c.png",
                    "Amazon HR Team",
                    "hr@amaz0n-careers.net",
                    "jordan.lee@university.edu",
                    "Tue 10/13/2026 8:40 AM",
                    "Onboarding – Equipment Check Enclosed",
                    bodyC,
                    false),
            ],
            [
                "Look-alike domain: amaz0n uses a zero instead of o",
                "Hired with no interview",
                "$45/hour for simple data entry",
                "A check arrives before any work",
                "Asked to send money back by Zelle",
                "24-hour deadline",
            ],
            [
                "Do not deposit the check or send money",
                "Check the sender on JobLens and the company's official careers site",
                "Report the domain on JobLens; tell your bank if you deposited the check",
            ],
            "hr@amaz0n-careers.net",
            "Has amaz0n-careers.net been reported?",
            bodyC);
    }

    private static CaseStudy CreateCase2()
    {
        const string bodyC =
            """
            Hi Priya,
            Video calls are not needed – you passed our chat interview!
            To process your offer and direct deposit, reply today with:
            • Social Security Number
            • Bank account and routing number
            • Photo of your driver's license
            """;

        return new CaseStudy(
            "case2",
            "Chat-only interview, then an SSN request",
            "SSN or bank details before an offer",
            "A 'Deloitte' recruiter writes from Gmail, moves the interview to Telegram, then asks for SSN and bank details.",
            [
                new CaseEmail(
                    "1 · First contact",
                    "case2a.png",
                    "Daniel Brooks – Deloitte Recruiting",
                    "deloitte.recruiting.team2026@gmail.com",
                    "priya.shah@university.edu",
                    "Wed 10/14/2026 7:55 AM",
                    "Business Analyst Intern – Interview Invitation",
                    """
                    Hello Priya,
                    I'm a senior recruiter at Deloitte. Your resume was shortlisted for our Business Analyst Internship.
                    Interviews are held on Telegram. Please add me: @DeloitteHR_Daniel
                    Best regards,
                    Daniel Brooks
                    """,
                    false),
                new CaseEmail(
                    "2 · Student replies",
                    "case2b.png",
                    "Priya Shah",
                    "priya.shah@university.edu",
                    "deloitte.recruiting.team2026@gmail.com",
                    "Wed 10/14/2026 10:20 AM",
                    "RE: Business Analyst Intern – Interview Invitation",
                    """
                    Hi Daniel,
                    Thank you! Could we do a video call instead? Also, could you send the job posting link from Deloitte's careers site?
                    Best,
                    Priya
                    """,
                    true),
                new CaseEmail(
                    "3 · The ask",
                    "case2c.png",
                    "Daniel Brooks – Deloitte Recruiting",
                    "deloitte.recruiting.team2026@gmail.com",
                    "priya.shah@university.edu",
                    "Thu 10/15/2026 6:30 AM",
                    "Offer – Action Required Today",
                    bodyC,
                    false),
            ],
            [
                "Big-firm recruiter using a Gmail address",
                "Interview only on Telegram",
                "Refuses a video call",
                "Offer without a real interview",
                "Asks for SSN, bank and ID before a written offer",
            ],
            [
                "Never share SSN or bank details before a verified written offer",
                "Ask for a video call and an official company email",
                "Report the recruiter on JobLens; if you shared data, freeze your credit",
            ],
            "deloitte.recruiting.team2026@gmail.com",
            "How many reports involve Deloitte on Telegram?",
            bodyC);
    }

    private static CaseStudy CreateCase3()
    {
        const string bodyC =
            """
            Dear Wei,
            To start your H-1B paperwork, a one-time $500 sponsorship processing fee is required.
            Pay by Zelle or gift card today. The fee is refunded after your first paycheck.
            Unpaid fees will cancel your offer.
            """;

        return new CaseStudy(
            "case3",
            "Paid visa sponsorship",
            "Paid visa or sponsorship fee",
            "A look-alike Microsoft domain promises guaranteed H-1B sponsorship to an international student, then charges a fee.",
            [
                new CaseEmail(
                    "1 · First contact",
                    "case3a.png",
                    "Microsoft Talent Acquisition",
                    "hr@rnicrosoft-hiring.com",
                    "wei.chen@university.edu",
                    "Fri 10/16/2026 3:12 PM",
                    "Software Engineer Intern + Guaranteed H-1B Sponsorship",
                    """
                    Dear Wei,
                    We are pleased to offer you a Software Engineer Internship with guaranteed H-1B sponsorship.
                    Positions for international students are limited.
                    Please confirm your interest within 48 hours.
                    Microsoft Talent Acquisition
                    """,
                    false),
                new CaseEmail(
                    "2 · Student replies",
                    "case3b.png",
                    "Wei Chen",
                    "wei.chen@university.edu",
                    "hr@rnicrosoft-hiring.com",
                    "Fri 10/16/2026 5:47 PM",
                    "RE: Software Engineer Intern + Guaranteed H-1B Sponsorship",
                    """
                    Dear Hiring Team,
                    Thank you so much, I accept! I am on an F-1 visa. What do I need to do for the sponsorship?
                    Sincerely,
                    Wei Chen
                    """,
                    true),
                new CaseEmail(
                    "3 · The ask",
                    "case3c.png",
                    "Microsoft Talent Acquisition",
                    "hr@rnicrosoft-hiring.com",
                    "wei.chen@university.edu",
                    "Sat 10/17/2026 9:05 AM",
                    "Sponsorship Processing Fee – Due Today",
                    bodyC,
                    false),
            ],
            [
                "Look-alike domain: rn instead of m",
                "'Guaranteed' H-1B sponsorship",
                "Targets an international student",
                "Fee to keep the offer",
                "Zelle or gift card payment",
                "Refund promised later",
            ],
            [
                "Never pay a recruiter for sponsorship or an offer",
                "Ask your university's international student office",
                "Report the domain on JobLens",
            ],
            "hr@rnicrosoft-hiring.com",
            "Is hr@rnicrosoft-hiring.com safe?",
            bodyC);
    }

    private static CaseStudy CreateCase4()
    {
        const string bodyC =
            """
            Hi Sam,
            Your resume does not fully show your strengths, so hiring teams may overlook you.
            Before we schedule your interview, please contact our partner, Mrs. Rachel Hayes, a professional resume writer:
            rachelhayes.resume.pro@gmail.com
            Once your resume is updated, I will arrange your interview.
            """;

        return new CaseStudy(
            "case4",
            "'Fix your resume first' — the paid resume writer",
            "Upfront fee",
            "A 'Google recruiter' on Gmail collects a resume, then says a paid resume writer is required before the interview.",
            [
                new CaseEmail(
                    "1 · First contact",
                    "case4a.png",
                    "Emily Tran",
                    "emilytran.google.talent@gmail.com",
                    "sam.patel@university.edu",
                    "Mon 10/19/2026 2:23 AM",
                    "Early-Career Opportunities at Google",
                    """
                    Hi Sam,
                    I work with the recruiting team at Google. A colleague shared your profile with me.
                    We are evaluating students for internships in Software, AI/ML, and Data Science.
                    Please reply with your resume and availability.
                    """,
                    false),
                new CaseEmail(
                    "2 · Student replies",
                    "case4b.png",
                    "Sam Patel",
                    "sam.patel@university.edu",
                    "emilytran.google.talent@gmail.com",
                    "Mon 10/19/2026 3:52 PM",
                    "RE: Early-Career Opportunities at Google",
                    """
                    Hi Emily,
                    Thank you for reaching out! My resume is attached. I'm available Tuesday to Thursday, 10 AM – 4 PM.
                    Best regards,
                    Sam
                    """,
                    true),
                new CaseEmail(
                    "3 · The ask",
                    "case4c.png",
                    "Emily Tran",
                    "emilytran.google.talent@gmail.com",
                    "sam.patel@university.edu",
                    "Wed 10/21/2026 1:10 PM",
                    "Before We Schedule Your Interview",
                    bodyC,
                    false),
            ],
            [
                "Big-tech recruiter using a Gmail address",
                "Email sent at 2:23 AM from a stranger",
                "Vague 'colleague shared your profile'",
                "Interview held back until you pay a third party",
                "'Partner' also uses Gmail",
            ],
            [
                "Real recruiters never require a paid resume service",
                "Find the role on the company's official careers page",
                "Do not pay or send more documents; report it on JobLens and to your career center",
            ],
            "emilytran.google.talent@gmail.com",
            "Is it normal for a recruiter to require a paid resume writer before an interview?",
            bodyC);
    }
}
