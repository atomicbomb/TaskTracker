using System.Windows;
using TaskTracker.ViewModels;
using TaskTracker.Views;

namespace TaskTracker.Views;

/// <summary>
/// Interaction logic for JiraProjectsWindow.xaml
/// </summary>
public partial class JiraProjectsWindow : Window
{
    public JiraProjectsWindow()
    {
        InitializeComponent();
    }
    
    public JiraProjectsWindow(JiraProjectsViewModel viewModel) : this()
    {
        DataContext = viewModel;
        
        if (viewModel != null)
        {
            viewModel.CloseRequested += OnCloseRequested;
        }
    }
    
    private void OnCloseRequested(object? sender, EventArgs e)
    {
        Close();
    }
    
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (DataContext is JiraProjectsViewModel viewModel)
        {
            viewModel.CloseRequested -= OnCloseRequested;
        }
        base.OnClosing(e);
    }
}
