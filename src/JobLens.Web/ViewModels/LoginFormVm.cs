using System.ComponentModel.DataAnnotations;

namespace JobLens.Web.ViewModels;

public sealed class LoginFormVm
{
    [Required]
    [EmailAddress]
    [Display(Name = "Email")]
    public string Email { get; set; } = "";

    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = "";

    public string? ReturnUrl { get; set; }
}
