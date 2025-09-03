
using System.Windows.Threading;
using TaskTracker.Models;

namespace TaskTracker.Services
{

	public interface ITimerService
	{
		void Start();
		void Stop();
		void SetPromptInterval(int minutes);
		void SetUpdateInterval(int minutes);
		void SetCalendarScanInterval(int minutes);
		void StartLunchBreak(int durationMinutes);
		void EndLunchBreak();
		bool IsOnLunchBreak { get; }
		TimeSpan LunchBreakRemaining { get; }

		event EventHandler? TaskPromptRequested;
		event EventHandler? UpdateDataRequested;
		event EventHandler? CalendarScanRequested;
		event EventHandler? LunchBreakEnded;
		event EventHandler? TrackingStarted;
		event EventHandler? TrackingEnded;
	}

		public class TimerService : ITimerService
	{
			private int? _lunchTimeEntryId;

			public void EndLunchBreak()
			{
				_lunchTimer?.Stop();
				_lunchTimer = null;
				var endTime = DateTime.Now;
				if (_isOnLunchBreak && _lunchStartTime.HasValue && _lunchTimeEntryId.HasValue)
				{
					_ = Task.Run(async () =>
					{
						try { await _timeTrackingService.UpdateTimeEntryEndAsync(_lunchTimeEntryId.Value, endTime); }
						catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Error finalizing lunch entry early: {ex.Message}"); }
					});
				}
				_isOnLunchBreak = false;
				_lunchStartTime = null;
				_lunchTimeEntryId = null;
				LunchBreakEnded?.Invoke(this, EventArgs.Empty);
				CheckTrackingStatus();
			}
		private readonly IConfigurationService _configurationService;
		private readonly ITimeTrackingService _timeTrackingService;
		private readonly ISystemTrayService _systemTrayService;
	private readonly ITaskManagementService _taskManagementService;

		private DispatcherTimer? _promptTimer;
		private DispatcherTimer? _updateTimer;
		private DispatcherTimer? _calendarTimer;
		private DispatcherTimer? _statusTimer;
		private DispatcherTimer? _lunchTimer;

		private DateTime? _lunchStartTime;
		private int _lunchDurationMinutes;
		private bool _isOnLunchBreak;

		public bool IsOnLunchBreak => _isOnLunchBreak;

		public TimeSpan LunchBreakRemaining
		{
			get
			{
				if (!_isOnLunchBreak || !_lunchStartTime.HasValue)
					return TimeSpan.Zero;

				var elapsed = DateTime.Now - _lunchStartTime.Value;
				var remaining = TimeSpan.FromMinutes(_lunchDurationMinutes) - elapsed;
				return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
			}
		}

		public event EventHandler? TaskPromptRequested;
		public event EventHandler? UpdateDataRequested;
		public event EventHandler? CalendarScanRequested;
		public event EventHandler? LunchBreakEnded;
		public event EventHandler? TrackingStarted;
		public event EventHandler? TrackingEnded;

		public TimerService(
			IConfigurationService configurationService,
			ITimeTrackingService timeTrackingService,
			ISystemTrayService systemTrayService,
            ITaskManagementService taskManagementService)
		{
			_configurationService = configurationService;
			_timeTrackingService = timeTrackingService;
			_systemTrayService = systemTrayService;
            _taskManagementService = taskManagementService;
		}

		public void Start()
		{
			System.Diagnostics.Debug.WriteLine("=== TimerService.Start() called ===");

			Stop(); // Stop any existing timers

			var settings = _configurationService.AppSettings;

			System.Diagnostics.Debug.WriteLine($"Prompt interval: {settings.PromptIntervalMinutes} minutes");
			System.Diagnostics.Debug.WriteLine($"Update interval: {settings.UpdateIntervalMinutes} minutes");
			System.Diagnostics.Debug.WriteLine($"Calendar scan enabled: {settings.Google.Enabled}, interval: {settings.Google.ScanIntervalMinutes} minutes");

			// Task prompt timer
			_promptTimer = new DispatcherTimer
			{
				Interval = TimeSpan.FromMinutes(settings.PromptIntervalMinutes)
			};
			_promptTimer.Tick += OnPromptTimerTick;
			_promptTimer.Start();

			System.Diagnostics.Debug.WriteLine("Prompt timer started");

			// Data update timer
			_updateTimer = new DispatcherTimer
			{
				Interval = TimeSpan.FromMinutes(settings.UpdateIntervalMinutes)
			};
			_updateTimer.Tick += OnUpdateTimerTick;
			_updateTimer.Start();

			// Google Calendar scan timer (optional)
			if (settings.Google.Enabled && settings.Google.ScanIntervalMinutes > 0)
			{
				_calendarTimer = new DispatcherTimer
				{
					Interval = TimeSpan.FromMinutes(settings.Google.ScanIntervalMinutes)
				};
				_calendarTimer.Tick += OnCalendarTimerTick;
				_calendarTimer.Start();
			}

			// Status check timer (every minute)
			_statusTimer = new DispatcherTimer
			{
				Interval = TimeSpan.FromMinutes(1)
			};
			_statusTimer.Tick += OnStatusTimerTick;
			_statusTimer.Start();

			// Check if we should start tracking immediately
			CheckTrackingStatus();

			System.Diagnostics.Debug.WriteLine("=== TimerService.Start() completed ===");
		}

		public void Stop()
		{
			_promptTimer?.Stop();
			_updateTimer?.Stop();
			_statusTimer?.Stop();
			_calendarTimer?.Stop();
			_lunchTimer?.Stop();

			_promptTimer = null;
			_updateTimer = null;
			_calendarTimer = null;
			_statusTimer = null;
			_lunchTimer = null;
		}

		public void SetPromptInterval(int minutes)
		{
			if (_promptTimer != null)
			{
				_promptTimer.Interval = TimeSpan.FromMinutes(minutes);
			}
		}

		public void SetUpdateInterval(int minutes)
		{
			if (_updateTimer != null)
			{
				_updateTimer.Interval = TimeSpan.FromMinutes(minutes);
			}
		}

		public void SetCalendarScanInterval(int minutes)
		{
			if (minutes <= 0)
			{
				_calendarTimer?.Stop();
				_calendarTimer = null;
				return;
			}

			if (_calendarTimer == null)
			{
				_calendarTimer = new DispatcherTimer();
				_calendarTimer.Tick += OnCalendarTimerTick;
			}
			_calendarTimer.Interval = TimeSpan.FromMinutes(minutes);
			if (!_calendarTimer.IsEnabled) _calendarTimer.Start();
		}

		public void StartLunchBreak(int durationMinutes)
		{
			_lunchDurationMinutes = durationMinutes;
			_lunchStartTime = DateTime.Now;
			_isOnLunchBreak = true;
			_lunchTimeEntryId = null;

			// Create open lunch entry
			_ = Task.Run(async () =>
			{
				try
				{
					var lunchTask = await _taskManagementService.EnsureLunchTaskAsync();
					_lunchTimeEntryId = await _timeTrackingService.CreateOpenTimeEntryAsync(lunchTask.Id, _lunchStartTime.Value);
				}
				catch (Exception ex)
				{
					System.Diagnostics.Debug.WriteLine($"Error creating lunch time entry: {ex.Message}");
				}
			});

			// Update system tray to show lunch status
			_systemTrayService.UpdateStatus(TrayIconStatus.Lunch);

			// Stop any active tracking
			_ = Task.Run(async () => await _timeTrackingService.StopTrackingAsync());

			// Set up lunch break timer
			_lunchTimer = new DispatcherTimer
			{
				Interval = TimeSpan.FromMinutes(durationMinutes)
			};
			_lunchTimer.Tick += OnLunchTimerTick;
			_lunchTimer.Start();
		}

		private void OnPromptTimerTick(object? sender, EventArgs e)
		{
			System.Diagnostics.Debug.WriteLine("=== OnPromptTimerTick called ===");

			if (ShouldPromptUser())
			{
				System.Diagnostics.Debug.WriteLine("Firing TaskPromptRequested event");
				TaskPromptRequested?.Invoke(this, EventArgs.Empty);
			}
			else
			{
				System.Diagnostics.Debug.WriteLine("Not firing TaskPromptRequested event - conditions not met");
			}
		}

		private void OnUpdateTimerTick(object? sender, EventArgs e)
		{
			if (IsWithinTrackingHours())
			{
				UpdateDataRequested?.Invoke(this, EventArgs.Empty);
			}
		}

		private void OnCalendarTimerTick(object? sender, EventArgs e)
		{
			if (IsWithinTrackingHours())
			{
				CalendarScanRequested?.Invoke(this, EventArgs.Empty);
			}
		}

		private async void OnStatusTimerTick(object? sender, EventArgs e)
		{
			CheckTrackingStatus();
			await CheckEndOfDayAsync();
		}

		private void OnLunchTimerTick(object? sender, EventArgs e)
		{
			_lunchTimer?.Stop();
			_lunchTimer = null;
			var endTime = DateTime.Now;
			if (_lunchStartTime.HasValue && _lunchTimeEntryId.HasValue)
			{
				_ = Task.Run(async () =>
				{
					try { await _timeTrackingService.UpdateTimeEntryEndAsync(_lunchTimeEntryId.Value, endTime); }
					catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Error finalizing lunch entry: {ex.Message}"); }
				});
			}
			_isOnLunchBreak = false;
			_lunchStartTime = null;
			_lunchTimeEntryId = null;
			LunchBreakEnded?.Invoke(this, EventArgs.Empty);
			CheckTrackingStatus();
		}

		private bool ShouldPromptUser()
		{
			// Debug output
			System.Diagnostics.Debug.WriteLine($"=== ShouldPromptUser Check ===");
			System.Diagnostics.Debug.WriteLine($"Is on lunch break: {_isOnLunchBreak}");

			// Don't prompt if on lunch break
			if (_isOnLunchBreak)
			{
				System.Diagnostics.Debug.WriteLine("Not prompting: On lunch break");
				return false;
			}

			// Check tracking hours
			var isWithinHours = IsWithinTrackingHours();
			System.Diagnostics.Debug.WriteLine($"Is within tracking hours: {isWithinHours}");

			// Don't prompt if outside tracking hours
			if (!isWithinHours)
			{
				System.Diagnostics.Debug.WriteLine("Not prompting: Outside tracking hours");
				return false;
			}

			// Check JIRA configuration
			var jiraConfigured = _configurationService.JiraSettings.IsConfigured;
			System.Diagnostics.Debug.WriteLine($"JIRA configured: {jiraConfigured}");
			System.Diagnostics.Debug.WriteLine($"JIRA Server: '{_configurationService.JiraSettings.ServerUrl}'");
			System.Diagnostics.Debug.WriteLine($"JIRA Email: '{_configurationService.JiraSettings.Email}'");
			System.Diagnostics.Debug.WriteLine($"JIRA Token: '{(_configurationService.JiraSettings.ApiToken.Length > 0 ? "[SET]" : "[EMPTY]")}'");

			// Don't prompt if JIRA is not configured
			if (!jiraConfigured)
			{
				System.Diagnostics.Debug.WriteLine("Not prompting: JIRA not configured");
				return false;
			}

			System.Diagnostics.Debug.WriteLine("Should prompt: All conditions met");
			return true;
		}

		private bool IsWithinTrackingHours()
		{
			var now = TimeOnly.FromDateTime(DateTime.Now);
			var startTime = _configurationService.AppSettings.TrackingStartTime;
			var endTime = _configurationService.AppSettings.TrackingEndTime;

			System.Diagnostics.Debug.WriteLine($"Current time: {now}");
			System.Diagnostics.Debug.WriteLine($"Tracking start: {startTime}");
			System.Diagnostics.Debug.WriteLine($"Tracking end: {endTime}");

			var result = _timeTrackingService.IsWithinTrackingHours(now, startTime, endTime);
			System.Diagnostics.Debug.WriteLine($"IsWithinTrackingHours result: {result}");

			return result;
		}

		private void CheckTrackingStatus()
		{
			if (_isOnLunchBreak)
			{
				_systemTrayService.UpdateStatus(TrayIconStatus.Lunch);
				return;
			}

			var isWithinHours = IsWithinTrackingHours();
			var status = isWithinHours ? TrayIconStatus.Active : TrayIconStatus.Inactive;
			_systemTrayService.UpdateStatus(status);

			// Fire tracking events
			var wasWithinHours = _promptTimer?.IsEnabled == true;
			if (isWithinHours && !wasWithinHours)
			{
				TrackingStarted?.Invoke(this, EventArgs.Empty);
			}
			else if (!isWithinHours && wasWithinHours)
			{
				TrackingEnded?.Invoke(this, EventArgs.Empty);
			}
		}

		private async Task CheckEndOfDayAsync()
		{
			var now = TimeOnly.FromDateTime(DateTime.Now);
			var endTime = _timeTrackingService.ParseTimeString(_configurationService.AppSettings.TrackingEndTime);

			// Check if we just passed the end time
			if (now >= endTime && now <= endTime.AddMinutes(1))
			{
				try
				{
					await _timeTrackingService.StopTrackingAsync();

					// Show notification
					System.Windows.MessageBox.Show(
						"End of tracking day reached. Current task has been automatically stopped.",
						"TaskTracker",
						System.Windows.MessageBoxButton.OK,
						System.Windows.MessageBoxImage.Information);
				}
				catch (Exception ex)
				{
					System.Diagnostics.Debug.WriteLine($"Error stopping tracking at end of day: {ex.Message}");
				}
			}
		}
	}
}