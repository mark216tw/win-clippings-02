using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Clippings.App.Helpers;
using Clippings.Platform.Interop;

namespace Clippings.App.Views;

public partial class RecordingTargetDialog : Window
{
    private readonly WindowEnumerationService _windows;

    public RecordingTargetDialog(WindowEnumerationService windows)
    {
        _windows = windows;
        InitializeComponent();
        RefreshTargets();
    }

    public WindowRecordingTarget? SelectedTarget { get; private set; }

    private void RefreshTargets()
    {
        var targets = _windows.GetRecordingTargets()
            .Select(target => new TargetOption(
                target,
                target.Title,
                target.ProcessName,
                ShellIconHelper.GetIcon(target.ExecutablePath)))
            .ToArray();
        TargetList.ItemsSource = targets;
        TargetList.SelectedIndex = targets.Length > 0 ? 0 : -1;
        EmptyText.Visibility = targets.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        TargetList.Visibility = targets.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        => ConfirmButton.IsEnabled = TargetList.SelectedItem is TargetOption;

    private void OnTargetDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (TargetList.SelectedItem is TargetOption)
        {
            ConfirmSelection();
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            e.Handled = true;
        }
    }

    private void OnRefreshClick(object sender, RoutedEventArgs e) => RefreshTargets();

    private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;

    private void OnConfirmClick(object sender, RoutedEventArgs e) => ConfirmSelection();

    private void ConfirmSelection()
    {
        if (TargetList.SelectedItem is not TargetOption option)
        {
            return;
        }

        SelectedTarget = option.Target;
        DialogResult = true;
    }

    private sealed record TargetOption(
        WindowRecordingTarget Target,
        string Title,
        string ProcessName,
        ImageSource? Icon);
}
