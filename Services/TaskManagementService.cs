using Microsoft.EntityFrameworkCore;
using TaskTracker.Data;
using TaskTracker.Models;

namespace TaskTracker.Services;

public interface ITaskManagementService
{
    Task<List<JiraProject>> GetProjectsAsync();
    Task<List<JiraProject>> GetSelectedProjectsAsync();
    Task<List<JiraTask>> GetTasksForProjectsAsync(List<int> projectIds);
    Task<List<JiraTask>> GetTasksForProjectsAsync(List<string> projectCodes);
    Task UpdateProjectSelectionAsync(int projectId, bool isSelected);
    Task RefreshProjectsFromJiraAsync();
    Task RefreshTasksFromJiraAsync();
    Task<JiraTask?> AddTaskByNumberAsync(string taskNumber);
    Task<JiraTask> AddManualTaskAsync(int projectId, string summary);
    Task<JiraTask> EnsureLunchTaskAsync();
    Task RemoveTaskAsync(int taskId);
    Task<JiraTask?> GetTaskByIdAsync(int taskId);
    Task<JiraTask?> GetTaskByNumberAsync(string taskNumber);
}

public class TaskManagementService : ITaskManagementService
{
    private readonly TaskTrackerDbContext _dbContext;
    private readonly IJiraApiService _jiraApiService;

    public TaskManagementService(TaskTrackerDbContext dbContext, IJiraApiService jiraApiService)
    {
        _dbContext = dbContext;
        _jiraApiService = jiraApiService;
    }

    public async Task<List<JiraProject>> GetProjectsAsync()
    {
        return await _dbContext.JiraProjects
            .OrderBy(p => p.ProjectCode)
            .ToListAsync();
    }

    public async Task<List<JiraProject>> GetSelectedProjectsAsync()
    {
        return await _dbContext.JiraProjects
            .Where(p => p.IsSelected)
            .OrderBy(p => p.ProjectCode)
            .ToListAsync();
    }

    public async Task<List<JiraTask>> GetTasksForProjectsAsync(List<int> projectIds)
    {
        return await _dbContext.JiraTasks
            .Include(t => t.Project)
            .Where(t => projectIds.Contains(t.ProjectId) && t.IsActive)
            .OrderBy(t => t.JiraTaskNumber)
            .ToListAsync();
    }

    public async Task<List<JiraTask>> GetTasksForProjectsAsync(List<string> projectCodes)
    {
        return await _dbContext.JiraTasks
            .Include(t => t.Project)
            .Where(t => projectCodes.Contains(t.Project.ProjectCode) && t.IsActive)
            .OrderBy(t => t.JiraTaskNumber)
            .ToListAsync();
    }

    public async Task UpdateProjectSelectionAsync(int projectId, bool isSelected)
    {
        var project = await _dbContext.JiraProjects.FindAsync(projectId);
        if (project != null)
        {
            project.IsSelected = isSelected;
            await _dbContext.SaveChangesAsync();
        }
    }

    public async Task RefreshProjectsFromJiraAsync()
    {
        var jiraProjects = await _jiraApiService.GetProjectsAsync();
        
        foreach (var jiraProject in jiraProjects)
        {
            var existingProject = await _dbContext.JiraProjects
                .FirstOrDefaultAsync(p => p.ProjectCode == jiraProject.ProjectCode);

            if (existingProject != null)
            {
                // Update existing project
                existingProject.ProjectName = jiraProject.ProjectName;
                existingProject.LastUpdated = DateTime.UtcNow;
            }
            else
            {
                // Add new project
                _dbContext.JiraProjects.Add(jiraProject);
            }
        }

        await _dbContext.SaveChangesAsync();
    }

    public async Task RefreshTasksFromJiraAsync()
    {
        var selectedProjects = await GetSelectedProjectsAsync();
        if (!selectedProjects.Any()) return;

        var projectKeys = selectedProjects.Select(p => p.ProjectCode).ToList();
        var jiraTasks = await _jiraApiService.GetTasksForUserAsync(projectKeys);

        foreach (var jiraTask in jiraTasks)
        {
            var project = selectedProjects.FirstOrDefault(p => p.ProjectCode == jiraTask.Project.ProjectCode);
            if (project == null) continue;

            var existingTask = await _dbContext.JiraTasks
                .FirstOrDefaultAsync(t => t.JiraTaskNumber == jiraTask.JiraTaskNumber);

            if (existingTask != null)
            {
                // Update existing task
                existingTask.Summary = jiraTask.Summary;
                existingTask.StatusName = jiraTask.StatusName;
                existingTask.StatusCategoryKey = jiraTask.StatusCategoryKey;
                existingTask.LastUpdated = DateTime.UtcNow;
                existingTask.IsActive = true; // Re-activate if it was previously deactivated
            }
            else
            {
                // Create new task without navigation properties to avoid EF tracking issues
                var newTask = new JiraTask
                {
                    JiraTaskNumber = jiraTask.JiraTaskNumber,
                    Summary = jiraTask.Summary,
                    StatusName = jiraTask.StatusName,
                    StatusCategoryKey = jiraTask.StatusCategoryKey,
                    ProjectId = project.Id,
                    IsActive = true,
                    LastUpdated = DateTime.UtcNow
                };
                _dbContext.JiraTasks.Add(newTask);
            }
        }

        await _dbContext.SaveChangesAsync();
    }

    public async Task<JiraTask?> AddTaskByNumberAsync(string taskNumber)
    {
        if (string.IsNullOrWhiteSpace(taskNumber)) return null;

        // Check if task already exists
        var existingTask = await GetTaskByNumberAsync(taskNumber);
        if (existingTask != null) return existingTask;

        // Fetch from JIRA
        var jiraTask = await _jiraApiService.GetTaskByKeyAsync(taskNumber);
        if (jiraTask == null) return null;

        // Find the project
        var project = await _dbContext.JiraProjects
            .FirstOrDefaultAsync(p => p.ProjectCode == jiraTask.Project.ProjectCode);

        if (project == null)
        {
            // Create project if it doesn't exist
            project = new JiraProject
            {
                ProjectCode = jiraTask.Project.ProjectCode,
                ProjectName = jiraTask.Project.ProjectName,
                IsSelected = true, // Auto-select when adding a task
                LastUpdated = DateTime.UtcNow
            };
            _dbContext.JiraProjects.Add(project);
            await _dbContext.SaveChangesAsync();
        }

        // Create new task without navigation properties to avoid EF tracking issues
        var newTask = new JiraTask
        {
            JiraTaskNumber = jiraTask.JiraTaskNumber,
            Summary = jiraTask.Summary,
            ProjectId = project.Id,
            IsActive = true,
            LastUpdated = DateTime.UtcNow
        };
        _dbContext.JiraTasks.Add(newTask);
        await _dbContext.SaveChangesAsync();

        return newTask;
    }

    public async Task<JiraTask> AddManualTaskAsync(int projectId, string summary)
    {
        if (string.IsNullOrWhiteSpace(summary))
            throw new ArgumentException("Summary is required", nameof(summary));

        var project = await _dbContext.JiraProjects.FirstOrDefaultAsync(p => p.Id == projectId)
                      ?? throw new InvalidOperationException($"Project {projectId} not found");

        // Create a pseudo JIRA key for manual tasks, ensure uniqueness by timestamp
        var keyPrefix = project.ProjectCode;
        var key = $"{keyPrefix}-MAN-{DateTime.UtcNow:yyyyMMddHHmmssfff}";

        var task = new JiraTask
        {
            JiraTaskNumber = key,
            Summary = summary,
            ProjectId = project.Id,
            IsActive = true,
            LastUpdated = DateTime.UtcNow
        };

        _dbContext.JiraTasks.Add(task);
        await _dbContext.SaveChangesAsync();
        return task;
    }

    /// <summary>
    /// Ensures a special internal LUNCH task exists (for non-loggable lunch break time) and returns it.
    /// Creates an INTERNAL project if necessary.
    /// </summary>
    public async Task<JiraTask> EnsureLunchTaskAsync()
    {
        const string internalProjectCode = "INTERNAL";
        const string internalProjectName = "Internal Tasks";
        const string lunchTaskKey = "LUNCH";

        // Look for existing lunch task
        var existingLunch = await _dbContext.JiraTasks
            .Include(t => t.Project)
            .FirstOrDefaultAsync(t => t.JiraTaskNumber == lunchTaskKey);
        if (existingLunch != null)
        {
            if (!existingLunch.IsActive) existingLunch.IsActive = true;
            await _dbContext.SaveChangesAsync();
            return existingLunch;
        }

        // Ensure internal project exists
        var internalProject = await _dbContext.JiraProjects
            .FirstOrDefaultAsync(p => p.ProjectCode == internalProjectCode);
        if (internalProject == null)
        {
            internalProject = new JiraProject
            {
                ProjectCode = internalProjectCode,
                ProjectName = internalProjectName,
                IsSelected = false, // do not include in normal selection
                LastUpdated = DateTime.UtcNow
            };
            _dbContext.JiraProjects.Add(internalProject);
            await _dbContext.SaveChangesAsync();
        }

        var lunchTask = new JiraTask
        {
            JiraTaskNumber = lunchTaskKey,
            Summary = "Lunch Break",
            ProjectId = internalProject.Id,
            IsActive = true,
            LastUpdated = DateTime.UtcNow
        };
        _dbContext.JiraTasks.Add(lunchTask);
        await _dbContext.SaveChangesAsync();
        return lunchTask;
    }

    public async Task RemoveTaskAsync(int taskId)
    {
        var task = await _dbContext.JiraTasks.FindAsync(taskId);
        if (task != null)
        {
            task.IsActive = false;
            await _dbContext.SaveChangesAsync();
        }
    }

    public async Task<JiraTask?> GetTaskByIdAsync(int taskId)
    {
        return await _dbContext.JiraTasks
            .Include(t => t.Project)
            .FirstOrDefaultAsync(t => t.Id == taskId);
    }

    public async Task<JiraTask?> GetTaskByNumberAsync(string taskNumber)
    {
        return await _dbContext.JiraTasks
            .Include(t => t.Project)
            .FirstOrDefaultAsync(t => t.JiraTaskNumber == taskNumber && t.IsActive);
    }
}
