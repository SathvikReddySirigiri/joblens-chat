namespace JobLensChat.Models;

public sealed record UserAccount(
    int UserId,
    string Email,
    string DisplayName,
    string PasswordHash,
    string Role,
    string CreatedAt);
