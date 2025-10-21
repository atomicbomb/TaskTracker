using Microsoft.EntityFrameworkCore;
using TaskTracker.Data;
using TaskTracker.Models;

namespace TaskTracker.Services;

public interface ILoggingService
{
    Task LogAsync(string level, string message, string? source = null, string? details = null, string? eventId = null, string? correlationId = null);
    Task<List<LogEntry>> GetLogsAsync(DateTime? fromUtc = null, DateTime? toUtc = null, string? level = null, string? source = null, string? text = null, int max = 500);
}

public class LoggingService : ILoggingService
{
    private readonly IDbContextFactory<TaskTrackerDbContext> _dbFactory;
    private static readonly SemaphoreSlim _writeLock = new(1,1);

    public LoggingService(IDbContextFactory<TaskTrackerDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task LogAsync(string level, string message, string? source = null, string? details = null, string? eventId = null, string? correlationId = null)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        try
        {
            await _writeLock.WaitAsync();
            await using var db = _dbFactory.CreateDbContext();
            db.LogEntries.Add(new LogEntry
            {
                Level = level,
                Message = message.Length > 500 ? message[..500] : message,
                Details = details,
                Source = source ?? "General",
                EventId = eventId,
                CorrelationId = correlationId,
                ThreadId = Environment.CurrentManagedThreadId.ToString(),
                User = Environment.UserName,
                UtcTimestamp = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
        catch { /* swallow logging errors */ }
        finally
        {
            if (_writeLock.CurrentCount == 0) _writeLock.Release();
        }
    }

    public async Task<List<LogEntry>> GetLogsAsync(DateTime? fromUtc = null, DateTime? toUtc = null, string? level = null, string? source = null, string? text = null, int max = 500)
    {
        await using var db = _dbFactory.CreateDbContext();
        var q = db.LogEntries.AsNoTracking().OrderByDescending(l => l.UtcTimestamp).AsQueryable();
        if (fromUtc.HasValue) q = q.Where(l => l.UtcTimestamp >= fromUtc.Value);
        if (toUtc.HasValue) q = q.Where(l => l.UtcTimestamp <= toUtc.Value);
        if (!string.IsNullOrWhiteSpace(level)) q = q.Where(l => l.Level == level);
        if (!string.IsNullOrWhiteSpace(source)) q = q.Where(l => l.Source == source);
        if (!string.IsNullOrWhiteSpace(text)) q = q.Where(l => l.Message.Contains(text) || (l.Details != null && l.Details.Contains(text)));
        return await q.Take(max).ToListAsync();
    }
}