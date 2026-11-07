using System.Security.Claims;
using JobLens.Application.Services;
using JobLens.Web.Security;
using JobLens.Web.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace JobLens.Web.Controllers;

public sealed class AccountController(AccountService accounts) : Controller
{
    [HttpGet("/Account/Login")]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToLocal(returnUrl);
        }

        return View(new LoginFormVm { ReturnUrl = returnUrl });
    }

    [HttpPost("/Account/Login")]
    [AllowAnonymous]
    [EnableRateLimiting(LoginRateLimiting.PolicyName)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginFormVm form, CancellationToken cancellationToken)
    {
        form.ReturnUrl = form.ReturnUrl;
        if (!ModelState.IsValid)
        {
            return View(form);
        }

        var result = await accounts.SignInCheckAsync(form.Email, form.Password, cancellationToken);
        if (!result.Success || result.UserId is null || result.Email is null || result.Role is null || result.DisplayName is null)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Invalid email or password.");
            return View(form);
        }

        await SignInCookieAsync(result.UserId.Value, result.DisplayName, result.Email, result.Role);
        return RedirectToLocal(form.ReturnUrl);
    }

    [HttpPost("/Account/Logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }

    [HttpGet("/Account/Register")]
    [AllowAnonymous]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        return View(new RegisterFormVm());
    }

    [HttpPost("/Account/Register")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterFormVm form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(form);
        }

        var result = await accounts.RegisterStudentAsync(form.Email, form.DisplayName, form.Password, cancellationToken);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Registration failed.");
            return View(form);
        }

        var signedIn = await accounts.SignInCheckAsync(form.Email, form.Password, cancellationToken);
        if (signedIn.Success && signedIn.UserId is not null && signedIn.Email is not null
            && signedIn.Role is not null && signedIn.DisplayName is not null)
        {
            await SignInCookieAsync(signedIn.UserId.Value, signedIn.DisplayName, signedIn.Email, signedIn.Role);
            TempData["Success"] = "Welcome to JobLens. Your student account is ready.";
            return RedirectToAction("Index", "Home");
        }

        TempData["Success"] = "Account created. Please sign in.";
        return RedirectToAction(nameof(Login));
    }

    [HttpGet("/Account/AccessDenied")]
    [AllowAnonymous]
    public IActionResult AccessDenied() => View();

    private async Task SignInCookieAsync(int userId, string displayName, string email, string role)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, displayName),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Role, role),
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = true,
                AllowRefresh = true,
            });
    }

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Index", "Home");
    }
}
