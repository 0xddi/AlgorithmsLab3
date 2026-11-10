using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AlgorithmsUI.Theme;

namespace AlgorithmsUI.Controls;

public sealed class QueueCanvas : Control
{
    private static readonly IBrush TubeBrush = Palette.Brush(Palette.BorderStrong);
    private static readonly IBrush ItemBgBrush = Palette.Brush(Palette.SurfaceRaised);
    private static readonly IBrush HeadBgBrush = Palette.Brush(Palette.Accent);
    private static readonly IBrush TailBgBrush = Palette.Brush(Palette.VizIdleSurface);
    private static readonly IBrush TextBrush = Palette.Brush(Palette.Foreground);
    private static readonly IBrush MutedTextBrush = Palette.Brush(Palette.MutedForeground);
    private static readonly IBrush ArrowBrush = Palette.Brush(Palette.VizActive);
    private static readonly IPen TubePen = new Pen(TubeBrush, 3);
    private static readonly IPen HeadPen = new Pen(Palette.Brush(Palette.VizActive), 2);
    private static readonly IPen TailPen = new Pen(Palette.Brush(Palette.VizWrite), 2);

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

        double centerY = h / 2;
        double tubeHeight = 80;
        double tubeTop = centerY - tubeHeight / 2;
        double tubeBottom = centerY + tubeHeight / 2;
        double marginX = 80;
        double tubeLeft = marginX;
        double tubeRight = w - marginX;

        // Draw top and bottom rails of the pipe
        context.DrawLine(TubePen, new Point(tubeLeft, tubeTop), new Point(tubeRight, tubeTop));
        context.DrawLine(TubePen, new Point(tubeLeft, tubeBottom), new Point(tubeRight, tubeBottom));

        // Draw Flow Indicators:
        // Left: "◄── ИЗВЛЕЧЕНИЕ (DEQUEUE / HEAD)"
        var headLabel = new FormattedText(
            "◄── DEQUEUE (ГОЛОВА)",
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter, Segoe UI", FontStyle.Normal, FontWeight.SemiBold),
            12,
            ArrowBrush);
        context.DrawText(headLabel, new Point(tubeLeft - 10, tubeTop - 24));

        // Right: "ДОБАВЛЕНИЕ (ENQUEUE / TAIL) ──►"
        var tailLabel = new FormattedText(
            "ВСТАВКА (ХВОСТ) ──►",
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter, Segoe UI", FontStyle.Normal, FontWeight.SemiBold),
            12,
            Palette.Brush(Palette.VizWrite));
        context.DrawText(tailLabel, new Point(tubeRight - tailLabel.Width + 10, tubeTop - 24));

        if (_items.Count == 0)
        {
            var emptyText = new FormattedText(
                "[ ОЧЕРЕДЬ ПУСТА ]",
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Inter, Segoe UI", FontStyle.Normal, FontWeight.Medium),
                14,
                MutedTextBrush);
            context.DrawText(emptyText, new Point((w - emptyText.Width) / 2, centerY - emptyText.Height / 2));
            return;
        }

        // Items in MyQueue.ToList() are in order: head is [0], tail is [count - 1]
        // Leftmost item in pipe is head, rightmost item is tail!
        int count = _items.Count;
        double availableWidth = (tubeRight - tubeLeft) - 20;
        double itemPadding = 8;
        double itemWidth = Math.Min(90, Math.Max(40, (availableWidth / count) - itemPadding));
        double itemHeight = tubeHeight - 16;
        double itemY = tubeTop + 8;

        int maxVisible = (int)(availableWidth / (itemWidth + itemPadding));
        int visibleCount = Math.Min(count, maxVisible);

        for (int i = 0; i < visibleCount; i++)
        {
            string value = _items[i];
            double itemX = tubeLeft + 10 + i * (itemWidth + itemPadding);
            var rect = new Rect(itemX, itemY, itemWidth, itemHeight);

            bool isHead = (i == 0);
            bool isTail = (i == count - 1);

            var bg = isHead ? HeadBgBrush : ItemBgBrush;
            context.FillRectangle(bg, rect, 6);

            if (isHead)
                context.DrawRectangle(HeadPen, rect, 6);
            else if (isTail)
                context.DrawRectangle(TailPen, rect, 6);

            // Item value
            var valText = new FormattedText(
                value,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Cascadia Mono, Consolas", FontStyle.Normal, isHead ? FontWeight.Bold : FontWeight.Normal),
                Math.Min(14, itemHeight / 2),
                TextBrush);
            context.DrawText(valText, new Point(itemX + (itemWidth - valText.Width) / 2, itemY + (itemHeight - valText.Height) / 2));

            // Small index underneath
            var idxText = new FormattedText(
                isHead ? "HEAD" : (isTail ? "TAIL" : $"#{i}"),
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Inter, Segoe UI", FontStyle.Normal, FontWeight.SemiBold),
                10,
                isHead ? ArrowBrush : (isTail ? Palette.Brush(Palette.VizWrite) : MutedTextBrush));
            context.DrawText(idxText, new Point(itemX + (itemWidth - idxText.Width) / 2, tubeBottom + 6));
        }

        if (count > visibleCount)
        {
            var moreText = new FormattedText(
                $"... и еще {count - visibleCount} элементов ...",
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Inter, Segoe UI", FontStyle.Normal, FontWeight.Normal),
                11,
                MutedTextBrush);
            context.DrawText(moreText, new Point((w - moreText.Width) / 2, tubeBottom + 26));
        }

        if (!string.IsNullOrEmpty(_statusAction))
        {
            var actionText = new FormattedText(
                _statusAction,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Cascadia Mono, Consolas", FontStyle.Normal, FontWeight.SemiBold),
                12,
                Palette.Brush(Palette.VizDone));
            context.DrawText(actionText, new Point((w - actionText.Width) / 2, tubeBottom + 45));
        }
    }
}
