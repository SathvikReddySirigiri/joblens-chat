using System.Globalization;
using JobLens.Domain.Constants;

namespace JobLens.Application.Dtos.Pages;

/// <summary>Stable red-flag patterns keyed to seeded scam types; pick by ISO week.</summary>
public static class ScamPatternCatalog
{
    private static readonly HomeRedFlag[] Patterns =
    [
        new(
            "Fake check",
            "A scammer 'hires' you, sends a check or deposit for equipment, then asks you to wire leftover funds before the check bounces.",
            [
                "You are asked to buy a laptop or gear with funds they sent.",
                "Pressure to deposit and wire money the same day.",
                "The check amount is larger than any agreed pay.",
            ],
            "Do not deposit the check or send money. Verify the offer on the company's official careers site and report it.",
            "How do Fake check job scams work, and what should students do?"),
        new(
            "Equipment purchase",
            "The recruiter says you must buy a specific laptop or kit from their vendor, often with a prepaid card or crypto.",
            [
                "Vendor link is not on the company's official domain.",
                "Payment must be made before any onboarding paperwork.",
                "They refuse to ship gear through IT or HR.",
            ],
            "Pause the purchase. Confirm with the career center and the company's official IT/HR channel.",
            "Explain Equipment purchase recruitment scams and how to verify vendors."),
        new(
            "Upfront fee",
            "You are told to pay for training, background checks, uniforms, or 'visa processing' before the job starts.",
            [
                "Any request for fees before a signed offer.",
                "Payment via gift cards, crypto, or peer-to-peer apps.",
                "Vague receipts or no company invoice.",
            ],
            "Legitimate employers do not charge students to start. Refuse payment and file a report.",
            "What are Upfront fee internship scams and how should I respond?"),
        new(
            "Chat-only interview",
            "The entire 'interview' happens on Telegram, WhatsApp, or Discord with no video and no company email.",
            [
                "No video call and no official calendar invite.",
                "The interviewer uses a free or look-alike domain.",
                "They push you to move off LinkedIn immediately.",
            ],
            "Ask for a video interview from an official company address. If they refuse, walk away and report it.",
            "Why are Chat-only interview scams risky for students?"),
        new(
            "Identity theft",
            "Early requests for SSN, banking details, copies of ID, or passwords under the guise of 'onboarding'.",
            [
                "Sensitive IDs requested before a written offer.",
                "Forms hosted on unfamiliar domains.",
                "Urgency to 'verify identity' the same day.",
            ],
            "Never share SSN or banking data until HR confirms on an official channel. Report the outreach.",
            "How do Identity theft job scams ask for personal data?"),
        new(
            "Fake staffing agency",
            "A look-alike staffing firm claims to place you at a well-known company, then asks for fees or personal data.",
            [
                "Agency domain is a look-alike of a real employer.",
                "No verifiable LinkedIn or official careers page.",
                "They ask for payment or documents before any interview.",
            ],
            "Verify the agency and the employer separately. Prefer campus Handshake listings and report impostors.",
            "How can I spot a Fake staffing agency scam?"),
    ];

    public static IReadOnlyList<HomeRedFlag> All => Patterns;

    public static HomeRedFlag ForIsoWeek(DateOnly utcDate)
    {
        var week = ISOWeek.GetWeekOfYear(utcDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        var index = Math.Abs(week - 1) % Patterns.Length;
        return Patterns[index];
    }

    public static HomeRedFlag ForScamType(string scamType)
    {
        var match = Patterns.FirstOrDefault(pattern =>
            string.Equals(pattern.Name, scamType, StringComparison.OrdinalIgnoreCase));
        return match ?? Patterns[0];
    }

    public static bool IsKnownType(string scamType) =>
        ScamTypes.All.Contains(scamType, StringComparer.OrdinalIgnoreCase);
}
