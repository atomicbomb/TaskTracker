using System.Windows;
using TaskTracker.ViewModels;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace TaskTracker.Views;

public partial class SummaryWindow : Window
{
    private bool _isApplyingTaskEdit;
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

    private async void EntriesGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.EditAction != DataGridEditAction.Commit) return;

    // Handle Task # edits
    if (e.Column is DataGridTemplateColumn && e.Column.Header?.ToString() == "Task #")
        {
            if (DataContext is SummaryViewModel vm && e.Row?.Item is TaskTracker.Models.TimeEntry entry)
            {
                // In a TemplateColumn, EditingElement is often a ContentPresenter; find the TextBox inside
                var tb = FindVisualChild<System.Windows.Controls.TextBox>(e.EditingElement) ?? e.EditingElement as System.Windows.Controls.TextBox;
                var newTaskNumber = tb?.Text?.Trim();

                if (!string.IsNullOrWhiteSpace(newTaskNumber) && newTaskNumber != entry.Task?.JiraTaskNumber)
                {
                    if (_isApplyingTaskEdit) return;
                    _isApplyingTaskEdit = true;

                    // Defer the update until after the DataGrid fully commits the edit to avoid re-entrancy/freezes
                    await Dispatcher.BeginInvoke(async () =>
                    {
                        try
                        {
                            var (success, updated) = await vm.ChangeTimeEntryTaskByNumberAsync(entry, newTaskNumber);
                            if (success && updated != null)
                            {
                                // Replace the item in the collection so the grid updates only this row
                                var list = vm.TimeEntries;
                                var idx = list.IndexOf(entry);
                                if (idx >= 0)
                                {
                                    list[idx] = updated;
                                }
                            }
                            else
                            {
                                // Show inline feedback
                                System.Media.SystemSounds.Beep.Play();
                                // Optionally, we could adorn the cell; for now set StatusMessage (already set in VM)
                            }
                        }
                        finally
                        {
                            _isApplyingTaskEdit = false;
                        }
                    }, DispatcherPriority.Background);
                }
            }
        }

        // Handle Start/End time edits
        if (e.Column is DataGridTemplateColumn && (e.Column.Header?.ToString() == "Start" || e.Column.Header?.ToString() == "End"))
        {
            if (DataContext is SummaryViewModel vm && e.Row?.Item is TaskTracker.Models.TimeEntry entry)
            {
                var tb = FindVisualChild<System.Windows.Controls.TextBox>(e.EditingElement) ?? e.EditingElement as System.Windows.Controls.TextBox;
                var text = tb?.Text?.Trim();
                if (string.IsNullOrWhiteSpace(text)) return;

                if (_isApplyingTaskEdit) return;
                _isApplyingTaskEdit = true;

                await Dispatcher.BeginInvoke(async () =>
                {
                    try
                    {
                        // parse HH:mm
                        if (!TimeSpan.TryParse(text, out var parsed))
                        {
                            System.Media.SystemSounds.Beep.Play();
                            return;
                        }

                        var date = entry.StartTime.Date;
                        var newStart = entry.StartTime;
                        DateTime? newEnd = entry.EndTime;

                        if (e.Column.Header?.ToString() == "Start")
                        {
                            newStart = new DateTime(date.Year, date.Month, date.Day, parsed.Hours, parsed.Minutes, 0);
                        }
                        else
                        {
                            newEnd = new DateTime(date.Year, date.Month, date.Day, parsed.Hours, parsed.Minutes, 0);
                        }

                        // basic validation
                        if (newEnd.HasValue && newEnd.Value <= newStart)
                        {
                            System.Media.SystemSounds.Beep.Play();
                            return;
                        }

                        // call service via VM helper
                        var updated = await vm.UpdateTimeEntryTimesAsync(entry, newStart, newEnd);
                        if (updated != null)
                        {
                            var list = vm.TimeEntries;
                            var idx = list.IndexOf(entry);
                            if (idx >= 0) list[idx] = updated;
                        }
                        else
                        {
                            System.Media.SystemSounds.Beep.Play();
                        }
                    }
                    finally
                    {
                        _isApplyingTaskEdit = false;
                    }
                }, DispatcherPriority.Background);
            }
        }
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        if (parent == null) return null;

        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild)
                return typedChild;

            var result = FindVisualChild<T>(child);
            if (result != null)
                return result;
        }
        return null;
    }
}
