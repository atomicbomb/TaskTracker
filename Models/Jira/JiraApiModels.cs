using Newtonsoft.Json;

namespace TaskTracker.Models.Jira;

public class JiraProjectResponse
{
    [JsonProperty("key")]
    public string Key { get; set; } = string.Empty;
    
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;
}

public class JiraIssueResponse
{
    [JsonProperty("key")]
    public string Key { get; set; } = string.Empty;
    
    [JsonProperty("fields")]
    public JiraIssueFields Fields { get; set; } = new();
}

public class JiraIssueFields
{
    [JsonProperty("summary")]
    public string Summary { get; set; } = string.Empty;
    
    [JsonProperty("status")]
    public JiraStatus Status { get; set; } = new();
    
    [JsonProperty("assignee")]
    public JiraUser? Assignee { get; set; }
    
    [JsonProperty("project")]
    public JiraProjectResponse Project { get; set; } = new();
}

public class JiraStatus
{
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonProperty("statusCategory")]
    public JiraStatusCategory StatusCategory { get; set; } = new();
}

public class JiraStatusCategory
{
    [JsonProperty("key")]
    public string Key { get; set; } = string.Empty;
}

public class JiraUser
{
    [JsonProperty("emailAddress")]
    public string EmailAddress { get; set; } = string.Empty;
    
    [JsonProperty("displayName")]
    public string DisplayName { get; set; } = string.Empty;
}

public class JiraSearchResponse
{
    [JsonProperty("issues")]
    public List<JiraIssueResponse> Issues { get; set; } = new();
    
    [JsonProperty("total")]
    public int Total { get; set; }
}
