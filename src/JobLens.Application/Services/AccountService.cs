using System.Globalization;
using System.Text.RegularExpressions;
using JobLens.Application.Common;
using JobLens.Application.Dtos;
using JobLens.Application.Interfaces;
using JobLens.Domain.Entities;

namespace JobLens.Application.Services;

public sealed class AccountService(IUserRepository users, IPasswordHasher passwords)
{
    private const string InvalidCredentials = "Invalid email or password.";

    public async Task<AuthResult> SignInCheckAsync(string email, string secret, CancellationToken cancellationToken)
    {
        var normalizedEmail = (email ?? "").Trim();
        var secretValue = secret ?? "";
        if (normalizedEmail.Length == 0 || secretValue.Length == 0 || !DomainNormalizer.IsEmail(normalizedEmail))
        {
            return AuthResult.Fail(InvalidCredentials);
        }

        var user = await users.GetByEmailAsync(normalizedEmail, cancellationToken);
        if (user is null || !passwords.Verify(user.Email, user.PasswordHash, secretValue))
        {
            return AuthResult.Fail(InvalidCredentials);
        }

        return AuthResult.Ok(user.UserId, user.DisplayName, user.Email, user.Role);
    }

    public async Task<ServiceResult> RegisterStudentAsync(
        string email,
        string displayName,
        string secret,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = (email ?? "").Trim().ToLowerInvariant();
        var name = (displayName ?? "").Trim();
        var secretValue = secret ?? "";

        if (!DomainNormalizer.IsEmail(normalizedEmail))
        {
            return ServiceResult.Fail("Enter a valid email address.");
        }

        if (name.Length is 0 or > 80)
        {
            return ServiceResult.Fail("Display name is required (up to 80 characters).");
        }

        if (!IsAcceptableSecret(secretValue))
        {
            return ServiceResult.Fail("Password must be at least 10 characters and include a letter and a digit.");
        }

        if (await users.EmailExistsAsync(normalizedEmail, cancellationToken))
        {
            return ServiceResult.Fail("An account with this email already exists.");
        }

        var createdAt = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var hash = passwords.Hash(normalizedEmail, secretValue);
        var id = await users.CreateAsync(
            new UserAccount(0, normalizedEmail, name, hash, "Student", createdAt),
            cancellationToken);
        return ServiceResult.Ok(id);
    }

    private static bool IsAcceptableSecret(string secret) =>
        secret.Length >= 10
        && Regex.IsMatch(secret, "[A-Za-z]")
        && Regex.IsMatch(secret, "[0-9]");
}
