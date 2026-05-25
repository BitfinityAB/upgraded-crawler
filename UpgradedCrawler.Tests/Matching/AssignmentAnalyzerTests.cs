using NSubstitute;
using UpgradedCrawler.Core.Entities;
using UpgradedCrawler.Core.Interfaces;
using UpgradedCrawler.Service.Matching;
using Xunit;

namespace UpgradedCrawler.Tests.Matching;

public class AssignmentAnalyzerTests
{
    private static AssignmentAnnouncement Ann(string id = "id-001") =>
        new(id, "https://example.com/job", "upgraded", "Senior .NET Developer", DateTime.Now);

    [Fact]
    public async Task ParsesValidJsonResponse()
    {
        var aiClient = Substitute.For<IAiTextClient>();
        const string json = """{"score": 85, "reason": "Good match.", "cold_email": "Hej!", "cover_letter": "Till er,"}""";
        aiClient.CompleteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>())
                .Returns(json);

        var analyzer = new AssignmentAnalyzer(aiClient, Substitute.For<ILogging>());
        var result = await analyzer.AnalyzeAsync(Ann(), "Job description", "My profile", []);

        Assert.Equal(85, result.MatchScore);
        Assert.Equal("Good match.", result.MatchReason);
        Assert.Equal("Hej!", result.ColdEmailDraft);
        Assert.Equal("Till er,", result.CoverLetterDraft);
        Assert.Equal("id-001", result.AssignmentId);
        Assert.Equal("upgraded", result.ProviderId);
    }

    [Fact]
    public async Task MalformedJson_ReturnsScoreMinusOne()
    {
        var aiClient = Substitute.For<IAiTextClient>();
        aiClient.CompleteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>())
                .Returns("not json");

        var logging = Substitute.For<ILogging>();
        var analyzer = new AssignmentAnalyzer(aiClient, logging);
        var result = await analyzer.AnalyzeAsync(Ann("id-002"), "Description", "Profile", []);

        Assert.Equal(-1, result.MatchScore);
        logging.Received().Log(Arg.Any<string>());
    }

    [Fact]
    public async Task FeedbackIncludedInUserMessage()
    {
        var aiClient = Substitute.For<IAiTextClient>();
        aiClient.CompleteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>())
                .Returns("""{"score": 70, "reason": "OK", "cold_email": "E", "cover_letter": "C"}""");

        var analyzer = new AssignmentAnalyzer(aiClient, Substitute.For<ILogging>());
        var feedback = new List<FeedbackEntry> { new("Senior .NET at Volvo", 82, "accepted") };

        await analyzer.AnalyzeAsync(Ann(), "Description", "Profile", feedback);

        await aiClient.Received().CompleteAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Is<string>(u => u.Contains("ACCEPTED") && u.Contains("Volvo")),
            Arg.Any<int>());
    }
}
