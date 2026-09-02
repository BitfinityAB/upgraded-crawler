using UpgradedCrawler.Service.Matching;
using Xunit;

namespace UpgradedCrawler.Tests.Matching;

public class FeedbackLoaderTests
{
    private static string TempDraftsDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(Path.Combine(dir, "accepted"));
        Directory.CreateDirectory(Path.Combine(dir, "rejected"));
        return dir;
    }

    [Fact]
    public async Task LoadsFeedbackFromBothFolders()
    {
        var dir = TempDraftsDir();
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(dir, "accepted", "2026-05-20_upgraded_JOB-001.md"),
                "# Senior .NET Developer — Score: 82/100\nContent.");
            await File.WriteAllTextAsync(
                Path.Combine(dir, "rejected", "2026-05-19_missprym_mp-007.md"),
                "# Fullstack on-site — Score: 71/100\nContent.");

            var loader = new FeedbackLoader(dir);
            var result = await loader.LoadAsync();

            Assert.Equal(2, result.Count);
            Assert.Contains(result, f => f.Verdict == "accepted" && f.Score == 82 && f.Title.Contains("Senior .NET"));
            Assert.Contains(result, f => f.Verdict == "rejected" && f.Score == 71);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task EmptyFolders_ReturnsEmptyList()
    {
        var dir = TempDraftsDir();
        try
        {
            var loader = new FeedbackLoader(dir);
            var result = await loader.LoadAsync();
            Assert.Empty(result);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task ReturnsAtMost20Entries()
    {
        var dir = TempDraftsDir();
        try
        {
            for (int i = 0; i < 25; i++)
            {
                await File.WriteAllTextAsync(
                    Path.Combine(dir, "accepted", $"2026-05-{i:D2}_upgraded_JOB-{i:D3}.md"),
                    $"# Title {i} — Score: {50 + i}/100\nContent.");
            }

            var loader = new FeedbackLoader(dir);
            var result = await loader.LoadAsync();

            Assert.Equal(20, result.Count);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}
