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
    void ShowSummaryWindow();
    Task ShowTaskPromptAsync();
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
        // Set up timer event handlers
        _timerService.TaskPromptRequested += OnTaskPromptRequested;
        _timerService.UpdateDataRequested += OnUpdateDataRequested;
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
                await ShowTaskPromptAsync();
            }
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

        _mainWindow.Show();
        _mainWindow.Activate();
        _mainWindow.WindowState = System.Windows.WindowState.Normal;
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

    public void ShowSummaryWindow()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var viewModel = scope.ServiceProvider.GetRequiredService<SummaryViewModel>();
            var window = new SummaryWindow(viewModel);
            
            // Initialize data after window is created but before showing
            Task.Run(async () =>
            {
                try
                {
                    await viewModel.InitializeAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error initializing Summary window data: {ex.Message}");
                }
            });
            
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

    public async Task ShowTaskPromptAsync()
    {
        try
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
                // Task selection handled by viewmodel
            };

            viewModel.LunchStarted += (s, e) =>
            {
                _timerService.StartLunchBreak(e.DurationMinutes);
            };

            viewModel.PromptTimedOut += async (s, e) =>
            {
                // If no selection made, continue with current task if any
                using var timeScope = _serviceProvider.CreateScope();
                var timeService = timeScope.ServiceProvider.GetRequiredService<ITimeTrackingService>();
                var activeEntry = await timeService.GetActiveTimeEntryAsync();
                
                // Continue silently with existing active task
            };

            // Show the dialog
            var result = window.ShowDialog();
        }
        catch (Exception)
        {
            // Log error silently, don't show message box to user
        }
    }

    private async void OnTaskPromptRequested(object? sender, EventArgs e)
    {
        await ShowTaskPromptAsync();
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
            await ShowTaskPromptAsync();
        });
    }

    private async void OnTrackingStarted(object? sender, EventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("Tracking started for the day");
        
        // Show task prompt to start the day
        await Task.Delay(2000);
        await ShowTaskPromptAsync();
    }

    private async void OnTrackingEnded(object? sender, EventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("Tracking ended for the day");
        
        // Stop any active tracking
        using var scope = _serviceProvider.CreateScope();
        var timeTrackingService = scope.ServiceProvider.GetRequiredService<ITimeTrackingService>();
        await timeTrackingService.StopTrackingAsync();
    }
}
