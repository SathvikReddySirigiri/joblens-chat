using JobLensChat.Data;
using JobLensChat.Models;
using JobLensChat.Services;
using Microsoft.AspNetCore.Mvc;

namespace JobLensChat.Controllers;

public sealed class DataCheckController : Controller
{
    private static readonly string[] RiskEmails =
    [
        "hr@amaz0n-careers.net",
        "talent@rnicrosoft-hiring.com",
        "recruiting@google.com",
        "deloitte.jobs2026@gmail.com",
        "hr@safe-company-example.com",
    ];

    private readonly ScamRepository _repository;
    private readonly RecruiterRiskService _riskService;
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DataCheckController> _logger;
    private readonly RdapService _rdap;

    public DataCheckController(
        ScamRepository repository,
        RecruiterRiskService riskService,
        IWebHostEnvironment environment,
        IConfiguration configuration,
        ILogger<DataCheckController> logger,
        RdapService rdap)
    {
        _repository = repository;
        _riskService = riskService;
        _environment = environment;
        _configuration = configuration;
        _logger = logger;
        _rdap = rdap;
    }

    [HttpGet("/DataCheck")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        var risks = new List<RecruiterRiskResult>(RiskEmails.Length);
        foreach (var email in RiskEmails)
        {
            risks.Add(await _riskService.CheckAsync(email, cancellationToken));
        }

        var model = new DataCheckViewModel(
            DatabasePath.Resolve(_configuration, _environment),
            await _repository.GetCountsAsync(cancellationToken),
            await _repository.TopImpersonatedCompaniesAsync(5, null, cancellationToken),
            await _repository.SummarizeAsync(null, null, cancellationToken),
            await _repository.GetReportsByDomainAsync("amaz0n-careers.net", cancellationToken),
            risks,
            [
                await _rdap.LookupAsync("google.com", cancellationToken),
                await _rdap.LookupAsync("amaz0n-careers.net", cancellationToken),
            ]);

        return View(model);
    }

    [HttpPost("/DataCheck/Reset")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reset(CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        try
        {
            await DatabaseInitializer.ResetAsync(_configuration, _environment, _logger, cancellationToken);
            TempData["ResetMessage"] = "Demo data was reset.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Demo data reset failed.");
            TempData["ResetMessage"] = "Demo data could not be reset. See the server log for details.";
        }

        return RedirectToAction(nameof(Index));
    }
}
