using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using TaskTracker.Models;
using TaskTracker.Services;

namespace TaskTracker.ViewModels;

public class LogViewerViewModel : ViewModelBase
{
    private readonly ILoggingService _loggingService;
    private DateTime? _fromDate = DateTime.Today.AddDays(-1);
    private DateTime? _toDate = DateTime.Today.AddDays(1);
    private string? _selectedLevel;
    private string? _selectedSource;
    private string? _searchText;
    private int _maxRows = 500;
    private bool _isLoading;
    private ObservableCollection<LogEntry> _logs = new();
    private ObservableCollection<string> _sources = new();
    private ObservableCollection<string> _levels = new();
    private bool _suppressAutoRefresh;

    public LogViewerViewModel(ILoggingService loggingService)
    {
        _loggingService = loggingService;
        Levels = new ObservableCollection<string>(new[] { "", "Debug", "Info", "Warn", "Error" });
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsLoading);
        ExportCommand = new AsyncRelayCommand(ExportAsync, () => !IsLoading && Logs.Any());
        ResetCommand = new AsyncRelayCommand(ResetAsync, () => !IsLoading);
        CloseCommand = new RelayCommand(() => CloseRequested?.Invoke(this, EventArgs.Empty));
        _ = RefreshAsync();
    }

    public ObservableCollection<LogEntry> Logs { get => _logs; set => SetProperty(ref _logs, value); }
    public ObservableCollection<string> Sources { get => _sources; set => SetProperty(ref _sources, value); }
    public ObservableCollection<string> Levels { get => _levels; set => SetProperty(ref _levels, value); }

    public DateTime? FromDate { get => _fromDate; set { if (SetProperty(ref _fromDate, value) && !_suppressAutoRefresh) _ = RefreshAsync(); } }
    public DateTime? ToDate { get => _toDate; set { if (SetProperty(ref _toDate, value) && !_suppressAutoRefresh) _ = RefreshAsync(); } }
    public string? SelectedLevel { get => _selectedLevel; set { if (SetProperty(ref _selectedLevel, value) && !_suppressAutoRefresh) _ = RefreshAsync(); } }
    public string? SelectedSource { get => _selectedSource; set { if (SetProperty(ref _selectedSource, value) && !_suppressAutoRefresh) _ = RefreshAsync(); } }
    public string? SearchText { get => _searchText; set { if (SetProperty(ref _searchText, value) && !_suppressAutoRefresh) _ = RefreshAsync(); } }
    public int MaxRows { get => _maxRows; set { if (SetProperty(ref _maxRows, value) && !_suppressAutoRefresh) _ = RefreshAsync(); } }

    public bool IsLoading { get => _isLoading; set { if (SetProperty(ref _isLoading, value)) { (RefreshCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged(); (ExportCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged(); } } }

    public ICommand RefreshCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand ResetCommand { get; }
    public ICommand CloseCommand { get; }

    public event EventHandler? CloseRequested;

    private async Task RefreshAsync()
    {
        try
        {
            IsLoading = true;
            // Interpret selected dates as whole-day inclusive ranges in local time
            DateTime? fromUtc = FromDate.HasValue 
                ? DateTime.SpecifyKind(FromDate.Value.Date, DateTimeKind.Local).ToUniversalTime() 
                : (DateTime?)null;
            DateTime? toUtc = ToDate.HasValue 
                ? DateTime.SpecifyKind(ToDate.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Local).ToUniversalTime() 
                : (DateTime?)null;
            var list = await _loggingService.GetLogsAsync(fromUtc, toUtc, SelectedLevel, SelectedSource, SearchText, MaxRows);
            Logs = new ObservableCollection<LogEntry>(list.OrderByDescending(l => l.UtcTimestamp));
            // update sources list
            Sources = new ObservableCollection<string>(Logs.Select(l => l.Source).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().OrderBy(s => s));
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Failed to load logs: {ex.Message}", "Log Viewer", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ResetAsync()
    {
        try
        {
            _suppressAutoRefresh = true;
            FromDate = DateTime.Today.AddDays(-1);
            ToDate = DateTime.Today.AddDays(1);
            SelectedLevel = null;
            SelectedSource = null;
            SearchText = null;
            MaxRows = 500;
        }
        finally
        {
            _suppressAutoRefresh = false;
        }
        await RefreshAsync();
    }

    private async Task ExportAsync()
    {
        try
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                DefaultExt = "csv",
                FileName = $"Logs_{DateTime.Now:yyyyMMdd_HHmmss}"
            };
            if (dlg.ShowDialog() == true)
            {
                var lines = new List<string>();
                lines.Add("UtcTimestamp,Level,Source,Message,Details,EventId,ThreadId,User,CorrelationId");
                foreach (var l in Logs)
                {
                    string esc(string? v) => string.IsNullOrEmpty(v) ? "" : '"' + v.Replace("\"", "\"\"") + '"';
                    lines.Add($"{l.UtcTimestamp:o},{l.Level},{esc(l.Source)},{esc(l.Message)},{esc(l.Details)},{esc(l.EventId)},{esc(l.ThreadId)},{esc(l.User)},{esc(l.CorrelationId)}");
                }
                await File.WriteAllLinesAsync(dlg.FileName, lines);
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Failed to export logs: {ex.Message}", "Export Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }
}
