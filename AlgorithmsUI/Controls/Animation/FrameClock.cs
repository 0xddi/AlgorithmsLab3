using System.Diagnostics;
using Avalonia.Threading;

namespace AlgorithmsUI.Controls.Animation;

/// <summary>A control that can be advanced by the shared <see cref="FrameClock"/>.</summary>
public interface IFrameListener
{
    /// <summary>Updates the animation state; returns <c>true</c> while another frame is needed.</summary>
    bool Advance(double deltaSeconds);
}

/// <summary>
/// One 60 fps timer shared by every animated control. The timer only runs while at least one
/// listener still moves, so an idle window costs nothing and all pages animate against the same clock
/// (no per-control timers that drift apart and make the motion look uneven).
/// </summary>
public static class FrameClock
{
    private const double MaxDelta = 0.064; // never step more than ~4 frames, e.g. after a drag or a breakpoint.
    private static readonly List<IFrameListener> Listeners = new();
    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    private static DispatcherTimer? _timer;
    private static double _lastSeconds;

    public static void Subscribe(IFrameListener listener)
    {
        if (Listeners.Contains(listener))
            return;

        Listeners.Add(listener);
        RequestFrame();
    }

    public static void Unsubscribe(IFrameListener listener) => Listeners.Remove(listener);

    /// <summary>Asks for a frame; call it whenever a control's target state changed.</summary>
    public static void RequestFrame()
    {
        _timer ??= new DispatcherTimer(
            TimeSpan.FromMilliseconds(16),
            DispatcherPriority.Background,
            (_, _) => Tick());

        if (_timer.IsEnabled)
            return;

        _lastSeconds = Clock.Elapsed.TotalSeconds;
        _timer.Start();
    }

    /// <summary>Advances every listener once and stops the clock when nothing is moving.</summary>
    public static void Pump(double deltaSeconds)
    {
        bool busy = false;
        for (int i = Listeners.Count - 1; i >= 0; i--)
        {
            if (i >= Listeners.Count)
                continue;

            busy |= Listeners[i].Advance(deltaSeconds);
        }

        if (!busy)
            _timer?.Stop();
    }

    private static void Tick()
    {
        double now = Clock.Elapsed.TotalSeconds;
        double delta = Math.Clamp(now - _lastSeconds, 0d, MaxDelta);
        _lastSeconds = now;
        Pump(delta);
    }
}
