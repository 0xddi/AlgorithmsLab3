using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace AlgorithmsUI.Theme;

/// <summary>
/// The single motion language of the app: durations and easings live here so page changes, tab changes
/// and micro-interactions all feel like one product instead of a mix of instant jumps.
/// </summary>
public static class Motion
{
    /// <summary>Hover / press feedback.</summary>
    public static readonly TimeSpan Fast = TimeSpan.FromMilliseconds(140);

    /// <summary>Panels, tab content, status labels.</summary>
    public static readonly TimeSpan Normal = TimeSpan.FromMilliseconds(240);


    /// <summary>Default curve for anything that appears.</summary>
    public static Easing Enter { get; } = new CubicEaseOut();

    /// <summary>Default curve for anything that leaves.</summary>
    public static Easing Exit { get; } = new CubicEaseIn();

    public static Task AnimateAsync(
        Animatable target,
        AvaloniaProperty property,
        double from,
        double to,
        TimeSpan duration,
        Easing? easing = null,
        CancellationToken cancellationToken = default)
    {
        var animation = new Animation
        {
            Duration = duration,
            Easing = easing ?? Enter,
            FillMode = FillMode.Forward,
            Children =
            {
                new KeyFrame
                {
                    Cue = new Cue(0d),
                    Setters = { new Setter(property, from) }
                },
                new KeyFrame
                {
                    Cue = new Cue(1d),
                    Setters = { new Setter(property, to) }
                }
            }
        };

        return animation.RunAsync(target, cancellationToken);
    }

    /// <summary>
    /// Fades a panel in while it slides a few pixels into place. The vertical offset is animated through the
    /// control's <see cref="TranslateTransform"/>: Avalonia resolves <c>TranslateTransform.Y</c> against the
    /// animatable's <see cref="Visual.RenderTransform"/>, so the animation runs on the control itself.
    /// </summary>
    public static async Task FadeSlideInAsync(
        Control control,
        double offsetY = 14,
        TimeSpan? duration = null,
        CancellationToken cancellationToken = default)
    {
        var span = duration ?? Normal;
        control.IsVisible = true;
        control.Opacity = 0;
        control.RenderTransform = new TranslateTransform(0, offsetY);

        try
        {
            await Task.WhenAll(
                AnimateAsync(control, Visual.OpacityProperty, 0d, 1d, span, Enter, cancellationToken),
                AnimateAsync(control, TranslateTransform.YProperty, offsetY, 0d, span, Enter, cancellationToken));
        }
        finally
        {
            // Even when the animation is cancelled (fast tab clicking), the panel must never stay invisible.
            control.RenderTransform = null;
            control.Opacity = 1;
        }
    }
}
