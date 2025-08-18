using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using TaskTracker.Models;
using TaskTracker.Models.Jira;

namespace TaskTracker.Services;

public interface IJiraApiService
{
    Task<bool> TestConnectionAsync();
    Task<List<JiraProject>> GetProjectsAsync();
    Task<List<JiraTask>> GetTasksForUserAsync(List<string> projectKeys);
    Task<JiraTask?> GetTaskByKeyAsync(string taskKey);
    Task<(string StatusName, string StatusCategoryKey)?> GetIssueStatusAsync(string taskKey);
}

public class JiraApiService : IJiraApiService, IDisposable
{
    private readonly System.Net.Http.HttpClient _httpClient;
    private readonly JiraSettings _jiraSettings;

    public JiraApiService(System.Net.Http.HttpClient httpClient, JiraSettings jiraSettings)
    {
        _httpClient = httpClient;
        _jiraSettings = jiraSettings;
        
        ConfigureHttpClient();
    }

    private void ConfigureHttpClient()
    {
        if (!_jiraSettings.IsConfigured) return;

        _httpClient.BaseAddress = new Uri(_jiraSettings.ServerUrl);
        
        // Create basic auth header
        var authToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_jiraSettings.Email}:{_jiraSettings.ApiToken}"));
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authToken);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<bool> TestConnectionAsync()
    {
        if (!_jiraSettings.IsConfigured) return false;

        try
        {
            var response = await _httpClient.GetAsync("/rest/api/2/myself");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<JiraProject>> GetProjectsAsync()
    {
        if (!_jiraSettings.IsConfigured) return new List<JiraProject>();

        try
        {
            // Use JQL to find only projects where the user has active tasks
            var jql = "assignee = currentUser() AND statusCategory != Done";
            var encodedJql = Uri.EscapeDataString(jql);
            
            var response = await _httpClient.GetAsync($"/rest/api/2/search?jql={encodedJql}&fields=project&maxResults=1000");
            if (!response.IsSuccessStatusCode) return new List<JiraProject>();

            var content = await response.Content.ReadAsStringAsync();
            var searchResponse = JsonConvert.DeserializeObject<JiraSearchResponse>(content);
            
            if (searchResponse?.Issues == null) return new List<JiraProject>();

            // Extract unique projects from the search results
            var projectMap = new Dictionary<string, JiraProject>();
            foreach (var issue in searchResponse.Issues)
            {
                if (issue.Fields?.Project != null)
                {
                    var projectKey = issue.Fields.Project.Key;
                    if (!projectMap.ContainsKey(projectKey))
                    {
                        projectMap[projectKey] = new JiraProject
                        {
                            ProjectCode = issue.Fields.Project.Key,
                            ProjectName = issue.Fields.Project.Name,
                            LastUpdated = DateTime.UtcNow
                        };
                    }
                }
            }

            return projectMap.Values.OrderBy(p => p.ProjectName).ToList();
        }
        catch
        {
            return new List<JiraProject>();
        }
    }

    public async Task<List<JiraTask>> GetTasksForUserAsync(List<string> projectKeys)
    {
        if (!_jiraSettings.IsConfigured || !projectKeys.Any()) return new List<JiraTask>();

        try
        {
            var projectFilter = string.Join(",", projectKeys);
            var jql = $"assignee = currentUser() AND project in ({projectFilter}) AND statusCategory != Done ORDER BY key ASC";
            var encodedJql = Uri.EscapeDataString(jql);
            
            var response = await _httpClient.GetAsync($"/rest/api/2/search?jql={encodedJql}&fields=summary,status,assignee,project");
            if (!response.IsSuccessStatusCode) return new List<JiraTask>();

            var content = await response.Content.ReadAsStringAsync();
            var searchResponse = JsonConvert.DeserializeObject<JiraSearchResponse>(content);
            
            if (searchResponse?.Issues == null) return new List<JiraTask>();

            var tasks = new List<JiraTask>();
            foreach (var issue in searchResponse.Issues)
            {
                tasks.Add(new JiraTask
                {
                    JiraTaskNumber = issue.Key,
                    Summary = issue.Fields.Summary,
                    LastUpdated = DateTime.UtcNow,
                    StatusName = issue.Fields.Status?.Name,
                    StatusCategoryKey = issue.Fields.Status?.StatusCategory?.Key,
                    // ProjectId will be set when saving to database
                    Project = new JiraProject
                    {
                        ProjectCode = issue.Fields.Project.Key,
                        ProjectName = issue.Fields.Project.Name
                    }
                });
            }

            return tasks;
        }
        catch
        {
            return new List<JiraTask>();
        }
    }

    public async Task<JiraTask?> GetTaskByKeyAsync(string taskKey)
    {
        if (!_jiraSettings.IsConfigured || string.IsNullOrWhiteSpace(taskKey)) return null;

        try
        {
            var response = await _httpClient.GetAsync($"/rest/api/2/issue/{taskKey}?fields=summary,status,assignee,project");
            if (!response.IsSuccessStatusCode) return null;

            var content = await response.Content.ReadAsStringAsync();
            var issue = JsonConvert.DeserializeObject<JiraIssueResponse>(content);
            
            if (issue == null) return null;

            return new JiraTask
            {
                JiraTaskNumber = issue.Key,
                Summary = issue.Fields.Summary,
                LastUpdated = DateTime.UtcNow,
                StatusName = issue.Fields.Status?.Name,
                StatusCategoryKey = issue.Fields.Status?.StatusCategory?.Key,
                Project = new JiraProject
                {
                    ProjectCode = issue.Fields.Project.Key,
                    ProjectName = issue.Fields.Project.Name
                }
            };
        }
        catch
        {
            return null;
        }
    }

    public async Task<(string StatusName, string StatusCategoryKey)?> GetIssueStatusAsync(string taskKey)
    {
        if (!_jiraSettings.IsConfigured || string.IsNullOrWhiteSpace(taskKey)) return null;

        try
        {
            var response = await _httpClient.GetAsync($"/rest/api/2/issue/{taskKey}?fields=status");
            if (!response.IsSuccessStatusCode) return null;

            var content = await response.Content.ReadAsStringAsync();
            var issue = JsonConvert.DeserializeObject<Models.Jira.JiraIssueResponse>(content);
            if (issue?.Fields?.Status == null) return null;
            var statusName = issue.Fields.Status.Name ?? string.Empty;
            var categoryKey = issue.Fields.Status.StatusCategory?.Key ?? string.Empty;
            return (statusName, categoryKey);
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
