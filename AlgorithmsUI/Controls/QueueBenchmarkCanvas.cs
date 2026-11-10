using System.Globalization;
using Avalonia;
using Avalonia.Media;
using AlgorithmsUI.Controls.Animation;
using AlgorithmsUI.Theme;

namespace AlgorithmsUI.Controls;

/// <summary>
/// Animated view of the queue comparison benchmark. The two measurements still run in tight loops (that is
/// what makes the timings meaningful); this canvas replays the same operation mix in compressed time and
/// shows both queues working side by side: the same enqueue/dequeue stream drives MyQueue and StandardQueue,
/// while live counters, the tail window and a progress bar show what is happening.
/// </summary>
public sealed class QueueBenchmarkCanvas : AnimatedControl
{
    private const int MaxWindowItems = 6;
    private const int MaxSamples = 6;

    private static readonly IBrush PanelBrush = Palette.Brush(Palette.Surface);
    private static readonly IBrush PanelBorderBrush = Palette.Brush(Palette.Border);
    private static readonly IBrush ItemBgBrush = Palette.Brush(Palette.SurfaceRaised);
    private static readonly IBrush HeadBgBrush = Palette.Brush(Palette.Accent);
    private static readonly IBrush TextBrush = Palette.Brush(Palette.Foreground);
    private static readonly IBrush MutedBrush = Palette.Brush(Palette.MutedForeground);
    private static readonly IBrush HeadBrush = Palette.Brush(Palette.VizActive);
    private static readonly IBrush TailBrush = Palette.Brush(Palette.VizWrite);
    private static readonly IBrush DoneBrush = Palette.Brush(Palette.VizDone);
    private static readonly IPen PanelPen = new Pen(PanelBorderBrush, 1);
    private static readonly IPen HeadPen = new Pen(Palette.Brush(Palette.VizActive), 2);
    private static readonly IPen TailPen = new Pen(Palette.Brush(Palette.VizWrite), 2);
    private static readonly IPen ChipPen = new Pen(Palette.Brush(Palette.Border), 1);

    private readonly List<WindowItem> _window = new();
    private readonly List<SampleVisual> _samples = new();
    private readonly SmoothValue _progress = new(0, 14);
    private readonly SmoothValue _enter = new(0, 9);

    private int _total;
    private string _mixLabel = "";
    private int _processed;
    private int _enqueues;
    private int _dequeues;
    private int _size;
    private bool _started;
    private bool _completed;
    private bool _truncated;
    private string _resultText = "";
    private TimeSpan? _myTime;
    private TimeSpan? _stdTime;

    public QueueBenchmarkCanvas()
    {
        _enter.SnapTo(1);
    }

    public void Begin(int total, string mixLabel)
    {
        _total = total;
        _mixLabel = mixLabel;
        _processed = 0;
        _enqueues = 0;
        _dequeues = 0;
        _size = 0;
        _started = true;
        _completed = false;
        _truncated = false;
        _resultText = "";
        _myTime = null;
        _stdTime = null;
        _window.Clear();
        _samples.Clear();
        _progress.SnapTo(0);
        _enter.SnapTo(0);
        _enter.Target = 1;
        RequestAnimation();
    }

    public void UpdateCounters(int processed, int enqueues, int dequeues, int size)
    {
        _processed = processed;
        _enqueues = enqueues;
        _dequeues = dequeues;
        _size = size;
        RequestAnimation();
    }

    /// <summary>Latest items at the tail of the queue (oldest first); the leftmost ones may be hidden.</summary>
    public void ShowWindow(IReadOnlyList<string> items, bool truncated)
    {
        _truncated = truncated;
        SyncWindow(items);
        RequestAnimation();
    }

    public void AddSample(bool isEnqueue, string text)
    {
        var sample = new SampleVisual(text, isEnqueue);
        sample.Presence.SnapTo(0);
        sample.Slot.SnapTo(-0.6);
        _samples.Insert(0, sample);

        for (int i = 0; i < _samples.Count; i++)
        {
            _samples[i].Slot.Target = i;
            _samples[i].Presence.Target = i < MaxSamples ? 1 : 0;
        }

        RequestAnimation();
    }

    public void Complete(TimeSpan myTime, TimeSpan standardTime, string resultText)
    {
        _completed = true;
        _myTime = myTime;
        _stdTime = standardTime;
        _resultText = resultText;
        _progress.Target = 1;
        RequestAnimation();
    }

    protected override bool OnAdvance(double deltaSeconds)
    {
        bool changed = _enter.Advance(deltaSeconds);
        changed |= _progress.Advance(deltaSeconds);

        if (_total > 0)
            _progress.Target = Math.Clamp(_processed / (double)_total, 0, 1);

        for (int i = _window.Count - 1; i >= 0; i--)
        {
            var item = _window[i];
            changed |= item.Presence.Advance(deltaSeconds);
            changed |= item.Slot.Advance(deltaSeconds);
            if (item.Presence.Target == 0 && item.Presence.Settled)
                _window.RemoveAt(i);
        }

        for (int i = _samples.Count - 1; i >= 0; i--)
        {
            var sample = _samples[i];
            changed |= sample.Presence.Advance(deltaSeconds);
            changed |= sample.Slot.Advance(deltaSeconds);
            if (sample.Presence.Target == 0 && sample.Presence.Settled)
                _samples.RemoveAt(i);
        }

        return changed;
    }

    private void SyncWindow(IReadOnlyList<string> items)
    {
        // The replay shows the newest items. When the queue grows fast the whole window is replaced at
        // once; dropping the old chips immediately keeps their text from overlapping the new chips while
        // both sets would otherwise fade in the same slots. The fade-in of the new set keeps it smooth.
        _window.RemoveAll(item => !items.Contains(item.Key));

        for (int i = 0; i < items.Count; i++)
        {
            string value = items[i];
            var item = _window.Find(v => v.Key == value);
            if (item == null)
            {
                item = new WindowItem(value);
                item.Presence.SnapTo(0);
                item.Slot.SnapTo(i);
                _window.Add(item);
            }

            item.TargetSlot = i;
            item.Slot.Target = i;
            item.Presence.Target = 1;
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Bounds.Width < 10 || Bounds.Height < 10)
            return;

        double w = Bounds.Width;
        double h = Bounds.Height;

        using (context.PushOpacity(Ease.Clamp01(_enter.Value)))
        {
            DrawHeader(context, w);

            if (!_started)
            {
                DrawIdleState(context, new Rect(14, 58, w - 28, h - 80));
                return;
            }

            double laneHeight = 122;
            double laneGap = 10;
            double top = 58;

            DrawLane(context, new Rect(14, top, w - 28, laneHeight),
                "MyQueue · свой связный список", _myTime, _stdTime,
                faster: IsMyFaster());

            DrawLane(context, new Rect(14, top + laneHeight + laneGap, w - 28, laneHeight),
                "StandardQueue · BCL", _stdTime, _myTime,
                faster: !IsMyFaster());

            double streamTop = top + 2 * (laneHeight + laneGap) + 2;
            double footerTop = h - 120;
            DrawStream(context, new Rect(14, streamTop, w - 28, Math.Max(60, footerTop - streamTop - 8)));
            DrawFooter(context, w, h);
        }
    }

    private static string FormatTime(TimeSpan elapsed) => elapsed.TotalMilliseconds < 0.1
        ? $"{elapsed.TotalMicroseconds:F1} мкс"
        : $"{elapsed.TotalMilliseconds:F2} мс";

    private bool IsMyFaster() =>
        _myTime.HasValue && _stdTime.HasValue && _myTime.Value <= _stdTime.Value;

    private void DrawHeader(DrawingContext context, double width)
    {
        var title = new FormattedText(
            "СРАВНЕНИЕ ОЧЕРЕДЕЙ",
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter, Segoe UI", FontStyle.Normal, FontWeight.SemiBold),
            12,
            MutedBrush);
        context.DrawText(title, new Point(14, 10));

        if (!_started)
            return;

        var subtitle = new FormattedText(
            $"{_total:N0} операций · {_mixLabel}",
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Cascadia Mono, Consolas", FontStyle.Normal, FontWeight.SemiBold),
            11.5,
            Palette.Brush(Palette.VizActive))
        {
            MaxTextWidth = Math.Max(120, width - 28)
        };
        context.DrawText(subtitle, new Point(14, 32));
    }

    private void DrawLane(DrawingContext context, Rect lane, string title, TimeSpan? time, TimeSpan? other, bool faster)
    {
        context.FillRectangle(PanelBrush, lane, 10);
        context.DrawRectangle(PanelPen, lane, 10);

        var titleText = new FormattedText(
            title,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter, Segoe UI", FontStyle.Normal, FontWeight.SemiBold),
            11,
            TextBrush);
        context.DrawText(titleText, new Point(lane.X + 12, lane.Y + 9));

        // Measured time badge appears when the replay is over.
        if (time.HasValue)
        {
            var timeText = new FormattedText(
                FormatTime(time.Value),
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Cascadia Mono, Consolas", FontStyle.Normal, FontWeight.Bold),
                12,
                faster ? DoneBrush : MutedBrush);
            context.DrawText(timeText, new Point(lane.Right - 12 - timeText.Width, lane.Y + 9));
        }

        double pipeLeft = lane.X + 12;
        double pipeRight = lane.Right - 12;
        double pipeTop = lane.Y + 30;
        double pipeBottom = lane.Y + lane.Height - 34;

        context.DrawLine(PanelPen, new Point(pipeLeft, pipeBottom), new Point(pipeRight, pipeBottom));

        if (_size == 0 && _window.Count == 0)
        {
            var empty = new FormattedText(
                "очередь пуста",
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Cascadia Mono, Consolas", FontStyle.Italic, FontWeight.Normal),
                12,
                MutedBrush);
            context.DrawText(empty, new Point(
                pipeLeft + (pipeRight - pipeLeft - empty.Width) / 2,
                pipeTop + (pipeBottom - pipeTop - empty.Height) / 2));
        }

        double itemHeight = Math.Min(42, pipeBottom - pipeTop);
        double itemY = pipeBottom - itemHeight;
        double gap = 8;
        double indent = _truncated ? 26 : 0;
        int visible = Math.Min(MaxWindowItems, _window.Count);
        double available = pipeRight - pipeLeft - indent - (visible - 1) * gap;
        double itemWidth = Math.Clamp(available / Math.Max(1, visible), 40, 96);

        if (_truncated)
        {
            var more = new FormattedText(
                "…",
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Cascadia Mono, Consolas", FontStyle.Normal, FontWeight.Bold),
                14,
                MutedBrush);
            context.DrawText(more, new Point(pipeLeft + 6, itemY + itemHeight / 2 - more.Height / 2));
        }

        foreach (var item in _window)
        {
            double presence = Ease.Clamp01(item.Presence.Value);
            if (presence <= 0.01)
                continue;

            double slot = item.Slot.Value;
            if (slot < -0.1 || slot > visible - 0.5)
                continue;

            double x = pipeLeft + indent + slot * (itemWidth + gap);
            double scale = 0.86 + 0.14 * Ease.OutCubic(presence);
            var rect = new Rect(x, itemY, itemWidth, itemHeight);
            var center = new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);

            bool isHead = item.TargetSlot == 0 && !_truncated;
            bool isTail = item.TargetSlot == _window.Count - 1;

            using (context.PushOpacity(presence))
            using (context.PushTransform(
                       Matrix.CreateTranslation(-center.X, -center.Y)
                       * Matrix.CreateScale(scale, scale)
                       * Matrix.CreateTranslation(center.X, center.Y)))
            {
                context.FillRectangle(isHead ? HeadBgBrush : ItemBgBrush, rect, 5);
                if (isHead)
                    context.DrawRectangle(HeadPen, rect, 5);
                else if (isTail)
                    context.DrawRectangle(TailPen, rect, 5);

                var text = new FormattedText(
                    item.Text,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Cascadia Mono, Consolas", FontStyle.Normal, isHead ? FontWeight.Bold : FontWeight.Normal),
                    Math.Min(12, itemHeight - 8),
                    TextBrush);
                context.DrawText(text, new Point(
                    rect.X + (rect.Width - text.Width) / 2,
                    rect.Y + (rect.Height - text.Height) / 2));
            }
        }

        var counters = new FormattedText(
            $"вставок {_enqueues:N0}   ·   извлечений {_dequeues:N0}   ·   размер {_size:N0}",
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Cascadia Mono, Consolas", FontStyle.Normal, FontWeight.Normal),
            10.5,
            MutedBrush);
        context.DrawText(counters, new Point(pipeLeft, lane.Bottom - 22));
    }

    private void DrawStream(DrawingContext context, Rect area)
    {
        var title = new FormattedText(
            "ПОТОК ОПЕРАЦИЙ (ОДИН И ТОТ ЖЕ ДЛЯ ОБЕИХ ОЧЕРЕДЕЙ)",
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter, Segoe UI", FontStyle.Normal, FontWeight.SemiBold),
            10.5,
            MutedBrush);
        context.DrawText(title, new Point(area.X, area.Y));

        double chipTop = area.Y + 20;
        double chipHeight = 26;
        double gap = 5;

        foreach (var sample in _samples)
        {
            double presence = Ease.Clamp01(sample.Presence.Value);
            if (presence <= 0.01)
                continue;

            double y = chipTop + sample.Slot.Value * (chipHeight + gap);
            if (y < chipTop - 1 || y > area.Bottom - chipHeight)
                continue;

            var rect = new Rect(area.X, y, area.Width, chipHeight);
            var accent = sample.IsEnqueue ? HeadBrush : TailBrush;

            using (context.PushOpacity(presence))
            {
                context.FillRectangle(ItemBgBrush, rect, 5);
                context.DrawRectangle(ChipPen, rect, 5);
                context.FillRectangle(accent, new Rect(rect.X, rect.Y + 4, 3, rect.Height - 8), 1.5f);

                var icon = new FormattedText(
                    sample.IsEnqueue ? "▲" : "▼",
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Inter, Segoe UI", FontStyle.Normal, FontWeight.Bold),
                    10,
                    accent);
                context.DrawText(icon, new Point(rect.X + 10, rect.Y + (rect.Height - icon.Height) / 2));

                var text = new FormattedText(
                    sample.Text,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Cascadia Mono, Consolas", FontStyle.Normal, FontWeight.Normal),
                    11,
                    TextBrush)
                {
                    MaxTextWidth = Math.Max(40, rect.Width - 44),
                    Trimming = TextTrimming.CharacterEllipsis
                };
                context.DrawText(text, new Point(rect.X + 28, rect.Y + (rect.Height - text.Height) / 2));
            }
        }
    }

    private void DrawFooter(DrawingContext context, double width, double height)
    {
        double left = 14;
        double right = width - 14;

        var done = new FormattedText(
            _processed.ToString("N0"),
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Cascadia Mono, Consolas", FontStyle.Normal, FontWeight.Bold),
            14,
            _completed ? DoneBrush : TextBrush);
        var total = new FormattedText(
            $" / {_total:N0} операций",
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Cascadia Mono, Consolas", FontStyle.Normal, FontWeight.Normal),
            12,
            MutedBrush);

        double countersY = height - 104;
        context.DrawText(done, new Point(left, countersY - 3));
        context.DrawText(total, new Point(left + done.Width, countersY));

        string status = _completed
            ? $"Готово · {_resultText}"
            : "Идёт сравнение: анимация ускорена, счётчики считают все операции.";

        var statusText = new FormattedText(
            status,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Cascadia Mono, Consolas", FontStyle.Normal, FontWeight.SemiBold),
            11.5,
            _completed ? DoneBrush : Palette.Brush(Palette.VizWrite))
        {
            MaxTextWidth = Math.Max(120, right - left)
        };

        // Progress bar.
        var track = new Rect(left, height - 76, right - left, 8);
        context.FillRectangle(Palette.Brush(Palette.SurfaceRaised), track, 4);
        double fill = Math.Clamp(_progress.Value, 0, 1) * track.Width;
        if (fill > 1)
        {
            var fillBrush = _completed ? DoneBrush : Palette.Brush(Palette.VizActive);
            context.FillRectangle(fillBrush, new Rect(track.X, track.Y, fill, track.Height), 4);
        }

        context.DrawText(statusText, new Point(left, height - 56));
    }

    private static void DrawIdleState(DrawingContext context, Rect area)
    {
        var title = new FormattedText(
            "СРАВНЕНИЕ ЕЩЁ НЕ ЗАПУСКАЛОСЬ",
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter, Segoe UI", FontStyle.Normal, FontWeight.SemiBold),
            13,
            MutedBrush);
        context.DrawText(title, new Point(14, area.Y + area.Height / 2 - 26));

        var hint = new FormattedText(
            "Выберите число операций и состав, затем нажмите «Запустить сравнение».",
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter, Segoe UI", FontStyle.Normal, FontWeight.Normal),
            12,
            MutedBrush)
        {
            MaxTextWidth = Math.Max(120, area.Width - 8)
        };
        context.DrawText(hint, new Point(14, area.Y + area.Height / 2));
    }

    private sealed class WindowItem
    {
        public WindowItem(string text)
        {
            Text = text;
            Key = text;
        }

        public string Key { get; }

        public string Text { get; }

        public int TargetSlot { get; set; }

        public SmoothValue Presence { get; } = new(0, 18);

        public SmoothValue Slot { get; } = new(0, 14);
    }

    private sealed class SampleVisual
    {
        public SampleVisual(string text, bool isEnqueue)
        {
            Text = text;
            IsEnqueue = isEnqueue;
        }

        public string Text { get; }

        public bool IsEnqueue { get; }

        public SmoothValue Presence { get; } = new(0, 18);

        public SmoothValue Slot { get; } = new(-0.6, 20);
    }
}
