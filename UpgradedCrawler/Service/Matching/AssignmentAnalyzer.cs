using System.Text.Json;
using System.Text.Json.Serialization;
using UpgradedCrawler.Core.Entities;
using UpgradedCrawler.Core.Interfaces;

namespace UpgradedCrawler.Service.Matching;

public class AssignmentAnalyzer(IAiTextClient aiClient, ILogging logging)
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private const string SystemPrompt = """
        You are a career advisor helping a senior .NET/fullstack developer find consulting
        assignments in Sweden. You will receive the user's profile and an assignment description.
        Analyze the match and write application materials in Swedish.

        Return JSON:
        {
          "score": <integer 0-100>,
          "reason": "<2-3 sentences in English explaining the match score>",
          "cold_email": "<complete cold email in Swedish, addressed to the staffing company>",
          "cover_letter": "<complete cover letter (personligt brev) in Swedish>"
        }

        Score guide: 80-100 = strong match, 60-79 = decent match, below 60 = weak match.
        """;

    public async Task<AssignmentAnalysis> AnalyzeAsync(
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

        var json = await aiClient.CompleteAsync("claude-sonnet-4-6", SystemPrompt, userMessage, maxTokens: 4000);

        try
        {
            var parsed = JsonSerializer.Deserialize<AnalysisResponse>(json, JsonOpts)!;
            return new AssignmentAnalysis
            {
                AssignmentId = announcement.AssignmentId,
                ProviderId = announcement.ProviderId,
                Description = description,
                MatchScore = parsed.Score,
                MatchReason = parsed.Reason ?? string.Empty,
                ColdEmailDraft = parsed.ColdEmail ?? string.Empty,
                CoverLetterDraft = parsed.CoverLetter ?? string.Empty,
                AnalyzedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            logging.Log($"AssignmentAnalyzer: failed to parse response for '{announcement.AssignmentId}': {ex.Message}");
            return new AssignmentAnalysis
            {
                AssignmentId = announcement.AssignmentId,
                ProviderId = announcement.ProviderId,
                Description = description,
                MatchScore = -1,
                MatchReason = "Analysis failed",
                AnalyzedAt = DateTime.UtcNow
            };
        }
    }

    private static string BuildFeedbackSection(IReadOnlyList<FeedbackEntry> feedback)
    {
        var lines = feedback.Select(f => $"- {f.Verdict.ToUpper()} (score {f.Score}): \"{f.Title}\"");
        return $"\n=== Your feedback on previous matches ===\n{string.Join("\n", lines)}\n\nUse this to calibrate your score for the current assignment.";
    }
}

internal class AnalysisResponse
{
    [JsonPropertyName("score")] public int Score { get; set; }
    [JsonPropertyName("reason")] public string? Reason { get; set; }
    [JsonPropertyName("cold_email")] public string? ColdEmail { get; set; }
    [JsonPropertyName("cover_letter")] public string? CoverLetter { get; set; }
}
