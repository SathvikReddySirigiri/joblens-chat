using JobLensChat.Models;
using Microsoft.AspNetCore.Identity;

namespace JobLensChat.Services;

public static class UserPassword
{
    private static readonly PasswordHasher<UserAccount> Hasher = new();

    public static string Hash(string email, string password)
    {
        var marker = new UserAccount(0, email, email, "", "Student", "");
        return Hasher.HashPassword(marker, password);
    }

    public static bool Verify(string email, string passwordHash, string password)
    {
        var marker = new UserAccount(0, email, email, passwordHash, "Student", "");
        var result = Hasher.VerifyHashedPassword(marker, passwordHash, password);
        return result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }
}
