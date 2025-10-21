using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaskTracker.Models;

public class LogEntry
{
    public long Id { get; set; }

    [Required]
    public DateTime UtcTimestamp { get; set; } = DateTime.UtcNow;

    [MaxLength(50)]
    public string Level { get; set; } = "Debug"; // Debug, Info, Warn, Error

    [MaxLength(200)]
    public string Source { get; set; } = string.Empty; // Class or category

    [MaxLength(100)]
    public string? EventId { get; set; }

    [MaxLength(500)]
    public string Message { get; set; } = string.Empty;

    public string? Details { get; set; } // Full text, stack trace, etc.

    [MaxLength(100)]
    public string? ThreadId { get; set; }

    [MaxLength(100)]
    public string? User { get; set; }

    // Correlation for grouping operations
    [MaxLength(100)]
    public string? CorrelationId { get; set; }
}