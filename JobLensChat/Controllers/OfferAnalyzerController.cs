using JobLensChat.Services;
using JobLensChat.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace JobLensChat.Controllers;

public sealed class OfferAnalyzerController(OfferAnalyzerService analyzer, OfferTextExtractor extractor) : Controller
{
    [HttpGet("/OfferAnalyzer")]
    public IActionResult Index() => View(new OfferAnalyzerPageVm());

    [HttpPost("/OfferAnalyzer")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Analyze(OfferAnalyzerPageVm form, CancellationToken cancellationToken)
    {
        var text = form.Text?.Trim() ?? "";
        if (text.Length is < 100 or > 15_000)
        {
            ModelState.AddModelError(nameof(form.Text), "Paste 100 to 15,000 characters.");
            return View("Index", form);
        }

        form.Result = await analyzer.AnalyzeAsync(text, recordCheck: true, cancellationToken);
        form.Text = "";
        return View("Index", form);
    }

    [HttpPost("/OfferAnalyzer/Upload")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(2_200_000)]
    public async Task<IActionResult> Upload(IFormFile? file, CancellationToken cancellationToken)
    {
        var page = new OfferAnalyzerPageVm();
        if (file is null || file.Length == 0)
        {
            page.UploadError = "Choose a PDF or Word file.";
            return View("Index", page);
        }

        if (file.Length > OfferTextExtractor.MaxBytes)
        {
            page.UploadError = "The file must be 2 MB or smaller.";
            return View("Index", page);
        }

        await using var stream = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        var extracted = extractor.Extract(file.FileName, buffer.ToArray());
        if (!extracted.Success)
        {
            page.UploadError = extracted.Error;
            return View("Index", page);
        }

        page.Result = await analyzer.AnalyzeAsync(extracted.Text, recordCheck: true, cancellationToken);
        return View("Index", page);
    }
}
