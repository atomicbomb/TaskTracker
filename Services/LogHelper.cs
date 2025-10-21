namespace TaskTracker.Services;

public static class LogHelper
{
    public static ILoggingService? Logger { get; set; }

    public static void Debug(string msg, string? source = null) => _ = SafeLog("Debug", msg, source);
    public static void Info(string msg, string? source = null) => _ = SafeLog("Info", msg, source);
    public static void Warn(string msg, string? source = null, string? details = null) => _ = SafeLog("Warn", msg, source, details);
    public static void Error(string msg, string? source = null, string? details = null) => _ = SafeLog("Error", msg, source, details);

    private static async Task SafeLog(string level, string msg, string? source, string? details = null)
    {
        try { if (Logger != null) await Logger.LogAsync(level, msg, source, details); }
        catch { /* ignore */ }
    }
}