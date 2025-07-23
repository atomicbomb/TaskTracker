using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TaskTracker.Data;

public class TaskTrackerDbContextFactory : IDesignTimeDbContextFactory<TaskTrackerDbContext>
{
    public TaskTrackerDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<TaskTrackerDbContext>();
        optionsBuilder.UseSqlite("Data Source=tasktracker.db");
        
        return new TaskTrackerDbContext(optionsBuilder.Options);
    }
}
