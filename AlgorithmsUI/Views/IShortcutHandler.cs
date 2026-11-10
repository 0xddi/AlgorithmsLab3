using Avalonia.Input;

namespace AlgorithmsUI.Views;

public interface IShortcutHandler
{
    bool HandleShortcut(Key key);
}
