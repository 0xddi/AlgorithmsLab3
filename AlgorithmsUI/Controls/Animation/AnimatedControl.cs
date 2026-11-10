using Avalonia;
using Avalonia.Controls;

namespace AlgorithmsUI.Controls.Animation;

/// <summary>
/// Base class for the hand-drawn canvases. Instead of snapping straight to the new state, a canvas keeps
/// smoothed values (<see cref="SmoothValue"/>) for positions, opacities and scales; every frame moves them
/// a little closer to the target and redraws. That is what turns the step-by-step algorithms into motion.
/// </summary>
public abstract class AnimatedControl : Control, IFrameListener
{
    protected AnimatedControl()
    {
        ClipToBounds = true;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        FrameClock.Subscribe(this);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        FrameClock.Unsubscribe(this);
        base.OnDetachedFromVisualTree(e);
    }

    bool IFrameListener.Advance(double deltaSeconds)
    {
        bool changed;
        try
        {
            changed = OnAdvance(deltaSeconds);
        }
        catch
        {
            // A broken animation must never take the window down; just stop asking for frames.
            changed = false;
        }

        if (changed && VisualRoot != null)
            InvalidateVisual();

        return changed;
    }

    /// <summary>Moves every animated value; returns <c>true</c> while the drawing still changes.</summary>
    protected abstract bool OnAdvance(double deltaSeconds);

    /// <summary>Wakes the shared clock after a state change.</summary>
    protected void RequestAnimation() => FrameClock.RequestFrame();

    /// <summary>Changes a target only when it really changed, and wakes the clock in that case.</summary>
    protected void SetTarget(SmoothValue value, double target)
    {
        if (Math.Abs(value.Target - target) < 0.001d)
            return;

        value.Target = target;
        RequestAnimation();
    }
}

/// <summary>
/// Exponential smoothing towards a target: frame-rate independent, never overshoots and settles exactly
/// on the target, so positions and fades look continuous instead of stepping on every timer tick.
/// </summary>
public sealed class SmoothValue
{
    public SmoothValue(double value = 0, double speed = 14)
    {
        Value = value;
        Target = value;
        Speed = speed;
    }

    public double Value { get; private set; }

    public double Target { get; set; }

    /// <summary>Higher is snappier; 8 is slow and calm, 20 is quick.</summary>
    public double Speed { get; set; }

    public bool Settled => Math.Abs(Target - Value) < 0.0005;

    public bool Advance(double deltaSeconds)
    {
        double difference = Target - Value;
        if (Math.Abs(difference) < 0.0005)
        {
            if (Value == Target)
                return false;

            Value = Target;
            return true;
        }

        Value += difference * (1d - Math.Exp(-Speed * deltaSeconds));
        if (Math.Abs(Target - Value) < 0.0005)
            Value = Target;

        return true;
    }

    public void SnapTo(double value)
    {
        Value = value;
        Target = value;
    }
}

/// <summary>Easing curves used by the canvases; the same curves as the XAML transitions in <c>Theme/Motion.cs</c>.</summary>
public static class Ease
{
    public static double Clamp01(double t) => t < 0d ? 0d : t > 1d ? 1d : t;

    public static double OutCubic(double t)
    {
        t = Clamp01(t);
        double inverse = 1d - t;
        return 1d - inverse * inverse * inverse;
    }


    public static double InOutCubic(double t)
    {
        t = Clamp01(t);
        return t < 0.5d ? 4d * t * t * t : 1d - Math.Pow(-2d * t + 2d, 3d) / 2d;
    }

    public static double OutBack(double t)
    {
        t = Clamp01(t);
        const double c1 = 1.70158d;
        const double c3 = c1 + 1d;
        double shifted = t - 1d;
        return 1d + c3 * shifted * shifted * shifted + c1 * shifted * shifted;
    }



    public static double Lerp(double from, double to, double t) => from + (to - from) * t;

    /// <summary>0..1 wave used for gentle "breathing" highlights.</summary>
    public static double Pulse(double time, double speed = 1.6) => 0.5d + 0.5d * Math.Sin(time * speed * Math.PI * 2d);
}
