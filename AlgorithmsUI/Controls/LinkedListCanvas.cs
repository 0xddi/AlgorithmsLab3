using System.Globalization;
using Avalonia;
using Avalonia.Media;
using AlgorithmsUI.Controls.Animation;
using AlgorithmsUI.Theme;

namespace AlgorithmsUI.Controls;

/// <summary>
/// Linked list. Nodes keep their identity by value, so an operation that shifts elements (remove, insert,
/// reverse) is drawn as nodes gliding to their new cells: appearing nodes pop in, vanishing ones shrink away.
/// </summary>
public sealed class LinkedListCanvas : AnimatedControl
{
    private static readonly IBrush NodeBgBrush = Palette.Brush(Palette.SurfaceRaised);
    private static readonly IBrush TextBrush = Palette.Brush(Palette.Foreground);
    private static readonly IBrush MutedTextBrush = Palette.Brush(Palette.MutedForeground);
    private static readonly IBrush HeadBrush = Palette.Brush(Palette.VizActive);
    private static readonly IBrush TailBrush = Palette.Brush(Palette.VizWrite);
    private static readonly IBrush ArrowBrush = Palette.Brush(Palette.VizActive);
    private static readonly IBrush NullBrush = Palette.Brush(Palette.MutedForeground);
    private static readonly IPen NodeBorderPen = new Pen(Palette.Brush(Palette.Border), 1.5);
    private static readonly IPen ArrowPen = new Pen(Palette.Brush(Palette.VizActive), 2);
    private static readonly IPen HighlightPen = new Pen(Palette.Brush(Palette.VizCompare), 2);

    private readonly List<NodeVisual> _primary = new();
    private readonly List<NodeVisual> _secondary = new();
    private readonly FadeText _caption = new();
    private readonly SmoothValue _highlight = new(0, 12);
    private readonly SmoothValue _arrowOpacity = new(1, 22);
    private IReadOnlyList<string> _primaryList = Array.Empty<string>();
    private IReadOnlyList<string>? _secondaryList;
    private int _highlightIndex1 = -1;
    private int _highlightIndex2 = -1;
    private double _time;

    public IReadOnlyList<string> PrimaryList
    {
        get => _primaryList;
        set
        {
            _primaryList = value ?? Array.Empty<string>();
            Sync(_primary, _primaryList, "p");
            RequestAnimation();
        }
    }

    public IReadOnlyList<string>? SecondaryList
    {
        get => _secondaryList;
        set
        {
            _secondaryList = value;
            Sync(_secondary, _secondaryList ?? Array.Empty<string>(), "s", visible: value != null);
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

    public void SetHighlights(int idx1, int idx2)
    {
        _highlightIndex1 = idx1;
        _highlightIndex2 = idx2;
        SetTarget(_highlight, idx1 >= 0 || idx2 >= 0 ? 1 : 0);
    }

    protected override bool OnAdvance(double deltaSeconds)
    {
        _time += deltaSeconds;
        bool changed = _caption.Advance(deltaSeconds);
        changed |= _highlight.Advance(deltaSeconds);

        bool nodesMoving = AnyNodeMoving(_primary) || AnyNodeMoving(_secondary);
        SetTarget(_arrowOpacity, nodesMoving ? 0 : 1);
        changed |= _arrowOpacity.Advance(deltaSeconds);

        changed |= AdvanceList(_primary, deltaSeconds);
        changed |= AdvanceList(_secondary, deltaSeconds);
        return changed;
    }

    private static bool AnyNodeMoving(List<NodeVisual> visuals)
    {
        foreach (var visual in visuals)
        {
            if (Math.Abs(visual.X.Target - visual.X.Value) > 0.75 ||
                Math.Abs(visual.Y.Target - visual.Y.Value) > 0.75 ||
                Math.Abs(visual.Presence.Target - visual.Presence.Value) > 0.05)
                return true;
        }

        return false;
    }

    private static bool AdvanceList(List<NodeVisual> visuals, double deltaSeconds)
    {
        bool changed = false;
        for (int i = visuals.Count - 1; i >= 0; i--)
        {
            var visual = visuals[i];
            changed |= visual.Presence.Advance(deltaSeconds);
            changed |= visual.X.Advance(deltaSeconds);
            changed |= visual.Y.Advance(deltaSeconds);

            if (visual.Presence.Target == 0 && visual.Presence.Settled)
                visuals.RemoveAt(i);
        }

        return changed;
    }

    private void Sync(List<NodeVisual> visuals, IReadOnlyList<string> source, string prefix, bool visible = true)
    {
        var occurrences = new Dictionary<string, int>();
        var alive = new HashSet<NodeVisual>();

        for (int i = 0; i < source.Count; i++)
        {
            string value = source[i];
            occurrences.TryGetValue(value, out int seen);
            occurrences[value] = seen + 1;
            string key = $"{prefix}:{value}#{seen}";

            var visual = visuals.Find(v => v.Key == key);
            if (visual == null)
            {
                visual = new NodeVisual(key, value);
                visual.Presence.SnapTo(0);
                visual.X.SnapTo(0);
                visual.Y.SnapTo(0);
                visuals.Add(visual);
            }
            else
            {
                visual.Text = value;
            }

            visual.Index = i;
            visual.Presence.Target = visible ? 1 : 0;
            alive.Add(visual);
        }

        foreach (var visual in visuals)
        {
            if (alive.Contains(visual))
                continue;

            visual.Presence.Target = 0;
            visual.X.Target = visual.X.Value;
            visual.Y.Target = visual.Y.Value;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double width = double.IsInfinity(availableSize.Width) ? 800 : availableSize.Width;
        double height = MeasureHeight(width);
        return new Size(availableSize.Width, double.IsInfinity(availableSize.Height) ? height : Math.Max(availableSize.Height, height));
    }

    private double MeasureHeight(double width)
    {
        const double nodeWidth = 76;
        const double arrowLength = 34;
        const double stepX = nodeWidth + arrowLength;
        const double paddingLeft = 44;
        const double rowPitch = 44 + 58;

        int nodesPerRow = Math.Max(1, (int)((width - paddingLeft - 60) / stepX));
        int rows1 = Math.Max(1, (_primaryList.Count + nodesPerRow - 1) / nodesPerRow);
        double height1 = 60 + (rows1 - 1) * rowPitch + 44 + 50;

        if (_secondaryList is { Count: > 0 })
        {
            int rows2 = Math.Max(1, (_secondaryList.Count + nodesPerRow - 1) / nodesPerRow);
            double height2 = 40 + (rows2 - 1) * rowPitch + 44 + 50;
            return height1 + height2;
        }

        return height1;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Bounds.Width < 10 || Bounds.Height < 10)
            return;

        double w = Bounds.Width;
        double h = Bounds.Height;

        const double nodeWidth = 76;
        const double nodeHeight = 44;
        const double arrowLength = 34;
        const double stepX = nodeWidth + arrowLength;
        const double paddingLeft = 44;
        const double rowGap = 58;
        const double rowPitch = nodeHeight + rowGap;

        int nodesPerRow = Math.Max(1, (int)((w - paddingLeft - 60) / stepX));
        bool split = _secondaryList != null && _secondaryList.Count > 0;

        double primaryStartY;
        if (split)
        {
            int rows1 = Math.Max(1, (_primaryList.Count + nodesPerRow - 1) / nodesPerRow);
            double h1 = (rows1 - 1) * rowPitch + nodeHeight;
            primaryStartY = 45;
            double secondaryStartY = Math.Max(primaryStartY + h1 + 65, h / 2 + 20);
            LayoutNodes(_primary, primaryStartY, nodesPerRow, stepX, paddingLeft, rowPitch, nodeWidth);
            LayoutNodes(_secondary, secondaryStartY, nodesPerRow, stepX, paddingLeft, rowPitch, nodeWidth);
        }
        else
        {
            int rowCount = Math.Max(1, (_primaryList.Count + nodesPerRow - 1) / nodesPerRow);
            double totalListHeight = (rowCount - 1) * rowPitch + nodeHeight;
            double bottomMargin = string.IsNullOrEmpty(_caption.Text) ? 24 : 44;
            primaryStartY = rowCount <= 1
                ? Math.Max(50, (h - totalListHeight) / 2)
                : Math.Max(50, (h - totalListHeight - bottomMargin) / 2);
            LayoutNodes(_primary, primaryStartY, nodesPerRow, stepX, paddingLeft, rowPitch, nodeWidth);
            LayoutNodes(_secondary, primaryStartY, nodesPerRow, stepX, paddingLeft, rowPitch, nodeWidth);
        }

        DrawList(context, _primary, split ? "СПИСОК 1 (ДО РАЗДЕЛЕНИЯ):" : "СВЯЗНЫЙ СПИСОК (ГОЛОВА ──► ХВОСТ):",
            primaryStartY, nodeWidth, nodeHeight, isSecondary: false);

        if (split)
        {
            int rows1 = Math.Max(1, (_primaryList.Count + nodesPerRow - 1) / nodesPerRow);
            double h1 = (rows1 - 1) * rowPitch + nodeHeight;
            double secondaryStartY = Math.Max(primaryStartY + h1 + 65, h / 2 + 20);
            DrawList(context, _secondary, "СПИСОК 2 (ПОСЛЕ РАЗДЕЛЕНИЯ):",
                secondaryStartY, nodeWidth, nodeHeight, isSecondary: true);
        }

        _caption.Draw(context, w / 2, h - 26, Palette.Brush(Palette.VizDone), 12, Math.Max(120, w - 40));
    }

    private void LayoutNodes(
        List<NodeVisual> visuals,
        double startY,
        int nodesPerRow,
        double stepX,
        double paddingLeft,
        double rowPitch,
        double nodeWidth)
    {
        foreach (var visual in visuals)
        {
            if (visual.Presence.Target == 0)
            {
                // Vanishing node stays where it was last seen.
                visual.X.Target = visual.X.Value;
                visual.Y.Target = visual.Y.Value;
                continue;
            }

            int row = visual.Index / nodesPerRow;
            int col = visual.Index % nodesPerRow;
            double targetX = paddingLeft + col * stepX;
            double targetY = startY + row * rowPitch;

            if (!visual.Placed)
            {
                // A brand new node appears in its own cell (with a scale/fade pop) instead of flying
                // across the canvas from the origin.
                visual.X.SnapTo(targetX);
                visual.Y.SnapTo(targetY);
                visual.Placed = true;
            }
            else
            {
                SetTarget(visual.X, targetX);
                SetTarget(visual.Y, targetY);
            }
        }
    }

    private void DrawList(
        DrawingContext context,
        List<NodeVisual> visuals,
        string title,
        double startY,
        double nodeWidth,
        double nodeHeight,
        bool isSecondary)
    {
        var titleText = new FormattedText(
            title,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter, Segoe UI", FontStyle.Normal, FontWeight.SemiBold),
            12,
            MutedTextBrush);
        context.DrawText(titleText, new Point(30, Math.Max(6, startY - 44)));

        var visible = visuals
            .Where(v => v.Presence.Value > 0.01)
            .OrderBy(v => v.Index)
            .ToList();

        if (visible.Count == 0)
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

        for (int i = 0; i < visible.Count; i++)
        {
            var visual = visible[i];
            bool isHead = visual.Index == 0 && visual.Presence.Target == 1;
            bool isTail = visual.Index == (isSecondary ? _secondaryList?.Count ?? 0 : _primaryList.Count) - 1 &&
                          visual.Presence.Target == 1;
            bool highlighted = _highlight.Value > 0.02 &&
                               visual.Presence.Target == 1 &&
                               (visual.Index == _highlightIndex1 || visual.Index == _highlightIndex2);

            bool hasNext = i < visible.Count - 1;
            var next = hasNext ? visible[i + 1] : null;

            DrawNode(context, visual, nodeWidth, nodeHeight, isHead, isTail, highlighted);

            // Arrows are hidden while the nodes travel and fade back in once the row has settled, so a
            // re-flow never turns into a web of lines crossing the canvas.
            if (_arrowOpacity.Value <= 0.01)
                continue;

            using (context.PushOpacity(Ease.Clamp01(_arrowOpacity.Value)))
            {
                if (next != null)
                {
                    DrawArrow(context,
                        new Point(visual.X.Value + nodeWidth - 12, visual.Y.Value + nodeHeight / 2),
                        new Point(next.X.Value, next.Y.Value + nodeHeight / 2),
                        visual.X.Value + nodeWidth,
                        visual.Y.Value + nodeHeight,
                        nodeHeight);
                }
                else
                {
                    DrawArrow(context,
                        new Point(visual.X.Value + nodeWidth - 12, visual.Y.Value + nodeHeight / 2),
                        new Point(visual.X.Value + nodeWidth + 24, visual.Y.Value + nodeHeight / 2),
                        visual.X.Value + nodeWidth,
                        visual.Y.Value + nodeHeight,
                        nodeHeight);

                    var nullText = new FormattedText(
                        "null",
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        new Typeface("Cascadia Mono, Consolas", FontStyle.Italic, FontWeight.SemiBold),
                        12,
                        NullBrush);
                    context.DrawText(nullText, new Point(
                        visual.X.Value + nodeWidth + 28,
                        visual.Y.Value + nodeHeight / 2 - nullText.Height / 2));
                }
            }
        }
    }

    private void DrawNode(
        DrawingContext context,
        NodeVisual visual,
        double nodeWidth,
        double nodeHeight,
        bool isHead,
        bool isTail,
        bool highlighted)
    {
        double presence = Ease.Clamp01(visual.Presence.Value);
        double x = visual.X.Value;
        double y = visual.Y.Value;
        double scale = 0.8 + 0.2 * Ease.OutBack(presence);
        var center = new Point(x + nodeWidth / 2, y + nodeHeight / 2);

        using (context.PushOpacity(presence))
        using (context.PushTransform(
                   Matrix.CreateTranslation(-center.X, -center.Y)
                   * Matrix.CreateScale(scale, scale)
                   * Matrix.CreateTranslation(center.X, center.Y)))
        {
            var nodeRect = new Rect(x, y, nodeWidth, nodeHeight);
            context.FillRectangle(NodeBgBrush, nodeRect, 6);

            if (highlighted)
            {
                double pulse = 0.6 + 0.4 * Ease.Pulse(_time, 1.1);
                using (context.PushOpacity(pulse))
                    context.DrawRectangle(HighlightPen, nodeRect, 6);
            }
            else
            {
                context.DrawRectangle(NodeBorderPen, nodeRect, 6);
            }

            double dividerX = x + 52;
            context.DrawLine(NodeBorderPen, new Point(dividerX, y), new Point(dividerX, y + nodeHeight));

            var valueText = new FormattedText(
                visual.Text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Cascadia Mono, Consolas", FontStyle.Normal, FontWeight.Bold),
                13,
                TextBrush);
            context.DrawText(valueText, new Point(x + (52 - valueText.Width) / 2, y + (nodeHeight - valueText.Height) / 2));

            double dotCenterX = dividerX + 12;
            double dotCenterY = y + nodeHeight / 2;
            context.FillRectangle(ArrowBrush, new Rect(dotCenterX - 3, dotCenterY - 3, 6, 6), 3);

            if (isHead)
            {
                var headTag = new FormattedText(
                    "ГОЛОВА",
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
                    "ХВОСТ",
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Inter, Segoe UI", FontStyle.Normal, FontWeight.Bold),
                    10,
                    TailBrush);
                context.DrawText(tailTag, new Point(x + (52 - tailTag.Width) / 2, y + nodeHeight + 4));
            }
        }
    }

    /// <summary>Straight arrow when the next node is to the right, an S-curve down to the next row when the list wraps.</summary>
    private static void DrawArrow(DrawingContext context, Point from, Point to, double nodeRight, double rowBottom, double nodeHeight)
    {
        if (Math.Abs(to.Y - from.Y) < 8)
        {
            context.DrawLine(ArrowPen, from, to);
            DrawArrowHead(context, to);
            return;
        }

        // Down out of the last node of the row, left through the gap, then into the first node of the next row.
        double exitX = nodeRight + 16;
        double channelY = (rowBottom + (to.Y - nodeHeight / 2)) / 2;
        double turnX = to.X - 22;

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(from, false);
            ctx.LineTo(new Point(exitX, from.Y));
            ctx.LineTo(new Point(exitX, channelY));
            ctx.LineTo(new Point(turnX, channelY));
            ctx.LineTo(new Point(turnX, to.Y));
            ctx.LineTo(to);
        }

        context.DrawGeometry(null, ArrowPen, geometry);
        DrawArrowHead(context, to);
    }

    private static void DrawArrowHead(DrawingContext context, Point tip)
    {
        var head = new StreamGeometry();
        using (var ctx = head.Open())
        {
            ctx.BeginFigure(new Point(tip.X - 7, tip.Y - 4.5), true);
            ctx.LineTo(tip);
            ctx.LineTo(new Point(tip.X - 7, tip.Y + 4.5));
        }

        context.DrawGeometry(ArrowBrush, ArrowPen, head);
    }

    private sealed class NodeVisual
    {
        public NodeVisual(string key, string text)
        {
            Key = key;
            Text = text;
        }

        public string Key { get; }

        public string Text { get; set; }

        public int Index { get; set; }

        public bool Placed { get; set; }

        public SmoothValue Presence { get; } = new(0, 16);

        public SmoothValue X { get; } = new(0, 12);

        public SmoothValue Y { get; } = new(0, 12);
    }
}
