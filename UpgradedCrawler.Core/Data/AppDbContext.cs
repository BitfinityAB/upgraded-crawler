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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AssignmentAnnouncement>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            
            // Ensure AssignmentId + ProviderId combination is unique
            entity.HasIndex(e => new { e.AssignmentId, e.ProviderId })
                  .IsUnique()
                  .HasDatabaseName("IX_Assignments_AssignmentId_ProviderId");
        });
    }
}
