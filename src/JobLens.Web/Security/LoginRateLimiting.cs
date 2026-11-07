using System.Threading.RateLimiting;
using JobLens.Application.Dtos;
using JobLens.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.RateLimiting;

namespace JobLens.Web.Security;

public static class LoginRateLimiting
{
    public const string PolicyName = "login";
    public const string ExpensivePolicyName = "expensive";
    public const string TooManyAttemptsMessage = "Too many attempts. Please wait a minute and try again.";
    public const string ExpensiveWaitMessage = "Please wait a minute";

    public static IServiceCollection AddLoginRateLimiter(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(PolicyName, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    }));

            options.AddPolicy(ExpensivePolicyName, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: "expensive:" + (httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    }));

            options.OnRejected = RenderTooManyAttemptsAsync;
        });
        return services;
    }

    private static async ValueTask RenderTooManyAttemptsAsync(
        OnRejectedContext context,
        CancellationToken cancellationToken)
    {
        var httpContext = context.HttpContext;
        httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

        var path = httpContext.Request.Path.Value ?? "";
        if (path.StartsWith("/OfferAnalyzer", StringComparison.OrdinalIgnoreCase))
        {
            await RenderViewAsync(
                httpContext,
                "~/Views/OfferAnalyzer/Index.cshtml",
                "OfferAnalyzer",
                "Index",
                new OfferAnalyzerPageVm(),
                ExpensiveWaitMessage,
                cancellationToken);
            return;
        }

        if (path.StartsWith("/CheckRecruiter", StringComparison.OrdinalIgnoreCase))
        {
            await RenderViewAsync(
                httpContext,
                "~/Views/CheckRecruiter/Index.cshtml",
                "CheckRecruiter",
                "Index",
                new CheckRecruiterPageVm(),
                ExpensiveWaitMessage,
                cancellationToken);
            return;
        }

        if (path.StartsWith("/Chat", StringComparison.OrdinalIgnoreCase)
            || string.Equals(httpContext.GetRouteData()?.Values["controller"]?.ToString(), "Chat", StringComparison.OrdinalIgnoreCase))
        {
            await RenderViewAsync(
                httpContext,
                "~/Views/Chat/Index.cshtml",
                "Chat",
                "Index",
                new ChatViewModel(),
                ExpensiveWaitMessage,
                cancellationToken);
            return;
        }

        var form = new LoginFormVm();
        if (httpContext.Request.HasFormContentType)
        {
            var posted = await httpContext.Request.ReadFormAsync(cancellationToken);
            form.Email = posted["Email"].ToString();
            form.ReturnUrl = posted["ReturnUrl"].ToString();
        }

        await RenderViewAsync(
            httpContext,
            "~/Views/Account/Login.cshtml",
            "Account",
            "Login",
            form,
            TooManyAttemptsMessage,
            cancellationToken);
    }

    private static async Task RenderViewAsync(
        HttpContext httpContext,
        string viewName,
        string controller,
        string action,
        object model,
        string message,
        CancellationToken cancellationToken)
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError(string.Empty, message);

        var routeData = httpContext.GetRouteData() ?? new RouteData();
        routeData.Values["controller"] ??= controller;
        routeData.Values["action"] ??= action;

        var actionContext = new ActionContext(httpContext, routeData, new ActionDescriptor());
        var viewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), modelState)
        {
            Model = model,
        };
        var tempData = httpContext.RequestServices
            .GetRequiredService<ITempDataDictionaryFactory>()
            .GetTempData(httpContext);

        await new ViewResult
        {
            ViewName = viewName,
            ViewData = viewData,
            TempData = tempData,
        }.ExecuteResultAsync(actionContext);
    }
}
