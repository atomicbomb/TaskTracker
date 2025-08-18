# TaskTracker

A Windows desktop WPF application for tracking time spent on JIRA tasks with automated prompting, Google Calendar assists, and system tray integration.

## Features

- System Tray Integration: Background app with tray icon, context menu (Show, Exit)
- JIRA Cloud Integration: Fetch projects/tasks; auto-refresh on app start
- Automated Time Tracking: Prompt at intervals; Manual Task Update supported
- Task Management: Add tasks from JIRA or by number; manual tasks supported
- Project Management: Select which JIRA projects to track (UI filters out DEMO/TEST)
- Time Summaries: Daily, Weekly, and per-entry (“Time Entries”) views
- In-grid Editing (Summary):
   - Edit Task # per entry (row-scoped update)
   - Edit Start and End times (HH:mm) with validation
   - Delete time entries
- JIRA Tasks Status: Shows current Status text and a colored indicator per status category
- Lunch Break Support: Start/End lunch
- Themes: Dark/Light
- SQLite Database: Local storage for offline use
- Google Calendar (optional): OAuth desktop flow; scan calendars for events containing [JIRA-KEY] and add tasks

## Architecture

- MVVM pattern with ViewModels and INotifyPropertyChanged
- Dependency Injection (Microsoft.Extensions.DependencyInjection)
- Entity Framework Core (SQLite)
- Async/await for API and DB
- System tray via Windows Forms NotifyIcon

### Key Services
- ApplicationService, ConfigurationService
- JiraApiService, TaskManagementService, TimeTrackingService
- TimerService (prompts, optional calendar scanning)
- SystemTrayService (tray icon + context menu)
- GoogleIntegrationService (OAuth + Calendar scan)

### ViewModels
- MainViewModel, SettingsViewModel
- JiraProjectsViewModel, JiraTasksViewModel
- SummaryViewModel (Daily/Weekly/Time Entries), TaskPromptViewModel

## Current Behaviors and UI Notes

- Time Entries view shows entries for the selected date only; date navigation appears in both Daily and Time Entries views.
- JIRA Projects form excludes projects with codes DEMO and TEST from its list.
- JIRA Tasks form includes Status (text) and a colored dot by statusCategory (To Do/In Progress/Done).
- Manual Task Update: available from the main window to set a current task manually.

## Setup

1) JIRA configuration
- In Settings, enter:
   - Server URL (e.g., https://yourcompany.atlassian.net)
   - Email and API Token (create at Atlassian Account → Security → API tokens)
- Use JIRA Projects/Tasks windows to load and select content.

2) Google Calendar (optional)
- In Settings, enable Google integration and provide Client ID/Client Secret from Google Cloud Console (OAuth 2.0, Desktop app/Installed app).
- The app uses the loopback (LocalServerCodeReceiver) OAuth flow and requests Calendar Readonly scope.
- After connecting, you can test and set a scan interval. Events containing "[ABC-123]" style keys in the title are detected and tasks are added if valid.

3) Tracking
- The app runs in the system tray. Prompts appear during configured hours.
- Use Summary to inspect/edit/delete entries; use JIRA Tasks to manage task list.

## Build & Run

```powershell
dotnet build
dotnet run
```

Windows 10/11 with .NET 8.0 Runtime is required.

## Configuration Storage

The app stores settings in `appsettings.json` next to the executable, including:
- JIRA: ServerUrl, Email, ApiToken
- App: Theme, prompt interval/hours
- Google (optional): Enabled, ClientId, ClientSecret, RefreshToken, ScanIntervalMinutes

Secrets are stored locally; keep your device secure.

## System Tray Indicators

- 🟢 Active tracking hours
- 🔴 Inactive/outside hours
- 🟠 Lunch break

## License

This project is licensed under the MIT License — see LICENSE.
