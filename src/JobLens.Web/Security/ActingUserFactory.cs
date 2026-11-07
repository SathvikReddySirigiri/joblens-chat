using System.Security.Claims;
using JobLens.Application.Dtos;

namespace JobLens.Web.Security;

public static class ActingUserFactory
{
    public static ActingUser? TryCreate(ClaimsPrincipal user)
    {
        if (user.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var idValue = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var role = user.FindFirstValue(ClaimTypes.Role);
        if (!int.TryParse(idValue, out var userId) || userId <= 0 || string.IsNullOrWhiteSpace(role))
        {
            return null;
        }

        return new ActingUser(userId, role);
    }

    public static ActingUser Require(ClaimsPrincipal user) =>
        TryCreate(user) ?? throw new InvalidOperationException("A signed-in user is required.");
}
