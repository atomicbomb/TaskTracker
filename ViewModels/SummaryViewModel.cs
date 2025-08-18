using System.Collections.ObjectModel;
using System.Windows.Input;
using TaskTracker.Models;
using TaskTracker.Services;

namespace TaskTracker.ViewModels;

public class DailySummaryViewModel : ViewModelBase
{
    private DateTime _date;
    private TimeSpan _totalTime;
    private List<TaskSummaryViewModel> _taskSummaries = new();

    public DateTime Date
    {
        get => _date;
        set
        {
            if (SetProperty(ref _date, value))
            {
                OnPropertyChanged(nameof(DateDisplay));
            }
        }
    }

    public TimeSpan TotalTime
    {
        get => _totalTime;
        set
        {
            if (SetProperty(ref _totalTime, value))
            {
                OnPropertyChanged(nameof(TotalTimeDisplay));
            }
        }
    }

    public List<TaskSummaryViewModel> TaskSummaries
    {
        get => _taskSummaries;
        set => SetProperty(ref _taskSummaries, value);
    }
    
    public string DateDisplay => Date.ToString("dddd, MMMM dd, yyyy");
    public string TotalTimeDisplay => $"{(int)TotalTime.TotalHours:D2}:{TotalTime.Minutes:D2}";
}

public class TaskSummaryViewModel : ViewModelBase
{
    public string ProjectName { get; set; } = string.Empty;
    public string TaskNumber { get; set; } = string.Empty;
    public string TaskSummary { get; set; } = string.Empty;
    public TimeSpan TimeSpent { get; set; }
    public int EntryCount { get; set; }
    
    public string TimeSpentDisplay => $"{(int)TimeSpent.TotalHours:D2}:{TimeSpent.Minutes:D2}";
    public string EntryCountDisplay => $"{EntryCount} {(EntryCount == 1 ? "entry" : "entries")}";
}

public class WeeklySummaryViewModel : ViewModelBase
{
    private DateTime _weekStartDate;
    private DateTime _weekEndDate;
    private TimeSpan _totalTime;
    private List<DailySummaryViewModel> _dailySummaries = new();

    public DateTime WeekStartDate
    {
        get => _weekStartDate;
        set
        {
            if (SetProperty(ref _weekStartDate, value))
            {
                OnPropertyChanged(nameof(WeekDisplay));
            }
        }
    }

    public DateTime WeekEndDate
    {
        get => _weekEndDate;
        set
        {
            if (SetProperty(ref _weekEndDate, value))
            {
                OnPropertyChanged(nameof(WeekDisplay));
            }
        }
    }

    public TimeSpan TotalTime
    {
        get => _totalTime;
        set
        {
            if (SetProperty(ref _totalTime, value))
            {
                OnPropertyChanged(nameof(TotalTimeDisplay));
            }
        }
    }

    public List<DailySummaryViewModel> DailySummaries
    {
        get => _dailySummaries;
        set => SetProperty(ref _dailySummaries, value);
    }
    
    public string WeekDisplay => $"Week of {WeekStartDate:MMM dd} - {WeekEndDate:MMM dd, yyyy}";
    public string TotalTimeDisplay => $"{(int)TotalTime.TotalHours:D2}:{TotalTime.Minutes:D2}";
}

public class SummaryViewModel : ViewModelBase
{
    private readonly ITimeTrackingService _timeTrackingService;
    private readonly ITaskManagementService _taskManagementService;
    
    private DateTime _selectedDate = DateTime.Today;
    private bool _isLoading;
    private string _statusMessage = "Ready";
    private DailySummaryViewModel? _dailySummary;
    private WeeklySummaryViewModel? _weeklySummary;
    private ObservableCollection<TimeEntry> _timeEntries = new();
    private string _selectedView = "Daily"; // Daily, Weekly, Entries

    public SummaryViewModel(ITimeTrackingService timeTrackingService, ITaskManagementService taskManagementService)
    {
        _timeTrackingService = timeTrackingService;
        _taskManagementService = taskManagementService;

    PreviousDayCommand = new RelayCommand(PreviousDay);
    NextDayCommand = new RelayCommand(NextDay);
    DeleteEntryCommand = new AsyncRelayCommand(async (obj) =>
        {
            var entry = obj as TimeEntry;
            if (entry != null)
            {
                await DeleteEntryAsync(entry);
            }
            else
            {
                await DeleteSelectedEntry();
            }
    }, obj => obj is TimeEntry || SelectedEntry != null);
        TodayCommand = new RelayCommand(GoToToday);
        PreviousWeekCommand = new RelayCommand(PreviousWeek);
        NextWeekCommand = new RelayCommand(NextWeek);
        ThisWeekCommand = new RelayCommand(GoToThisWeek);
    RefreshCommand = new AsyncRelayCommand(Refresh, () => !_isLoading);
    ExportCommand = new AsyncRelayCommand(Export, () => !_isLoading);
    CloseCommand = new RelayCommand(Close);

        // Don't load data immediately in constructor to avoid blocking UI
        // Data will be loaded when window is shown or user interactions trigger it
    }

    /// <summary>
    /// Initialize data after the window is loaded
    /// </summary>
    public async Task InitializeAsync()
    {
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            await LoadDataAsync();
        });
    }

    public DateTime SelectedDate
    {
        get => _selectedDate;
        set
        {
            if (SetProperty(ref _selectedDate, value))
            {
                _ = LoadDataAsync();
            }
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            SetProperty(ref _isLoading, value);
            (RefreshCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            (ExportCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public DailySummaryViewModel? DailySummary
    {
        get => _dailySummary;
        set => SetProperty(ref _dailySummary, value);
    }

    public WeeklySummaryViewModel? WeeklySummary
    {
        get => _weeklySummary;
        set => SetProperty(ref _weeklySummary, value);
    }

    public ObservableCollection<TimeEntry> TimeEntries
    {
        get => _timeEntries;
        set => SetProperty(ref _timeEntries, value);
    }

    // For potential future UI use
    private string _newTaskNumber = string.Empty;
    public string NewTaskNumber { get => _newTaskNumber; set => SetProperty(ref _newTaskNumber, value); }

    public string SelectedView
    {
        get => _selectedView;
        set
        {
            if (SetProperty(ref _selectedView, value))
            {
                _ = LoadDataAsync();
            }
        }
    }

    // Commands
    public ICommand PreviousDayCommand { get; }
    public ICommand NextDayCommand { get; }
    public ICommand TodayCommand { get; }
    public ICommand PreviousWeekCommand { get; }
    public ICommand NextWeekCommand { get; }
    public ICommand ThisWeekCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand CloseCommand { get; }
    public ICommand DeleteEntryCommand { get; }
    // No explicit command; handled via view event calling ChangeTimeEntryTaskByNumberAsync

    // Events
    private TimeEntry? _selectedEntry;
    public TimeEntry? SelectedEntry
    {
        get => _selectedEntry;
        set
        {
            if (SetProperty(ref _selectedEntry, value))
            {
                (DeleteEntryCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    // Events
    public event EventHandler? CloseRequested;

    private async Task LoadDataAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Loading summary data...";
            
            System.Diagnostics.Debug.WriteLine($"Loading data for view: {SelectedView}, date: {SelectedDate}");

            switch (SelectedView)
            {
                case "Daily":
                    await LoadDailySummary();
                    break;
                case "Weekly":
                    await LoadWeeklySummary();
                    break;
                case "Entries":
                    await LoadTimeEntries();
                    break;
            }

            StatusMessage = "Data loaded successfully";
            System.Diagnostics.Debug.WriteLine("Data loading completed");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading data: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"Error loading data: {ex.Message}\nStack trace: {ex.StackTrace}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    // Called by view to update times of a single entry
    public async Task<TimeEntry?> UpdateTimeEntryTimesAsync(TimeEntry entry, DateTime newStart, DateTime? newEnd)
    {
        try
        {
            await _timeTrackingService.UpdateTimeEntryTimesAsync(entry.Id, newStart, newEnd);
            // Reload the entry with includes
            var updated = await _timeTrackingService.GetTimeEntryByIdAsync(entry.Id);
            return updated;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to update times: {ex.Message}";
            return null;
        }
    }

    private async Task DeleteSelectedEntry()
    {
        if (SelectedEntry == null) return;
        try
        {
            await _timeTrackingService.DeleteTimeEntryAsync(SelectedEntry.Id);
            TimeEntries.Remove(SelectedEntry);
            SelectedEntry = null;
            StatusMessage = "Entry deleted";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Delete failed: {ex.Message}";
        }
    }

    private async Task DeleteEntryAsync(TimeEntry entry)
    {
        try
        {
            await _timeTrackingService.DeleteTimeEntryAsync(entry.Id);
            var toRemove = TimeEntries.FirstOrDefault(e => e.Id == entry.Id) ?? entry;
            TimeEntries.Remove(toRemove);
            if (SelectedEntry?.Id == entry.Id)
            {
                SelectedEntry = null;
            }
            StatusMessage = "Entry deleted";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Delete failed: {ex.Message}";
        }
    }

    private TimeSpan CalculateEntryDuration(TimeEntry entry)
    {
        if (entry.Duration.HasValue)
        {
            return entry.Duration.Value;
        }
        
        // For active entries (no EndTime), calculate duration from StartTime to now
        if (entry.EndTime == null)
        {
            return DateTime.Now - entry.StartTime;
        }
        
        return TimeSpan.Zero;
    }

    private async Task LoadDailySummary()
    {
        var entries = await _timeTrackingService.GetTimeEntriesAsync(SelectedDate, SelectedDate.AddDays(1));
        System.Diagnostics.Debug.WriteLine($"Found {entries.Count} time entries for date {SelectedDate}");
        
        // Include active entry if it started today
        var activeEntry = await _timeTrackingService.GetActiveTimeEntryAsync();
        if (activeEntry != null)
        {
            System.Diagnostics.Debug.WriteLine($"Found active entry: Task {activeEntry.TaskId}, started {activeEntry.StartTime}");
            if (activeEntry.StartTime.Date == SelectedDate.Date &&
                !entries.Any(e => e.Id == activeEntry.Id))
            {
                entries.Add(activeEntry);
                System.Diagnostics.Debug.WriteLine("Added active entry to today's entries");
            }
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("No active entry found");
        }
        
        var dailySummary = new DailySummaryViewModel
        {
            Date = SelectedDate,
            TotalTime = TimeSpan.FromMinutes(entries.Sum(e => CalculateEntryDuration(e).TotalMinutes))
        };

        var taskGroups = entries
            .GroupBy(e => new { e.Task.JiraTaskNumber, e.Task.Summary, e.Task.Project.ProjectName })
            .Select(g => new TaskSummaryViewModel
            {
                ProjectName = g.Key.ProjectName,
                TaskNumber = g.Key.JiraTaskNumber,
                TaskSummary = g.Key.Summary,
                TimeSpent = TimeSpan.FromMinutes(g.Sum(e => CalculateEntryDuration(e).TotalMinutes)),
                EntryCount = g.Count()
            })
            .OrderByDescending(t => t.TimeSpent)
            .ToList();

        dailySummary.TaskSummaries = taskGroups;
        System.Diagnostics.Debug.WriteLine($"Created daily summary with {taskGroups.Count} task groups, total time: {dailySummary.TotalTime}");
        
        // Ensure UI update happens on UI thread
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            DailySummary = dailySummary;
            System.Diagnostics.Debug.WriteLine("Daily summary set on UI thread");
        });
    }

    private async Task LoadWeeklySummary()
    {
        // Calculate week start (Monday) and end (Sunday)
        var daysSinceMonday = (int)SelectedDate.DayOfWeek - 1;
        if (daysSinceMonday < 0) daysSinceMonday = 6; // Sunday
        
        var weekStart = SelectedDate.AddDays(-daysSinceMonday);
        var weekEnd = weekStart.AddDays(6);

        var entries = await _timeTrackingService.GetTimeEntriesAsync(weekStart, weekEnd.AddDays(1));
        
        // Include active entry if it started this week
        var activeEntry = await _timeTrackingService.GetActiveTimeEntryAsync();
        if (activeEntry != null && 
            activeEntry.StartTime.Date >= weekStart.Date && 
            activeEntry.StartTime.Date <= weekEnd.Date &&
            !entries.Any(e => e.Id == activeEntry.Id))
        {
            entries.Add(activeEntry);
        }
        
        var weeklySummary = new WeeklySummaryViewModel
        {
            WeekStartDate = weekStart,
            WeekEndDate = weekEnd,
            TotalTime = TimeSpan.FromMinutes(entries.Sum(e => CalculateEntryDuration(e).TotalMinutes))
        };

        var dailySummaries = new List<DailySummaryViewModel>();
        for (var date = weekStart; date <= weekEnd; date = date.AddDays(1))
        {
            var dayEntries = entries.Where(e => e.StartTime.Date == date.Date).ToList();
            
            var dailySummary = new DailySummaryViewModel
            {
                Date = date,
                TotalTime = TimeSpan.FromMinutes(dayEntries.Sum(e => CalculateEntryDuration(e).TotalMinutes))
            };

            var taskGroups = dayEntries
                .GroupBy(e => new { e.Task.JiraTaskNumber, e.Task.Summary, e.Task.Project.ProjectName })
                .Select(g => new TaskSummaryViewModel
                {
                    ProjectName = g.Key.ProjectName,
                    TaskNumber = g.Key.JiraTaskNumber,
                    TaskSummary = g.Key.Summary,
                    TimeSpent = TimeSpan.FromMinutes(g.Sum(e => CalculateEntryDuration(e).TotalMinutes)),
                    EntryCount = g.Count()
                })
                .OrderByDescending(t => t.TimeSpent)
                .ToList();

            dailySummary.TaskSummaries = taskGroups;
            dailySummaries.Add(dailySummary);
        }

        weeklySummary.DailySummaries = dailySummaries;
        
        // Ensure UI update happens on UI thread
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            WeeklySummary = weeklySummary;
        });
    }

    private async Task LoadTimeEntries()
    {
    // For the Time Entries view, always show only the selected date
    var endDate = SelectedDate.AddDays(1);
        var entries = await _timeTrackingService.GetTimeEntriesAsync(SelectedDate, endDate);
        
        // Also include the current active entry if it exists and started today
        var activeEntry = await _timeTrackingService.GetActiveTimeEntryAsync();
        if (activeEntry != null && 
            activeEntry.StartTime.Date >= SelectedDate.Date && 
            activeEntry.StartTime.Date < endDate.Date &&
            !entries.Any(e => e.Id == activeEntry.Id))
        {
            entries.Add(activeEntry);
        }
        
        // Ensure UI update happens on UI thread
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            TimeEntries = new ObservableCollection<TimeEntry>(entries.OrderByDescending(e => e.StartTime));
        });
    }

    private void PreviousDay()
    {
        SelectedDate = SelectedDate.AddDays(-1);
    }

    private void NextDay()
    {
        SelectedDate = SelectedDate.AddDays(1);
    }

    private void GoToToday()
    {
        SelectedDate = DateTime.Today;
    }

    private void PreviousWeek()
    {
        SelectedDate = SelectedDate.AddDays(-7);
    }

    private void NextWeek()
    {
        SelectedDate = SelectedDate.AddDays(7);
    }

    private void GoToThisWeek()
    {
        SelectedDate = DateTime.Today;
    }

    private async Task Refresh()
    {
        await LoadDataAsync();
    }

    private async Task Export()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Preparing export...";

            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv|Text files (*.txt)|*.txt|All files (*.*)|*.*",
                DefaultExt = "csv",
                FileName = $"TimeTracking_{SelectedDate:yyyy-MM-dd}"
            };

            if (saveDialog.ShowDialog() == true)
            {
                var endDate = SelectedView == "Weekly" ? SelectedDate.AddDays(7) : SelectedDate.AddDays(1);
                var entries = await _timeTrackingService.GetTimeEntriesAsync(SelectedDate, endDate);

                var csvContent = "Date,Start Time,End Time,Duration (Minutes),Project,Task,Summary\n";
                
                foreach (var entry in entries.OrderBy(e => e.StartTime))
                {
                    var line = $"{entry.StartTime:yyyy-MM-dd}," +
                              $"{entry.StartTime:HH:mm}," +
                              $"{entry.EndTime:HH:mm}," +
                              $"{(int)(entry.Duration?.TotalMinutes ?? 0)}," +
                              $"\"{entry.Task.Project.ProjectName}\"," +
                              $"\"{entry.Task.JiraTaskNumber}\"," +
                              $"\"{entry.Task.Summary.Replace("\"", "\"\"")}\"\n";
                    csvContent += line;
                }

                await System.IO.File.WriteAllTextAsync(saveDialog.FileName, csvContent);
                StatusMessage = $"Exported {entries.Count} entries to {System.IO.Path.GetFileName(saveDialog.FileName)}";

                System.Windows.MessageBox.Show(
                    $"Successfully exported {entries.Count} time entries to:\n{saveDialog.FileName}",
                    "Export Complete",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            else
            {
                StatusMessage = "Export cancelled";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export failed: {ex.Message}";
            System.Windows.MessageBox.Show(
                $"Failed to export data:\n\n{ex.Message}",
                "Export Error",
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

    public async Task<(bool Success, TimeEntry? Updated)> ChangeTimeEntryTaskByNumberAsync(TimeEntry entry, string newTaskNumber)
    {
        if (entry == null) return (false, null);
        if (string.IsNullOrWhiteSpace(newTaskNumber))
        {
            StatusMessage = "Task number cannot be empty.";
            return (false, null);
        }

        try
        {
            IsLoading = true;
            StatusMessage = $"Updating entry #{entry.Id} to {newTaskNumber}...";

            // Find task or add from JIRA
            var task = await _taskManagementService.GetTaskByNumberAsync(newTaskNumber);
            if (task == null)
            {
                // Try to add from JIRA
                task = await _taskManagementService.AddTaskByNumberAsync(newTaskNumber);
            }

            if (task == null)
            {
                StatusMessage = $"Task '{newTaskNumber}' not found in JIRA.";
                return (false, null);
            }

            await _timeTrackingService.UpdateTimeEntryTaskAsync(entry.Id, task.Id);

            // Fetch the updated entry (detached) for in-place update in the grid
            var updated = await _timeTrackingService.GetTimeEntryByIdAsync(entry.Id);
            StatusMessage = "Time entry updated.";
            return (true, updated);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to update entry: {ex.Message}";
            System.Diagnostics.Debug.WriteLine(ex);
            return (false, null);
        }
        finally
        {
            IsLoading = false;
        }
    }
}
