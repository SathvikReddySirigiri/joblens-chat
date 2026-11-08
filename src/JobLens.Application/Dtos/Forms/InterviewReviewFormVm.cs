using System.ComponentModel.DataAnnotations;

namespace JobLens.Application.Dtos.Forms;

public sealed class InterviewReviewFormVm
{
    public int ReviewId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Company is required.")]
    public int CompanyId { get; set; }

    [Required]
    [StringLength(120)]
    public string Role { get; set; } = "";

    [Range(1, 5)]
    public int Rating { get; set; }

    [Required]
    [StringLength(1000, MinimumLength = 20)]
    public string Experience { get; set; } = "";

    public bool WasLegit { get; set; }

    [RegularExpression(@"^\d{4}-\d{2}-\d{2}$", ErrorMessage = "Use a yyyy-MM-dd date.")]
    public string? ReviewDate { get; set; }
}

