using System.Collections.ObjectModel;
using System.Windows.Input;
using TaskTracker.Models;
using TaskTracker.Services;

namespace TaskTracker.ViewModels;

public class JiraTaskViewModel : ViewModelBase
{
    private readonly JiraTask _task;

    public JiraTaskViewModel(JiraTask task)
    {
        _task = task;
    }

    public int Id => _task.Id;
    public string ProjectName => _task.Project.ProjectName;
    public string JiraTaskNumber => _task.JiraTaskNumber;
    public string Summary => _task.Summary;
    public DateTime LastUpdated => _task.LastUpdated;
    public bool IsActive => _task.IsActive;

    public JiraTask ToModel() => _task;
}

public class JiraTasksViewModel : ViewModelBase
{
    private readonly ITaskManagementService _taskManagementService;
    private readonly IConfigurationService _configurationService;
    
    private bool _isLoading;
    private string _statusMessage = "Ready";
    private string _newTaskNumber = string.Empty;
    private ObservableCollection<JiraTaskViewModel> _tasks = new();

    public JiraTasksViewModel(
        ITaskManagementService taskManagementService,
        IConfigurationService configurationService)
    {
        _taskManagementService = taskManagementService;
        _configurationService = configurationService;

        // Initialize commands
        RefreshFromJiraCommand = new AsyncRelayCommand(RefreshFromJira, () => !_isLoading);
        AddTaskCommand = new AsyncRelayCommand(AddTask, () => !string.IsNullOrWhiteSpace(_newTaskNumber));
        RemoveSelectedTasksCommand = new AsyncRelayCommand(RemoveSelectedTasks);
        CloseCommand = new RelayCommand(Close);

        LoadTasksAsync();
    }

    public ObservableCollection<JiraTaskViewModel> Tasks
    {
        get => _tasks;
        set => SetProperty(ref _tasks, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            SetProperty(ref _isLoading, value);
            (RefreshFromJiraCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string NewTaskNumber
    {
        get => _newTaskNumber;
        set
        {
            SetProperty(ref _newTaskNumber, value);
            (AddTaskCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    // Commands
    public ICommand RefreshFromJiraCommand { get; }
    public ICommand AddTaskCommand { get; }
    public ICommand RemoveSelectedTasksCommand { get; }
    public ICommand CloseCommand { get; }

    // Events
    public event EventHandler? CloseRequested;

    private async void LoadTasksAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Loading tasks...";

            var selectedProjects = await _taskManagementService.GetSelectedProjectsAsync();
            if (!selectedProjects.Any())
            {
                StatusMessage = "No projects selected. Please select projects first.";
                return;
            }

            var projectIds = selectedProjects.Select(p => p.Id).ToList();
            var tasks = await _taskManagementService.GetTasksForProjectsAsync(projectIds);
            
            var taskViewModels = tasks.Select(t => new JiraTaskViewModel(t)).ToList();
            Tasks = new ObservableCollection<JiraTaskViewModel>(taskViewModels);

            StatusMessage = $"Loaded {tasks.Count} tasks";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading tasks: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task RefreshFromJira()
    {
        if (!_configurationService.JiraSettings.IsConfigured)
        {
            System.Windows.MessageBox.Show(
                "JIRA integration is not configured. Please configure JIRA settings first.",
                "JIRA Not Configured",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return;
        }

        try
        {
            IsLoading = true;
            StatusMessage = "Refreshing tasks from JIRA...";

            await _taskManagementService.RefreshTasksFromJiraAsync();
            
            // Reload the updated tasks
            var selectedProjects = await _taskManagementService.GetSelectedProjectsAsync();
            var projectIds = selectedProjects.Select(p => p.Id).ToList();
            var tasks = await _taskManagementService.GetTasksForProjectsAsync(projectIds);
            
            var taskViewModels = tasks.Select(t => new JiraTaskViewModel(t)).ToList();
            Tasks = new ObservableCollection<JiraTaskViewModel>(taskViewModels);

            StatusMessage = $"Refreshed {tasks.Count} tasks from JIRA";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error refreshing from JIRA: {ex.Message}";
            System.Windows.MessageBox.Show(
                $"Failed to refresh tasks from JIRA:\n\n{ex.Message}",
                "Refresh Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task AddTask()
    {
        if (string.IsNullOrWhiteSpace(NewTaskNumber)) return;

        try
        {
            IsLoading = true;
            StatusMessage = $"Adding task {NewTaskNumber}...";

            var task = await _taskManagementService.AddTaskByNumberAsync(NewTaskNumber.Trim());
            
            if (task != null)
            {
                var taskViewModel = new JiraTaskViewModel(task);
                Tasks.Add(taskViewModel);
                NewTaskNumber = string.Empty;
                StatusMessage = $"Added task {task.JiraTaskNumber}";
            }
            else
            {
                StatusMessage = $"Task {NewTaskNumber} not found or not accessible";
                System.Windows.MessageBox.Show(
                    $"Task '{NewTaskNumber}' could not be found or you don't have access to it.\n\nPlease check:\n• Task number is correct\n• Task is assigned to you\n• Task is not marked as Done",
                    "Task Not Found",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error adding task: {ex.Message}";
            System.Windows.MessageBox.Show(
                $"Failed to add task:\n\n{ex.Message}",
                "Add Task Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task RemoveSelectedTasks(object? parameter = null)
    {
        if (parameter is not System.Collections.IList selectedItems || selectedItems.Count == 0)
        {
            StatusMessage = "No tasks selected for removal";
            return;
        }

        var selectedTasks = selectedItems.Cast<JiraTaskViewModel>().ToList();
        var taskNames = string.Join(", ", selectedTasks.Select(t => t.JiraTaskNumber));

        var result = System.Windows.MessageBox.Show(
            $"Are you sure you want to remove the following tasks from tracking?\n\n{taskNames}\n\nThis will not delete the tasks from JIRA, only remove them from your local tracking list.",
            "Confirm Removal",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (result != System.Windows.MessageBoxResult.Yes) return;

        try
        {
            IsLoading = true;
            StatusMessage = $"Removing {selectedTasks.Count} tasks...";

            foreach (var taskVM in selectedTasks)
            {
                await _taskManagementService.RemoveTaskAsync(taskVM.Id);
                Tasks.Remove(taskVM);
            }

            StatusMessage = $"Removed {selectedTasks.Count} tasks from tracking";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error removing tasks: {ex.Message}";
            System.Windows.MessageBox.Show(
                $"Failed to remove tasks:\n\n{ex.Message}",
                "Remove Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void Close()
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}
