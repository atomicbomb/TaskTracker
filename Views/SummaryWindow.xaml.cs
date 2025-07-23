using System.Windows;
using TaskTracker.ViewModels;

namespace TaskTracker.Views;

public partial class SummaryWindow : Window
{
    public SummaryWindow(SummaryViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        
        // Handle close request from ViewModel
        viewModel.CloseRequested += (_, _) => Close();
        
        // Set window icon if available
        try
        {
            Icon = System.Windows.Application.Current.MainWindow?.Icon;
        }
        catch
        {
            // Ignore icon errors
        }
    }
}
