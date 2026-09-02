using NSubstitute;
using UpgradedCrawler.Core.Entities;
using UpgradedCrawler.Core.Interfaces;
using UpgradedCrawler.Service.Matching;
using Xunit;

namespace UpgradedCrawler.Tests.Matching;

public class TitlePreFilterTests
{
    private static AssignmentAnnouncement A(string id, string title) =>
        new(id, $"https://example.com/{id}", "p", title, DateTime.Now);

    [Fact]
    public async Task ReturnsRelevantIndices()
    {
        var aiClient = Substitute.For<IAiTextClient>();
        aiClient.CompleteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>())
                .Returns("""{"relevant": [0, 2]}""");

        var filter = new TitlePreFilter(aiClient, Substitute.For<ILogging>());
        var assignments = new List<AssignmentAnnouncement>
        {
            A("a", "Senior .NET Developer"),
            A("b", "Art Director"),
            A("c", "React Developer"),
        };

        var result = await filter.FilterAsync(assignments, []);

        Assert.Equal(new HashSet<int> { 0, 2 }, result);
    }

    [Fact]
    public async Task MalformedJson_IncludesAllAssignments()
    {
        var aiClient = Substitute.For<IAiTextClient>();
        aiClient.CompleteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>())
                .Returns("not valid json");

        var filter = new TitlePreFilter(aiClient, Substitute.For<ILogging>());
        var assignments = new List<AssignmentAnnouncement> { A("a", "Title A"), A("b", "Title B") };

        var result = await filter.FilterAsync(assignments, []);

        Assert.Equal(new HashSet<int> { 0, 1 }, result);
    }

    [Fact]
    public async Task EmptyInput_ReturnsEmptySet()
    {
        var aiClient = Substitute.For<IAiTextClient>();
        var filter = new TitlePreFilter(aiClient, Substitute.For<ILogging>());

        var result = await filter.FilterAsync([], []);

        Assert.Empty(result);
        await aiClient.DidNotReceive().CompleteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>());
    }

    [Fact]
    public async Task FeedbackAppendedToSystemPrompt()
    {
        var aiClient = Substitute.For<IAiTextClient>();
        aiClient.CompleteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>())
                .Returns("""{"relevant": [0]}""");

        var filter = new TitlePreFilter(aiClient, Substitute.For<ILogging>());
        var feedback = new List<FeedbackEntry>
        {
            new("Senior .NET at Volvo", 82, "accepted"),
        };

        await filter.FilterAsync([A("a", "Senior Developer")], feedback);

        await aiClient.Received().CompleteAsync(
            Arg.Any<string>(),
            Arg.Is<string>(s => s.Contains("ACCEPTED") && s.Contains("Volvo")),
            Arg.Any<string>(),
            Arg.Any<int>());
    }
}
