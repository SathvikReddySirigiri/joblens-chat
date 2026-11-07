using System.ComponentModel.DataAnnotations;

namespace JobLens.Application.Dtos;

public sealed class JobPostingFormVm
{
    public int PostingId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Company is required.")]
    public int CompanyId { get; set; }

    [Required]
    [StringLength(120)]
    public string Title { get; set; } = "";

    [Required]
    [StringLength(120)]
    public string Location { get; set; } = "";

    [Required]
    [RegularExpression("^[A-Za-z]{2}$", ErrorMessage = "State must be a 2-letter code.")]
    public string State { get; set; } = "";

    public bool IsRemote { get; set; }

    public bool IsPaid { get; set; }

    [Range(0, 500)]
    public double? PayRatePerHour { get; set; }

    [Required]
    [StringLength(2000, MinimumLength = 20)]
    public string Description { get; set; } = "";

    [RegularExpression(@"^\d{4}-\d{2}-\d{2}$", ErrorMessage = "Use a yyyy-MM-dd date.")]
    public string? PostedDate { get; set; }
}
