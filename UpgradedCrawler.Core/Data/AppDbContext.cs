using Microsoft.EntityFrameworkCore;
using UpgradedCrawler.Core.Entities;

namespace UpgradedCrawler.Core.Data;

public class AppDbContext : DbContext
{
    public DbSet<AssignmentAnnouncement>? Assignments { get; set; }

    public string DbPath { get; }

    public AppDbContext()
    {
        var folder = Environment.SpecialFolder.LocalApplicationData;
        var path = Environment.GetFolderPath(folder);
        DbPath = Path.Join(path, "assignments.db");
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseSqlite($"Data Source={DbPath}");
}
