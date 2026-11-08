using JobLens.Application.Interfaces;
using System.Text.Json;
using JobLens.Domain.Entities;
using JobLens.Application.Dtos;
using JobLens.Web.ViewModels;
using JobLens.Application.Services;
using JobLens.Application.Common;
using JobLens.Web.Chat;
using JobLens.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace JobLens.Web.Controllers;

public sealed class ChatController(ChatOrchestrator orchestrator, ILogger<ChatController> logger) : Controller
{
    public const string SessionKey = "JobLens.ChatTurns";
    private const int MaxTurns = 12;

    private static readonly JsonSerializerOptions SessionJson = new(JsonSerializerDefaults.Web);

    private static readonly string[] ToolNames =
    [
        "get_domain_report",
        "search_scam_reports",
        "summarize_scams",
        "top_impersonated_companies",
        "check_recruiter_email",
        "lookup_domain_registration",
        "search_jobs",
        "get_company_profile",
        "analyze_offer",
        "search_live_jobs",
    ];

    private static readonly string[] Samples =
    [
        "Which 5 companies are impersonated most?",
        "Has amaz0n-careers.net been reported?",
        "Is hr@rnicrosoft-hiring.com a real Microsoft recruiter?",
        "Give me a summary of all scam reports.",
        "Show approved Equipment purchase scams since September.",
        "Has safe-company-example.com been reported?",
        "When was google.com registered, and how old is the domain?",
    ];

    [HttpGet]
    public IActionResult Index()
    {
        return View(BuildModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(LoginRateLimiting.ExpensivePolicyName)]
    public async Task<IActionResult> Ask([Bind(nameof(ChatViewModel.Question))] ChatViewModel model, CancellationToken cancellationToken)
    {
        var question = model.Question?.Trim() ?? "";
        if (ModelState.IsValid && question.Length < 2)
        {
            ModelState.AddModelError(nameof(ChatViewModel.Question), "Ask about job-scam data must be between 2 and 500 characters.");
        }

        if (!ModelState.IsValid)
        {
            var invalid = BuildModel();
            invalid.Question = model.Question ?? "";
            return View("Index", invalid);
        }

        var turns = LoadTurns();
        turns.Add(new ChatTurn("user", question, [], DateTimeOffset.UtcNow));

        try
        {
            var result = await orchestrator.AskAsync(question, cancellationToken);
            turns.Add(new ChatTurn("assistant", result.Answer, result.ToolsUsed, DateTimeOffset.UtcNow));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Chat request failed while calling MCP tools.");
            turns.Add(new ChatTurn(
                "assistant",
                "Sorry — I couldn't reach the JobLens data tools right now. Please try again in a moment.",
                [],
                DateTimeOffset.UtcNow));
        }

        SaveTurns(turns);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Clear()
    {
        HttpContext.Session.Remove(SessionKey);
        return RedirectToAction(nameof(Index));
    }

    private ChatViewModel BuildModel() => new()
    {
        Turns = LoadTurns(),
        ProviderLabel = orchestrator.ProviderLabel,
        IsAiMode = orchestrator.IsAiConfigured,
        AvailableTools = ToolNames,
        SampleQuestions = Samples,
    };

    private List<ChatTurn> LoadTurns()
    {
        var json = HttpContext.Session.GetString(SessionKey);
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<ChatTurn>>(json, SessionJson) ?? [];
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Chat session could not be read. Starting a new transcript.");
            return [];
        }
    }

    private void SaveTurns(List<ChatTurn> turns)
    {
        if (turns.Count > MaxTurns)
        {
            turns = turns.GetRange(turns.Count - MaxTurns, MaxTurns);
        }

        HttpContext.Session.SetString(SessionKey, JsonSerializer.Serialize(turns, SessionJson));
    }
}
