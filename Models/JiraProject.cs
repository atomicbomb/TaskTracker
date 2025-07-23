using System.ComponentModel.DataAnnotations;

namespace TaskTracker.Models;

public class JiraProject
{
    public int Id { get; set; }
    
    [Required]
    public string ProjectCode { get; set; } = string.Empty;
    
    [Required]
    public string ProjectName { get; set; } = string.Empty;
    
    public bool IsSelected { get; set; }
    
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    
    // Navigation property
    public virtual ICollection<JiraTask> Tasks { get; set; } = new List<JiraTask>();
}
