using System.Net;
using NSubstitute;
using UpgradedCrawler.Core.Interfaces;
using UpgradedCrawler.Service;
using UpgradedCrawler.Tests.Infrastructure;
using Xunit;

namespace UpgradedCrawler.Tests.ProviderTests;

public class TeamPilotAssignmentServiceTests(SqliteTestFixture fixture) : IClassFixture<SqliteTestFixture>
{
    private const string Api = "https://api.teampilot.io/api/public/jobs";
    private readonly SqliteTestFixture _fixture = fixture;

    private static HttpResponseMessage Json(string fixtureName) =>
        new(HttpStatusCode.OK) { Content = new StringContent(TestFixtureLoader.Load(fixtureName)) };

    [Fact]
    public async Task ParsesAssignmentsAcrossPagesFromApi()
    {
        using var db = _fixture.CreateContext();
        var logging = Substitute.For<ILogging>();

        // Second job's detail is deliberately missing (404) to check a failed description doesn't drop the job.
        var handler = new FakeHttpMessageHandler(new Dictionary<string, HttpResponseMessage>
        {
            [$"{Api}?page=1&pageSize=50"] = Json("teampilot-jobs-page1.json"),
            [$"{Api}?page=2&pageSize=50"] = Json("teampilot-jobs-page2.json"),
            [$"{Api}/teampilot-test-assignment"] = Json("teampilot-job-detail.json"),
        });

        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient(handler));

        var service = new TeamPilotAssignmentService(factory, logging);
        var result = await service.GetAssignmentAnnouncementsAsync(db);

        Assert.Equal(2, result.Count);

        var first = result.Single(a => a.AssignmentId == "3f94271c-901d-4bad-ba46-da832ff6a1f6");
        Assert.Equal("https://portal.teampilot.io/jobs/teampilot-test-assignment", first.Url);
        Assert.Equal("TeamPilot Test Assignment", first.Title);
        Assert.Equal("TeamPilot test assignment description.", first.Description);
        Assert.Equal("teampilot", first.ProviderId);

        var second = result.Single(a => a.AssignmentId == "0e4fd066-b5d6-48a2-8232-31f98cd9e3bd");
        Assert.Equal("https://portal.teampilot.io/jobs/0e4fd066-b5d6-48a2-8232-31f98cd9e3bd", second.Url);
        Assert.Equal("Second Assignment", second.Title);
        Assert.Equal("", second.Description);
    }
}
