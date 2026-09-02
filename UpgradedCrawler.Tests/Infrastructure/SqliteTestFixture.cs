using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using UpgradedCrawler.Core.Data;

namespace UpgradedCrawler.Tests.Infrastructure;

public sealed class SqliteTestFixture : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteConnection _keepAliveConnection;

    public SqliteTestFixture()
    {
        _dbPath = Path.GetTempFileName();
        // Keep a persistent connection open so SQLite doesn't evict the in-memory state,
        // but use Pooling=False so disposal actually closes the handle on Windows.
        var connectionString = $"Data Source={_dbPath};Pooling=False";
        _keepAliveConnection = new SqliteConnection(connectionString);
        _keepAliveConnection.Open();

        using var context = CreateContext();
        context.Database.Migrate();
    }

    public AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_dbPath};Pooling=False")
            .Options;
        return new AppDbContext(options);
    }

    public void Dispose()
    {
        _keepAliveConnection.Close();
        _keepAliveConnection.Dispose();
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }
}
