using JobLens.Web.Security;
using Xunit;

namespace JobLens.Tests;

public sealed class LoginRateLimitingTests
{
    [Fact]
    public void Login_rate_limit_message_matches_phase_requirement()
    {
        Assert.Equal("Too many attempts. Please wait a minute and try again.", LoginRateLimiting.TooManyAttemptsMessage);
        Assert.Equal("login", LoginRateLimiting.PolicyName);
    }
}
