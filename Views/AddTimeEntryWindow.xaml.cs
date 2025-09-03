using System.Windows;
using TaskTracker.ViewModels;

namespace TaskTracker.Views;

public partial class AddTimeEntryWindow : Window
{
    public AddTimeEntryWindow()
    {
        InitializeComponent();
        Loaded += AddTimeEntryWindow_Loaded;
    }

    private void AddTimeEntryWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is AddTimeEntryViewModel vm)
        {
            vm.CloseRequested += (s, result) =>
            {
                DialogResult = result;
                Close();
            };
        }
    }
}
