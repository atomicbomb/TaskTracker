using System.Collections.ObjectModel;
using System.Windows.Input;
using TaskTracker.Models;
using TaskTracker.Services;

namespace TaskTracker.ViewModels;

public class JiraProjectViewModel : ViewModelBase
{
    private readonly JiraProject _project;
    private bool _isSelected;

    public JiraProjectViewModel(JiraProject project)
    {
        _project = project;
        _isSelected = project.IsSelected;
    }

    public int Id => _project.Id;
    public string ProjectCode => _project.ProjectCode;
    public string ProjectName => _project.ProjectName;
    
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public JiraProject ToModel()
    {
        _project.IsSelected = IsSelected;
        return _project;
    }
}

public class JiraProjectsViewModel : ViewModelBase
{
    private readonly ITaskManagementService _taskManagementService;
    private readonly IConfigurationService _configurationService;
    
    private bool _isLoading;
    private string _statusMessage = "Ready";
    private ObservableCollection<JiraProjectViewModel> _projects = new();

    public JiraProjectsViewModel(
        ITaskManagementService taskManagementService,
        IConfigurationService configurationService)
    {
        _taskManagementService = taskManagementService;
        _configurationService = configurationService;

        // Initialize commands
        RefreshFromJiraCommand = new AsyncRelayCommand(RefreshFromJira, () => !_isLoading);
        SaveSelectionCommand = new AsyncRelayCommand(SaveSelection);
        SelectAllCommand = new RelayCommand(SelectAll);
        SelectNoneCommand = new RelayCommand(SelectNone);
        CloseCommand = new RelayCommand(Close);

        LoadProjectsAsync();
    }

    public ObservableCollection<JiraProjectViewModel> Projects
    {
        get => _projects;
        set => SetProperty(ref _projects, value);
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

    // Commands
    public ICommand RefreshFromJiraCommand { get; }
    public ICommand SaveSelectionCommand { get; }
    public ICommand SelectAllCommand { get; }
    public ICommand SelectNoneCommand { get; }
    public ICommand CloseCommand { get; }

    // Events
    public event EventHandler? CloseRequested;

    private async void LoadProjectsAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Loading projects...";

            var projects = await _taskManagementService.GetProjectsAsync();
            // Exclude demo/test projects from the UI
            var filtered = projects
                .Where(p => !string.Equals(p.ProjectCode, "DEMO", StringComparison.OrdinalIgnoreCase)
                         && !string.Equals(p.ProjectCode, "TEST", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var projectViewModels = filtered.Select(p => new JiraProjectViewModel(p)).ToList();

            Projects = new ObservableCollection<JiraProjectViewModel>(projectViewModels);
            StatusMessage = $"Loaded {filtered.Count} projects";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading projects: {ex.Message}";
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
            StatusMessage = "Refreshing projects from JIRA...";

            await _taskManagementService.RefreshProjectsFromJiraAsync();
            
            // Reload the updated projects and filter out demo/test
            var projects = await _taskManagementService.GetProjectsAsync();
            var filtered = projects
                .Where(p => !string.Equals(p.ProjectCode, "DEMO", StringComparison.OrdinalIgnoreCase)
                         && !string.Equals(p.ProjectCode, "TEST", StringComparison.OrdinalIgnoreCase))
                .ToList();
            var projectViewModels = filtered.Select(p => new JiraProjectViewModel(p)).ToList();

            Projects = new ObservableCollection<JiraProjectViewModel>(projectViewModels);
            StatusMessage = $"Refreshed {filtered.Count} projects from JIRA";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error refreshing from JIRA: {ex.Message}";
            System.Windows.MessageBox.Show(
                $"Failed to refresh projects from JIRA:\n\n{ex.Message}",
                "Refresh Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task SaveSelection()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Saving project selection...";

            // Update project selections
            foreach (var projectVM in Projects)
            {
                await _taskManagementService.UpdateProjectSelectionAsync(projectVM.Id, projectVM.IsSelected);
            }

            StatusMessage = "Project selection saved successfully";
            
            // Auto-close after a brief delay
            await Task.Delay(1500);
            Close();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving selection: {ex.Message}";
            System.Windows.MessageBox.Show(
                $"Failed to save project selection:\n\n{ex.Message}",
                "Save Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void SelectAll()
    {
        foreach (var project in Projects)
        {
            project.IsSelected = true;
        }
        StatusMessage = $"Selected all {Projects.Count} projects";
    }

    private void SelectNone()
    {
        foreach (var project in Projects)
        {
            project.IsSelected = false;
        }
        StatusMessage = "Cleared all selections";
    }

    private void Close()
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}
