using System.ComponentModel;
using System.Windows.Input;
using TaskTracker.Models;
using TaskTracker.Services;

namespace TaskTracker.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private readonly IConfigurationService _configurationService;
    private readonly IJiraApiService _jiraApiService;
    private readonly ITimerService _timerService;
    
    private string _serverUrl = string.Empty;
    private string _email = string.Empty;
    private string _apiToken = string.Empty;
    private int _promptInterval = 15;
    private int _updateInterval = 15;
    private int _promptTimeout = 30;
    private string _trackingStartTime = "09:00";
    private string _trackingEndTime = "17:30";
    private int _defaultLunchDuration = 60;
    private string _selectedTheme = "Light";
    private string _testConnectionResult = string.Empty;
    private bool _isTestingConnection = false;

    public SettingsViewModel(
        IConfigurationService configurationService,
        IJiraApiService jiraApiService,
        ITimerService timerService)
    {
        _configurationService = configurationService;
        _jiraApiService = jiraApiService;
        _timerService = timerService;

        // Initialize commands
        TestConnectionCommand = new AsyncRelayCommand(TestConnection, () => !_isTestingConnection);
        SaveSettingsCommand = new AsyncRelayCommand(SaveSettings);
        CancelCommand = new RelayCommand(Cancel);

        LoadSettings();
    }

    // JIRA Integration Properties
    public string ServerUrl
    {
        get => _serverUrl;
        set => SetProperty(ref _serverUrl, value);
    }

    public string Email
    {
        get => _email;
        set => SetProperty(ref _email, value);
    }

    public string ApiToken
    {
        get => _apiToken;
        set => SetProperty(ref _apiToken, value);
    }

    public int PromptInterval
    {
        get => _promptInterval;
        set => SetProperty(ref _promptInterval, value);
    }

    public int UpdateInterval
    {
        get => _updateInterval;
        set => SetProperty(ref _updateInterval, value);
    }

    // Tracking Time Properties
    public string TrackingStartTime
    {
        get => _trackingStartTime;
        set => SetProperty(ref _trackingStartTime, value);
    }

    public string TrackingEndTime
    {
        get => _trackingEndTime;
        set => SetProperty(ref _trackingEndTime, value);
    }

    public int PromptTimeout
    {
        get => _promptTimeout;
        set => SetProperty(ref _promptTimeout, value);
    }

    // Lunch Properties
    public int DefaultLunchDuration
    {
        get => _defaultLunchDuration;
        set => SetProperty(ref _defaultLunchDuration, value);
    }

    // Theme Properties
    public string SelectedTheme
    {
        get => _selectedTheme;
        set => SetProperty(ref _selectedTheme, value);
    }

    public List<string> AvailableThemes => new() { "Light", "Dark" };

    // Test Connection Properties
    public string TestConnectionResult
    {
        get => _testConnectionResult;
        set => SetProperty(ref _testConnectionResult, value);
    }

    public bool IsTestingConnection
    {
        get => _isTestingConnection;
        set
        {
            SetProperty(ref _isTestingConnection, value);
            (TestConnectionCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    // Commands
    public ICommand TestConnectionCommand { get; }
    public ICommand SaveSettingsCommand { get; }
    public ICommand CancelCommand { get; }

    // Events
    public event EventHandler? SettingsSaved;
    public event EventHandler? SettingsCancelled;

    private void LoadSettings()
    {
        var jiraSettings = _configurationService.JiraSettings;
        var appSettings = _configurationService.AppSettings;

        ServerUrl = jiraSettings.ServerUrl;
        Email = jiraSettings.Email;
        ApiToken = jiraSettings.ApiToken;
        PromptInterval = appSettings.PromptIntervalMinutes;
        UpdateInterval = appSettings.UpdateIntervalMinutes;
        PromptTimeout = appSettings.PromptTimeoutSeconds;
        TrackingStartTime = appSettings.TrackingStartTime;
        TrackingEndTime = appSettings.TrackingEndTime;
        DefaultLunchDuration = appSettings.DefaultLunchDurationMinutes;
        SelectedTheme = appSettings.Theme;
    }

    private async Task TestConnection()
    {
        IsTestingConnection = true;
        TestConnectionResult = "Testing connection...";

        try
        {
            // Temporarily update JIRA settings for testing
            var originalSettings = _configurationService.JiraSettings;
            _configurationService.UpdateJiraSettings(ServerUrl, Email, ApiToken);

            var isConnected = await _jiraApiService.TestConnectionAsync();
            
            TestConnectionResult = isConnected 
                ? "✅ Connection successful!" 
                : "❌ Connection failed. Please check your credentials.";

            // Restore original settings if test failed
            if (!isConnected)
            {
                _configurationService.UpdateJiraSettings(
                    originalSettings.ServerUrl, 
                    originalSettings.Email, 
                    originalSettings.ApiToken);
            }
        }
        catch (Exception ex)
        {
            TestConnectionResult = $"❌ Connection error: {ex.Message}";
        }
        finally
        {
            IsTestingConnection = false;
        }
    }

    private async Task SaveSettings()
    {
        try
        {
            // Validate settings
            if (!ValidateSettings()) return;

            // Update JIRA settings
            _configurationService.UpdateJiraSettings(ServerUrl, Email, ApiToken);

            // Update app settings
            var newAppSettings = new AppSettings
            {
                PromptIntervalMinutes = PromptInterval,
                UpdateIntervalMinutes = UpdateInterval,
                PromptTimeoutSeconds = PromptTimeout,
                TrackingStartTime = TrackingStartTime,
                TrackingEndTime = TrackingEndTime,
                DefaultLunchDurationMinutes = DefaultLunchDuration,
                Theme = SelectedTheme
            };
            _configurationService.UpdateAppSettings(newAppSettings);

            // Save to file
            await _configurationService.SaveSettingsAsync();

            // Update timer intervals
            _timerService.SetPromptInterval(PromptInterval);
            _timerService.SetUpdateInterval(UpdateInterval);

            // Update theme if changed
            if (System.Windows.Application.Current is App app)
            {
                app.ChangeTheme(SelectedTheme);
            }

            SettingsSaved?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Error saving settings: {ex.Message}",
                "Settings Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    private void Cancel()
    {
        SettingsCancelled?.Invoke(this, EventArgs.Empty);
    }

    private bool ValidateSettings()
    {
        var errors = new List<string>();

        // Validate JIRA settings if provided
        if (!string.IsNullOrWhiteSpace(ServerUrl) || !string.IsNullOrWhiteSpace(Email) || !string.IsNullOrWhiteSpace(ApiToken))
        {
            if (string.IsNullOrWhiteSpace(ServerUrl))
                errors.Add("Server URL is required");
            else if (!Uri.TryCreate(ServerUrl, UriKind.Absolute, out _))
                errors.Add("Server URL is not valid");

            if (string.IsNullOrWhiteSpace(Email))
                errors.Add("Email is required");

            if (string.IsNullOrWhiteSpace(ApiToken))
                errors.Add("API Token is required");
        }

        // Validate intervals
        if (PromptInterval < 1)
            errors.Add("Prompt interval must be at least 1 minute");

        if (UpdateInterval < 1)
            errors.Add("Update interval must be at least 1 minute");

        if (PromptTimeout < 5)
            errors.Add("Prompt timeout must be at least 5 seconds");

        // Validate times
        try
        {
            var start = TimeOnly.Parse(TrackingStartTime);
            var end = TimeOnly.Parse(TrackingEndTime);
            
            if (end <= start)
                errors.Add("End time must be after start time");
        }
        catch
        {
            errors.Add("Invalid time format (use HH:mm)");
        }

        // Validate lunch duration
        if (DefaultLunchDuration < 1)
            errors.Add("Lunch duration must be at least 1 minute");

        if (errors.Any())
        {
            System.Windows.MessageBox.Show(
                $"Please fix the following errors:\n\n{string.Join("\n", errors)}",
                "Validation Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return false;
        }

        return true;
    }
}
