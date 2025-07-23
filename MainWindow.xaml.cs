using System.Windows;
using TaskTracker.ViewModels;

namespace TaskTracker;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private MainViewModel? _viewModel;

    public MainWindow()
    {
        InitializeComponent();
    }

    public MainWindow(MainViewModel viewModel) : this()
    {
        _viewModel = viewModel;
        DataContext = _viewModel;
        
        // Subscribe to the hide window event
        _viewModel.HideWindowRequested += OnHideWindowRequested;
    }

    private void OnHideWindowRequested(object? sender, EventArgs e)
    {
        Hide();
    }

    protected override async void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        
        if (_viewModel != null)
        {
            await _viewModel.InitializeAsync();
        }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // Hide instead of close when user clicks X
        e.Cancel = true;
        Hide();
    }
}