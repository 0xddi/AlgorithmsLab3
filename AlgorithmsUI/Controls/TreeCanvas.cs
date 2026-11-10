using System.Globalization;
using Avalonia;
using Avalonia.Media;
using AlgorithmsCore.Applications;
using AlgorithmsUI.Controls.Animation;
using AlgorithmsUI.Theme;

namespace AlgorithmsUI.Controls;

/// <summary>
/// Expression tree. A freshly built tree grows in level by level: parents fade in first, then each deeper
/// level, and the edges stretch towards their child while it appears. The tree is read as an expression
/// being assembled, not as a picture that suddenly replaces the previous one.
/// </summary>
public sealed class TreeCanvas : AnimatedControl
{
    private static readonly IBrush OpBrush = Palette.Brush(Palette.Accent);
    private static readonly IBrush LeafBrush = Palette.Brush(Palette.SurfaceRaised);
    private static readonly IBrush TextBrush = Palette.Brush(Palette.Foreground);
    private static readonly IBrush MutedTextBrush = Palette.Brush(Palette.MutedForeground);
    private static readonly IPen LinePen = new Pen(Palette.Brush(Palette.BorderStrong), 2);
    private static readonly IPen NodeBorderPen = new Pen(Palette.Brush(Palette.Border), 1.5);

    private const double RevealSeconds = 0.72;
    private ExpressionTree? _tree;
    private double _reveal = 1;
    private int _maxDepth;

    public ExpressionTree? Tree
    {
        get => _tree;
        set
        {
            if (ReferenceEquals(_tree, value))
                return;

            _tree = value;
            _reveal = 0;
            RequestAnimation();
        }
    }

    protected override bool OnAdvance(double deltaSeconds)
    {
        if (_reveal >= 1)
            return false;

        _reveal = Math.Min(1, _reveal + deltaSeconds / RevealSeconds);
        return true;
    }

    private sealed class LayoutNode
    {
        public ExpressionTree.Node Source = null!;
        public double X;
        public double Y;
        public double Width;
        public int Depth;
        public LayoutNode? Left;
        public LayoutNode? Right;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Bounds.Width < 10 || Bounds.Height < 10)
            return;

        double w = Bounds.Width;
        double h = Bounds.Height;

        if (_tree == null || _tree.Root == null)
        {
            var emptyText = new FormattedText(
                "[ ДЕРЕВО ВЫРАЖЕНИЯ НЕ ПОСТРОЕНО ]",
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Inter, Segoe UI", FontStyle.Normal, FontWeight.Medium),
                14,
                MutedTextBrush);
            context.DrawText(emptyText, new Point((w - emptyText.Width) / 2, h / 2 - emptyText.Height / 2));
            return;
        }

        _maxDepth = 0;
        var layoutRoot = BuildLayout(_tree.Root, 0);
        double treeWidth = layoutRoot.Width;
        double startX = Math.Max(20, (w - treeWidth) / 2);

        AssignCoordinates(layoutRoot, startX, 40, 60);

        DrawConnections(context, layoutRoot);
        DrawNodes(context, layoutRoot);
    }

    private LayoutNode BuildLayout(ExpressionTree.Node node, int depth)
    {
        var layout = new LayoutNode { Source = node, Depth = depth };
        double minNodeWidth = 46;
        _maxDepth = Math.Max(_maxDepth, depth);

        if (node.Left != null)
            layout.Left = BuildLayout(node.Left, depth + 1);

        if (node.Right != null)
            layout.Right = BuildLayout(node.Right, depth + 1);

        double childrenWidth = 0;
        if (layout.Left != null && layout.Right != null)
            childrenWidth = layout.Left.Width + layout.Right.Width + 16;
        else if (layout.Left != null)
            childrenWidth = layout.Left.Width + 16;
        else if (layout.Right != null)
            childrenWidth = layout.Right.Width + 16;

        layout.Width = Math.Max(minNodeWidth, childrenWidth);
        return layout;
    }

    private void AssignCoordinates(LayoutNode node, double x, double y, double levelHeight)
    {
        node.Y = y;

        if (node.Left != null && node.Right != null)
        {
            node.Left.X = x + node.Left.Width / 2;
            AssignCoordinates(node.Left, x, y + levelHeight, levelHeight);

            double rightX = x + node.Left.Width + 16;
            node.Right.X = rightX + node.Right.Width / 2;
            AssignCoordinates(node.Right, rightX, y + levelHeight, levelHeight);

            node.X = (node.Left.X + node.Right.X) / 2;
        }
        else if (node.Left != null)
        {
            node.Left.X = x + node.Left.Width / 2;
            AssignCoordinates(node.Left, x, y + levelHeight, levelHeight);
            node.X = node.Left.X;
        }
        else if (node.Right != null)
        {
            node.Right.X = x + node.Right.Width / 2;
            AssignCoordinates(node.Right, x, y + levelHeight, levelHeight);
            node.X = node.Right.X;
        }
        else
        {
            node.X = x + node.Width / 2;
        }
    }

    /// <summary>0..1 local reveal for a node at <paramref name="depth"/>: deeper levels start later.</summary>
    private double LocalProgress(int depth)
    {
        if (_reveal >= 1)
            return 1;

        double stagger = Math.Min(0.42, 0.09 * depth);
        return Ease.Clamp01((_reveal - stagger) / 0.58);
    }

    private void DrawConnections(DrawingContext context, LayoutNode node)
    {
        DrawConnection(context, node, node.Left);
        DrawConnection(context, node, node.Right);

        if (node.Left != null)
            DrawConnections(context, node.Left);

        if (node.Right != null)
            DrawConnections(context, node.Right);
    }

    private void DrawConnection(DrawingContext context, LayoutNode parent, LayoutNode? child)
    {
        if (child == null)
            return;

        double progress = LocalProgress(child.Depth);
        if (progress <= 0.01)
            return;

        var from = new Point(parent.X, parent.Y);
        var to = new Point(
            Ease.Lerp(parent.X, child.X, Ease.OutCubic(progress)),
            Ease.Lerp(parent.Y, child.Y, Ease.OutCubic(progress)));

        using (context.PushOpacity(Ease.Clamp01(progress * 1.4)))
            context.DrawLine(LinePen, from, to);
    }

    private void DrawNodes(DrawingContext context, LayoutNode node)
    {
        DrawNode(context, node);

        if (node.Left != null) DrawNodes(context, node.Left);
        if (node.Right != null) DrawNodes(context, node.Right);
    }

    private void DrawNode(DrawingContext context, LayoutNode node)
    {
        double progress = LocalProgress(node.Depth);
        if (progress <= 0.01)
            return;

        bool isOp = !node.Source.IsLeaf;
        double baseRadius = isOp ? 18 : 20;
        double scale = 0.55 + 0.45 * Ease.OutBack(progress);
        double radius = baseRadius * scale;
        var center = new Point(node.X, node.Y);

        using (context.PushOpacity(Ease.Clamp01(progress * 1.5)))
        {
            if (isOp)
            {
                context.DrawEllipse(OpBrush, NodeBorderPen, center, radius, radius);
            }
            else
            {
                var rect = new Rect(
                    node.X - radius - 4,
                    node.Y - radius + 2,
                    (radius + 4) * 2,
                    Math.Max(4, (radius - 2) * 2));
                context.FillRectangle(LeafBrush, rect, 6);
                context.DrawRectangle(NodeBorderPen, rect, 6);
            }

            var text = new FormattedText(
                node.Source.Token,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Cascadia Mono, Consolas", FontStyle.Normal, FontWeight.Bold),
                (isOp ? 13 : 12) * scale,
                TextBrush);
            context.DrawText(text, new Point(node.X - text.Width / 2, node.Y - text.Height / 2));
        }
    }
}
