using NSubstitute;
using UpgradedCrawler.Core.Interfaces;
using UpgradedCrawler.Service;
using UpgradedCrawler.Tests.Infrastructure;
using Xunit;

namespace UpgradedCrawler.Tests.ProviderTests;

public class AliantAssignmentServiceTests(SqliteTestFixture fixture) : IClassFixture<SqliteTestFixture>
{
    private readonly SqliteTestFixture _fixture = fixture;

    [Fact]
    public async Task ParsesAssignmentFromFixture()
    {
        using var db = _fixture.CreateContext();
        var logging = Substitute.For<ILogging>();
        var handler = new FakeHttpMessageHandler(TestFixtureLoader.Load("aliant-assignments.html"));
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(new HttpClient(handler));

        var service = new AliantAssignmentService(factory, logging);
        var result = await service.GetAssignmentAnnouncementsAsync(db);

        Assert.Single(result);
        Assert.Equal("456", result.First().AssignmentId);
        Assert.Equal("https://aliant.recman.se/job.php?job_id=456", result.First().Url);
        Assert.Equal("Aliant Test Assignment", result.First().Title);
    }
}
