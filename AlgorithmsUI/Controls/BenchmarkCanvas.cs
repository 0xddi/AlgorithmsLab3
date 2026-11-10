using System.Globalization;
using Avalonia;
using Avalonia.Media;
using AlgorithmsUI.Controls.Animation;
using AlgorithmsUI.Theme;

namespace AlgorithmsUI.Controls;

/// <summary>
/// Animated view of the performance benchmark. The measurement itself runs in one tight loop (that is what
/// makes the numbers meaningful); this canvas then replays the same operation mix in compressed time so the
/// pushes and pops are actually visible: the stack on the left, a stream of sampled operations on the right
/// and live counters underneath.
/// </summary>
public sealed class BenchmarkCanvas : AnimatedControl
{
    private const int MaxVisibleItems = 12;
    private const int MaxSamples = 10;

    private static readonly IBrush BeakerBrush = Palette.Brush(Palette.BorderStrong);
    private static readonly IBrush ItemBgBrush = Palette.Brush(Palette.SurfaceRaised);
    private static readonly IBrush TopBgBrush = Palette.Brush(Palette.Accent);
    private static readonly IBrush TextBrush = Palette.Brush(Palette.Foreground);
    private static readonly IBrush MutedBrush = Palette.Brush(Palette.MutedForeground);
    private static readonly IBrush PushBrush = Palette.Brush(Palette.VizActive);
    private static readonly IBrush PopBrush = Palette.Brush(Palette.VizWrite);
    private static readonly IBrush DoneBrush = Palette.Brush(Palette.VizDone);
    private static readonly IPen BeakerPen = new Pen(BeakerBrush, 3);
    private static readonly IPen TopPen = new Pen(Palette.Brush(Palette.VizActive), 2);
    private static readonly IPen ChipPen = new Pen(Palette.Brush(Palette.Border), 1);

    private readonly List<ItemVisual> _items = new();
    private readonly List<SampleVisual> _samples = new();
    private readonly SmoothValue _progress = new(0, 14);
    private readonly SmoothValue _enter = new(0, 9);

    private int _total;
    private string _mixLabel = "";
    private int _processed;
    private int _pushes;
    private int _pops;
    private int _size;
    private int _visibleItems;
    private bool _started;
    private bool _completed;
    private string _resultText = "";

    public BenchmarkCanvas()
    {
        _enter.SnapTo(1);
    }


    public void Begin(int total, string mixLabel)
    {
        _total = total;
        _mixLabel = mixLabel;
        _processed = _pushes = _pops = _size = 0;
        _started = true;
        _completed = false;
        _resultText = "";
        _items.Clear();
        _samples.Clear();
        _progress.SnapTo(0);
        _enter.SnapTo(0);
        _enter.Target = 1;
        RequestAnimation();
    }

    /// <summary>Live totals; called every frame so the counters and the progress bar tick smoothly.</summary>
    public void UpdateCounters(int processed, int pushes, int pops)
    {
        _processed = processed;
        _pushes = pushes;
        _pops = pops;
        RequestAnimation();
    }

    /// <summary>Snapshot of the stack for the beaker; called a few times per second.</summary>
    public void ShowStack(int size, IReadOnlyList<string> topItems)
    {
        _size = size;
        _visibleItems = Math.Min(MaxVisibleItems, topItems.Count);
        SyncItems(topItems);
        RequestAnimation();
    }

    /// <summary>Adds a sampled operation to the stream on the right; the oldest chips fade out.</summary>
    public void AddSample(bool isPush, string text)
    {
        var sample = new SampleVisual(text, isPush);
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

    public void Complete(string resultText)
    {
        _completed = true;
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

        for (int i = _items.Count - 1; i >= 0; i--)
        {
            var item = _items[i];
            changed |= item.Presence.Advance(deltaSeconds);
            changed |= item.Slot.Advance(deltaSeconds);
            if (item.Presence.Target == 0 && item.Presence.Settled)
                _items.RemoveAt(i);
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

    private void SyncItems(IReadOnlyList<string> topItems)
    {
        var alive = new HashSet<string>();
        for (int i = 0; i < topItems.Count && i < MaxVisibleItems; i++)
        {
            string value = topItems[i];
            alive.Add(value);

            var item = _items.Find(v => v.Key == value);
            if (item == null)
            {
                item = new ItemVisual(value);
                item.Presence.SnapTo(0);
                // The replay is fast: chips fade/scale in place at their slot instead of flying across
                // the beaker, otherwise a chip that lives one snapshot would never be visible.
                item.Slot.SnapTo(i);
                _items.Add(item);
            }

            item.TargetSlot = i;
            item.Slot.Target = i;
            item.Presence.Target = 1;
        }

        foreach (var item in _items)
        {
            if (alive.Contains(item.Key))
                continue;

            item.Presence.Target = 0;
            item.Slot.Target = item.Slot.Value;
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

            double top = 58;
            double bottom = Math.Max(top + 140, h - 132);
            double beakerWidth = Math.Min(250, w * 0.4);
            double beakerLeft = 14;
            var beaker = new Rect(beakerLeft, top, beakerWidth, bottom - top);

            double tapeLeft = beaker.Right + 18;
            var tape = new Rect(tapeLeft, top, Math.Max(120, w - tapeLeft - 14), bottom - top);

            DrawBeaker(context, beaker);
            DrawTape(context, tape);
            DrawFooter(context, w, h);
        }
    }

    private void DrawHeader(DrawingContext context, double width)
    {
        var title = new FormattedText(
            "АНИМИРОВАННЫЙ ЗАМЕР СКОРОСТИ",
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
            12,
            Palette.Brush(Palette.VizActive))
        {
            MaxTextWidth = Math.Max(120, width - 28)
        };
        context.DrawText(subtitle, new Point(14, 32));
    }

    private void DrawBeaker(DrawingContext context, Rect area)
    {
        if (!_started)
            return;

        double innerWidth = Math.Min(area.Width - 40, 210);
        double left = area.X + (area.Width - innerWidth) / 2;
        double right = left + innerWidth;
        double top = area.Y + 30;
        double bottom = area.Bottom;

        var label = new FormattedText(
            "СТЕК",
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter, Segoe UI", FontStyle.Normal, FontWeight.SemiBold),
            11,
            MutedBrush);
        context.DrawText(label, new Point(left, area.Y));

        string sizeLabel = _size > MaxVisibleItems
            ? $"размер: {_size:N0} (топ-{MaxVisibleItems})"
            : $"размер: {_size:N0}";

        var sizeText = new FormattedText(
            sizeLabel,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Cascadia Mono, Consolas", FontStyle.Normal, FontWeight.Normal),
            11,
            MutedBrush);
        context.DrawText(sizeText, new Point(right - sizeText.Width, area.Y));

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(left, top), false);
            ctx.LineTo(new Point(left, bottom));
            ctx.LineTo(new Point(right, bottom));
            ctx.LineTo(new Point(right, top));
        }

        context.DrawGeometry(null, BeakerPen, geometry);

        if (_items.Count == 0)
        {
            var empty = new FormattedText(
                "стек пуст",
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Cascadia Mono, Consolas", FontStyle.Italic, FontWeight.Normal),
                12,
                MutedBrush);
            context.DrawText(empty, new Point((left + right - empty.Width) / 2, (top + bottom) / 2));
            return;
        }

        int visible = Math.Max(1, _visibleItems);
        double available = bottom - top - 16;
        double itemHeight = Math.Min(30, Math.Max(16, available / Math.Max(1, visible) - 4));
        double padding = 4;
        double itemWidth = innerWidth - 12;

        foreach (var item in _items.OrderByDescending(v => v.Slot.Value))
        {
            double presence = Ease.Clamp01(item.Presence.Value);
            if (presence <= 0.01)
                continue;

            // Items that no longer exist in the stack keep their last slot while fading; they are not
            // squeezed back into the visible range, which used to draw two chips on top of each other.
            double slot = item.Slot.Value;
            if (slot < -0.05 || slot > visible - 0.5)
                continue;

            double itemBottom = bottom - 6 - (visible - 1 - slot) * (itemHeight + padding);
            double itemTop = itemBottom - itemHeight;
            if (itemTop < top - 4)
                continue;

            bool isTop = item.TargetSlot == 0;
            var rect = new Rect(left + 6, itemTop, itemWidth, itemHeight);
            var center = new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
            double scale = 0.82 + 0.18 * Ease.OutCubic(presence);

            using (context.PushOpacity(presence))
            using (context.PushTransform(
                       Matrix.CreateTranslation(-center.X, -center.Y)
                       * Matrix.CreateScale(scale, scale)
                       * Matrix.CreateTranslation(center.X, center.Y)))
            {
                context.FillRectangle(isTop ? TopBgBrush : ItemBgBrush, rect, 5);
                if (isTop)
                    context.DrawRectangle(TopPen, rect, 5);

                var text = new FormattedText(
                    item.Text,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Cascadia Mono, Consolas", FontStyle.Normal, isTop ? FontWeight.Bold : FontWeight.Normal),
                    Math.Min(12, itemHeight - 6),
                    TextBrush);
                context.DrawText(text, new Point(
                    rect.X + (rect.Width - text.Width) / 2,
                    rect.Y + (rect.Height - text.Height) / 2));
            }
        }
    }

    private void DrawTape(DrawingContext context, Rect area)
    {
        var title = new FormattedText(
            "ПОТОК ОПЕРАЦИЙ",
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter, Segoe UI", FontStyle.Normal, FontWeight.SemiBold),
            11,
            MutedBrush);
        context.DrawText(title, new Point(area.X, area.Y));

        var hint = new FormattedText(
            "последние",
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter, Segoe UI", FontStyle.Normal, FontWeight.Normal),
            10,
            MutedBrush);
        context.DrawText(hint, new Point(area.Right - hint.Width, area.Y));

        double chipTop = area.Y + 22;
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
            var accent = sample.IsPush ? PushBrush : PopBrush;

            using (context.PushOpacity(presence))
            {
                context.FillRectangle(ItemBgBrush, rect, 5);
                context.DrawRectangle(ChipPen, rect, 5);

                // A small coloured bar on the left tells push from pop at a glance.
                context.FillRectangle(accent, new Rect(rect.X, rect.Y + 4, 3, rect.Height - 8), 1.5f);

                var icon = new FormattedText(
                    sample.IsPush ? "▲" : "▼",
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

    private static void DrawIdleState(DrawingContext context, Rect area)
    {
        var title = new FormattedText(
            "ЗАМЕР ЕЩЁ НЕ ЗАПУСКАЛСЯ",
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter, Segoe UI", FontStyle.Normal, FontWeight.SemiBold),
            13,
            MutedBrush);
        context.DrawText(title, new Point(area.X, area.Y + area.Height / 2 - 26));

        var hint = new FormattedText(
            "Выберите число операций и состав, затем нажмите «Запустить тест скорости».",
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter, Segoe UI", FontStyle.Normal, FontWeight.Normal),
            12,
            MutedBrush)
        {
            MaxTextWidth = Math.Max(120, area.Width - 8)
        };
        context.DrawText(hint, new Point(area.X, area.Y + area.Height / 2));
    }

    private void DrawFooter(DrawingContext context, double width, double height)
    {
        if (!_started)
            return;

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

        double countersY = height - 118;
        context.DrawText(done, new Point(left, countersY - 3));
        context.DrawText(total, new Point(left + done.Width, countersY));

        var mix = new FormattedText(
            $"push {_pushes:N0}   ·   pop {_pops:N0}",
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Cascadia Mono, Consolas", FontStyle.Normal, FontWeight.Normal),
            12,
            MutedBrush);
        context.DrawText(mix, new Point(right - mix.Width, countersY));

        // Progress bar.
        var track = new Rect(left, height - 92, right - left, 8);
        context.FillRectangle(Palette.Brush(Palette.SurfaceRaised), track, 4);

        double fill = Math.Clamp(_progress.Value, 0, 1) * track.Width;
        if (fill > 1)
        {
            var fillBrush = _completed ? DoneBrush : Palette.Brush(Palette.VizActive);
            context.FillRectangle(fillBrush, new Rect(track.X, track.Y, fill, track.Height), 4);
        }

        string status = _completed
            ? $"Готово · {_resultText}"
            : "Идёт замер: анимация ускорена, счётчики считают все операции.";
        var statusText = new FormattedText(
            status,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Cascadia Mono, Consolas", FontStyle.Normal, FontWeight.SemiBold),
            12,
            _completed ? DoneBrush : Palette.Brush(Palette.VizWrite))
        {
            MaxTextWidth = Math.Max(120, right - left)
        };
        context.DrawText(statusText, new Point(left, height - 72));

        var hintText = new FormattedText(
            "Бегущая строка справа показывает выборку операций из общего потока.",
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter, Segoe UI", FontStyle.Normal, FontWeight.Normal),
            11,
            MutedBrush)
        {
            MaxTextWidth = Math.Max(120, right - left)
        };
        context.DrawText(hintText, new Point(left, height - 50));
    }

    private sealed class ItemVisual
    {
        public ItemVisual(string text)
        {
            Text = text;
            Key = text;
        }

        public string Key { get; }

        public string Text { get; }

        public int TargetSlot { get; set; }

        public SmoothValue Presence { get; } = new(0, 20);

        public SmoothValue Slot { get; } = new(0, 16);
    }

    private sealed class SampleVisual
    {
        public SampleVisual(string text, bool isPush)
        {
            Text = text;
            IsPush = isPush;
        }

        public string Text { get; }

        public bool IsPush { get; }

        public SmoothValue Presence { get; } = new(0, 18);

        public SmoothValue Slot { get; } = new(-0.6, 20);
    }
}
