using Microsoft.Extensions.DependencyInjection;
using TaskTracker.Models;
using TaskTracker.ViewModels;
using TaskTracker.Views;

namespace TaskTracker.Services;

public interface IApplicationService
{
    Task InitializeAsync();
    void ShowMainWindow();
    void ShowSettingsWindow();
    void ShowJiraProjectsWindow();
    void ShowJiraTasksWindow();
    Task ShowSummaryWindow();
    void ShowTaskPrompt();
    Task ExitApplicationAsync();
}

public class ApplicationService : IApplicationService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ITimerService _timerService;
    private readonly IConfigurationService _configurationService;
    private readonly ISystemTrayService _systemTrayService;
    
    private MainWindow? _mainWindow;

    public ApplicationService(
        IServiceProvider serviceProvider,
        ITimerService timerService,
        IConfigurationService configurationService,
        ISystemTrayService systemTrayService)
    {
        _serviceProvider = serviceProvider;
        _timerService = timerService;
        _configurationService = configurationService;
        _systemTrayService = systemTrayService;
    }

    public async Task InitializeAsync()
    {
        try
        {
            // Set up timer event handlers
            _timerService.TaskPromptRequested += OnTaskPromptRequested;
            _timerService.UpdateDataRequested += OnUpdateDataRequested;
            _timerService.CalendarScanRequested += OnCalendarScanRequested;
            _timerService.LunchBreakEnded += OnLunchBreakEnded;
            _timerService.TrackingStarted += OnTrackingStarted;
            _timerService.TrackingEnded += OnTrackingEnded;

            // Start the timer service
            _timerService.Start();

            // Show initial task prompt if we're in tracking hours and JIRA is configured
            var jiraConfigured = _configurationService.JiraSettings.IsConfigured;
            
            if (jiraConfigured)
            {
                var now = TimeOnly.FromDateTime(DateTime.Now);
                using var scope = _serviceProvider.CreateScope();
                var timeTrackingService = scope.ServiceProvider.GetRequiredService<ITimeTrackingService>();
                var taskManagementService = scope.ServiceProvider.GetRequiredService<ITaskManagementService>();

                // Proactively refresh projects and tasks from JIRA
                try
                {
                    await taskManagementService.RefreshProjectsFromJiraAsync();
                    await taskManagementService.RefreshTasksFromJiraAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Initial JIRA refresh failed: {ex.Message}");
                }
                
                // Kick off an initial Google scan if enabled
                try
                {
                    if (_configurationService.AppSettings.Google.Enabled)
                    {
                        var google = scope.ServiceProvider.GetRequiredService<IGoogleIntegrationService>();
                        var today = DateOnly.FromDateTime(DateTime.Now);
                        int diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
                        var start = today.AddDays(-diff);
                        var end = start.AddDays(11);
                        _ = google.ScanCalendarAsync(start, end);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Initial Google scan failed: {ex.Message}");
                }
                
                var isWithinHours = timeTrackingService.IsWithinTrackingHours(
                    now,
                    _configurationService.AppSettings.TrackingStartTime,
                    _configurationService.AppSettings.TrackingEndTime);
                
                // Check if there are any selected projects before showing prompt
                var selectedProjects = await taskManagementService.GetSelectedProjectsAsync();
                
                if (isWithinHours && selectedProjects.Any())
                {
                    // Delay to ensure application is fully loaded
                    await Task.Delay(2000);
                    ShowTaskPrompt();
                }
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Error during application initialization:\n\n{ex.Message}\n\nStack trace:\n{ex.StackTrace}",
                "Initialization Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    public void ShowMainWindow()
    {
        if (_mainWindow == null)
        {
            using var scope = _serviceProvider.CreateScope();
            var mainViewModel = scope.ServiceProvider.GetRequiredService<MainViewModel>();
            _mainWindow = new MainWindow(mainViewModel);
        }

        // If window is hidden, show it
        if (!_mainWindow.IsVisible)
        {
            _mainWindow.Show();
        }
        
        // Bring to front and activate
        _mainWindow.Activate();
        _mainWindow.WindowState = System.Windows.WindowState.Normal;
        _mainWindow.BringIntoView();
    }

    public void ShowSettingsWindow()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var viewModel = scope.ServiceProvider.GetRequiredService<SettingsViewModel>();
            var window = new SettingsWindow(viewModel);
            
            window.ShowDialog();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Error opening Settings window:\n\n{ex.Message}\n\nStack trace:\n{ex.StackTrace}",
                "Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    public void ShowJiraProjectsWindow()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var viewModel = scope.ServiceProvider.GetRequiredService<JiraProjectsViewModel>();
            var window = new JiraProjectsWindow(viewModel);
            
            window.ShowDialog();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Error opening JIRA Projects window:\n\n{ex.Message}",
                "Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    public void ShowJiraTasksWindow()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var viewModel = scope.ServiceProvider.GetRequiredService<JiraTasksViewModel>();
            var window = new JiraTasksWindow(viewModel);
            
            window.ShowDialog();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Error opening JIRA Tasks window:\n\n{ex.Message}",
                "Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    public async Task ShowSummaryWindow()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var viewModel = scope.ServiceProvider.GetRequiredService<SummaryViewModel>();
            var window = new SummaryWindow(viewModel);
            
            // Initialize data on the UI thread
            try
            {
                await viewModel.InitializeAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing Summary window data: {ex.Message}");
            }
            
            window.ShowDialog();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Error opening Summary window:\n\n{ex.Message}",
                "Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    public void ShowTaskPrompt()
    {
        try
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                using var scope = _serviceProvider.CreateScope();
                var viewModel = scope.ServiceProvider.GetRequiredService<TaskPromptViewModel>();
                var window = new TaskPromptWindow(viewModel);

                // Make sure the window is visible and on top
                window.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen;
                window.Topmost = true;
                window.ShowInTaskbar = true;
                window.WindowState = System.Windows.WindowState.Normal;

                // Set up event handlers
                viewModel.TaskSelected += (s, e) =>
                {
                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"Task selected: {e.SelectedTask.Summary} (ID: {e.SelectedTask.Id})");
                        // Task selection and switching is already handled by the viewmodel
                        // Just log the selection
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error in TaskSelected event handler: {ex.Message}");
                        System.Windows.MessageBox.Show(
                            $"Error handling task selection:\n\n{ex.Message}\n\nStack trace:\n{ex.StackTrace}",
                            "Task Selection Error",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Error);
                    }
                };

                viewModel.LunchStarted += (s, e) =>
                {
                    try
                    {
                        _timerService.StartLunchBreak(e.DurationMinutes);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error starting lunch break: {ex.Message}");
                    }
                };

                viewModel.PromptTimedOut += async (s, e) =>
                {
                    try
                    {
                        // If no selection made, continue with current task if any
                        using var timeScope = _serviceProvider.CreateScope();
                        var timeService = timeScope.ServiceProvider.GetRequiredService<ITimeTrackingService>();
                        var activeEntry = await timeService.GetActiveTimeEntryAsync();
                        System.Diagnostics.Debug.WriteLine("Task prompt timed out");
                        // Continue silently with existing active task
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error in PromptTimedOut handler: {ex.Message}");
                    }
                };

                // Show the dialog
                var result = window.ShowDialog();
            });
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Error showing task prompt:\n\n{ex.Message}\n\nStack trace:\n{ex.StackTrace}",
                "Task Prompt Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    private void OnTaskPromptRequested(object? sender, EventArgs e)
    {
        ShowTaskPrompt();
    }

    private async void OnUpdateDataRequested(object? sender, EventArgs e)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var taskManagementService = scope.ServiceProvider.GetRequiredService<ITaskManagementService>();
            
            // Refresh tasks from JIRA
            await taskManagementService.RefreshTasksFromJiraAsync();
            
            System.Diagnostics.Debug.WriteLine("Data updated from JIRA");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error updating data: {ex.Message}");
        }
    }

    private void OnLunchBreakEnded(object? sender, EventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("Lunch break ended");
        
        // Show task prompt to resume work
        _ = Task.Run(async () =>
        {
            await Task.Delay(1000); // Brief delay
            ShowTaskPrompt();
        });
    }

    private async void OnCalendarScanRequested(object? sender, EventArgs e)
    {
        try
        {
            if (!_configurationService.AppSettings.Google.Enabled)
                return;

            using var scope = _serviceProvider.CreateScope();
            var google = scope.ServiceProvider.GetRequiredService<IGoogleIntegrationService>();

            // Compute Monday of current week and Friday of next week
            var today = DateOnly.FromDateTime(DateTime.Now);
            int diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
            var start = today.AddDays(-diff);
            // Friday of next week = start + 11 days
            var end = start.AddDays(11);

            var added = await google.ScanCalendarAsync(start, end);
            System.Diagnostics.Debug.WriteLine($"Google scan complete, tasks added: {added}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error during Google calendar scan: {ex.Message}");
        }
    }

    private async void OnTrackingStarted(object? sender, EventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("Tracking started for the day");
        
        // Show task prompt to start the day
        await Task.Delay(2000);
        ShowTaskPrompt();
    }

    private async void OnTrackingEnded(object? sender, EventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("Tracking ended for the day");
        
        // Stop any active tracking
        using var scope = _serviceProvider.CreateScope();
        var timeTrackingService = scope.ServiceProvider.GetRequiredService<ITimeTrackingService>();
        await timeTrackingService.StopTrackingAsync();
    }

    public async Task ExitApplicationAsync()
    {
        try
        {
            // Stop any active tracking
            using var scope = _serviceProvider.CreateScope();
            var timeTrackingService = scope.ServiceProvider.GetRequiredService<ITimeTrackingService>();
            await timeTrackingService.StopTrackingAsync();
            
            // Save configuration
            await _configurationService.SaveSettingsAsync();
            
            // Shutdown the application
            System.Windows.Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error during application exit: {ex.Message}");
            // Force exit even if there's an error
            System.Windows.Application.Current.Shutdown();
        }
    }
}
