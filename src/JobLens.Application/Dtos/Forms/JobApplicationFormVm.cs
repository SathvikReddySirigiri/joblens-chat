using System.ComponentModel.DataAnnotations;

namespace JobLens.Application.Dtos.Forms;

public sealed class JobApplicationFormVm
{
    public int ApplicationId { get; set; }

    [Required]
    [StringLength(120, MinimumLength = 2)]
    public string CompanyName { get; set; } = "";

    public int? CompanyId { get; set; }

    [Required]
    [StringLength(120, MinimumLength = 2)]
    public string RoleTitle { get; set; } = "";

    [Required]
    public string Source { get; set; } = "Other";

    [StringLength(500)]
    public string? JobUrl { get; set; }

    [EmailAddress]
    [StringLength(200)]
    public string? RecruiterEmail { get; set; }

    [Required]
    public string Status { get; set; } = "Saved";

    [RegularExpression(@"^\d{4}-\d{2}-\d{2}$", ErrorMessage = "Use a yyyy-MM-dd date.")]
    public string? AppliedDate { get; set; }

    [RegularExpression(@"^\d{4}-\d{2}-\d{2}$", ErrorMessage = "Use a yyyy-MM-dd date.")]
    public string? NextFollowUpDate { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }
}
