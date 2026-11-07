using System.ComponentModel.DataAnnotations;

namespace JobLens.Application.Dtos;

public sealed class RecruiterFormVm
{
    public int RecruiterId { get; set; }

    [Required]
    [EmailAddress]
    [StringLength(200)]
    public string Email { get; set; } = "";

    public int? CompanyId { get; set; }

    [RegularExpression(@"^\d{4}-\d{2}-\d{2}$", ErrorMessage = "Use a yyyy-MM-dd date.")]
    public string? FirstSeen { get; set; }

    [StringLength(500)]
    public string Notes { get; set; } = "";
}
