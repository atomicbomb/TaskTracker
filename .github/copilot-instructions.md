# TaskTracker - Copilot Instructions

<!-- Use this file to provide workspace-specific custom instructions to Copilot. For more details, visit https://code.visualstudio.com/docs/copilot/copilot-customization#_use-a-githubcopilotinstructionsmd-file -->

## Project Overview
TaskTracker is a Windows desktop WPF application that:
- Runs in the system tray
- Integrates with JIRA Cloud API for project and task management
- Tracks time spent on JIRA tasks with automated prompting
- Uses SQLite database for local data storage
- Supports dark/light theme modes
- Implements MVVM pattern with WPF

## Architecture Guidelines
- Use MVVM pattern for all UI components
- Implement INotifyPropertyChanged for ViewModels
- Use Entity Framework Core with SQLite for data persistence
- Use dependency injection for services
- Separate concerns: Services for business logic, ViewModels for UI logic
- Use async/await patterns for JIRA API calls and database operations

## Key Components
- **Models**: Database entities and JIRA data models
- **Views**: WPF user controls and windows
- **ViewModels**: UI binding and command logic
- **Services**: JIRA API integration, database operations, system tray management
- **Data**: Entity Framework context and migrations
- **Themes**: Dark/Light mode resource dictionaries

## Coding Standards
- Use nullable reference types
- Implement proper error handling and logging
- Follow async/await best practices
- Use configuration patterns for app settings
- Implement proper disposal of resources
- Use Windows Forms NotifyIcon for system tray functionality
