using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AlgorithmsCore.Applications;
using AlgorithmsUI.Theme;

namespace AlgorithmsUI.Controls;

public sealed class TreeCanvas : Control
{
    private static readonly IBrush OpBrush = Palette.Brush(Palette.Accent);
    private static readonly IBrush LeafBrush = Palette.Brush(Palette.SurfaceRaised);
    private static readonly IBrush TextBrush = Palette.Brush(Palette.Foreground);
    private static readonly IBrush MutedTextBrush = Palette.Brush(Palette.MutedForeground);
    private static readonly IPen LinePen = new Pen(Palette.Brush(Palette.BorderStrong), 2);
    private static readonly IPen NodeBorderPen = new Pen(Palette.Brush(Palette.Border), 1.5);

    private ExpressionTree? _tree;

    public ExpressionTree? Tree
    {
        get => _tree;
        set
        {
            _tree = value;
            InvalidateVisual();
        }
    }

    private class LayoutNode
    {
        public ExpressionTree.Node Source;
        public double X;
        public double Y;
        public double Width;
        public LayoutNode? Left;
        public LayoutNode? Right;

        public LayoutNode(ExpressionTree.Node source) => Source = source;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Bounds.Width < 10 || Bounds.Height < 10) return;

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

        // Layout the tree
        var layoutRoot = BuildLayout(_tree.Root, 0);
        double treeWidth = layoutRoot.Width;
        double startX = Math.Max(20, (w - treeWidth) / 2);

        AssignCoordinates(layoutRoot, startX, 40, 60);

        // Render connections first (so lines appear behind node circles)
        DrawConnections(context, layoutRoot);

        // Render nodes
        DrawNodes(context, layoutRoot);
    }

    private LayoutNode BuildLayout(ExpressionTree.Node node, int depth)
    {
        var layout = new LayoutNode(node);
        double minNodeWidth = 46;

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

    private void DrawConnections(DrawingContext context, LayoutNode node)
    {
        if (node.Left != null)
        {
            context.DrawLine(LinePen, new Point(node.X, node.Y), new Point(node.Left.X, node.Left.Y));
            DrawConnections(context, node.Left);
        }
        if (node.Right != null)
        {
            context.DrawLine(LinePen, new Point(node.X, node.Y), new Point(node.Right.X, node.Right.Y));
            DrawConnections(context, node.Right);
        }
    }

    private void DrawNodes(DrawingContext context, LayoutNode node)
    {
        bool isOp = !node.Source.IsLeaf;
        double radius = isOp ? 18 : 20;

        var center = new Point(node.X, node.Y);
        var bgBrush = isOp ? OpBrush : LeafBrush;

        if (isOp)
        {
            context.DrawEllipse(bgBrush, NodeBorderPen, center, radius, radius);
        }
        else
        {
            var rect = new Rect(node.X - radius - 4, node.Y - radius + 2, (radius + 4) * 2, (radius - 2) * 2);
            context.FillRectangle(bgBrush, rect, 6);
            context.DrawRectangle(NodeBorderPen, rect, 6);
        }

        var text = new FormattedText(
            node.Source.Token,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Cascadia Mono, Consolas", FontStyle.Normal, FontWeight.Bold),
            isOp ? 13 : 12,
            TextBrush);
        context.DrawText(text, new Point(node.X - text.Width / 2, node.Y - text.Height / 2));

        if (node.Left != null) DrawNodes(context, node.Left);
        if (node.Right != null) DrawNodes(context, node.Right);
    }
}
