using System.Reflection;
using JobLens.Web.Controllers;
using JobLens.Web.Security;
using Microsoft.AspNetCore.RateLimiting;
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

    [Fact]
    public void Expensive_policy_is_named_and_messaged()
    {
        Assert.Equal("expensive", LoginRateLimiting.ExpensivePolicyName);
        Assert.Equal("Please wait a minute", LoginRateLimiting.ExpensiveWaitMessage);
    }

    [Fact]
    public void Expensive_policy_attribute_is_on_chat_offer_recruiter_and_jobs()
    {
        AssertHasExpensive(typeof(ChatController), nameof(ChatController.Ask));
        AssertHasExpensive(typeof(OfferAnalyzerController), nameof(OfferAnalyzerController.Analyze));
        AssertHasExpensive(typeof(OfferAnalyzerController), nameof(OfferAnalyzerController.Upload));
        AssertHasExpensive(typeof(CheckRecruiterController), nameof(CheckRecruiterController.Check));
        AssertHasExpensive(typeof(JobsController), nameof(JobsController.Index));
    }

    private static void AssertHasExpensive(Type controller, string methodName)
    {
        var method = controller.GetMethod(methodName)
            ?? throw new InvalidOperationException($"{controller.Name}.{methodName} not found.");
        var attribute = method.GetCustomAttribute<EnableRateLimitingAttribute>();
        Assert.NotNull(attribute);
        Assert.Equal(LoginRateLimiting.ExpensivePolicyName, attribute.PolicyName);
    }
}
