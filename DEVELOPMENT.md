# TaskTracker Development Guide

## Project Structure

```
TaskTracker/
├── .github/
│   └── copilot-instructions.md     # Copilot customization instructions
├── .vscode/
│   └── tasks.json                  # VS Code build tasks
├── Data/
│   └── TaskTrackerDbContext.cs     # Entity Framework database context
├── Models/
│   ├── AppConfiguration.cs         # Application settings models
│   ├── JiraProject.cs             # JIRA project entity
│   ├── JiraTask.cs                # JIRA task entity  
│   ├── TimeEntry.cs               # Time tracking entity
│   └── Jira/
│       └── JiraApiModels.cs       # JIRA API response models
├── Services/
│   ├── ConfigurationService.cs    # Application settings management
│   ├── JiraApiService.cs          # JIRA Cloud API integration
│   ├── SystemTrayService.cs       # System tray functionality
│   ├── TaskManagementService.cs   # Task and project management
│   └── TimeTrackingService.cs     # Time tracking logic
├── Themes/
│   ├── DarkTheme.xaml             # Dark theme resources
│   └── LightTheme.xaml            # Light theme resources
├── ViewModels/
│   ├── ViewModelBase.cs           # Base MVVM classes and commands
│   └── MainViewModel.cs           # Main window view model
├── Views/                          # (To be created)
│   ├── SettingsWindow.xaml
│   ├── JiraProjectsWindow.xaml
│   ├── JiraTasksWindow.xaml
│   ├── SummaryWindow.xaml
│   └── TaskPromptWindow.xaml
├── App.xaml                       # Application resources
├── App.xaml.cs                    # Application startup logic
├── MainWindow.xaml                # Main application window
├── MainWindow.xaml.cs             # Main window code-behind
├── appsettings.json               # Application configuration
└── TaskTracker.csproj             # Project file
```

## Architecture

The application follows the **MVVM (Model-View-ViewModel)** pattern:

### Models
- **Entity Models**: `JiraProject`, `JiraTask`, `TimeEntry` - Database entities
- **Configuration Models**: `AppSettings`, `JiraSettings` - Application settings
- **API Models**: JIRA API response models for deserialization

### Services (Business Logic)
- **ConfigurationService**: Manages app settings and persistence
- **JiraApiService**: Handles JIRA Cloud API communication
- **TaskManagementService**: Project and task management operations
- **TimeTrackingService**: Time tracking logic and database operations
- **SystemTrayService**: System tray icon and notifications

### Views & ViewModels
- **MainWindow/MainViewModel**: Primary application interface
- **Planned Views**: Settings, Projects, Tasks, Summary, Task Prompt dialogs

## Key Features Implemented

✅ **Core Infrastructure**
- Dependency injection setup
- Entity Framework with SQLite
- JIRA Cloud API integration
- System tray functionality
- Theme support (Dark/Light)
- MVVM pattern implementation

✅ **Database Models**
- JIRA projects and tasks storage
- Time entry tracking with relationships
- Configuration persistence

✅ **Services Layer**
- JIRA API client with authentication
- Time tracking with business rules
- Configuration management
- System tray with status indicators

## Next Steps

To complete the application, you'll need to create:

1. **Additional Windows**:
   - SettingsWindow - JIRA configuration and app settings
   - JiraProjectsWindow - Project selection interface
   - JiraTasksWindow - Task management interface  
   - SummaryWindow - Daily time summaries with navigation
   - TaskPromptWindow - Modal task selection dialog

2. **Core Application Logic**:
   - Timer-based task prompting system
   - Lunch break handling
   - End-of-day automatic task closing
   - Task prompt timeout handling

3. **Additional Features**:
   - Data migration/upgrades
   - Error handling and logging
   - Application settings validation
   - JIRA connection testing UI

## Running the Application

```bash
# Debug mode
dotnet run

# Release build
dotnet build --configuration Release
dotnet run --configuration Release

# VS Code tasks
Ctrl+Shift+P → "Tasks: Run Task" → "build"
```

## Development Notes

- The app starts minimized to system tray
- Double-click tray icon to show main window
- Main window hides (not closes) when X button clicked
- Database is created automatically on first run
- Configuration saved to `appsettings.json`
