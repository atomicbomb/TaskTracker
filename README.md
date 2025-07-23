# TaskTracker

A Windows desktop WPF application for tracking time spent on JIRA tasks with automated prompting and system tray integration.

## Features

- **System Tray Integration**: Runs quietly in the background
- **JIRA Cloud API Integration**: Fetch projects and tasks directly from JIRA
- **Automated Time Tracking**: Prompts at configurable intervals to track current task
- **Project Management**: Select which JIRA projects to track
- **Task Management**: Add tasks manually or fetch from JIRA
- **Time Summaries**: View daily, weekly, and detailed time entry reports
- **Lunch Break Support**: Track lunch breaks separately
- **Dark/Light Theme**: Configurable UI themes
- **SQLite Database**: Local data storage for offline operation

## Architecture

- **MVVM Pattern**: Clean separation of concerns with ViewModels
- **Dependency Injection**: Service-based architecture
- **Entity Framework Core**: Database operations with SQLite
- **Async/Await**: Non-blocking operations for API calls and database access

## Key Components

### Services
- `ApplicationService`: Coordinates all application windows and services
- `JiraApiService`: Handles JIRA Cloud REST API integration
- `TimeTrackingService`: Manages time entry operations
- `TaskManagementService`: Handles project and task management
- `TimerService`: Automated prompting and break management
- `SystemTrayService`: System tray icon and notifications
- `ConfigurationService`: Application and JIRA settings management

### ViewModels
- `TaskPromptViewModel`: Task selection and lunch break prompts
- `SummaryViewModel`: Time tracking reports and summaries
- `SettingsViewModel`: Application configuration
- `JiraProjectsViewModel`: Project selection and management
- `JiraTasksViewModel`: Task management
- `MainViewModel`: Main application window

### Models
- `JiraProject`: Project information from JIRA
- `JiraTask`: Task information from JIRA
- `TimeEntry`: Individual time tracking entries
- `AppSettings`: Application configuration
- `JiraSettings`: JIRA API configuration

## Getting Started

1. **Configure JIRA Settings**:
   - Open Settings from the system tray
   - Enter your JIRA Cloud URL (e.g., `https://yourcompany.atlassian.net`)
   - Provide your JIRA username and API token
   - Set your tracking hours (when prompts should appear)

2. **Select Projects**:
   - Use "JIRA Projects" to fetch and select projects you want to track
   - Only selected projects will appear in task prompts

3. **Add Tasks**:
   - Use "JIRA Tasks" to fetch tasks from selected projects
   - Or manually add specific task numbers

4. **Start Tracking**:
   - The application will prompt you at configured intervals
   - Select your current task or indicate lunch breaks
   - View summaries to see your time allocation

## Configuration

### JIRA API Token
1. Go to [Atlassian Account Settings](https://id.atlassian.com/manage-profile/security/api-tokens)
2. Create a new API token
3. Use your email address as username and the token as password

### Tracking Hours
- Configure start/end times for when prompts should appear
- Set prompt intervals (how often you're asked about current task)
- Customize lunch break durations

## Technology Stack

- **.NET 8.0**: Latest .NET framework
- **WPF**: Windows Presentation Foundation for UI
- **Entity Framework Core**: Database operations
- **SQLite**: Local database storage
- **JIRA Cloud REST API v2**: External integration
- **System.Windows.Forms**: System tray functionality

## Requirements

- Windows 10/11
- .NET 8.0 Runtime
- JIRA Cloud instance (for full functionality)

## Build Instructions

```bash
# Clone the repository
git clone https://github.com/yourusername/TaskTracker.git
cd TaskTracker

# Build the application
dotnet build

# Run the application
dotnet run
```

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests if applicable
5. Submit a pull request

## Acknowledgments

- JIRA Cloud REST API for task integration
- Entity Framework Core team for database abstraction
- .NET team for the excellent framework
- **Dark/Light Themes**: Support for both dark and light UI themes
- **Local Data Storage**: Uses SQLite for local data persistence
- **Daily Summaries**: View detailed time breakdowns by day

## System Requirements

- Windows 10 or later
- .NET 8.0 Runtime
- JIRA Cloud account with API token

## Configuration

The application stores its configuration in `appsettings.json`:

- **JIRA Integration**: Server URL, email, and API token
- **Tracking Schedule**: Start/end times and prompt intervals
- **UI Preferences**: Theme and timeout settings

## Getting Started

1. Run the application - it will start in the system tray
2. Configure JIRA settings through the Settings menu
3. Select which JIRA projects to track in the Projects menu
4. The application will automatically prompt you during tracking hours

## System Tray Status

- 🟢 Green: Active tracking hours
- 🔴 Red: Outside tracking hours  
- 🟠 Orange: On lunch break

## Development

Built with:
- WPF (.NET 8.0)
- Entity Framework Core with SQLite
- JIRA REST API v2
- MVVM Architecture

## License

This project is for personal use.
