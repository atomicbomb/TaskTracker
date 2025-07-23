using System.IO;
using System.Net.Http;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using TaskTracker.Data;
using TaskTracker.Services;
using TaskTracker.ViewModels;
using TaskTracker.Models;

namespace TaskTracker;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private IServiceProvider _serviceProvider = null!;
    private ISystemTrayService _systemTrayService = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Configure services
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        // Initialize database
        await InitializeDatabaseAsync();

        // Load configuration
        var configService = _serviceProvider.GetRequiredService<IConfigurationService>();
        await configService.LoadSettingsAsync();

        // Load theme
        LoadTheme(configService.AppSettings.Theme);

        // Initialize system tray
        _systemTrayService = _serviceProvider.GetRequiredService<ISystemTrayService>();
        _systemTrayService.Initialize();
        _systemTrayService.MainWindowRequested += OnMainWindowRequested;
        _systemTrayService.ExitRequested += OnExitRequested;

        // Initialize application service (starts timers and task prompting)
        var applicationService = _serviceProvider.GetRequiredService<IApplicationService>();
        await applicationService.InitializeAsync();

        // Don't show main window initially - app runs in system tray
        ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Database
        services.AddDbContext<TaskTrackerDbContext>(options =>
        {
            var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tasktracker.db");
            options.UseSqlite($"Data Source={dbPath}");
        });

        // Services
        services.AddSingleton<IConfigurationService, ConfigurationService>();
        services.AddSingleton<ISystemTrayService, SystemTrayService>();
        services.AddSingleton<ITimerService, TimerService>();
        services.AddSingleton<IApplicationService, ApplicationService>();
        services.AddScoped<ITimeTrackingService, TimeTrackingService>();
        services.AddScoped<ITaskManagementService, TaskManagementService>();
        
        // HTTP Client and JIRA Service
        services.AddHttpClient();
        services.AddScoped<IJiraApiService>(provider =>
        {
            var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient();
            var configService = provider.GetRequiredService<IConfigurationService>();
            return new JiraApiService(httpClient, configService.JiraSettings);
        });

        // ViewModels
        services.AddTransient<MainViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<JiraProjectsViewModel>();
        services.AddTransient<JiraTasksViewModel>();
        services.AddTransient<SummaryViewModel>();
        services.AddTransient<TaskPromptViewModel>();
        
        // Views
        services.AddTransient<MainWindow>(provider =>
        {
            var viewModel = provider.GetRequiredService<MainViewModel>();
            return new MainWindow(viewModel);
        });
    }

    private async Task InitializeDatabaseAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TaskTrackerDbContext>();
        await context.Database.EnsureCreatedAsync();
    }

    private void LoadTheme(string theme)
    {
        var themeUri = theme.ToLower() == "dark" 
            ? new Uri("Themes/DarkTheme.xaml", UriKind.Relative)
            : new Uri("Themes/LightTheme.xaml", UriKind.Relative);

        try
        {
            var themeDict = new ResourceDictionary { Source = themeUri };
            Resources.MergedDictionaries.Clear();
            Resources.MergedDictionaries.Add(themeDict);
        }
        catch (Exception ex)
        {
            // Log error and continue with default theme
            System.Diagnostics.Debug.WriteLine($"Error loading theme: {ex.Message}");
        }
    }

    private void OnMainWindowRequested(object? sender, EventArgs e)
    {
        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
        mainWindow.Activate();
    }

    private async void OnExitRequested(object? sender, EventArgs e)
    {
        // Stop any active time tracking
        using var scope = _serviceProvider.CreateScope();
        var timeTrackingService = scope.ServiceProvider.GetRequiredService<ITimeTrackingService>();
        await timeTrackingService.StopTrackingAsync();

        // Save configuration
        var configService = _serviceProvider.GetRequiredService<IConfigurationService>();
        await configService.SaveSettingsAsync();

        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Ensure cleanup
        _systemTrayService?.Dispose();
        
        if (_serviceProvider is IDisposable disposable)
            disposable.Dispose();

        base.OnExit(e);
    }

    public void ChangeTheme(string theme)
    {
        LoadTheme(theme);
    }

    public T GetService<T>() where T : class
    {
        return _serviceProvider.GetRequiredService<T>();
    }
}

