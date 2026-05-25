using NSubstitute;
using UpgradedCrawler.Core.Entities;
using UpgradedCrawler.Core.Interfaces;
using UpgradedCrawler.Service.Matching;
using Xunit;

namespace UpgradedCrawler.Tests.Matching;

public class ProfileLoaderTests
{
    private static string TempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public async Task LoadsPreferencesSection()
    {
        var dir = TempDir();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(dir, "preferences.md"), "I want remote work.");
            var opts = new MatchingOptions { ProfileFolder = dir, CvFileName = "cv.pdf" };
            var loader = new ProfileLoader(opts, Substitute.For<ILogging>());

            var result = await loader.LoadAsync();

            Assert.Contains("=== Preferences ===", result);
            Assert.Contains("I want remote work.", result);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task MissingCv_LogsWarningAndContinues()
    {
        var dir = TempDir();
        try
        {
            var logging = Substitute.For<ILogging>();
            var opts = new MatchingOptions { ProfileFolder = dir, CvFileName = "cv.pdf" };
            var loader = new ProfileLoader(opts, logging);

            var result = await loader.LoadAsync();

            logging.Received().Log(Arg.Is<string>(s => s.Contains("cv.pdf")));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task LoadsLinkedInProfileCsv()
    {
        var dir = TempDir();
        try
        {
            var liDir = Path.Combine(dir, "linkedin-export");
            Directory.CreateDirectory(liDir);
            await File.WriteAllTextAsync(
                Path.Combine(liDir, "Profile.csv"),
                "First Name,Last Name,Headline,Summary\nJohn,Doe,Senior .NET Developer,Expert in C#");

            var opts = new MatchingOptions
            {
                ProfileFolder = dir,
                CvFileName = "cv.pdf",
                LinkedInExportFolder = "linkedin-export"
            };
            var loader = new ProfileLoader(opts, Substitute.For<ILogging>());

            var result = await loader.LoadAsync();

            Assert.Contains("=== LinkedIn Profile ===", result);
            Assert.Contains("Senior .NET Developer", result);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}
