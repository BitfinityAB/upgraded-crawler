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

    private static UpgradedAssignmentService CreateService(string pageHtml)
    {
        var handler = new FakeHttpMessageHandler(new Dictionary<string, HttpResponseMessage>
        {
            ["https://upgraded.se/lediga-uppdrag/"] =
                new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(pageHtml) },
        });

        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient(handler));

        return new UpgradedAssignmentService(factory, Substitute.For<ILogging>());
    }

    [Fact]
    public async Task ParsesAssignmentsFromFixture()
    {
        using var db = _fixture.CreateContext();
        var service = CreateService(TestFixtureLoader.Load("upgraded-page.html"));

        var result = (await service.GetAssignmentAnnouncementsAsync(db)).ToList();

        Assert.Equal(2, result.Count);
        Assert.Equal("172126", result[0].AssignmentId);
        Assert.Equal("https://upgraded.se/konsultuppdrag/net-backend-system-arkitekt/", result[0].Url);
        Assert.Equal(".Net backend/System arkitekt", result[0].Title);
        Assert.Equal("172122", result[1].AssignmentId);
        Assert.Equal("https://upgraded.se/konsultuppdrag/projektledare-152/", result[1].Url);
        Assert.Equal("Projektledare", result[1].Title);
    }

    [Fact]
    public async Task ThrowsWhenPageHasNoAssignmentCards()
    {
        using var db = _fixture.CreateContext();
        var service = CreateService("<html><body><p>Lediga uppdrag</p></body></html>");

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetAssignmentAnnouncementsAsync(db));
    }
}
