using System.ComponentModel.DataAnnotations;

namespace JobLensChat.ViewModels;

public sealed class ScamReportFormVm
{
    public int ReportId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Company is required.")]
    public int CompanyId { get; set; }

    [Required]
    [EmailAddress]
    [StringLength(200)]
    public string RecruiterEmail { get; set; } = "";

    [Required]
    [StringLength(40)]
    public string ScamType { get; set; } = "";

    [Required]
    [StringLength(40)]
    public string Channel { get; set; } = "";

    [Range(0, 1000000)]
    public double AmountRequestedUsd { get; set; }

    [Range(0, 20000)]
    public int DomainAgeDays { get; set; }

    [Required]
    [StringLength(500, MinimumLength = 20)]
    public string Description { get; set; } = "";
}
