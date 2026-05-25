using UpgradedCrawler.Core.Entities;
using UpgradedCrawler.Service.Matching;
using Xunit;

namespace UpgradedCrawler.Tests.Matching;

public class DraftFileWriterTests
{
    private static string TempDir()
        => Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    private static AssignmentAnnouncement Ann() =>
        new("JOB-001", "https://upgraded.se/job/JOB-001", "upgraded", "Senior .NET Developer", DateTime.Now);

    private static AssignmentAnalysis Analysis() => new()
    {
        AssignmentId = "JOB-001",
        ProviderId = "upgraded",
        MatchScore = 87,
        MatchReason = "Strong .NET match.",
        ColdEmailDraft = "Hej, jag är intresserad...",
        CoverLetterDraft = "Till er,",
        AnalyzedAt = new DateTime(2026, 5, 24, 14, 32, 0, DateTimeKind.Utc)
    };

    [Fact]
    public async Task CreatesFileWithCorrectFilename()
    {
        var dir = TempDir();
        try
        {
            var writer = new DraftFileWriter(dir);
            writer.EnsureFolderStructure();
            var filename = await writer.WriteAsync(Ann(), Analysis());

            Assert.Equal("2026-05-24_upgraded_JOB-001.md", filename);
            Assert.True(File.Exists(Path.Combine(dir, filename)));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task FileContainsExpectedSections()
    {
        var dir = TempDir();
        try
        {
            var writer = new DraftFileWriter(dir);
            writer.EnsureFolderStructure();
            var filename = await writer.WriteAsync(Ann(), Analysis());
            var content = await File.ReadAllTextAsync(Path.Combine(dir, filename));

            Assert.Contains("Score: 87/100", content);
            Assert.Contains("Strong .NET match.", content);
            Assert.Contains("Hej, jag är intresserad...", content);
            Assert.Contains("Till er,", content);
            Assert.Contains("## Cold Email", content);
            Assert.Contains("## Cover Letter", content);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task EnsureFolderStructure_CreatesSubfolders()
    {
        var dir = TempDir();
        try
        {
            var writer = new DraftFileWriter(dir);
            writer.EnsureFolderStructure();

            Assert.True(Directory.Exists(Path.Combine(dir, "accepted")));
            Assert.True(Directory.Exists(Path.Combine(dir, "rejected")));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task OverwritesExistingFile()
    {
        var dir = TempDir();
        try
        {
            var writer = new DraftFileWriter(dir);
            writer.EnsureFolderStructure();

            var filename = await writer.WriteAsync(Ann(), Analysis());
            var filename2 = await writer.WriteAsync(Ann(), Analysis());

            Assert.Equal(filename, filename2);
            Assert.Single(Directory.GetFiles(dir, "*.md"));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}
