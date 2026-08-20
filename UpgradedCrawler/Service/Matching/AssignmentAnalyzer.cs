using System.Text.Json;
using System.Text.Json.Serialization;
using UpgradedCrawler.Core.Entities;
using UpgradedCrawler.Core.Interfaces;

namespace UpgradedCrawler.Service.Matching;

public class AssignmentAnalyzer(IAiTextClient aiClient, ILogging logging)
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private const string ScoreSystemPrompt = """
        You are a career advisor helping a senior .NET/fullstack developer find consulting
        assignments in Sweden. You will receive the user's profile and an assignment description.
        Analyze the match quality.

        Return JSON only:
        {
          "score": <integer 0-100>,
          "reason": "<2-3 sentences in English explaining the match score>"
        }

        Score guide: 80-100 = strong match, 60-79 = decent match, below 60 = weak match.
        """;

    private const string DraftSystemPrompt = """
        You are a career advisor helping a senior .NET/fullstack developer apply for consulting
        assignments in Sweden. You will receive the user's profile, an assignment description,
        and a match analysis. Write application materials in Swedish.

        Return JSON only:
        {
          "cold_email": "<complete cold email in Swedish, addressed to the staffing company>",
          "cover_letter": "<complete cover letter (personligt brev) in Swedish>"
        }
        """;

    public async Task<(int Score, string Reason)> ScoreAsync(
        AssignmentAnnouncement announcement,
        string description,
        string profileText,
        IReadOnlyList<FeedbackEntry> feedback)
    {
        var feedbackSection = feedback.Count == 0 ? "" : BuildFeedbackSection(feedback);

        var userMessage = $"""
            === My Profile ===
            {profileText}

            === Assignment ===
            Title: {announcement.Title}
            URL: {announcement.Url}

            {description}
            {feedbackSection}
            """;

        var raw = await aiClient.CompleteAsync("claude-sonnet-4-6", ScoreSystemPrompt, userMessage, maxTokens: 512);
        var json = StripCodeFence(raw);

        try
        {
            var parsed = JsonSerializer.Deserialize<ScoreResponse>(json, JsonOpts)!;
            return (parsed.Score, parsed.Reason ?? string.Empty);
        }
        catch (Exception ex)
        {
            logging.Log($"AssignmentAnalyzer: failed to parse score response for '{announcement.AssignmentId}': {ex.Message}");
            return (-1, "Analysis failed");
        }
    }

    public async Task<(string ColdEmail, string CoverLetter)> GenerateDraftsAsync(
        AssignmentAnnouncement announcement,
        string description,
        string profileText,
        int score,
        string reason)
    {
        var userMessage = $"""
            === My Profile ===
            {profileText}

            === Assignment ===
            Title: {announcement.Title}
            URL: {announcement.Url}

            {description}

            === Match Analysis ===
            Score: {score}/100
            {reason}
            """;

        var raw = await aiClient.CompleteAsync("claude-sonnet-4-6", DraftSystemPrompt, userMessage, maxTokens: 4000);
        var json = StripCodeFence(raw);

        try
        {
            var parsed = JsonSerializer.Deserialize<DraftResponse>(json, JsonOpts)!;
            return (parsed.ColdEmail ?? string.Empty, parsed.CoverLetter ?? string.Empty);
        }
        catch (Exception ex)
        {
            logging.Log($"AssignmentAnalyzer: failed to parse draft response for '{announcement.AssignmentId}': {ex.Message}");
            return (string.Empty, string.Empty);
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

    private static string BuildFeedbackSection(IReadOnlyList<FeedbackEntry> feedback)
    {
        var lines = feedback.Select(f => $"- {f.Verdict.ToUpper()} (score {f.Score}): \"{f.Title}\"");
        return $"\n=== Your feedback on previous matches ===\n{string.Join("\n", lines)}\n\nUse this to calibrate your score for the current assignment.";
    }
}

internal class ScoreResponse
{
    [JsonPropertyName("score")] public int Score { get; set; }
    [JsonPropertyName("reason")] public string? Reason { get; set; }
}

internal class DraftResponse
{
    [JsonPropertyName("cold_email")] public string? ColdEmail { get; set; }
    [JsonPropertyName("cover_letter")] public string? CoverLetter { get; set; }
}
