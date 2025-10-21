using System.Windows;
using TaskTracker.ViewModels;

namespace TaskTracker.Views;

public partial class LogViewerWindow : Window
{
    public LogViewerWindow(LogViewerViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        vm.CloseRequested += (_, _) => Close();
    }
}
