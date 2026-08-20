using System.Text.Json;
using UpgradedCrawler.Core.Entities;
using UpgradedCrawler.Core.Interfaces;

namespace UpgradedCrawler.Service.Matching;

public class TitlePreFilter(IAiTextClient aiClient, ILogging logging)
{
    private const string BaseSystemPrompt = """
        You are a job relevance filter. The user is a senior .NET and fullstack developer
        (C#, ASP.NET Core, React, Azure, SQL). Return only the indices (0-based) of
        assignments that could plausibly be relevant — include anything technical or
        development-adjacent. Exclude obvious mismatches: art director, project manager
        (non-technical), nurse, teacher, driver, etc. Be inclusive when uncertain.
        Return JSON: {"relevant": [0, 2, 5]}
        """;

    public async Task<HashSet<int>> FilterAsync(
        IList<AssignmentAnnouncement> assignments,
        IReadOnlyList<FeedbackEntry> feedback)
    {
        if (assignments.Count == 0) return [];

        var systemPrompt = feedback.Count == 0
            ? BaseSystemPrompt
            : BuildPromptWithFeedback(feedback);

        var titleList = string.Join("\n", assignments.Select((a, i) => $"{i}. {a.Title}"));

        var raw = await aiClient.CompleteAsync("claude-haiku-4-5-20251001", systemPrompt, titleList, maxTokens: 256);
        var json = StripCodeFence(raw);

        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("relevant")
                      .EnumerateArray()
                      .Select(e => e.GetInt32())
                      .Where(i => i >= 0 && i < assignments.Count)
                      .ToHashSet();
        }
        catch (Exception ex)
        {
            logging.Log($"TitlePreFilter: could not parse response: {ex.Message}. Including all {assignments.Count} assignment(s).");
            return Enumerable.Range(0, assignments.Count).ToHashSet();
        }
    }

    private static string StripCodeFence(string text)
    {
        var trimmed = text.Trim();
        if (!trimmed.StartsWith('`')) return trimmed;
        var firstNewline = trimmed.IndexOf('\n');
        if (firstNewline < 0) return trimmed;
        var body = trimmed[(firstNewline + 1)..];
        var lastFence = body.LastIndexOf("```", StringComparison.Ordinal);
        return lastFence >= 0 ? body[..lastFence].Trim() : body.Trim();
    }

    private static string BuildPromptWithFeedback(IReadOnlyList<FeedbackEntry> feedback)
    {
        var lines = feedback.Select(f => $"- {f.Verdict.ToUpper()} ({f.Score}): \"{f.Title}\"");
        return $"{BaseSystemPrompt}\n\nRecent feedback (use to calibrate inclusivity):\n{string.Join("\n", lines)}";
    }
}
