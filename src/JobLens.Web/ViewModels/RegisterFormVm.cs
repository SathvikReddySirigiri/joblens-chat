using System.ComponentModel.DataAnnotations;

namespace JobLens.Web.ViewModels;

public sealed class RegisterFormVm
{
    [Required]
    [EmailAddress]
    [Display(Name = "Email")]
    public string Email { get; set; } = "";

    [Required]
    [StringLength(80, MinimumLength = 1)]
    [Display(Name = "Display name")]
    public string DisplayName { get; set; } = "";

    [Required]
    [DataType(DataType.Password)]
    [StringLength(100, MinimumLength = 10)]
    [Display(Name = "Password")]
    public string Password { get; set; } = "";

    [Required]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = "";
}
