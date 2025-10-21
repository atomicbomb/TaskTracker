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
    LogHelper.Debug("LoadSettingsAsync invoked", nameof(ConfigurationService));
    LogHelper.Debug($"Config file path: {_configFilePath}", nameof(ConfigurationService));
        
        try
        {
            if (File.Exists(_configFilePath))
            {
                LogHelper.Debug("Config file exists, loading...", nameof(ConfigurationService));
                var json = await File.ReadAllTextAsync(_configFilePath);
                var data = JsonConvert.DeserializeObject<ConfigurationData>(json);
                if (data != null)
                {
                    _configData = data;
                    LogHelper.Info("Configuration loaded successfully", nameof(ConfigurationService));
                    LogHelper.Debug($"JIRA Server: '{_configData.JiraSettings.ServerUrl}'", nameof(ConfigurationService));
                    LogHelper.Debug($"JIRA Email: '{_configData.JiraSettings.Email}'", nameof(ConfigurationService));
                    LogHelper.Debug($"JIRA Token Length: {_configData.JiraSettings.ApiToken.Length}", nameof(ConfigurationService));
                    LogHelper.Debug($"Tracking Start: {_configData.AppSettings.TrackingStartTime}", nameof(ConfigurationService));
                    LogHelper.Debug($"Tracking End: {_configData.AppSettings.TrackingEndTime}", nameof(ConfigurationService));
                    LogHelper.Debug($"JIRA IsConfigured: {_configData.JiraSettings.IsConfigured}", nameof(ConfigurationService));
                }
                else
                {
                    LogHelper.Warn("Config data deserialization returned null", nameof(ConfigurationService));
                }
            }
            else
            {
                LogHelper.Info("Config file does not exist, using defaults", nameof(ConfigurationService));
            }
        }
        catch (Exception ex)
        {
            // Log error and use default settings
            LogHelper.Error($"Error loading settings: {ex.Message}", nameof(ConfigurationService));
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
            LogHelper.Error($"Error saving settings: {ex.Message}", nameof(ConfigurationService));
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
