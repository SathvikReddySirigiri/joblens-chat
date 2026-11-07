namespace JobLens.Application.Dtos;

public sealed record AuthResult(
    bool Success,
    int? UserId,
    string? DisplayName,
    string? Email,
    string? Role,
    string? Error)
{
    public static AuthResult Ok(int userId, string displayName, string email, string role) =>
        new(true, userId, displayName, email, role, null);

    public static AuthResult Fail(string error) =>
        new(false, null, null, null, null, error);
}
