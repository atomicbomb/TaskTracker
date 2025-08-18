using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaskTracker.Models;

public class JiraTask
{
    public int Id { get; set; }
    
    [Required]
    public string JiraTaskNumber { get; set; } = string.Empty;
    
    [Required]
    public string Summary { get; set; } = string.Empty;
    
    public bool IsActive { get; set; } = true;
    [NotMapped]
    public string? StatusName { get; set; }
    [NotMapped]
    public string? StatusCategoryKey { get; set; }
    
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    
    // Foreign key
    public int ProjectId { get; set; }
    
    // Navigation property
    [ForeignKey("ProjectId")]
    public virtual JiraProject Project { get; set; } = null!;
    
    // Navigation property
    public virtual ICollection<TimeEntry> TimeEntries { get; set; } = new List<TimeEntry>();
}
