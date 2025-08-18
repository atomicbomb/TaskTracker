using System.IO;
using Newtonsoft.Json;
using TaskTracker.Models;

namespace TaskTracker.Services;

public interface IConfigurationService
{
    AppSettings AppSettings { get; }
    JiraSettings JiraSettings { get; }
    GoogleSettings GoogleSettings { get; }
    Task SaveSettingsAsync();
    Task LoadSettingsAsync();
    void UpdateJiraSettings(string serverUrl, string email, string apiToken);
    void UpdateAppSettings(AppSettings newSettings);
    void UpdateGoogleSettings(GoogleSettings newSettings);
}

public class ConfigurationService : IConfigurationService
{
    private readonly string _configFilePath;
    private ConfigurationData _configData;

    public AppSettings AppSettings => _configData.AppSettings;
    public JiraSettings JiraSettings => _configData.JiraSettings;
    public GoogleSettings GoogleSettings => _configData.AppSettings.Google;

    public ConfigurationService()
    {
        _configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
        _configData = new ConfigurationData();
    }

    public async Task LoadSettingsAsync()
    {
        System.Diagnostics.Debug.WriteLine("=== ConfigurationService.LoadSettingsAsync() ===");
        System.Diagnostics.Debug.WriteLine($"Config file path: {_configFilePath}");
        
        try
        {
            if (File.Exists(_configFilePath))
            {
                System.Diagnostics.Debug.WriteLine("Config file exists, loading...");
                var json = await File.ReadAllTextAsync(_configFilePath);
                var data = JsonConvert.DeserializeObject<ConfigurationData>(json);
                if (data != null)
                {
                    _configData = data;
                    System.Diagnostics.Debug.WriteLine("Configuration loaded successfully");
                    System.Diagnostics.Debug.WriteLine($"JIRA Server: '{_configData.JiraSettings.ServerUrl}'");
                    System.Diagnostics.Debug.WriteLine($"JIRA Email: '{_configData.JiraSettings.Email}'");
                    System.Diagnostics.Debug.WriteLine($"JIRA Token Length: {_configData.JiraSettings.ApiToken.Length}");
                    System.Diagnostics.Debug.WriteLine($"Tracking Start: {_configData.AppSettings.TrackingStartTime}");
                    System.Diagnostics.Debug.WriteLine($"Tracking End: {_configData.AppSettings.TrackingEndTime}");
                    System.Diagnostics.Debug.WriteLine($"JIRA IsConfigured: {_configData.JiraSettings.IsConfigured}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Config data deserialization returned null");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Config file does not exist, using defaults");
            }
        }
        catch (Exception ex)
        {
            // Log error and use default settings
            System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
        }
    }

    public async Task SaveSettingsAsync()
    {
        try
        {
            var json = JsonConvert.SerializeObject(_configData, Formatting.Indented);
            await File.WriteAllTextAsync(_configFilePath, json);
        }
        catch (Exception ex)
        {
            // Log error
            System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
        }
    }

    public void UpdateJiraSettings(string serverUrl, string email, string apiToken)
    {
        _configData.JiraSettings.ServerUrl = serverUrl?.Trim() ?? string.Empty;
        _configData.JiraSettings.Email = email?.Trim() ?? string.Empty;
        _configData.JiraSettings.ApiToken = apiToken?.Trim() ?? string.Empty;
    }

    public void UpdateAppSettings(AppSettings newSettings)
    {
        _configData.AppSettings = newSettings;
    }

    public void UpdateGoogleSettings(GoogleSettings newSettings)
    {
        _configData.AppSettings.Google = newSettings;
    }

    private class ConfigurationData
    {
        public AppSettings AppSettings { get; set; } = new();
        public JiraSettings JiraSettings { get; set; } = new();
    }
}
