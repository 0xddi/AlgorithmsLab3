using System.Globalization;
using Avalonia;
using Avalonia.Media;
using AlgorithmsUI.Controls.Animation;
using AlgorithmsUI.Theme;

namespace AlgorithmsUI.Controls;

/// <summary>
/// FIFO pipe. Items slide along the pipe instead of jumping: a new item enters from the right (enqueue),
/// a removed item leaves to the left (dequeue) and everything between glides to its new slot.
/// </summary>
public sealed class QueueCanvas : AnimatedControl
{
    private static readonly IBrush TubeBrush = Palette.Brush(Palette.BorderStrong);
    private static readonly IBrush ItemBgBrush = Palette.Brush(Palette.SurfaceRaised);
    private static readonly IBrush HeadBgBrush = Palette.Brush(Palette.Accent);
    private static readonly IBrush TextBrush = Palette.Brush(Palette.Foreground);
    private static readonly IBrush MutedTextBrush = Palette.Brush(Palette.MutedForeground);
    private static readonly IBrush ArrowBrush = Palette.Brush(Palette.VizActive);
    private static readonly IPen TubePen = new Pen(TubeBrush, 3);
    private static readonly IPen HeadPen = new Pen(Palette.Brush(Palette.VizActive), 2);
    private static readonly IPen TailPen = new Pen(Palette.Brush(Palette.VizWrite), 2);

    private readonly List<ItemVisual> _visuals = new();
    private readonly FadeText _caption = new();
    private readonly SmoothValue _itemWidth = new(72, 10);
    private IReadOnlyList<string> _items = Array.Empty<string>();
    private double _time;

    public IReadOnlyList<string> Items
    {
        get => _items;
        set
        {
            _items = value ?? Array.Empty<string>();
            SyncVisuals();
            RequestAnimation();
        }
    }

    public string? StatusAction
    {
        get => _caption.Text;
        set
        {
            if (_caption.Update(value))
                RequestAnimation();
        }
    }

    protected override bool OnAdvance(double deltaSeconds)
    {
        _time += deltaSeconds;
        bool changed = _caption.Advance(deltaSeconds);
        changed |= _itemWidth.Advance(deltaSeconds);

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
        var occurrences = new Dictionary<string, int>();
        var alive = new HashSet<ItemVisual>();

        for (int i = 0; i < _items.Count; i++)
        {
            string value = _items[i];
            occurrences.TryGetValue(value, out int seen);
            occurrences[value] = seen + 1;
            string key = $"{value}#{seen}";

            var visual = _visuals.Find(v => v.Key == key);
            if (visual == null)
            {
                visual = new ItemVisual(key, value);
                // New items arrive from the tail side (right) and fade in.
                visual.Presence.SnapTo(0);
                visual.Slot.SnapTo(i + 1);
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

            // Removed from the head: drift further left while fading, so the queue really flows.
            visual.Presence.Target = 0;
            if (visual.Slot.Target > -2)
                visual.Slot.Target -= 1;
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Bounds.Width < 10 || Bounds.Height < 10)
            return;

        double w = Bounds.Width;
        double h = Bounds.Height;

        double centerY = h / 2;
        double tubeHeight = 80;
        double tubeTop = centerY - tubeHeight / 2;
        double tubeBottom = centerY + tubeHeight / 2;
        double marginX = 80;
        double tubeLeft = marginX;
        double tubeRight = w - marginX;

        context.DrawLine(TubePen, new Point(tubeLeft, tubeTop), new Point(tubeRight, tubeTop));
        context.DrawLine(TubePen, new Point(tubeLeft, tubeBottom), new Point(tubeRight, tubeBottom));

        var headLabel = new FormattedText(
            "◄── ИЗВЛЕЧЕНИЕ (ГОЛОВА)",
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter, Segoe UI", FontStyle.Normal, FontWeight.SemiBold),
            12,
            ArrowBrush);
        context.DrawText(headLabel, new Point(tubeLeft - 10, tubeTop - 24));

        var tailLabel = new FormattedText(
            "ВСТАВКА (ХВОСТ) ──►",
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter, Segoe UI", FontStyle.Normal, FontWeight.SemiBold),
            12,
            Palette.Brush(Palette.VizWrite));
        context.DrawText(tailLabel, new Point(tubeRight - tailLabel.Width + 10, tubeTop - 24));

        int count = _items.Count;

        if (count == 0 && _visuals.Count == 0)
        {
            var emptyText = new FormattedText(
                "[ ОЧЕРЕДЬ ПУСТА ]",
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Inter, Segoe UI", FontStyle.Normal, FontWeight.Medium),
                14,
                MutedTextBrush);
            context.DrawText(emptyText, new Point((w - emptyText.Width) / 2, centerY - emptyText.Height / 2));
            _caption.Draw(context, w / 2, tubeBottom + 45, Palette.Brush(Palette.VizDone), 12, Math.Max(120, w - 40));
            return;
        }

        double availableWidth = (tubeRight - tubeLeft) - 20;
        double itemPadding = 8;
        SetTarget(_itemWidth, Math.Min(90, Math.Max(40, availableWidth / Math.Max(1, count) - itemPadding)));

        double itemWidth = _itemWidth.Value;
        double itemHeight = tubeHeight - 16;
        double itemY = tubeTop + 8;

        foreach (var visual in _visuals.OrderBy(v => v.Slot.Value))
        {
            double presence = Ease.Clamp01(visual.Presence.Value);
            if (presence <= 0.01)
                continue;

            double itemX = tubeLeft + 10 + visual.Slot.Value * (itemWidth + itemPadding);
            if (itemX > tubeRight + itemWidth || itemX < tubeLeft - itemWidth * 2)
                continue;

            bool isHead = visual.TargetSlot == 0 && visual.Presence.Target == 1;
            bool isTail = visual.TargetSlot == count - 1 && visual.Presence.Target == 1;
            double scale = 0.86 + 0.14 * Ease.OutCubic(presence);
            var center = new Point(itemX + itemWidth / 2, itemY + itemHeight / 2);

            using (context.PushOpacity(presence))
            using (context.PushTransform(
                       Matrix.CreateTranslation(-center.X, -center.Y)
                       * Matrix.CreateScale(scale, scale)
                       * Matrix.CreateTranslation(center.X, center.Y)))
            {
                var rect = new Rect(itemX, itemY, itemWidth, itemHeight);
                context.FillRectangle(isHead ? HeadBgBrush : ItemBgBrush, rect, 6);

                if (isHead)
                {
                    context.DrawRectangle(HeadPen, rect, 6);
                }
                else if (isTail)
                {
                    context.DrawRectangle(TailPen, rect, 6);
                }

                var valueText = new FormattedText(
                    visual.Text,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Cascadia Mono, Consolas", FontStyle.Normal, isHead ? FontWeight.Bold : FontWeight.Normal),
                    Math.Min(14, itemHeight / 2),
                    TextBrush);
                context.DrawText(valueText, new Point(
                    itemX + (itemWidth - valueText.Width) / 2,
                    itemY + (itemHeight - valueText.Height) / 2));

                var tag = new FormattedText(
                    isHead ? "ГОЛОВА" : isTail ? "ХВОСТ" : $"#{visual.TargetSlot}",
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Inter, Segoe UI", FontStyle.Normal, FontWeight.SemiBold),
                    10,
                    isHead ? ArrowBrush : isTail ? Palette.Brush(Palette.VizWrite) : MutedTextBrush);

                using (context.PushOpacity(0.6 + 0.4 * Ease.Pulse(_time, 0.8)))
                    context.DrawText(tag, new Point(itemX + (itemWidth - tag.Width) / 2, tubeBottom + 6));
            }
        }

        _caption.Draw(context, w / 2, tubeBottom + 45, Palette.Brush(Palette.VizDone), 12, Math.Max(120, w - 40));
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

        public int TargetSlot { get; set; }

        public SmoothValue Presence { get; } = new(0, 18);

        public SmoothValue Slot { get; } = new(0, 11);
    }
}
