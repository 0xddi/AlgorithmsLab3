using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AlgorithmsUI.Controls.Animation;
using AlgorithmsUI.Theme;

namespace AlgorithmsUI.Controls;

public sealed class StackCanvas : AnimatedControl
{
    private static readonly IBrush BeakerBrush = Palette.Brush(Palette.BorderStrong);
    private static readonly IBrush ItemBgBrush = Palette.Brush(Palette.SurfaceRaised);
    private static readonly IBrush TopBgBrush = Palette.Brush(Palette.Accent);
    private static readonly IBrush TextBrush = Palette.Brush(Palette.Foreground);
    private static readonly IBrush MutedTextBrush = Palette.Brush(Palette.MutedForeground);
    private static readonly IBrush ArrowBrush = Palette.Brush(Palette.VizActive);
    private static readonly IPen BeakerPen = new Pen(BeakerBrush, 3);
    private static readonly IPen TopPen = new Pen(Palette.Brush(Palette.VizActive), 2);

    private readonly List<ItemVisual> _visuals = new();
    private IReadOnlyList<string> _items = Array.Empty<string>();
    private string? _statusAction;
    private string? _shownStatus;

    private readonly SmoothValue _emptyOpacity = new(0, 12);
    private readonly SmoothValue _statusOpacity = new(1, 12);
    private readonly SmoothValue _statusShift = new(0, 12);
    private double _time;

    public StackCanvas()
    {
        _emptyOpacity.SnapTo(1);
    }

    public IReadOnlyList<string> Items
    {
        get => _items;
        set
        {
            _items = value ?? Array.Empty<string>();
            _emptyOpacity.Target = _items.Count == 0 ? 1 : 0;
            SyncVisuals();
            RequestAnimation();
        }
    }

    public string? StatusAction
    {
        get => _statusAction;
        set
        {
            _statusAction = value;
            if (string.IsNullOrEmpty(value))
            {
                _statusOpacity.Target = 0;
            }
            else if (_shownStatus != value)
            {
                _shownStatus = value;
                _statusOpacity.SnapTo(0);
                _statusShift.SnapTo(8);
                _statusOpacity.Target = 1;
                _statusShift.Target = 0;
            }

            RequestAnimation();
        }
    }

    protected override bool OnAdvance(double deltaSeconds)
    {
        _time += deltaSeconds;
        bool changed = false;

        changed |= _emptyOpacity.Advance(deltaSeconds);
        changed |= _statusOpacity.Advance(deltaSeconds);
        changed |= _statusShift.Advance(deltaSeconds);

        for (int i = _visuals.Count - 1; i >= 0; i--)
        {
            var visual = _visuals[i];
            changed |= visual.Presence.Advance(deltaSeconds);
            changed |= visual.Slot.Advance(deltaSeconds);

            if (visual.Presence.Target == 0 && visual.Presence.Settled)
                _visuals.RemoveAt(i);
        }

        return changed;
    }

    private void SyncVisuals()
    {
        int count = _items.Count;
        var occurrences = new Dictionary<string, int>();
        var alive = new HashSet<ItemVisual>();

        for (int i = 0; i < count; i++)
        {
            string value = _items[i];
            occurrences.TryGetValue(value, out int seen);
            occurrences[value] = seen + 1;
            string key = $"{value}#{seen}";

            var visual = _visuals.Find(v => v.Key == key);
            if (visual == null)
            {
                visual = new ItemVisual(key, value);
                // New items enter through the top of the beaker (push semantics) and fade in.
                visual.Presence.SnapTo(0);
                visual.Slot.SnapTo(i - 1);
                _visuals.Add(visual);
            }
            else
            {
                visual.Text = value;
            }

            visual.TargetSlot = i;
            visual.Slot.Target = i;
            visual.Presence.Target = 1;
            alive.Add(visual);
        }

        foreach (var visual in _visuals)
        {
            if (alive.Contains(visual))
                continue;

            visual.Presence.Target = 0;
            visual.Slot.Target = visual.Slot.Value;
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Bounds.Width < 10 || Bounds.Height < 10)
            return;

        double w = Bounds.Width;
        double h = Bounds.Height;

        // Beaker geometry.
        double beakerWidth = Math.Min(320, w * 0.55);
        double left = (w - beakerWidth) / 2;
        double right = left + beakerWidth;
        double bottom = h - 40;
        double top = 50;
        double beakerHeight = bottom - top;

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(left, top), false);
            ctx.LineTo(new Point(left, bottom));
            ctx.LineTo(new Point(right, bottom));
            ctx.LineTo(new Point(right, top));
        }

        context.DrawGeometry(null, BeakerPen, geometry);

        var header = new FormattedText(
            "ВЕРШИНА СТЕКА",
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter, Segoe UI", FontStyle.Normal, FontWeight.SemiBold),
            12,
            MutedTextBrush);
        context.DrawText(header, new Point((w - header.Width) / 2, top - 24));

        int count = _items.Count;
        if (_emptyOpacity.Value > 0.01)
        {
            var emptyText = new FormattedText(
                "[ СТЕК ПУСТ ]",
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Inter, Segoe UI", FontStyle.Normal, FontWeight.Medium),
                14,
                MutedTextBrush);

            using (context.PushOpacity(_emptyOpacity.Value))
                context.DrawText(emptyText, new Point((w - emptyText.Width) / 2, (bottom + top) / 2));
        }

        if (_visuals.Count == 0)
        {
            DrawStatus(context, w, bottom);
            return;
        }

        double itemPadding = 4;
        double availableHeight = beakerHeight - 20;
        double itemHeight = Math.Min(38, Math.Max(20, (availableHeight / Math.Max(1, count)) - itemPadding));
        int maxVisible = Math.Max(1, (int)(availableHeight / (itemHeight + itemPadding)));
        int visibleCount = Math.Min(Math.Max(count, 0), maxVisible);

        foreach (var visual in _visuals.OrderByDescending(v => v.Slot.Value))
        {
            double presence = Ease.Clamp01(visual.Presence.Value);
            if (presence <= 0.01)
                continue;

            double slot = visual.Slot.Value;
            if (slot < -1.2 || slot > visibleCount)
                continue;

            double itemBottom = bottom - 8 - (visibleCount - 1 - slot) * (itemHeight + itemPadding);
            double itemTop = itemBottom - itemHeight;
            double itemLeft = left + 8;
            double itemWidth = beakerWidth - 16;

            bool isTop = visual.TargetSlot == 0;
            double scale = 0.84 + 0.16 * Ease.OutCubic(presence);
            var center = new Point(itemLeft + itemWidth / 2, itemTop + itemHeight / 2);

            using (context.PushOpacity(presence))
            using (context.PushTransform(
                       Matrix.CreateTranslation(-center.X, -center.Y)
                       * Matrix.CreateScale(scale, scale)
                       * Matrix.CreateTranslation(center.X, center.Y)))
            {
                var rect = new Rect(itemLeft, itemTop, itemWidth, itemHeight);
                context.FillRectangle(isTop ? TopBgBrush : ItemBgBrush, rect, 6);

                if (isTop)
                {
                    context.DrawRectangle(TopPen, rect, 6);

                    // The TOP marker breathes slowly so the eye finds the working end of the stack.
                    double glow = 0.55 + 0.45 * Ease.Pulse(_time, 0.9);
                    var topLabel = new FormattedText(
                        "◄── ВЕРШИНА",
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        new Typeface("Inter, Segoe UI", FontStyle.Normal, FontWeight.Bold),
                        12,
                        ArrowBrush);

                    using (context.PushOpacity(glow))
                        context.DrawText(topLabel, new Point(right + 12, itemTop + (itemHeight - topLabel.Height) / 2));
                }

                var valueText = new FormattedText(
                    visual.Text,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Cascadia Mono, Consolas", FontStyle.Normal, isTop ? FontWeight.Bold : FontWeight.Normal),
                    Math.Min(14, itemHeight - 4),
                    TextBrush);
                context.DrawText(valueText, new Point(
                    itemLeft + (itemWidth - valueText.Width) / 2,
                    itemTop + (itemHeight - valueText.Height) / 2));
            }
        }

        if (count > visibleCount)
        {
            var moreText = new FormattedText(
                $"... и еще {count - visibleCount} элементов ниже ...",
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Inter, Segoe UI", FontStyle.Normal, FontWeight.Normal),
                11,
                MutedTextBrush);
            context.DrawText(moreText, new Point((w - moreText.Width) / 2, bottom - 4));
        }

        DrawStatus(context, w, bottom);
    }

    private void DrawStatus(DrawingContext context, double width, double bottom)
    {
        if (string.IsNullOrEmpty(_statusAction) || _statusOpacity.Value <= 0.01)
            return;

        var actionText = new FormattedText(
            _statusAction!,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Cascadia Mono, Consolas", FontStyle.Normal, FontWeight.SemiBold),
            12,
            Palette.Brush(Palette.VizWrite))
        {
            MaxTextWidth = Math.Max(80, width - 40)
        };

        using (context.PushOpacity(_statusOpacity.Value))
        using (context.PushTransform(Matrix.CreateTranslation(0, _statusShift.Value)))
            context.DrawText(actionText, new Point((width - actionText.Width) / 2, bottom + 12));
    }

    private sealed class ItemVisual
    {
        public ItemVisual(string key, string text)
        {
            Key = key;
            Text = text;
        }

        public string Key { get; }

        public string Text { get; set; }

        public int TargetSlot { get; set; } = -1;

        public SmoothValue Presence { get; } = new(0, 18);

        public SmoothValue Slot { get; } = new(0, 12);
    }
}
