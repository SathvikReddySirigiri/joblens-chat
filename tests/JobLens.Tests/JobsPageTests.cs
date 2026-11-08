using JobLens.Web.Controllers;
using JobLens.Web.Security;
using JobLens.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Reflection;
using Xunit;

namespace JobLens.Tests;

public sealed class JobsPageTests
{
    [Fact]
    public void ResolveTab_defaults_to_live()
    {
        Assert.Equal(JobsPageVm.LiveTab, JobsPageVm.ResolveTab(null));
        Assert.Equal(JobsPageVm.LiveTab, JobsPageVm.ResolveTab(""));
        Assert.Equal(JobsPageVm.LiveTab, JobsPageVm.ResolveTab("LIVE"));
        Assert.Equal(JobsPageVm.LiveTab, JobsPageVm.ResolveTab("unknown"));
        Assert.Equal(JobsPageVm.JobLensTab, JobsPageVm.ResolveTab("joblens"));
        Assert.Equal(JobsPageVm.JobLensTab, JobsPageVm.ResolveTab("JobLens"));
    }

    [Fact]
    public void LiveJobs_index_redirects_to_Jobs_live_tab()
    {
        var controller = new LiveJobsController();
        var result = Assert.IsType<RedirectToActionResult>(
            controller.Index(
                category: "IT",
                categoryCode: null,
                level: "Internship",
                location: null,
                companyName: null,
                keyword: null,
                hiringPath: "student",
                remoteOnly: false,
                page: 2));

        Assert.Equal("Index", result.ActionName);
        Assert.Equal("Jobs", result.ControllerName);
        Assert.Equal(JobsPageVm.LiveTab, result.RouteValues!["tab"]);
        Assert.Equal("IT", result.RouteValues["category"]);
        Assert.Equal(2, result.RouteValues["page"]);
    }

    [Fact]
    public void JobPostings_index_redirects_to_Jobs_joblens_tab()
    {
        var controller = new JobPostingsController(
            null!,
            null!,
            null!);
        var result = Assert.IsType<RedirectToActionResult>(
            controller.Index(search: "intern", company: 3, remote: true, paid: null, state: "FL", minPay: null, exclude: false, q: null, asked: false, page: 1));

        Assert.Equal("Index", result.ActionName);
        Assert.Equal("Jobs", result.ControllerName);
        Assert.Equal(JobsPageVm.JobLensTab, result.RouteValues!["tab"]);
        Assert.Equal("intern", result.RouteValues["search"]);
        Assert.Equal(3, result.RouteValues["company"]);
        Assert.Equal(true, result.RouteValues["remote"]);
        Assert.Equal("FL", result.RouteValues["state"]);
    }

    [Fact]
    public void Jobs_index_uses_expensive_rate_limit()
    {
        var method = typeof(JobsController).GetMethod(nameof(JobsController.Index))
            ?? throw new InvalidOperationException("JobsController.Index not found.");
        var attribute = method.GetCustomAttribute<EnableRateLimitingAttribute>();
        Assert.NotNull(attribute);
        Assert.Equal(LoginRateLimiting.ExpensivePolicyName, attribute.PolicyName);
    }
}
