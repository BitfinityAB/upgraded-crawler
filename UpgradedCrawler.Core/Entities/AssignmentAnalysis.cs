namespace UpgradedCrawler.Core.Entities;

// Mutable class (not record) — EF change tracking standard pattern.
public class AssignmentAnalysis
{
    public int Id { get; set; }
    public string AssignmentId { get; set; } = string.Empty;
    public string ProviderId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Match score 0–100. 0 = filtered out by title pre-screen. -1 = analysis API call failed.
    /// </summary>
    public int MatchScore { get; set; }
    public string MatchReason { get; set; } = string.Empty;
    public string ColdEmailDraft { get; set; } = string.Empty;
    public string CoverLetterDraft { get; set; } = string.Empty;
    public DateTime AnalyzedAt { get; set; }
}
