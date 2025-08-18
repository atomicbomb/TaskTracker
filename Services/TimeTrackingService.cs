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
    Task UpdateTimeEntryTaskAsync(int timeEntryId, int newTaskId);
    Task UpdateTimeEntryTimesAsync(int timeEntryId, DateTime newStart, DateTime? newEnd);
    Task<TimeEntry?> GetTimeEntryByIdAsync(int id);
    Task DeleteTimeEntryAsync(int id);
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
            .AsNoTracking()
            .Include(te => te.Task)
            .ThenInclude(t => t.Project)
            .Where(te => te.EndTime == null)
            .OrderByDescending(te => te.StartTime)
            .FirstOrDefaultAsync();
    }

    public async Task StartTrackingAsync(int taskId)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"StartTrackingAsync called with TaskId: {taskId}");
            
            // Check if the task exists in the database
            var taskExists = await _dbContext.JiraTasks.AnyAsync(t => t.Id == taskId);
            if (!taskExists)
            {
                throw new InvalidOperationException($"Task with ID {taskId} does not exist in the database");
            }
            
            // Stop any currently active tracking
            await StopTrackingAsync();

            var timeEntry = new TimeEntry
            {
                TaskId = taskId,
                StartTime = DateTime.Now,
                Date = DateOnly.FromDateTime(DateTime.Now)
            };

            System.Diagnostics.Debug.WriteLine($"Adding time entry for task {taskId} at {timeEntry.StartTime}");
            _dbContext.TimeEntries.Add(timeEntry);
            await _dbContext.SaveChangesAsync();
            System.Diagnostics.Debug.WriteLine("Time entry saved successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in StartTrackingAsync: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            throw; // Re-throw to propagate the error
        }
    }

    public async Task StopTrackingAsync()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("StopTrackingAsync called");
            // Fetch a tracked active entry (do NOT use AsNoTracking here)
            var activeEntry = await _dbContext.TimeEntries
                .Where(te => te.EndTime == null)
                .OrderByDescending(te => te.StartTime)
                .FirstOrDefaultAsync();
            if (activeEntry != null)
            {
                System.Diagnostics.Debug.WriteLine($"Stopping active time entry for task {activeEntry.TaskId}");
                activeEntry.EndTime = DateTime.Now;
                await _dbContext.SaveChangesAsync();
                System.Diagnostics.Debug.WriteLine("Active time entry stopped successfully");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("No active time entry to stop");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in StopTrackingAsync: {ex.Message}");
            throw;
        }
    }

    public async Task SwitchTaskAsync(int newTaskId)
    {
        var activeEntry = await GetActiveTimeEntryAsync();
        
        // If already tracking the same task, do nothing
        if (activeEntry?.TaskId == newTaskId) return;

        // Stop current tracking and start new
        if (activeEntry != null)
        {
            // Ensure we close the current entry with a tracked instance
            var trackedActive = await _dbContext.TimeEntries
                .Where(te => te.Id == activeEntry.Id && te.EndTime == null)
                .FirstOrDefaultAsync();
            if (trackedActive != null)
            {
                trackedActive.EndTime = DateTime.Now;
                await _dbContext.SaveChangesAsync();
            }
        }
        await StartTrackingAsync(newTaskId);
    }

    public async Task<List<TimeEntry>> GetTimeEntriesForDateAsync(DateOnly date)
    {
        return await _dbContext.TimeEntries
            .AsNoTracking()
            .Include(te => te.Task)
            .ThenInclude(t => t.Project)
            .Where(te => te.Date == date)
            .OrderBy(te => te.StartTime)
            .ToListAsync();
    }

    public async Task<List<TimeEntry>> GetTimeEntriesAsync(DateTime startDate, DateTime endDate)
    {
        return await _dbContext.TimeEntries
            .AsNoTracking()
            .Include(te => te.Task)
            .ThenInclude(t => t.Project)
            .Where(te => te.StartTime >= startDate && te.StartTime < endDate)
            .OrderBy(te => te.StartTime)
            .ToListAsync();
    }

    public async Task<TimeEntry?> GetLastTimeEntryAsync()
    {
        return await _dbContext.TimeEntries
            .AsNoTracking()
            .Include(te => te.Task)
            .ThenInclude(t => t.Project)
            .OrderByDescending(te => te.StartTime)
            .FirstOrDefaultAsync();
    }

    public async Task<TimeEntry?> GetTimeEntryByIdAsync(int id)
    {
        return await _dbContext.TimeEntries
            .AsNoTracking()
            .Include(te => te.Task)
            .ThenInclude(t => t.Project)
            .FirstOrDefaultAsync(te => te.Id == id);
    }

    public async Task UpdateTimeEntryTaskAsync(int timeEntryId, int newTaskId)
    {
        var entry = await _dbContext.TimeEntries.FirstOrDefaultAsync(te => te.Id == timeEntryId);
        if (entry == null) throw new InvalidOperationException($"Time entry {timeEntryId} not found");

        var taskExists = await _dbContext.JiraTasks.AnyAsync(t => t.Id == newTaskId);
        if (!taskExists) throw new InvalidOperationException($"Task {newTaskId} not found");

        entry.TaskId = newTaskId;
        await _dbContext.SaveChangesAsync();

        // Ensure navigation is loaded if caller uses this context instance further
        await _dbContext.Entry(entry).Reference(e => e.Task).LoadAsync();
        if (entry.Task != null)
        {
            await _dbContext.Entry(entry.Task).Reference(t => t.Project).LoadAsync();
        }
    }

    public async Task UpdateTimeEntryTimesAsync(int timeEntryId, DateTime newStart, DateTime? newEnd)
    {
        var entry = await _dbContext.TimeEntries.FirstOrDefaultAsync(te => te.Id == timeEntryId)
                    ?? throw new InvalidOperationException($"Time entry {timeEntryId} not found");

        if (newEnd.HasValue && newEnd.Value <= newStart)
            throw new InvalidOperationException("End time must be after start time");

        entry.StartTime = newStart;
        entry.EndTime = newEnd;
        entry.Date = DateOnly.FromDateTime(newStart);
        await _dbContext.SaveChangesAsync();
    }

    public async Task DeleteTimeEntryAsync(int id)
    {
        var entry = await _dbContext.TimeEntries.FirstOrDefaultAsync(te => te.Id == id)
                    ?? throw new InvalidOperationException($"Time entry {id} not found");
        _dbContext.TimeEntries.Remove(entry);
        await _dbContext.SaveChangesAsync();
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
