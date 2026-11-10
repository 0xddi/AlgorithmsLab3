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
        // A focused text box keeps its own editing shortcuts (including Ctrl+Z inside the box).
        if (FocusManager?.GetFocusedElement() is TextBox)
            return;

        if (e.KeyModifiers == KeyModifiers.Control)
        {
            if (Pages.SelectedItem is ApplicationsView applications)
            {
                bool handled = e.Key switch
                {
                    Key.Z => applications.HandleEditorUndo(),
                    Key.Y => applications.HandleEditorRedo(),
                    _ => false
                };

                if (handled)
                    e.Handled = true;
            }

            return;
        }

        if (e.KeyModifiers != KeyModifiers.None)
            return;

        if (Pages.SelectedItem is IShortcutHandler page && page.HandleShortcut(e.Key))
            e.Handled = true;
    }
}
