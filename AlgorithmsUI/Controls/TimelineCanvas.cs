using System.Globalization;
using Avalonia;
using Avalonia.Media;
using AlgorithmsCore.Applications;
using AlgorithmsUI.Controls.Animation;
using AlgorithmsUI.Theme;

namespace AlgorithmsUI.Controls;

/// <summary>
/// Round Robin Gantt chart. Time slices grow out of the axis one after another, the playhead glides to the
/// end of the current quantum and the running process keeps a slow pulse, so the schedule looks like it is
/// being executed instead of appearing in one frame.
/// </summary>
public sealed class TimelineCanvas : AnimatedControl
{
    private static readonly IBrush TextBrush = Palette.Brush(Palette.Foreground);
    private static readonly IBrush MutedBrush = Palette.Brush(Palette.MutedForeground);
    private static readonly IPen AxisPen = new Pen(Palette.Brush(Palette.BorderStrong), 2);
    private static readonly IPen BlockBorderPen = new Pen(Palette.Brush(Palette.Border), 1.5);
    private static readonly IPen ActivePen = new Pen(Palette.Brush(Palette.VizCompare), 2);

    private static readonly Color[] ProcessColors =
    [
        Color.Parse("#2563EB"),
        Color.Parse("#D97706"),
        Color.Parse("#16A34A"),
        Color.Parse("#9333EA"),
        Color.Parse("#0891B2"),
        Color.Parse("#E11D48"),
    ];

    private readonly Dictionary<int, SmoothValue> _slices = new();
    private readonly FadeText _caption = new();
    private readonly SmoothValue _playhead = new(0, 10);
    private readonly SmoothValue _playheadAlpha = new(0, 12);
    private readonly SmoothValue _emptyOpacity = new(1, 12);
    private List<RoundRobinScheduler.ExecutionSlice> _data = new();
    private List<SimProcess> _processes = new();
    private int _currentSliceIndex = -1;
    private double _time;

    public void SetData(List<RoundRobinScheduler.ExecutionSlice> slices, List<SimProcess> processes, int currentSlice = -1)
    {
        int previousSlice = _currentSliceIndex;
        _data = slices;
        _processes = processes;
        _currentSliceIndex = currentSlice;
        int shownCount = currentSlice >= 0 ? currentSlice + 1 : slices.Count;

        for (int i = 0; i < slices.Count; i++)
        {
            if (!_slices.TryGetValue(i, out var presence))
            {
                presence = new SmoothValue(0, 14);
                _slices[i] = presence;
            }

            presence.Target = i < shownCount ? 1 : 0;
        }

        foreach (var pair in _slices)
        {
            if (pair.Key >= slices.Count)
                pair.Value.Target = 0;
        }

        _emptyOpacity.Target = slices.Count == 0 ? 1 : 0;

        if (currentSlice >= 0 && currentSlice < slices.Count)
        {
            if (previousSlice < 0 || currentSlice < previousSlice)
                _playhead.SnapTo(slices[currentSlice].EndTime);
            else
                _playhead.Target = slices[currentSlice].EndTime;

            _playheadAlpha.Target = 1;
            _caption.Update($"t={slices[currentSlice].StartTime}..{slices[currentSlice].EndTime}: {slices[currentSlice].ProcessName} (осталось {slices[currentSlice].RemainingAfter})");
        }
        else if (currentSlice < 0)
        {
            _playheadAlpha.Target = 0;
            _caption.Update(null);
        }
        else
        {
            _playheadAlpha.Target = 0;
            _caption.Update("Симуляция завершена — все процессы обслужены.");
        }

        RequestAnimation();
    }

    protected override bool OnAdvance(double deltaSeconds)
    {
        _time += deltaSeconds;
        bool changed = _caption.Advance(deltaSeconds);
        changed |= _playhead.Advance(deltaSeconds);
        changed |= _playheadAlpha.Advance(deltaSeconds);
        changed |= _emptyOpacity.Advance(deltaSeconds);

        foreach (var presence in _slices.Values)
            changed |= presence.Advance(deltaSeconds);

        return changed;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Bounds.Width < 10 || Bounds.Height < 10)
            return;

        double w = Bounds.Width;
        double h = Bounds.Height;

        if (_data.Count == 0)
        {
            if (_emptyOpacity.Value > 0.01)
            {
                var emptyText = new FormattedText(
                    "[ НАСТРОЙТЕ ПРОЦЕССЫ И ЗАПУСТИТЕ СИМУЛЯЦИЮ ]",
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Inter, Segoe UI", FontStyle.Normal, FontWeight.Medium),
                    14,
                    MutedBrush);
                using (context.PushOpacity(_emptyOpacity.Value))
                    context.DrawText(emptyText, new Point((w - emptyText.Width) / 2, h / 2 - emptyText.Height / 2));
            }

            return;
        }

        double startX = 40;
        double startY = 40;
        double totalTime = _data[^1].EndTime;
        double availableWidth = w - startX * 2;
        double timeScale = availableWidth / Math.Max(1, totalTime);
        double blockHeight = 44;

        var header = new FormattedText(
            "ДИАГРАММА ГАНТТА (ВЫДЕЛЕНИЕ КВАНТОВ ВРЕМЕНИ ПО FIFO):",
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter, Segoe UI", FontStyle.Normal, FontWeight.SemiBold),
            12,
            MutedBrush);
        context.DrawText(header, new Point(startX, startY - 24));

        int shownCount = _currentSliceIndex >= 0 ? _currentSliceIndex + 1 : _data.Count;
        double axisProgress = 0;

        for (int i = 0; i < _data.Count; i++)
        {
            if (!_slices.TryGetValue(i, out var presence) || presence.Value <= 0.01)
                continue;

            var slice = _data[i];
            double eased = Ease.OutCubic(Ease.Clamp01(presence.Value));
            double x1 = startX + slice.StartTime * timeScale;
            double fullWidth = Math.Max(2, (slice.EndTime - slice.StartTime) * timeScale);
            double sliceWidth = Math.Max(1, fullWidth * (0.15 + 0.85 * eased));
            double y = startY - (i == _currentSliceIndex ? 2 * Ease.Pulse(_time, 1.2) : 0);

            int processIndex = _processes.FindIndex(p => p.Name == slice.ProcessName);
            var color = ProcessColors[Math.Abs(processIndex) % ProcessColors.Length];
            var fillBrush = new SolidColorBrush(color);

            var rect = new Rect(x1, y, sliceWidth, blockHeight);
            using (context.PushOpacity(eased))
            {
                context.FillRectangle(fillBrush, rect, 4);

                bool isActive = i == _currentSliceIndex;
                if (isActive)
                {
                    double glow = 0.55 + 0.45 * Ease.Pulse(_time, 1.2);
                    using (context.PushOpacity(glow))
                        context.DrawRectangle(ActivePen, rect, 4);
                }
                else
                {
                    context.DrawRectangle(BlockBorderPen, rect, 4);
                }

                if (sliceWidth > 24)
                {
                    var label = new FormattedText(
                        slice.ProcessName,
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        new Typeface("Inter, Segoe UI", FontStyle.Normal, FontWeight.Bold),
                        12,
                        TextBrush);
                    context.DrawText(label, new Point(
                        x1 + (sliceWidth - label.Width) / 2,
                        y + (blockHeight - label.Height) / 2));
                }

                var tick = new FormattedText(
                    slice.EndTime.ToString(),
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Cascadia Mono, Consolas", FontStyle.Normal, FontWeight.Normal),
                    10,
                    MutedBrush);
                context.DrawText(tick, new Point(x1 + fullWidth - tick.Width / 2, startY + blockHeight + 6));
            }

            if (i < shownCount)
                axisProgress = Math.Max(axisProgress, eased);
        }

        var zeroTick = new FormattedText(
            "0",
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Cascadia Mono, Consolas", FontStyle.Normal, FontWeight.Normal),
            10,
            MutedBrush);
        context.DrawText(zeroTick, new Point(startX - zeroTick.Width / 2, startY + blockHeight + 6));

        double axisEnd = startX + availableWidth * Ease.OutCubic(axisProgress);
        context.DrawLine(AxisPen, new Point(startX, startY + blockHeight + 2), new Point(axisEnd, startY + blockHeight + 2));

        // Playhead: a soft vertical line that slides to the end of the running quantum.
        if (_playheadAlpha.Value > 0.01)
        {
            double playheadX = startX + _playhead.Value * timeScale;
            using (context.PushOpacity(_playheadAlpha.Value * 0.75))
            {
                var playheadPen = new Pen(Palette.Brush(Palette.VizCompare), 1.5, dashStyle: new DashStyle([4, 4], 0));
                context.DrawLine(playheadPen,
                    new Point(playheadX, startY - 10),
                    new Point(playheadX, startY + blockHeight + 18));
            }
        }

        _caption.Draw(context, w / 2, startY + blockHeight + 40, Palette.Brush(Palette.VizActive), 13, Math.Max(120, w - 40));

        if (_currentSliceIndex >= 0 && _currentSliceIndex < _data.Count)
        {
            var current = _data[_currentSliceIndex];
            string queue = current.QueueAfter.Count > 0 ? string.Join(" ──► ", current.QueueAfter) : "[пусто]";
            var queueText = new FormattedText(
                $"Состояние MyQueue:  {queue}",
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Cascadia Mono, Consolas", FontStyle.Normal, FontWeight.Normal),
                12,
                MutedBrush);
            using (context.PushOpacity(_caption.Opacity))
                context.DrawText(queueText, new Point(startX, startY + blockHeight + 66));
        }
    }
}
