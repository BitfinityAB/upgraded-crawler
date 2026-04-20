using NSubstitute;
using UpgradedCrawler.Core.Interfaces;
using UpgradedCrawler.Helpers;
using UpgradedCrawler.Tests.Infrastructure;
using Xunit;

namespace UpgradedCrawler.Tests;

public class AssignmentCleanupHelperTests(SqliteTestFixture fixture) : IClassFixture<SqliteTestFixture>
{
    private readonly SqliteTestFixture _fixture = fixture;

    [Fact]
    public async Task StaleAssignment_NotOnWebsite_IsRemoved()
    {
        using var db = _fixture.CreateContext();
        var logging = Substitute.For<ILogging>();

        db.Assignments!.Add(new("stale-1", "https://example.com", "p1", "Stale", DateTime.Now.AddDays(-31)));
        await db.SaveChangesAsync();

        AssignmentCleanupHelper.CleanupOldAssignments(db, "p1", [], logging);
        await db.SaveChangesAsync();

        Assert.Empty(db.Assignments!.Where(a => a.ProviderId == "p1"));
    }

    [Fact]
    public async Task RecentAssignment_NotOnWebsite_IsKept()
    {
        using var db = _fixture.CreateContext();
        var logging = Substitute.For<ILogging>();

        db.Assignments!.Add(new("recent-1", "https://example.com", "p2", "Recent", DateTime.Now.AddDays(-5)));
        await db.SaveChangesAsync();

        AssignmentCleanupHelper.CleanupOldAssignments(db, "p2", [], logging);
        await db.SaveChangesAsync();

        Assert.Single(db.Assignments!.Where(a => a.ProviderId == "p2"));
    }

    [Fact]
    public async Task OldAssignment_StillOnWebsite_IsKept()
    {
        using var db = _fixture.CreateContext();
        var logging = Substitute.For<ILogging>();

        db.Assignments!.Add(new("active-1", "https://example.com", "p3", "Active", DateTime.Now.AddDays(-60)));
        await db.SaveChangesAsync();

        AssignmentCleanupHelper.CleanupOldAssignments(db, "p3", ["active-1"], logging);
        await db.SaveChangesAsync();

        Assert.Single(db.Assignments!.Where(a => a.ProviderId == "p3"));
    }
}
