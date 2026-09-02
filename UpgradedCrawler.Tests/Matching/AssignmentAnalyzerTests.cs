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
    public async Task ScoreAsync_ParsesValidJsonResponse()
    {
        var aiClient = Substitute.For<IAiTextClient>();
        aiClient.CompleteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>())
                .Returns("""{"score": 85, "reason": "Good match."}""");

        var analyzer = new AssignmentAnalyzer(aiClient, Substitute.For<ILogging>());
        var (score, reason) = await analyzer.ScoreAsync(Ann(), "Job description", "My profile", []);

        Assert.Equal(85, score);
        Assert.Equal("Good match.", reason);
    }

    [Fact]
    public async Task ScoreAsync_MalformedJson_ReturnsMinusOne()
    {
        var aiClient = Substitute.For<IAiTextClient>();
        aiClient.CompleteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>())
                .Returns("not json");

        var logging = Substitute.For<ILogging>();
        var analyzer = new AssignmentAnalyzer(aiClient, logging);
        var (score, reason) = await analyzer.ScoreAsync(Ann("id-002"), "Description", "Profile", []);

        Assert.Equal(-1, score);
        logging.Received().Log(Arg.Any<string>());
    }

    [Fact]
    public async Task ScoreAsync_FeedbackIncludedInUserMessage()
    {
        var aiClient = Substitute.For<IAiTextClient>();
        aiClient.CompleteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>())
                .Returns("""{"score": 70, "reason": "OK"}""");

        var analyzer = new AssignmentAnalyzer(aiClient, Substitute.For<ILogging>());
        var feedback = new List<FeedbackEntry> { new("Senior .NET at Volvo", 82, "accepted") };

        await analyzer.ScoreAsync(Ann(), "Description", "Profile", feedback);

        await aiClient.Received().CompleteAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Is<string>(u => u.Contains("ACCEPTED") && u.Contains("Volvo")),
            Arg.Any<int>());
    }

    [Fact]
    public async Task GenerateDraftsAsync_ParsesValidJsonResponse()
    {
        var aiClient = Substitute.For<IAiTextClient>();
        aiClient.CompleteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>())
                .Returns("""{"cold_email": "Hej!", "cover_letter": "Till er,"}""");

        var analyzer = new AssignmentAnalyzer(aiClient, Substitute.For<ILogging>());
        var (coldEmail, coverLetter) = await analyzer.GenerateDraftsAsync(Ann(), "Description", "Profile", 85, "Good match.");

        Assert.Equal("Hej!", coldEmail);
        Assert.Equal("Till er,", coverLetter);
    }

    [Fact]
    public async Task GenerateDraftsAsync_MalformedJson_ReturnsEmptyStrings()
    {
        var aiClient = Substitute.For<IAiTextClient>();
        aiClient.CompleteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>())
                .Returns("not json");

        var logging = Substitute.For<ILogging>();
        var analyzer = new AssignmentAnalyzer(aiClient, logging);
        var (coldEmail, coverLetter) = await analyzer.GenerateDraftsAsync(Ann(), "Description", "Profile", 85, "Good match.");

        Assert.Equal(string.Empty, coldEmail);
        Assert.Equal(string.Empty, coverLetter);
        logging.Received().Log(Arg.Any<string>());
    }
}
