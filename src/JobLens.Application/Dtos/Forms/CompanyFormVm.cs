using System.ComponentModel.DataAnnotations;

namespace JobLens.Application.Dtos.Forms;

public sealed class CompanyFormVm
{
    public int CompanyId { get; set; }

    [Required]
    [StringLength(120)]
    public string Name { get; set; } = "";

    [Required]
    [StringLength(200)]
    public string OfficialDomain { get; set; } = "";

    [Required]
    [StringLength(80)]
    public string Industry { get; set; } = "";

    [Required]
    [RegularExpression("^[A-Za-z]{2}$", ErrorMessage = "State must be a 2-letter code.")]
    public string HqState { get; set; } = "";
}

