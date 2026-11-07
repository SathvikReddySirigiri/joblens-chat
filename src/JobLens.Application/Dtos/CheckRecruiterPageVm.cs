using System.ComponentModel.DataAnnotations;
using JobLens.Domain.Entities;
using JobLens.Application.Dtos;

namespace JobLens.Application.Dtos;

public sealed class CheckRecruiterPageVm
{
    [Required(ErrorMessage = "Recruiter email is required.")]
    [EmailAddress]
    [StringLength(200)]
    public string Email { get; set; } = "";

    public int? CompanyId { get; set; }

    public RecruiterRiskResult? Result { get; set; }

    public IReadOnlyList<ScamReport> Reports { get; set; } = [];

    public Recruiter? Recruiter { get; set; }
}
