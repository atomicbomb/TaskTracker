using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaskTracker.Models;

public class TimeEntry
{
    public int Id { get; set; }
    
    [Required]
    public DateTime StartTime { get; set; }
    
    public DateTime? EndTime { get; set; }
    
    [Required]
    public DateOnly Date { get; set; }
    
    // Foreign key
    public int TaskId { get; set; }
    
    // Navigation property
    [ForeignKey("TaskId")]
    public virtual JiraTask Task { get; set; } = null!;
    
    // Optional user comment
    public string? Comment { get; set; }
    
    // Calculated property for duration
    [NotMapped]
    public TimeSpan? Duration => EndTime.HasValue ? EndTime.Value - StartTime : null;
    
    // Calculated property for current duration (if still active)
    [NotMapped]
    public TimeSpan CurrentDuration => (EndTime ?? DateTime.Now) - StartTime;

    // Convenience for UI binding: total minutes (uses EndTime if set, otherwise now)
    [NotMapped]
    public int DurationMinutes
    {
        get
        {
            var effectiveEnd = EndTime ?? DateTime.Now;
            if (effectiveEnd < StartTime) return 0;
            return (int)Math.Round((effectiveEnd - StartTime).TotalMinutes);
        }
    }
}
