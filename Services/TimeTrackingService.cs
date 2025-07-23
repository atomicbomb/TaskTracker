using Microsoft.EntityFrameworkCore;
using TaskTracker.Data;
using TaskTracker.Models;

namespace TaskTracker.Services;

public interface ITimeTrackingService
{
    Task<TimeEntry?> GetActiveTimeEntryAsync();
    Task StartTrackingAsync(int taskId);
    Task StopTrackingAsync();
    Task SwitchTaskAsync(int newTaskId);
    Task<List<TimeEntry>> GetTimeEntriesForDateAsync(DateOnly date);
    Task<List<TimeEntry>> GetTimeEntriesAsync(DateTime startDate, DateTime endDate);
    Task<TimeEntry?> GetLastTimeEntryAsync();
    bool IsWithinTrackingHours(TimeOnly currentTime, string startTime, string endTime);
    TimeOnly ParseTimeString(string timeString);
}

public class TimeTrackingService : ITimeTrackingService
{
    private readonly TaskTrackerDbContext _dbContext;

    public TimeTrackingService(TaskTrackerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<TimeEntry?> GetActiveTimeEntryAsync()
    {
        return await _dbContext.TimeEntries
            .Include(te => te.Task)
            .ThenInclude(t => t.Project)
            .Where(te => te.EndTime == null)
            .OrderByDescending(te => te.StartTime)
            .FirstOrDefaultAsync();
    }

    public async Task StartTrackingAsync(int taskId)
    {
        // Stop any currently active tracking
        await StopTrackingAsync();

        var timeEntry = new TimeEntry
        {
            TaskId = taskId,
            StartTime = DateTime.Now,
            Date = DateOnly.FromDateTime(DateTime.Now)
        };

        _dbContext.TimeEntries.Add(timeEntry);
        await _dbContext.SaveChangesAsync();
    }

    public async Task StopTrackingAsync()
    {
        var activeEntry = await GetActiveTimeEntryAsync();
        if (activeEntry != null)
        {
            activeEntry.EndTime = DateTime.Now;
            await _dbContext.SaveChangesAsync();
        }
    }

    public async Task SwitchTaskAsync(int newTaskId)
    {
        var activeEntry = await GetActiveTimeEntryAsync();
        
        // If already tracking the same task, do nothing
        if (activeEntry?.TaskId == newTaskId) return;

        // Stop current tracking and start new
        await StopTrackingAsync();
        await StartTrackingAsync(newTaskId);
    }

    public async Task<List<TimeEntry>> GetTimeEntriesForDateAsync(DateOnly date)
    {
        return await _dbContext.TimeEntries
            .Include(te => te.Task)
            .ThenInclude(t => t.Project)
            .Where(te => te.Date == date)
            .OrderBy(te => te.StartTime)
            .ToListAsync();
    }

    public async Task<List<TimeEntry>> GetTimeEntriesAsync(DateTime startDate, DateTime endDate)
    {
        return await _dbContext.TimeEntries
            .Include(te => te.Task)
            .ThenInclude(t => t.Project)
            .Where(te => te.StartTime >= startDate && te.StartTime < endDate)
            .OrderBy(te => te.StartTime)
            .ToListAsync();
    }

    public async Task<TimeEntry?> GetLastTimeEntryAsync()
    {
        return await _dbContext.TimeEntries
            .Include(te => te.Task)
            .ThenInclude(t => t.Project)
            .OrderByDescending(te => te.StartTime)
            .FirstOrDefaultAsync();
    }

    public bool IsWithinTrackingHours(TimeOnly currentTime, string startTime, string endTime)
    {
        try
        {
            var start = ParseTimeString(startTime);
            var end = ParseTimeString(endTime);
            
            return currentTime >= start && currentTime <= end;
        }
        catch
        {
            return false;
        }
    }

    public TimeOnly ParseTimeString(string timeString)
    {
        // Handle formats like "09:00", "9:00 AM", "17:30", etc.
        if (TimeOnly.TryParse(timeString, out var time))
        {
            return time;
        }
        
        // Default fallback
        throw new ArgumentException($"Invalid time format: {timeString}");
    }
}
