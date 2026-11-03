using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AlgorithmsUI.Theme;

namespace AlgorithmsUI.Controls;

public sealed class StackCanvas : Control
{
    private static readonly IBrush BeakerBrush = Palette.Brush(Palette.BorderStrong);
    private static readonly IBrush ItemBgBrush = Palette.Brush(Palette.SurfaceRaised);
    private static readonly IBrush TopBgBrush = Palette.Brush(Palette.Accent);
    private static readonly IBrush TextBrush = Palette.Brush(Palette.Foreground);
    private static readonly IBrush MutedTextBrush = Palette.Brush(Palette.MutedForeground);
    private static readonly IBrush ArrowBrush = Palette.Brush(Palette.VizActive);
    private static readonly IPen BeakerPen = new Pen(BeakerBrush, 3);
    private static readonly IPen TopPen = new Pen(Palette.Brush(Palette.VizActive), 2);

    private IReadOnlyList<string> _items = Array.Empty<string>();
    private string? _statusAction;

    public IReadOnlyList<string> Items
    {
        get => _items;
        set
        {
            _items = value ?? Array.Empty<string>();
            InvalidateVisual();
        }
    }

    public string? StatusAction
    {
        get => _statusAction;
        set
        {
            _statusAction = value;
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Bounds.Width < 10 || Bounds.Height < 10) return;

        double w = Bounds.Width;
        double h = Bounds.Height;

        // Container dimensions
        double beakerWidth = Math.Min(320, w * 0.55);
        double left = (w - beakerWidth) / 2;
        double right = left + beakerWidth;
        double bottom = h - 40;
        double top = 50;
        double beakerHeight = bottom - top;

        // Draw beaker walls (left wall, bottom, right wall)
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(left, top), false);
            ctx.LineTo(new Point(left, bottom));
            ctx.LineTo(new Point(right, bottom));
            ctx.LineTo(new Point(right, top));
        }
        context.DrawGeometry(null, BeakerPen, geometry);

        // Header text: "СТЕК (LIFO)"
        var header = new FormattedText(
            "ВЕРШИНА СТЕКА (TOP)",
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter, Segoe UI", FontStyle.Normal, FontWeight.SemiBold),
            12,
            MutedTextBrush);
        context.DrawText(header, new Point((w - header.Width) / 2, top - 24));

        if (_items.Count == 0)
        {
            var emptyText = new FormattedText(
                "[ СТЕК ПУСТ ]",
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Inter, Segoe UI", FontStyle.Normal, FontWeight.Medium),
                14,
                MutedTextBrush);
            context.DrawText(emptyText, new Point((w - emptyText.Width) / 2, (bottom + top) / 2));
            return;
        }

        // In our Stack: _items[0] is TOP, _items[1] is next, _items[last] is BOTTOM
        // Items stacked vertically: bottom item rests at bottom - padding, TOP item is highest
        int count = _items.Count;
        double itemPadding = 4;
        double availableHeight = beakerHeight - 20;
        double itemHeight = Math.Min(38, Math.Max(20, (availableHeight / count) - itemPadding));

        // Limit drawn items if too many to avoid overflow
        int maxVisible = (int)(availableHeight / (itemHeight + itemPadding));
        int visibleCount = Math.Min(count, maxVisible);

        for (int i = 0; i < visibleCount; i++)
        {
            // _items[i] is at depth i from top
            // reverse index from bottom: bottom is index (count - 1 - i)
            // wait: items in Stack.ToList() are ordered head (top) to tail (bottom).
            // So _items[0] is top, _items[visibleCount - 1] is bottom
            int itemIndexFromTop = i;
            string value = _items[itemIndexFromTop];

            // Y coordinate from bottom
            double itemBottom = bottom - 8 - (visibleCount - 1 - i) * (itemHeight + itemPadding);
            double itemTop = itemBottom - itemHeight;
            double itemLeft = left + 8;
            double itemWidth = beakerWidth - 16;

            bool isTop = (i == 0);
            var rect = new Rect(itemLeft, itemTop, itemWidth, itemHeight);
            var brush = isTop ? TopBgBrush : ItemBgBrush;
            context.FillRectangle(brush, rect, 6);
            if (isTop)
            {
                context.DrawRectangle(TopPen, rect, 6);

                // Draw TOP pointer arrow on right
                var topLabel = new FormattedText(
                    "◄── TOP",
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Inter, Segoe UI", FontStyle.Normal, FontWeight.Bold),
                    12,
                    ArrowBrush);
                context.DrawText(topLabel, new Point(right + 12, itemTop + (itemHeight - topLabel.Height) / 2));
            }

            // Draw item value text
            var valText = new FormattedText(
                value,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Cascadia Mono, Consolas", FontStyle.Normal, isTop ? FontWeight.Bold : FontWeight.Normal),
                Math.Min(14, itemHeight - 4),
                TextBrush);
            context.DrawText(valText, new Point(itemLeft + (itemWidth - valText.Width) / 2, itemTop + (itemHeight - valText.Height) / 2));
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

        // Draw action tag if present
        if (!string.IsNullOrEmpty(_statusAction))
        {
            var actionText = new FormattedText(
                _statusAction,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Cascadia Mono, Consolas", FontStyle.Normal, FontWeight.SemiBold),
                12,
                Palette.Brush(Palette.VizWrite));
            context.DrawText(actionText, new Point((w - actionText.Width) / 2, bottom + 12));
        }
    }
}
