using System.Net;
using NSubstitute;
using UpgradedCrawler.Core.Interfaces;
using UpgradedCrawler.Service;
using UpgradedCrawler.Tests.Infrastructure;
using Xunit;

namespace UpgradedCrawler.Tests.ProviderTests;

public class UpgradedAssignmentServiceTests(SqliteTestFixture fixture) : IClassFixture<SqliteTestFixture>
{
    private readonly SqliteTestFixture _fixture = fixture;

    [Fact]
    public async Task ParsesAssignmentFromFixture()
    {
        using var db = _fixture.CreateContext();
        var logging = Substitute.For<ILogging>();

        var handler = new FakeHttpMessageHandler(new Dictionary<string, HttpResponseMessage>
        {
            ["https://upgraded.se/lediga-uppdrag/"] =
                new HttpResponseMessage(HttpStatusCode.OK)
                { Content = new StringContent(TestFixtureLoader.Load("upgraded-nonce.html")) },
            ["https://upgraded.se/wp-admin/admin-ajax.php"] =
                new HttpResponseMessage(HttpStatusCode.OK)
                { Content = new StringContent(TestFixtureLoader.Load("upgraded-assignments.json")) },
        });

        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient(handler));

        var service = new UpgradedAssignmentService(factory, logging);
        var result = await service.GetAssignmentAnnouncementsAsync(db);

        Assert.Single(result);
        Assert.Equal("JOB-001", result.First().AssignmentId);
        Assert.Equal("https://upgraded.se/job/JOB-001", result.First().Url);
        Assert.Equal("Upgraded Test Assignment", result.First().Title);
    }
}
