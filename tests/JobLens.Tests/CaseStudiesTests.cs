using JobLens.Application.Services;
using JobLens.Web.Controllers;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace JobLens.Tests;

public sealed class CaseStudiesTests
{
    [Fact]
    public void Catalog_has_four_cases_with_three_emails_each_and_unique_ids()
    {
        Assert.Equal(4, CaseStudyCatalog.All.Count);
        Assert.Equal(4, CaseStudyCatalog.All.Select(item => item.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(CaseStudyCatalog.All, study => Assert.Equal(3, study.Emails.Count));
    }

    [Fact]
    public void Catalog_image_files_follow_naming_and_exist_on_disk()
    {
        var casesRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "JobLens.Web", "wwwroot", "images", "cases"));
        Assert.True(Directory.Exists(casesRoot), $"Missing cases folder at {casesRoot}");

        for (var n = 1; n <= 4; n++)
        {
            var study = CaseStudyCatalog.Find($"case{n}");
            Assert.NotNull(study);
            var letters = new[] { "a", "b", "c" };
            for (var i = 0; i < 3; i++)
            {
                var expected = $"case{n}{letters[i]}.png";
                Assert.Equal(expected, study!.Emails[i].ImageFile);
                Assert.True(
                    File.Exists(Path.Combine(casesRoot, expected)),
                    $"Missing image file {expected}");
            }
        }
    }

    [Fact]
    public void Unknown_case_id_returns_not_found()
    {
        var controller = new CaseStudiesController();
        Assert.IsType<NotFoundResult>(controller.Details("unknown"));
    }

    [Fact]
    public void OfferAnalyzer_prefill_uses_catalog_offer_text()
    {
        var controller = new OfferAnalyzerController(null!);
        var study = CaseStudyCatalog.Find("case1");
        Assert.NotNull(study);

        var result = Assert.IsType<ViewResult>(controller.Index("case1"));
        var model = Assert.IsType<JobLens.Application.Dtos.Pages.OfferAnalyzerPageVm>(result.Model);
        Assert.Equal(study!.OfferText, model.Text);

        var ignored = Assert.IsType<ViewResult>(controller.Index("unknown"));
        var empty = Assert.IsType<JobLens.Application.Dtos.Pages.OfferAnalyzerPageVm>(ignored.Model);
        Assert.True(string.IsNullOrEmpty(empty.Text));
    }
}
