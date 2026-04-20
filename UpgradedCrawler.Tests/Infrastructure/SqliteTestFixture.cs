using Microsoft.EntityFrameworkCore;
using UpgradedCrawler.Core.Data;

namespace UpgradedCrawler.Tests.Infrastructure;

public sealed class SqliteTestFixture : IDisposable
{
    private readonly string _dbPath;

    public SqliteTestFixture()
    {
        _dbPath = Path.GetTempFileName();
        using var context = CreateContext();
        context.Database.Migrate();
    }

    public AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;
        return new AppDbContext(options);
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }
}
