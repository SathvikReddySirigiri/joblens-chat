namespace JobLens.Application.Dtos;

/// <summary>Signed-in caller passed into Application services for ownership checks.</summary>
public sealed record ActingUser(int UserId, string Role)
{
    public const string ModeratorRole = "Moderator";
    public const string StudentRole = "Student";

    public bool IsModerator =>
        string.Equals(Role, ModeratorRole, StringComparison.OrdinalIgnoreCase);

    public bool IsStudent =>
        string.Equals(Role, StudentRole, StringComparison.OrdinalIgnoreCase);

    public static ActingUser Moderator(int userId) => new(userId, ModeratorRole);

    public static ActingUser Student(int userId) => new(userId, StudentRole);
}
