namespace UpgradedCrawler.Core.Entities;

public record MatchingOptions
{
    public bool Enabled { get; init; } = false;
    public int ScoreThreshold { get; init; } = 70;
    public string ProfileFolder { get; init; } = "profile";
    public string CvFileName { get; init; } = "cv.pdf";
    public string LinkedInExportFolder { get; init; } = "linkedin-export";
    public string DraftsFolder { get; init; } = string.Empty;  // validated at startup when Enabled = true
    public string AnthropicApiKey { get; init; } = string.Empty;
}
