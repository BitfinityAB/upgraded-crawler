using Microsoft.Extensions.Options;
using NSubstitute;
using UpgradedCrawler.Core.Entities;
using UpgradedCrawler.Core.Interfaces;
using UpgradedCrawler.Service;
using UpgradedCrawler.Tests.Infrastructure;
using Xunit;

namespace UpgradedCrawler.Tests.ProviderTests;

public class MissPrymAssignmentServiceTests(SqliteTestFixture fixture) : IClassFixture<SqliteTestFixture>
{
    private readonly SqliteTestFixture _fixture = fixture;

    [Fact]
    public async Task ParsesAssignmentFromFixture()
    {
        using var db = _fixture.CreateContext();
        var logging = Substitute.For<ILogging>();
        var options = Options.Create(new MissPrymOptions { ApiKey = "test-key" });
        var handler = new FakeHttpMessageHandler(TestFixtureLoader.Load("missprym-assignments.json"));
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(new HttpClient(handler));

        var service = new MissPrymAssignmentService(factory, logging, options);
        var result = await service.GetAssignmentAnnouncementsAsync(db);

        Assert.Single(result);
        Assert.Equal("mp-001", result.First().AssignmentId);
        Assert.Equal("https://hint.missprym.com/job-posting/mp-001", result.First().Url);
        Assert.Equal("MissPrym Test Assignment", result.First().Title);
        Assert.Equal("Detailed MissPrym description.", result.First().Description);
    }
}
