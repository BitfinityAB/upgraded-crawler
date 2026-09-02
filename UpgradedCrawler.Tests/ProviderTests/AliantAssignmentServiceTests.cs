using System.Net;
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

        var handler = new FakeHttpMessageHandler(new Dictionary<string, HttpResponseMessage>
        {
            ["https://aliant.recman.io/jobs?sort=newest"] =
                new HttpResponseMessage(HttpStatusCode.OK)
                { Content = new StringContent(TestFixtureLoader.Load("aliant-page.html")) },
            ["https://aliant.recman.io/api/jobs?sort=newest"] =
                new HttpResponseMessage(HttpStatusCode.OK)
                { Content = new StringContent(TestFixtureLoader.Load("aliant-jobs.json")) },
            ["https://aliant.recman.io/api/job/456"] =
                new HttpResponseMessage(HttpStatusCode.OK)
                { Content = new StringContent(TestFixtureLoader.Load("aliant-job-456.json")) },
        });

        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient(handler));

        var service = new AliantAssignmentService(factory, logging);
        var result = await service.GetAssignmentAnnouncementsAsync(db);

        Assert.Single(result);
        Assert.Equal("456", result.First().AssignmentId);
        Assert.Equal("https://aliant.recman.io/jobs/456", result.First().Url);
        Assert.Equal("Aliant Test Assignment", result.First().Title);
        Assert.Equal("<p>Aliant test assignment description.</p>", result.First().Description);
    }
}
