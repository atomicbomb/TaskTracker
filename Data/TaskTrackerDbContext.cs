using Microsoft.EntityFrameworkCore;
using TaskTracker.Models;

namespace TaskTracker.Data;

public class TaskTrackerDbContext : DbContext
{
    public TaskTrackerDbContext(DbContextOptions<TaskTrackerDbContext> options) : base(options)
    {
    }

    public DbSet<JiraProject> JiraProjects { get; set; }
    public DbSet<JiraTask> JiraTasks { get; set; }
    public DbSet<TimeEntry> TimeEntries { get; set; }
    public DbSet<LogEntry> LogEntries { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure JiraProject
        modelBuilder.Entity<JiraProject>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ProjectCode).IsUnique();
            entity.Property(e => e.ProjectCode).HasMaxLength(50);
            entity.Property(e => e.ProjectName).HasMaxLength(200);
        });

        // Configure JiraTask
        modelBuilder.Entity<JiraTask>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.JiraTaskNumber).IsUnique();
            entity.Property(e => e.JiraTaskNumber).HasMaxLength(50);
            entity.Property(e => e.Summary).HasMaxLength(500);
            
            entity.HasOne(e => e.Project)
                  .WithMany(p => p.Tasks)
                  .HasForeignKey(e => e.ProjectId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure TimeEntry
        modelBuilder.Entity<TimeEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Date).HasConversion<DateOnlyConverter, DateOnlyComparer>();
            
            entity.HasOne(e => e.Task)
                  .WithMany(t => t.TimeEntries)
                  .HasForeignKey(e => e.TaskId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure LogEntry
        modelBuilder.Entity<LogEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UtcTimestamp);
            entity.HasIndex(e => e.Level);
            entity.HasIndex(e => e.Source);
            entity.Property(e => e.Message).IsRequired();
        });
    }
}

// Custom converter for DateOnly (SQLite doesn't natively support DateOnly)
public class DateOnlyConverter : Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateOnly, DateTime>
{
    public DateOnlyConverter() : base(
        dateOnly => dateOnly.ToDateTime(TimeOnly.MinValue),
        dateTime => DateOnly.FromDateTime(dateTime))
    {
    }
}

public class DateOnlyComparer : Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<DateOnly>
{
    public DateOnlyComparer() : base(
        (x, y) => x == y,
        dateOnly => dateOnly.GetHashCode())
    {
    }
}
