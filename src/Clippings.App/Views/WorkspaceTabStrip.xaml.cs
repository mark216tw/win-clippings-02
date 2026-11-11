using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Clippings.App.Models;

namespace Clippings.App.Views;

public partial class WorkspaceTabStrip : UserControl
{
    public WorkspaceTabStrip()
    {
        InitializeComponent();
    }

    public event EventHandler? TabSelectionChanged;

    public event EventHandler<WorkspaceTab>? TabCloseRequested;

    public event EventHandler? CloseAllRequested;

    public IEnumerable? ItemsSource
    {
        get => TabList.ItemsSource;
        set => TabList.ItemsSource = value;
    }

    public WorkspaceTab? SelectedTab
    {
        get => TabList.SelectedItem as WorkspaceTab;
        set => TabList.SelectedItem = value;
    }

    public void SetCloseAllEnabled(bool enabled) => CloseAllButton.IsEnabled = enabled;

    private void OnCloseAllClick(object sender, RoutedEventArgs e)
        => CloseAllRequested?.Invoke(this, EventArgs.Empty);

    private void OnTabSelectionChanged(object sender, SelectionChangedEventArgs e)
        => TabSelectionChanged?.Invoke(this, EventArgs.Empty);

    private void OnCloseTabPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        e.Handled = true;
        if ((sender as FrameworkElement)?.DataContext is WorkspaceTab tab)
        {
            TabCloseRequested?.Invoke(this, tab);
        }
    }
}
