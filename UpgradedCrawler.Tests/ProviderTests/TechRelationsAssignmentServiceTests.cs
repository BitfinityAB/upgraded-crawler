using NSubstitute;
using UpgradedCrawler.Core.Interfaces;
using UpgradedCrawler.Service;
using UpgradedCrawler.Tests.Infrastructure;
using Xunit;

namespace UpgradedCrawler.Tests.ProviderTests;

public class TechRelationsAssignmentServiceTests(SqliteTestFixture fixture) : IClassFixture<SqliteTestFixture>
{
    private readonly SqliteTestFixture _fixture = fixture;

    private static string LoadFixture(string filename)
    {
        var assembly = typeof(SqliteTestFixture).Assembly;
        var name = assembly.GetManifestResourceNames().Single(n => n.EndsWith(filename));
        using var stream = assembly.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [Fact]
    public async Task ParsesOnlyUnassignedAssignmentsFromFixture()
    {
        using var db = _fixture.CreateContext();
        var logging = Substitute.For<ILogging>();
        var handler = new FakeHttpMessageHandler(LoadFixture("techrelations-assignments.json"));
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(new HttpClient(handler));

        var service = new TechRelationsAssignmentService(factory, logging);
        var result = await service.GetAssignmentAnnouncementsAsync(db);

        Assert.Single(result);
        Assert.Equal("999", result.First().AssignmentId);
        Assert.Equal("https://www.techrelations.se/konsultuppdrag/999", result.First().Url);
        Assert.Equal("TechRelations Test Assignment", result.First().Title);
    }
}
