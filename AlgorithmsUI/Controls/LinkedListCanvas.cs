using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AlgorithmsUI.Theme;

namespace AlgorithmsUI.Controls;

public sealed class LinkedListCanvas : Control
{
    private static readonly IBrush NodeBgBrush = Palette.Brush(Palette.SurfaceRaised);
    private static readonly IBrush PointerBgBrush = Palette.Brush(Palette.Surface);
    private static readonly IBrush TextBrush = Palette.Brush(Palette.Foreground);
    private static readonly IBrush MutedTextBrush = Palette.Brush(Palette.MutedForeground);
    private static readonly IBrush HeadBrush = Palette.Brush(Palette.VizActive);
    private static readonly IBrush TailBrush = Palette.Brush(Palette.VizWrite);
    private static readonly IBrush ArrowBrush = Palette.Brush(Palette.VizActive);
    private static readonly IBrush NullBrush = Palette.Brush(Palette.MutedForeground);
    private static readonly IPen NodeBorderPen = new Pen(Palette.Brush(Palette.Border), 1.5);
    private static readonly IPen ArrowPen = new Pen(Palette.Brush(Palette.VizActive), 2);
    private static readonly IPen HighlightPen = new Pen(Palette.Brush(Palette.VizCompare), 2);

    private IReadOnlyList<string> _primaryList = Array.Empty<string>();
    private IReadOnlyList<string>? _secondaryList;
    private string? _statusAction;
    private int _highlightIndex1 = -1;
    private int _highlightIndex2 = -1;

    public IReadOnlyList<string> PrimaryList
    {
        get => _primaryList;
        set
        {
            _primaryList = value ?? Array.Empty<string>();
            InvalidateVisual();
        }
    }

    public IReadOnlyList<string>? SecondaryList
    {
        get => _secondaryList;
        set
        {
            _secondaryList = value;
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

    public void SetHighlights(int idx1, int idx2)
    {
        _highlightIndex1 = idx1;
        _highlightIndex2 = idx2;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Bounds.Width < 10 || Bounds.Height < 10) return;

        double w = Bounds.Width;
        double h = Bounds.Height;

        if (_secondaryList != null && _secondaryList.Count > 0)
        {
            // Draw two lists: List 1 in upper half, List 2 in lower half
            DrawSingleList(context, _primaryList, "СПИСОК 1 (ДО РАЗДЕЛЕНИЯ):", 50, w);
            DrawSingleList(context, _secondaryList, "СПИСОК 2 (ПОСЛЕ РАЗДЕЛЕНИЯ):", h / 2 + 30, w);
        }
        else
        {
            // Draw single list centered
            double startY = Math.Max(70, h / 2 - 40);
            DrawSingleList(context, _primaryList, "СВЯЗНЫЙ СПИСОК (HEAD ──► TAIL):", startY, w);
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
            context.DrawText(actionText, new Point((w - actionText.Width) / 2, h - 30));
        }
    }

    private void DrawSingleList(DrawingContext context, IReadOnlyList<string> list, string title, double startY, double width)
    {
        var titleText = new FormattedText(
            title,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter, Segoe UI", FontStyle.Normal, FontWeight.SemiBold),
            12,
            MutedTextBrush);
        context.DrawText(titleText, new Point(30, startY - 30));

        if (list.Count == 0)
        {
            var emptyText = new FormattedText(
                "null (список пуст)",
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Cascadia Mono, Consolas", FontStyle.Italic, FontWeight.Normal),
                13,
                MutedTextBrush);
            context.DrawText(emptyText, new Point(30, startY + 15));
            return;
        }

        double nodeWidth = 76;
        double nodeHeight = 44;
        double arrowLength = 36;
        double stepX = nodeWidth + arrowLength;
        double paddingLeft = 30;

        // Auto-wrap rows if nodes exceed canvas width
        int nodesPerRow = Math.Max(1, (int)((width - paddingLeft - 80) / stepX));

        for (int i = 0; i < list.Count; i++)
        {
            int row = i / nodesPerRow;
            int col = i % nodesPerRow;

            double x = paddingLeft + col * stepX;
            double y = startY + row * (nodeHeight + 50);

            bool isHead = (i == 0);
            bool isTail = (i == list.Count - 1);
            bool isHighlighted = (i == _highlightIndex1 || i == _highlightIndex2);

            // Node container: split into data (left 52px) and pointer (right 24px)
            var nodeRect = new Rect(x, y, nodeWidth, nodeHeight);
            context.FillRectangle(NodeBgBrush, nodeRect, 6);
            context.DrawRectangle(isHighlighted ? HighlightPen : NodeBorderPen, nodeRect, 6);

            // Pointer divider
            double dividerX = x + 52;
            context.DrawLine(NodeBorderPen, new Point(dividerX, y), new Point(dividerX, y + nodeHeight));

            // Data text
            string val = list[i];
            var valText = new FormattedText(
                val,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Cascadia Mono, Consolas", FontStyle.Normal, FontWeight.Bold),
                13,
                TextBrush);
            context.DrawText(valText, new Point(x + (52 - valText.Width) / 2, y + (nodeHeight - valText.Height) / 2));

            // Pointer dot
            double dotCenterX = dividerX + 12;
            double dotCenterY = y + nodeHeight / 2;
            context.FillRectangle(ArrowBrush, new Rect(dotCenterX - 3, dotCenterY - 3, 6, 6), 3);

            // Arrow to next node or null
            double arrowEndX = x + nodeWidth + arrowLength;
            if (i < list.Count - 1)
            {
                if (col < nodesPerRow - 1)
                {
                    // Regular right arrow
                    DrawArrow(context, new Point(dotCenterX, dotCenterY), new Point(arrowEndX, dotCenterY));
                }
                else
                {
                    // Wrap-around arrow to next line
                    DrawWrapArrow(context, new Point(dotCenterX, dotCenterY), new Point(paddingLeft, y + nodeHeight + 50 + nodeHeight / 2));
                }
            }
            else
            {
                // Last node points to null
                DrawArrow(context, new Point(dotCenterX, dotCenterY), new Point(x + nodeWidth + 24, dotCenterY));
                var nullText = new FormattedText(
                    "null",
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Cascadia Mono, Consolas", FontStyle.Italic, FontWeight.SemiBold),
                    12,
                    NullBrush);
                context.DrawText(nullText, new Point(x + nodeWidth + 28, dotCenterY - nullText.Height / 2));
            }

            // Head and Tail tags
            if (isHead)
            {
                var headTag = new FormattedText(
                    "HEAD",
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Inter, Segoe UI", FontStyle.Normal, FontWeight.Bold),
                    10,
                    HeadBrush);
                context.DrawText(headTag, new Point(x + (52 - headTag.Width) / 2, y - 18));
            }
            if (isTail)
            {
                var tailTag = new FormattedText(
                    "TAIL",
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Inter, Segoe UI", FontStyle.Normal, FontWeight.Bold),
                    10,
                    TailBrush);
                context.DrawText(tailTag, new Point(x + (52 - tailTag.Width) / 2, y + nodeHeight + 4));
            }
        }
    }

    private static void DrawArrow(DrawingContext context, Point from, Point to)
    {
        context.DrawLine(ArrowPen, from, to);
        // Arrow head
        var headGeo = new StreamGeometry();
        using (var ctx = headGeo.Open())
        {
            ctx.BeginFigure(new Point(to.X - 6, to.Y - 4), true);
            ctx.LineTo(to);
            ctx.LineTo(new Point(to.X - 6, to.Y + 4));
        }
        context.DrawGeometry(ArrowBrush, ArrowPen, headGeo);
    }

    private static void DrawWrapArrow(DrawingContext context, Point from, Point to)
    {
        // Simple elbow connector to next row
        double right = from.X + 16;
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(from, false);
            ctx.LineTo(new Point(right, from.Y));
            ctx.LineTo(new Point(right, to.Y));
            ctx.LineTo(to);
        }
        context.DrawGeometry(null, ArrowPen, geo);
    }
}
