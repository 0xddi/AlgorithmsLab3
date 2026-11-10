using Avalonia;
using Avalonia.Controls;

namespace AlgorithmsUI.Controls;

/// <summary>One "label ............ value" line; the value uses the monospaced <c>stat-value</c> style.</summary>
public sealed class StatRow : Grid
{
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<StatRow, string?>(nameof(Label));

    public static readonly StyledProperty<string?> ValueProperty =
        AvaloniaProperty.Register<StatRow, string?>(nameof(Value), "—");

    public StatRow()
    {
        ColumnDefinitions = new ColumnDefinitions("*,Auto");

        var label = new TextBlock { Classes = { "stat-label" } };
        label[!TextBlock.TextProperty] = this[!LabelProperty];

        var value = new TextBlock { Classes = { "stat-value" }, Margin = new Thickness(12, 0, 0, 0) };
        value[!TextBlock.TextProperty] = this[!ValueProperty];
        SetColumn(value, 1);

        Children.Add(label);
        Children.Add(value);
    }

    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }
}
