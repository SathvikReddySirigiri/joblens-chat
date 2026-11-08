using System.ComponentModel.DataAnnotations;

namespace JobLens.Application.Dtos.Pages;

public sealed class EmailDraftPageVm
{
    public int? PrefillApplicationId { get; set; }

    [Required]
    public string Type { get; set; } = "FollowUpAfterApplying";

    [Required]
    public string Tone { get; set; } = "Professional";

    [StringLength(80)]
    public string? RecruiterName { get; set; }

    [EmailAddress]
    [StringLength(200)]
    public string? RecruiterEmail { get; set; }

    [Required]
    [StringLength(120, MinimumLength = 2)]
    public string CompanyName { get; set; } = "";

    [Required]
    [StringLength(120, MinimumLength = 2)]
    public string RoleTitle { get; set; } = "";

    [Required]
    [StringLength(80, MinimumLength = 2)]
    public string StudentName { get; set; } = "";

    [StringLength(500)]
    public string? ExtraNotes { get; set; }

    public EmailDraftResult? Result { get; set; }

    public string? MailtoUrl { get; set; }

    public IReadOnlyList<EmailDraftApplicationOption> Applications { get; set; } = [];
}

public sealed record EmailDraftApplicationOption(
    int ApplicationId,
    string Label,
    string CompanyName,
    string RoleTitle,
    string? RecruiterEmail,
    string? RecruiterName);
