using System.Windows;
using System.Windows.Controls;
using TaskTracker.ViewModels;

namespace TaskTracker.Views;

/// <summary>
/// Interaction logic for SettingsWindow.xaml
/// </summary>
public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        
        if (viewModel != null)
        {
            viewModel.SettingsSaved += OnSettingsSaved;
            viewModel.SettingsCancelled += OnSettingsCancelled;
        }
    }

    private void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel viewModel && sender is PasswordBox passwordBox)
        {
            viewModel.ApiToken = passwordBox.Password;
        }
    }

    private void OnGoogleSecretChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel viewModel && sender is PasswordBox passwordBox)
        {
            viewModel.GoogleClientSecret = passwordBox.Password;
        }
    }
    
    private void OnSettingsSaved(object? sender, EventArgs e)
    {
        DialogResult = true;
        Close();
    }
    
    private void OnSettingsCancelled(object? sender, EventArgs e)
    {
        DialogResult = false;
        Close();
    }
    
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (DataContext is SettingsViewModel viewModel)
        {
            viewModel.SettingsSaved -= OnSettingsSaved;
            viewModel.SettingsCancelled -= OnSettingsCancelled;
        }
        base.OnClosing(e);
    }
}
