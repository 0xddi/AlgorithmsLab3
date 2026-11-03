using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace AlgorithmsUI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        AddHandler(KeyDownEvent, OnShortcutKeyDown, RoutingStrategies.Tunnel);
    }

    private void OnShortcutKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers != KeyModifiers.None || FocusManager?.GetFocusedElement() is TextBox)
            return;

        if (Pages.SelectedItem is IShortcutHandler page && page.HandleShortcut(e.Key))
            e.Handled = true;
    }
}
