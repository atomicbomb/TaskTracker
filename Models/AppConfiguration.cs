namespace TaskTracker.Models;

public class AppSettings
{
    public int PromptIntervalMinutes { get; set; } = 15;
    public GoogleSettings Google { get; set; } = new GoogleSettings();
    public int UpdateIntervalMinutes { get; set; } = 15;
    public int PromptTimeoutSeconds { get; set; } = 30;
    public string TrackingStartTime { get; set; } = "09:00";
    public string TrackingEndTime { get; set; } = "17:30";
    public int DefaultLunchDurationMinutes { get; set; } = 60;
    public string Theme { get; set; } = "Light";
}

public class JiraSettings
{
    public string ServerUrl { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string ApiToken { get; set; } = string.Empty;
    
    public bool IsConfigured => 
        !string.IsNullOrWhiteSpace(ServerUrl) && 
        !string.IsNullOrWhiteSpace(Email) && 
        !string.IsNullOrWhiteSpace(ApiToken);
}

public class GoogleSettings
{
    public bool Enabled { get; set; }
    public string ClientId { get; set; } = string.Empty; // optional if using installed app flow
    public string ClientSecret { get; set; } = string.Empty; // optional
    public string RefreshToken { get; set; } = string.Empty;
    public int ScanIntervalMinutes { get; set; } = 60; // how often to scan calendar
    public bool IsConnected => !string.IsNullOrWhiteSpace(RefreshToken);
}
