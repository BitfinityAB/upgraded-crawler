using Microsoft.EntityFrameworkCore;
using UpgradedCrawler.Core.Entities;

namespace UpgradedCrawler.Core.Data;

public class AppDbContext : DbContext
{
    public DbSet<AssignmentAnnouncement>? Assignments { get; set; }
    public string DbPath { get; private set; }

    public AppDbContext()
    {
        var folder = Environment.SpecialFolder.LocalApplicationData;
        var path = Environment.GetFolderPath(folder);
        DbPath = Path.Join(path, "assignments.db");
    }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
        DbPath = string.Empty;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        if (!options.IsConfigured)
            options.UseSqlite($"Data Source={DbPath}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AssignmentAnnouncement>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.HasIndex(e => new { e.AssignmentId, e.ProviderId })
                  .IsUnique()
                  .HasDatabaseName("IX_Assignments_AssignmentId_ProviderId");
        });
    }
}
