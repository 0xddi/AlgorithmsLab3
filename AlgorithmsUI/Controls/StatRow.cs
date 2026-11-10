using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AlgorithmsUI.Theme;

namespace AlgorithmsUI.Controls;

/// <summary>One "label ............ value" line; the value uses the monospaced <c>stat-value</c> style.</summary>
public sealed class StatRow : Grid
{
    private static readonly TimeSpan FlashDuration = TimeSpan.FromMilliseconds(180);

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<StatRow, string?>(nameof(Label));

    public static readonly StyledProperty<string?> ValueProperty =
        AvaloniaProperty.Register<StatRow, string?>(nameof(Value), "—");

    private readonly TextBlock _valueText;
    private int _flashVersion;

    public StatRow()
    {
        ColumnDefinitions = new ColumnDefinitions("*,Auto");

        var label = new TextBlock { Classes = { "stat-label" } };
        label[!TextBlock.TextProperty] = this[!LabelProperty];

        _valueText = new TextBlock { Classes = { "stat-value" }, Margin = new Thickness(12, 0, 0, 0) };
        _valueText[!TextBlock.TextProperty] = this[!ValueProperty];
        SetColumn(_valueText, 1);

        Children.Add(label);
        Children.Add(_valueText);
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

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ValueProperty && change.OldValue is not null)
            _ = FlashValueAsync();
    }

    /// <summary>New numbers slide in and brighten; a value swap no longer looks like a silent text replacement.</summary>
    private async Task FlashValueAsync()
    {
        int version = ++_flashVersion;
        var translate = new TranslateTransform(0, 3);
        _valueText.RenderTransform = translate;

        try
        {
            await Task.WhenAll(
                Motion.AnimateAsync(_valueText, Visual.OpacityProperty, 0.3, 1, FlashDuration, Motion.Enter),
                Motion.AnimateAsync(translate, TranslateTransform.YProperty, 3, 0, FlashDuration, Motion.Enter));
        }
        catch
        {
            // A newer value already owns the visual state.
        }
        finally
        {
            if (version == _flashVersion)
            {
                _valueText.RenderTransform = null;
                _valueText.Opacity = 1;
            }
        }
    }
}
