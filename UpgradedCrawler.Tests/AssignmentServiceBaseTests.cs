using NSubstitute;
using UpgradedCrawler.Core.Interfaces;
using UpgradedCrawler.Service;
using UpgradedCrawler.Tests.Infrastructure;
using Xunit;

namespace UpgradedCrawler.Tests;

public class AssignmentServiceBaseTests(SqliteTestFixture fixture) : IClassFixture<SqliteTestFixture>
{
    private readonly SqliteTestFixture _fixture = fixture;

    private static FakeService CreateService(string providerId, IEnumerable<(string id, string url, string title, string description)> items)
    {
        var logging = Substitute.For<ILogging>();
        var factory = Substitute.For<IHttpClientFactory>();
        return new FakeService(factory, logging, providerId, items);
    }

    [Fact]
    public async Task NewAssignments_AreReturnedAndPersisted()
    {
        using var db = _fixture.CreateContext();
        var service = CreateService("fake-new", [("id-1", "https://example.com/1", "Title One", "")]);

        var result = await service.GetAssignmentAnnouncementsAsync(db);

        Assert.Single(result);
        Assert.Equal("id-1", result.First().AssignmentId);
        Assert.Equal("https://example.com/1", result.First().Url);
        Assert.Equal("Title One", result.First().Title);
        Assert.Single(db.Assignments!.Where(a => a.ProviderId == "fake-new"));
    }

    [Fact]
    public async Task ExistingAssignment_IsNotReturnedAgain()
    {
        using var db = _fixture.CreateContext();
        var service = CreateService("fake-dup", [("id-dup", "https://example.com/dup", "Dup", "")]);

        await service.GetAssignmentAnnouncementsAsync(db);
        var secondRun = await service.GetAssignmentAnnouncementsAsync(db);

        Assert.Empty(secondRun);
        Assert.Single(db.Assignments!.Where(a => a.AssignmentId == "id-dup" && a.ProviderId == "fake-dup"));
    }

    [Fact]
    public async Task OldAssignmentNotOnWebsite_IsRemovedAfter30Days()
    {
        using var db = _fixture.CreateContext();

        var old = new UpgradedCrawler.Core.Entities.AssignmentAnnouncement(
            "old-id", "https://example.com/old", "fake-old", "Old Title", DateTime.Now.AddDays(-31));
        db.Assignments!.Add(old);
        await db.SaveChangesAsync();

        var service = CreateService("fake-old", [("new-id", "https://example.com/new", "New", "")]);
        await service.GetAssignmentAnnouncementsAsync(db);

        Assert.DoesNotContain(db.Assignments!, a => a.AssignmentId == "old-id");
    }

    [Fact]
    public async Task RecentAssignmentNotOnWebsite_IsKeptWithin30Days()
    {
        using var db = _fixture.CreateContext();

        var recent = new UpgradedCrawler.Core.Entities.AssignmentAnnouncement(
            "recent-id", "https://example.com/recent", "fake-recent", "Recent Title", DateTime.Now.AddDays(-5));
        db.Assignments!.Add(recent);
        await db.SaveChangesAsync();

        var service = CreateService("fake-recent", [("other-id", "https://example.com/other", "Other", "")]);
        await service.GetAssignmentAnnouncementsAsync(db);

        Assert.Contains(db.Assignments!, a => a.AssignmentId == "recent-id");
    }

    private sealed class FakeService(
        IHttpClientFactory factory,
        ILogging logging,
        string providerId,
        IEnumerable<(string id, string url, string title, string description)> items)
        : AssignmentServiceBase(factory, logging)
    {
        protected override string ProviderId => providerId;
        protected override Task<IEnumerable<(string id, string url, string title, string description)>> FetchAssignmentsAsync()
            => Task.FromResult(items);
    }
}
