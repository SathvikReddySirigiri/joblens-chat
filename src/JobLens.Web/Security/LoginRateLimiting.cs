using System.Threading.RateLimiting;
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
    public const string TooManyAttemptsMessage = "Too many attempts. Please wait a minute and try again.";

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

            options.OnRejected = RenderLoginTooManyAttemptsAsync;
        });
        return services;
    }

    private static async ValueTask RenderLoginTooManyAttemptsAsync(
        OnRejectedContext context,
        CancellationToken cancellationToken)
    {
        var httpContext = context.HttpContext;
        httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

        var form = new LoginFormVm();
        if (httpContext.Request.HasFormContentType)
        {
            var posted = await httpContext.Request.ReadFormAsync(cancellationToken);
            form.Email = posted["Email"].ToString();
            form.ReturnUrl = posted["ReturnUrl"].ToString();
        }

        var modelState = new ModelStateDictionary();
        modelState.AddModelError(string.Empty, TooManyAttemptsMessage);

        var routeData = httpContext.GetRouteData() ?? new RouteData();
        routeData.Values["controller"] ??= "Account";
        routeData.Values["action"] ??= "Login";

        var actionContext = new ActionContext(httpContext, routeData, new ActionDescriptor());
        var viewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), modelState)
        {
            Model = form,
        };
        var tempData = httpContext.RequestServices
            .GetRequiredService<ITempDataDictionaryFactory>()
            .GetTempData(httpContext);

        await new ViewResult
        {
            ViewName = "~/Views/Account/Login.cshtml",
            ViewData = viewData,
            TempData = tempData,
        }.ExecuteResultAsync(actionContext);
    }
}
