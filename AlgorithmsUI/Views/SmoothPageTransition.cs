using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Media;
using AlgorithmsUI.Theme;

namespace AlgorithmsUI.Views;

/// <summary>
/// Page change used by the main carousel: the outgoing page drifts up and fades while the incoming page
/// arrives from below. A small offset plus a cross-fade reads as motion without the full-width slide that
/// makes a heavy page look like it is flying across the window.
/// </summary>
public sealed class SmoothPageTransition : IPageTransition
{
    public TimeSpan Duration { get; set; } = TimeSpan.FromMilliseconds(320);

    public double Offset { get; set; } = 26;

    public async Task Start(Visual? from, Visual? to, bool forward, CancellationToken cancellationToken)
    {
        double direction = forward ? 1d : -1d;
        var animations = new List<Task>(4);

        if (from is Control fromControl)
        {
            fromControl.RenderTransform = new TranslateTransform();
            animations.Add(Motion.AnimateAsync(
                fromControl, Visual.OpacityProperty, fromControl.Opacity, 0d, Duration * 0.7, Motion.Exit, cancellationToken));
            animations.Add(Motion.AnimateAsync(
                fromControl, TranslateTransform.YProperty, 0d, -Offset * 0.7 * direction, Duration * 0.7,
                Motion.Exit, cancellationToken));
        }

        if (to is Control toControl)
        {
            toControl.RenderTransform = new TranslateTransform(0, Offset * direction);
            toControl.Opacity = 0d;
            animations.Add(Motion.AnimateAsync(
                toControl, Visual.OpacityProperty, 0d, 1d, Duration, Motion.Enter, cancellationToken));
            animations.Add(Motion.AnimateAsync(
                toControl, TranslateTransform.YProperty, Offset * direction, 0d, Duration, Motion.Enter,
                cancellationToken));
        }

        bool cancelled = false;
        try
        {
            await Task.WhenAll(animations);
        }
        catch (OperationCanceledException)
        {
            // The carousel cancelled this page change because another page was selected: leave the visuals
            // to the transition that is taking over.
            cancelled = true;
        }

        if (cancelled)
            return;

        if (from is Control fromDone)
        {
            fromDone.RenderTransform = null;
            fromDone.Opacity = 0d;
        }

        if (to is Control toDone)
        {
            toDone.RenderTransform = null;
            toDone.Opacity = 1d;
        }
    }
}
