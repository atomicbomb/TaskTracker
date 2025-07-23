using System.Globalization;
using System.Windows;
using System.Windows.Data;
using TaskTracker.ViewModels;

namespace TaskTracker.Views;

/// <summary>
/// Interaction logic for TaskPromptWindow.xaml
/// </summary>
public partial class TaskPromptWindow : Window
{
    public TaskPromptWindow()
    {
        InitializeComponent();
    }
    
    public TaskPromptWindow(TaskPromptViewModel viewModel) : this()
    {
        DataContext = viewModel;
        
        if (viewModel != null)
        {
            viewModel.TaskSelected += OnTaskSelected;
            viewModel.LunchStarted += OnLunchStarted;
            viewModel.PromptCancelled += OnPromptCancelled;
            viewModel.PromptTimedOut += OnPromptTimedOut;
        }
    }
    
    private void OnTaskSelected(object? sender, TaskSelectedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
    
    private void OnLunchStarted(object? sender, LunchStartedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
    
    private void OnPromptCancelled(object? sender, EventArgs e)
    {
        DialogResult = false;
        Close();
    }
    
    private void OnPromptTimedOut(object? sender, EventArgs e)
    {
        DialogResult = null; // Indicates timeout
        Close();
    }
    
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (DataContext is TaskPromptViewModel viewModel)
        {
            viewModel.TaskSelected -= OnTaskSelected;
            viewModel.LunchStarted -= OnLunchStarted;
            viewModel.PromptCancelled -= OnPromptCancelled;
            viewModel.PromptTimedOut -= OnPromptTimedOut;
            viewModel.Dispose();
        }
        base.OnClosing(e);
    }
    
    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        // Ensure window stays on top and gets focus
        Topmost = true;
        Focus();
    }
}
