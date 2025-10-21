using System.Collections.ObjectModel;
using System.Windows.Input;
using TaskTracker.Models;
using TaskTracker.Services;

namespace TaskTracker.ViewModels;

public class TaskPromptViewModel : ViewModelBase
{
    private readonly ITaskManagementService _taskManagementService;
    private readonly ITimeTrackingService _timeTrackingService;
    private readonly ITimerService _timerService;
    private readonly IConfigurationService _configurationService;
    
    private ObservableCollection<JiraProject> _projects = new();
    private ObservableCollection<JiraTask> _tasks = new();
    private JiraProject? _selectedProject;
    private JiraTask? _selectedTask;
    private bool _isLoading;
    private bool _isLunchMode;
    private bool _isManualMode;
    private int _lunchDuration;
    private string _manualSummary = string.Empty;
    private System.Windows.Threading.DispatcherTimer? _timeoutTimer;

    public TaskPromptViewModel(
        ITaskManagementService taskManagementService,
        ITimeTrackingService timeTrackingService,
        ITimerService timerService,
        IConfigurationService configurationService)
    {
        _taskManagementService = taskManagementService ?? throw new ArgumentNullException(nameof(taskManagementService));
        _timeTrackingService = timeTrackingService ?? throw new ArgumentNullException(nameof(timeTrackingService));
        _timerService = timerService ?? throw new ArgumentNullException(nameof(timerService));
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));

        // Initialize commands
        ConfirmCommand = new AsyncRelayCommand(Confirm, () => CanConfirm());
    LunchCommand = new RelayCommand(StartLunch);
    ManualModeCommand = new RelayCommand(ToggleManualMode);
        CancelCommand = new RelayCommand(Cancel);

        // Initialize lunch duration with default
        _lunchDuration = _configurationService.AppSettings.DefaultLunchDurationMinutes;

        LoadDataAsync();
        StartTimeoutTimer();
    }

    public ObservableCollection<JiraProject> Projects
    {
        get => _projects;
        set => SetProperty(ref _projects, value ?? new ObservableCollection<JiraProject>());
    }

    public ObservableCollection<JiraTask> Tasks
    {
        get => _tasks;
        set => SetProperty(ref _tasks, value);
    }

    public JiraProject? SelectedProject
    {
        get => _selectedProject;
        set
        {
            if (SetProperty(ref _selectedProject, value))
            {
                LoadTasksForSelectedProject();
                (ConfirmCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public JiraTask? SelectedTask
    {
        get => _selectedTask;
        set
        {
            SetProperty(ref _selectedTask, value);
            (ConfirmCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public bool IsLunchMode
    {
        get => _isLunchMode;
        set => SetProperty(ref _isLunchMode, value);
    }

    public bool IsManualMode
    {
        get => _isManualMode;
        set
        {
            if (SetProperty(ref _isManualMode, value))
            {
                OnPropertyChanged(nameof(WindowTitle));
                (ConfirmCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public int LunchDuration
    {
        get => _lunchDuration;
        set => SetProperty(ref _lunchDuration, value);
    }

    public string WindowTitle => IsLunchMode ? "Lunch Break" : (IsManualMode ? "Manual Task" : "Current Task");

    public string ManualSummary
    {
        get => _manualSummary;
        set
        {
            if (SetProperty(ref _manualSummary, value))
            {
                (ConfirmCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    // Commands
    public ICommand ConfirmCommand { get; }
    public ICommand LunchCommand { get; }
    public ICommand ManualModeCommand { get; }
    public ICommand CancelCommand { get; }

    // Events
    public event EventHandler<TaskSelectedEventArgs>? TaskSelected;
    public event EventHandler<LunchStartedEventArgs>? LunchStarted;
    public event EventHandler? PromptCancelled;
    public event EventHandler? PromptTimedOut;

    private async void LoadDataAsync()
    {
        try
        {
            IsLoading = true;

            // Load selected projects
            var projects = await _taskManagementService.GetSelectedProjectsAsync();
            Projects = new ObservableCollection<JiraProject>(projects.OrderBy(p => p.ProjectCode));

            // Load last selected task to pre-populate
            var lastEntry = await _timeTrackingService.GetLastTimeEntryAsync();
            if (lastEntry?.Task != null)
            {
                // Find and select the project
                var lastProject = Projects.FirstOrDefault(p => p.Id == lastEntry.Task.ProjectId);
                if (lastProject != null)
                {
                    SelectedProject = lastProject;
                    // SelectedTask will be set when tasks are loaded
                    await Task.Delay(100); // Allow tasks to load
                    SelectedTask = Tasks.FirstOrDefault(t => t.Id == lastEntry.TaskId);
                }
            }
        }
        catch (Exception)
        {
            // Log error but continue
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async void LoadTasksForSelectedProject()
    {
        if (SelectedProject == null)
        {
            Tasks.Clear();
            return;
        }

        try
        {
            IsLoading = true;

            var projectIds = new List<int> { SelectedProject.Id };
            var tasks = await _taskManagementService.GetTasksForProjectsAsync(projectIds);
            
            Tasks = new ObservableCollection<JiraTask>(tasks.OrderBy(t => t.JiraTaskNumber));
            
            // Clear selected task if it's not in the new list
            if (SelectedTask != null && !Tasks.Any(t => t.Id == SelectedTask.Id))
            {
                SelectedTask = null;
            }
        }
        catch (Exception ex)
        {
            LogHelper.Error($"Error loading tasks for project {SelectedProject.ProjectCode}: {ex.Message}", nameof(TaskPromptViewModel));
            Tasks.Clear();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool CanConfirm()
    {
    if (IsLunchMode) return LunchDuration > 0;
    if (IsManualMode) return SelectedProject != null && !string.IsNullOrWhiteSpace(ManualSummary);
    return SelectedProject != null && SelectedTask != null;
    }

    private async Task Confirm()
    {
        try
        {
            LogHelper.Debug("Confirm() called", nameof(TaskPromptViewModel));
            StopTimeoutTimer();

            if (IsLunchMode)
            {
                LogHelper.Info($"Starting lunch break for {LunchDuration} minutes", nameof(TaskPromptViewModel));
                LunchStarted?.Invoke(this, new LunchStartedEventArgs(LunchDuration));
            }
            else if (IsManualMode)
            {
                if (SelectedProject == null || string.IsNullOrWhiteSpace(ManualSummary))
                    return;

                // Create a manual task under the selected project and switch to it
                var manualTask = await _taskManagementService.AddManualTaskAsync(SelectedProject.Id, ManualSummary);
                await _timeTrackingService.SwitchTaskAsync(manualTask.Id);
                TaskSelected?.Invoke(this, new TaskSelectedEventArgs(manualTask));
            }
            else if (SelectedTask != null)
            {
                LogHelper.Info($"Switching to task: {SelectedTask.Summary} (ID: {SelectedTask.Id})", nameof(TaskPromptViewModel));
                
                // Switch to the selected task
                await _timeTrackingService.SwitchTaskAsync(SelectedTask.Id);
                LogHelper.Debug("Task switch completed successfully", nameof(TaskPromptViewModel));
                
                TaskSelected?.Invoke(this, new TaskSelectedEventArgs(SelectedTask));
                LogHelper.Debug("TaskSelected event fired", nameof(TaskPromptViewModel));
            }
            else
            {
                LogHelper.Warn("No task selected in Confirm()", nameof(TaskPromptViewModel));
            }
        }
        catch (Exception ex)
        {
            LogHelper.Error($"Error in Confirm(): {ex.Message}", nameof(TaskPromptViewModel), ex.StackTrace);
            System.Windows.MessageBox.Show(
                $"Error confirming selection:\n\n{ex.Message}\n\nStack trace:\n{ex.StackTrace}",
                "Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    private void StartLunch()
    {
        IsLunchMode = !IsLunchMode;
        OnPropertyChanged(nameof(WindowTitle));
        (ConfirmCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
    }

    private void ToggleManualMode()
    {
        IsManualMode = !IsManualMode;
        if (!IsManualMode)
        {
            ManualSummary = string.Empty;
        }
    }

    private void Cancel()
    {
        StopTimeoutTimer();
        PromptCancelled?.Invoke(this, EventArgs.Empty);
    }

    private void StartTimeoutTimer()
    {
        var timeoutSeconds = _configurationService.AppSettings.PromptTimeoutSeconds;
        _timeoutTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(timeoutSeconds)
        };
        _timeoutTimer.Tick += OnTimeoutTimerTick;
        _timeoutTimer.Start();
    }

    private void StopTimeoutTimer()
    {
        _timeoutTimer?.Stop();
        _timeoutTimer = null;
    }

    private async void OnTimeoutTimerTick(object? sender, EventArgs e)
    {
        StopTimeoutTimer();

        // If a task is already selected (same as last time), continue with it
        if (SelectedTask != null)
        {
            await Confirm();
        }
        else
        {
            PromptTimedOut?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose()
    {
        StopTimeoutTimer();
    }
}

public class TaskSelectedEventArgs : EventArgs
{
    public JiraTask SelectedTask { get; }

    public TaskSelectedEventArgs(JiraTask selectedTask)
    {
        SelectedTask = selectedTask;
    }
}

public class LunchStartedEventArgs : EventArgs
{
    public int DurationMinutes { get; }

    public LunchStartedEventArgs(int durationMinutes)
    {
        DurationMinutes = durationMinutes;
    }
}
