using System;
using System.Windows.Input;
using TaskTracker.Services;

namespace TaskTracker.ViewModels
{
	public class MainViewModel : ViewModelBase
	{
		private readonly ITimerService _timerService;
		private readonly ISystemTrayService _systemTrayService;
		private readonly ITimeTrackingService _timeTrackingService;
		private readonly IConfigurationService _configurationService;
		private readonly IApplicationService _applicationService;

		private string _trackingStatus = "Inactive";
		private string _currentTaskDisplay = "No task selected";
		private string _statusText = "Ready";

		public event EventHandler? HideWindowRequested;

		public MainViewModel(
			ISystemTrayService systemTrayService,
			ITimeTrackingService timeTrackingService,
			ITimerService timerService,
			IConfigurationService configurationService,
			IApplicationService applicationService)
		{
			_systemTrayService = systemTrayService;
			_timeTrackingService = timeTrackingService;
			_timerService = timerService;
			_configurationService = configurationService;
			_applicationService = applicationService;

			// Initialize commands
			ShowSummaryCommand = new RelayCommand(ShowSummary);
			ShowSettingsCommand = new RelayCommand(ShowSettings);
			ShowJiraProjectsCommand = new RelayCommand(ShowJiraProjects);
			ShowJiraTasksCommand = new RelayCommand(ShowJiraTasks);
			MinimizeToTrayCommand = new RelayCommand(MinimizeToTray);
			ExitApplicationCommand = new AsyncRelayCommand(ExitApplication);
			TestTaskPromptCommand = new RelayCommand(TestTaskPrompt);
			CancelLunchCommand = new RelayCommand(CancelLunch, () => _timerService.IsOnLunchBreak);
			// Listen for lunch break changes
			var lunchTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
			lunchTimer.Tick += (s, e) => OnLunchTimerTick();
			lunchTimer.Start();
			// constructor ends, continue with class members
		}

		// constructor ends, continue with class members

		private string _lunchTimeRemaining = "";
		public string LunchTimeRemaining
		{
			get => _lunchTimeRemaining;
			set => SetProperty(ref _lunchTimeRemaining, value);
			// constructor ends, continue with class members
		}

		public bool IsOnLunchBreak => _timerService.IsOnLunchBreak;

		public ICommand CancelLunchCommand { get; }

		private void OnLunchTimerTick()
		{
			if (_timerService.IsOnLunchBreak)
			{
				var remaining = _timerService.LunchBreakRemaining;
				LunchTimeRemaining = $"{remaining.Minutes:D2}:{remaining.Seconds:D2} left";
			}
			else
			{
				LunchTimeRemaining = string.Empty;
			}
			(CancelLunchCommand as RelayCommand)?.RaiseCanExecuteChanged();
			OnPropertyChanged(nameof(IsOnLunchBreak));
		}
		// OnLunchTimerTick ends, continue with class members
		private void CancelLunch()
		{
			// End lunch break immediately
			_timerService.EndLunchBreak();
			// Show immediate popup (task prompt)
			_applicationService.ShowTaskPrompt();
			StatusText = "Lunch break cancelled. Tracking resumed.";
		}

		public string TrackingStatus
		{
			get => _trackingStatus;
			set => SetProperty(ref _trackingStatus, value);
		}

		public string CurrentTaskDisplay
		{
			get => _currentTaskDisplay;
			set => SetProperty(ref _currentTaskDisplay, value);
		}

		public string StatusText
		{
			get => _statusText;
			set => SetProperty(ref _statusText, value);
		}

		// Commands
		public ICommand ShowSummaryCommand { get; }
		public ICommand ShowSettingsCommand { get; }
		public ICommand ShowJiraProjectsCommand { get; }
		public ICommand ShowJiraTasksCommand { get; }
		public ICommand MinimizeToTrayCommand { get; }
		public ICommand ExitApplicationCommand { get; }
		public ICommand TestTaskPromptCommand { get; }

		public async Task InitializeAsync()
		{
			await UpdateStatusAsync();

			// Set up timer to update status periodically
			var timer = new System.Windows.Threading.DispatcherTimer
			{
				Interval = TimeSpan.FromSeconds(30)
			};
			timer.Tick += async (s, e) => await UpdateStatusAsync();
			timer.Start();
		}

		private async Task UpdateStatusAsync()
		{
			try
			{
				// Update tracking status based on current time and settings
				var now = TimeOnly.FromDateTime(DateTime.Now);
				var isWithinHours = _timeTrackingService.IsWithinTrackingHours(
					now,
					_configurationService.AppSettings.TrackingStartTime,
					_configurationService.AppSettings.TrackingEndTime);

				TrackingStatus = isWithinHours ? "Active" : "Inactive";

				// Update system tray icon
				var trayStatus = isWithinHours
					? TrayIconStatus.Active
					: TrayIconStatus.Inactive;
				_systemTrayService.UpdateStatus(trayStatus);

				// Update current task display
				var activeEntry = await _timeTrackingService.GetActiveTimeEntryAsync();
				if (activeEntry != null)
				{
					CurrentTaskDisplay = $"{activeEntry.Task.JiraTaskNumber} - {activeEntry.Task.Summary}";
					var duration = activeEntry.CurrentDuration;
					StatusText = $"Active since {activeEntry.StartTime:HH:mm} ({duration.Hours:D2}:{duration.Minutes:D2})";
				}
				else
				{
					CurrentTaskDisplay = "No task selected";
					StatusText = isWithinHours ? "Ready for task selection" : "Outside tracking hours";
				}
			}
			catch (Exception ex)
			{
				StatusText = $"Error: {ex.Message}";
			}
		}

		private async void ShowSummary()
		{
			StatusText = "Opening Summary window...";
			await _applicationService.ShowSummaryWindow();
		}

		private void ShowSettings()
		{
			StatusText = "Opening Settings window...";
			_applicationService.ShowSettingsWindow();
		}

		private void ShowJiraProjects()
		{
			StatusText = "Opening JIRA Projects window...";
			_applicationService.ShowJiraProjectsWindow();
		}

		private void ShowJiraTasks()
		{
			StatusText = "Opening JIRA Tasks window...";
			_applicationService.ShowJiraTasksWindow();
		}

		private void MinimizeToTray()
		{
			// Trigger event for the window to hide itself
			HideWindowRequested?.Invoke(this, EventArgs.Empty);
			StatusText = "Minimized to system tray";
		}

		private async Task ExitApplication()
		{
			try
			{
				await _applicationService.ExitApplicationAsync();
			}
			catch (Exception ex)
			{
				StatusText = $"Error during exit: {ex.Message}";
				// Force exit even if there's an error
				System.Windows.Application.Current.Shutdown();
			}
		}

		private void TestTaskPrompt()
		{
			System.Diagnostics.Debug.WriteLine("=== Manual task prompt test triggered ===");
			StatusText = "Testing task prompt...";
			try
			{
				_applicationService.ShowTaskPrompt();
				StatusText = "Task prompt test completed";
			}
			catch (Exception ex)
			{
				StatusText = $"Task prompt test failed: {ex.Message}";
				System.Diagnostics.Debug.WriteLine($"Task prompt test error: {ex.Message}");

			}
		}
	}
}