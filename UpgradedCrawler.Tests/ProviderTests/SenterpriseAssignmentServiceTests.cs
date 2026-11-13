using NSubstitute;
using UpgradedCrawler.Core.Interfaces;
using UpgradedCrawler.Service;
using UpgradedCrawler.Tests.Infrastructure;
using Xunit;

namespace UpgradedCrawler.Tests.ProviderTests;

public class SenterpriseAssignmentServiceTests(SqliteTestFixture fixture) : IClassFixture<SqliteTestFixture>
{
    private readonly SqliteTestFixture _fixture = fixture;

    [Fact]
    public async Task ParsesAssignmentsFromFixture()
    {
        using var db = _fixture.CreateContext();
        var logging = Substitute.For<ILogging>();
        var handler = new FakeHttpMessageHandler(TestFixtureLoader.Load("senterprise-jobs.html"));
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(new HttpClient(handler));

        var service = new SenterpriseAssignmentService(factory, logging);
        var result = await service.GetAssignmentAnnouncementsAsync(db);

        Assert.Equal(17, result.Count);

        var first = result.First();
        Assert.Equal("8732605", first.AssignmentId);
        Assert.Equal("https://jobb.senterprise.se/jobs/8732605-konsultuppdrag-fullstack-java-kotlin-react", first.Url);
        Assert.Equal("Fullstack Java/Kotlin React", first.Title);
        Assert.Equal("senterprise", first.ProviderId);

        Assert.Contains(result, a => a.Title == "Senior Fullstack Engineer – .NET, Azure & AI");
        Assert.Contains(result, a => a.Title == "Interim Senior Commercial Manager");
    }

    [Fact]
    public async Task ReturnsEmptyWhenListMissing()
    {
        using var db = _fixture.CreateContext();
        var logging = Substitute.For<ILogging>();
        var handler = new FakeHttpMessageHandler("<html><body></body></html>");
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(new HttpClient(handler));

        var service = new SenterpriseAssignmentService(factory, logging);
        var result = await service.GetAssignmentAnnouncementsAsync(db);

        Assert.Empty(result);
    }
}
