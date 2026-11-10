using System.Globalization;
using Avalonia;
using Avalonia.Media;
using AlgorithmsCore.Applications;
using AlgorithmsUI.Controls.Animation;
using AlgorithmsUI.Theme;

namespace AlgorithmsUI.Controls;

/// <summary>
/// Expression tree with two motion modes. A plain tree grows in level by level. A tree built from a postfix
/// expression is replayed token by token: the token ribbon shows what is being read, the bottom ribbon shows
/// the stack of subtrees, and every number / function / operator creates its node in place, so the animation
/// explains the postfix construction itself instead of only revealing the result.
/// </summary>
public sealed class TreeCanvas : AnimatedControl
{
    private static readonly IBrush OpBrush = Palette.Brush(Palette.Accent);
    private static readonly IBrush LeafBrush = Palette.Brush(Palette.SurfaceRaised);
    private static readonly IBrush TextBrush = Palette.Brush(Palette.Foreground);
    private static readonly IBrush MutedTextBrush = Palette.Brush(Palette.MutedForeground);
    private static readonly IBrush ChipBrush = Palette.Brush(Palette.Surface);
    private static readonly IBrush ActiveChipBrush = Palette.Brush(Palette.Accent);
    private static readonly IBrush ActiveChipTextBrush = Palette.Brush(Palette.OnHighlight);
    private static readonly IBrush ActionBrush = Palette.Brush(Palette.VizWrite);
    private static readonly IPen LinePen = new Pen(Palette.Brush(Palette.BorderStrong), 2);
    private static readonly IPen NodeBorderPen = new Pen(Palette.Brush(Palette.Border), 1.5);
    private static readonly IPen ChipPen = new Pen(Palette.Brush(Palette.Border), 1);
    private static readonly IPen TopChipPen = new Pen(Palette.Brush(Palette.VizActive), 1.5);
    private static readonly IPen PoppedPen = new Pen(Palette.Brush(Palette.VizCompare), 2);

    private const double RevealSeconds = 0.72;
    private const double BuildTotalSeconds = 3.2;
    private const double BuildHoldSeconds = 1.3;
    private const double OverlayFadeSeconds = 0.45;
    private const double NodePopSeconds = 0.22;
    private const double EdgeGrowSeconds = 0.26;

    private ExpressionTree? _tree;
    private ExpressionTree.Node? _root;
    private double _reveal = 1;

    // Build replay state.
    private readonly List<BuildStep> _steps = new();
    private readonly HashSet<ExpressionTree.Node> _visible = new();
    private int _stepIndex = -1;
    private double _stepTimer;
    private double _stepDuration = 0.45;
    private double _postTimer;
    private double _overlayOpacity;
    private bool _building;
    private bool _fading;
    private ExpressionTree.Node? _newest;

    public ExpressionTree? Tree
    {
        get => _tree;
        set
        {
            CancelBuild();
            _tree = value;
            _root = value?.Root;
            _reveal = 0;
            RequestAnimation();
        }
    }

    /// <summary>
    /// Replays the postfix construction: each token updates the stack of subtrees and creates one node.
    /// Returns <c>false</c> when the token sequence is not a valid postfix expression.
    /// </summary>
    public bool PlayBuild(IReadOnlyList<string> tokens)
    {
        _steps.Clear();
        _visible.Clear();
        _newest = null;
        _stepIndex = -1;
        _stepTimer = 0;
        _postTimer = 0;
        _overlayOpacity = 0;
        _fading = false;
        _reveal = 1;
        _tree = null;
        _root = null;

        var stack = new List<ExpressionTree.Node>();

        foreach (string token in tokens)
        {
            ExpressionTree.Node? node;
            BuildKind kind;
            string action;

            if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            {
                node = new ExpressionTree.Node(token);
                kind = BuildKind.Number;
                action = $"Число {token} → лист, push в стек";
            }
            else if (token is "sqrt" or "sin" or "cos" or "ln")
            {
                if (stack.Count < 1)
                    return false;

                var child = stack[^1];
                stack.RemoveAt(stack.Count - 1);
                node = new ExpressionTree.Node(token, child, null);
                kind = BuildKind.Unary;
                action = $"Функция {token}: pop 1 узел → новый узел, push";
            }
            else if (token is "+" or "-" or "*" or "/" or ":" or "^")
            {
                if (stack.Count < 2)
                    return false;

                var right = stack[^1];
                stack.RemoveAt(stack.Count - 1);
                var left = stack[^1];
                stack.RemoveAt(stack.Count - 1);
                node = new ExpressionTree.Node(token, left, right);
                kind = BuildKind.Binary;
                action = $"Оператор {token}: pop {right.Token} (правый) и {left.Token} (левый) → новый узел";
            }
            else
            {
                return false;
            }

            stack.Add(node);
            _steps.Add(new BuildStep(token, action, node, stack.ToList(), kind));
        }

        if (stack.Count != 1)
            return false;

        _root = stack[0];
        _stepDuration = Math.Clamp(BuildTotalSeconds / Math.Max(1, _steps.Count), 0.26, 0.46);
        _building = true;

        if (_steps.Count > 0)
            BeginStep(0);

        RequestAnimation();
        return true;
    }

    private void CancelBuild()
    {
        _steps.Clear();
        _visible.Clear();
        _building = false;
        _fading = false;
        _newest = null;
        _stepIndex = -1;
        _overlayOpacity = 0;
    }

    private void BeginStep(int index)
    {
        _stepIndex = index;
        _stepTimer = 0;
        _newest = _steps[index].Created;
        _visible.Add(_newest);
    }

    protected override bool OnAdvance(double deltaSeconds)
    {
        if (_building)
        {
            if (_fading)
            {
                // Only fade out here: adding opacity in the same frame would keep the overlay visible forever.
                _overlayOpacity = Math.Max(0, _overlayOpacity - deltaSeconds / OverlayFadeSeconds);
                if (_overlayOpacity <= 0)
                {
                    _building = false;
                    _newest = null;
                }

                return true;
            }

            _overlayOpacity = Math.Min(1, _overlayOpacity + deltaSeconds / 0.18);

            if (_stepIndex < _steps.Count - 1)
            {
                _stepTimer += deltaSeconds;
                if (_stepTimer >= _stepDuration)
                    BeginStep(_stepIndex + 1);
            }
            else
            {
                _stepTimer += deltaSeconds;
                _postTimer += deltaSeconds;

                if (_postTimer >= BuildHoldSeconds)
                    _fading = true;
            }

            return true;
        }

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

    private enum BuildKind
    {
        Number,
        Unary,
        Binary
    }

    private sealed class BuildStep
    {
        public BuildStep(string token, string action, ExpressionTree.Node created, List<ExpressionTree.Node> stack, BuildKind kind)
        {
            Token = token;
            Action = action;
            Created = created;
            Stack = stack;
            Kind = kind;
        }

        public string Token { get; }

        public string Action { get; }

        public ExpressionTree.Node Created { get; }

        public List<ExpressionTree.Node> Stack { get; }

        public BuildKind Kind { get; }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Bounds.Width < 10 || Bounds.Height < 10)
            return;

        double w = Bounds.Width;
        double h = Bounds.Height;

        if (_root == null)
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

        var layoutRoot = BuildLayout(_root, 0);
        double startX = Math.Max(20, (w - layoutRoot.Width) / 2);
        double startY = _steps.Count > 0 ? 96 : 40;

        AssignCoordinates(layoutRoot, startX, startY, 60);

        DrawConnections(context, layoutRoot);
        DrawNodes(context, layoutRoot);

        if (_building && _overlayOpacity > 0.01 && _stepIndex >= 0 && _stepIndex < _steps.Count)
            DrawBuildOverlay(context, w, h);
    }

    private LayoutNode BuildLayout(ExpressionTree.Node node, int depth)
    {
        var layout = new LayoutNode { Source = node, Depth = depth };
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

    /// <summary>0..1 local reveal for a node at <paramref name="depth"/>: deeper levels start later.</summary>
    private double LocalProgress(int depth)
    {
        if (_reveal >= 1)
            return 1;

        double stagger = Math.Min(0.42, 0.09 * depth);
        return Ease.Clamp01((_reveal - stagger) / 0.58);
    }

    private bool IsNodeVisible(ExpressionTree.Node node) => !_building || _visible.Contains(node);

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
        if (child == null || !IsNodeVisible(parent.Source))
            return;

        double progress;
        if (_building)
        {
            // Only the edges of the node created in the current step grow; older edges are already complete.
            progress = ReferenceEquals(parent.Source, _newest) ? Ease.Clamp01(_stepTimer / EdgeGrowSeconds) : 1;
        }
        else
        {
            progress = LocalProgress(child.Depth);
        }

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
        if (!IsNodeVisible(node.Source))
            return;

        double progress;
        if (_building)
        {
            progress = ReferenceEquals(node.Source, _newest) ? Ease.Clamp01(_stepTimer / NodePopSeconds) : 1;
        }
        else
        {
            progress = LocalProgress(node.Depth);
        }

        if (progress <= 0.01)
            return;

        bool isOp = !node.Source.IsLeaf;
        double baseRadius = isOp ? 18 : 20;
        double scale = 0.55 + 0.45 * Ease.OutBack(progress);
        double radius = baseRadius * scale;
        var center = new Point(node.X, node.Y);

        // The two subtrees the current operator takes from the stack are outlined while it is created.
        if (_building && _newest != null && _newest != node.Source &&
            (ReferenceEquals(node.Source, _newest.Left) || ReferenceEquals(node.Source, _newest.Right)))
        {
            double glow = Ease.Clamp01(1 - _stepTimer / 0.45);
            if (glow > 0.01)
            {
                using (context.PushOpacity(glow * 0.9))
                    context.DrawEllipse(null, PoppedPen, center, radius + 6, radius + 6);
            }
        }

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

    private void DrawBuildOverlay(DrawingContext context, double width, double height)
    {
        using (context.PushOpacity(Ease.Clamp01(_overlayOpacity)))
        {
            DrawTokenRibbon(context, width);
            DrawStatus(context, width);
            DrawStackRibbon(context, width, height);
        }
    }

    private void DrawTokenRibbon(DrawingContext context, double width)
    {
        double x = 12;
        double y = 10;
        const double lineHeight = 25;

        for (int i = 0; i < _steps.Count; i++)
        {
            string token = _steps[i].Token;
            var text = new FormattedText(
                token,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Cascadia Mono, Consolas", FontStyle.Normal, FontWeight.SemiBold),
                11,
                TextBrush);

            double chipWidth = Math.Max(26, text.Width + 14);
            if (x + chipWidth > width - 12 && x > 12)
            {
                x = 12;
                y += lineHeight;
            }

            if (y > 58)
                break; // never overlap the tree

            bool current = i == _stepIndex;
            bool done = i < _stepIndex;

            var rect = new Rect(x, y, chipWidth, 21);
            if (current)
            {
                context.FillRectangle(ActiveChipBrush, rect, 5);
            }
            else
            {
                context.FillRectangle(ChipBrush, rect, 5);
                context.DrawRectangle(ChipPen, rect, 5);
            }

            var labelBrush = current
                ? ActiveChipTextBrush
                : done
                    ? MutedTextBrush
                    : TextBrush;
            var label = new FormattedText(
                token,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Cascadia Mono, Consolas", FontStyle.Normal, current ? FontWeight.Bold : FontWeight.Normal),
                11,
                labelBrush);
            context.DrawText(label, new Point(
                rect.X + (rect.Width - label.Width) / 2,
                rect.Y + (rect.Height - label.Height) / 2));

            x += chipWidth + 4;
        }
    }

    private void DrawStatus(DrawingContext context, double width)
    {
        bool finished = _stepIndex == _steps.Count - 1 && _postTimer > 0.25;
        string status = finished
            ? "Готово: в стеке остался один узел — это корень дерева."
            : $"[{_stepIndex + 1}/{_steps.Count}] {_steps[_stepIndex].Action}";

        var text = new FormattedText(
            status,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Cascadia Mono, Consolas", FontStyle.Normal, FontWeight.SemiBold),
            11,
            finished ? Palette.Brush(Palette.VizDone) : ActionBrush)
        {
            MaxTextWidth = Math.Max(120, width - 24)
        };

        context.DrawText(text, new Point(12, 64));
    }

    private void DrawStackRibbon(DrawingContext context, double width, double height)
    {
        var stack = _steps[_stepIndex].Stack;

        var title = new FormattedText(
            "СТЕК УЗЛОВ",
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter, Segoe UI", FontStyle.Normal, FontWeight.SemiBold),
            10,
            MutedTextBrush);
        context.DrawText(title, new Point(12, height - 66));

        var topHint = new FormattedText(
            $"вершина стека →   узлов: {stack.Count}",
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter, Segoe UI", FontStyle.Normal, FontWeight.Normal),
            10,
            MutedTextBrush);
        context.DrawText(topHint, new Point(width - 12 - topHint.Width, height - 66));

        int start = Math.Max(0, stack.Count - 6);
        double x = 12;
        double y = height - 48;

        if (start > 0)
        {
            var more = new FormattedText(
                "…",
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Cascadia Mono, Consolas", FontStyle.Normal, FontWeight.Bold),
                13,
                MutedTextBrush);
            context.DrawText(more, new Point(x, y + 3));
            x += 18;
        }

        for (int i = start; i < stack.Count; i++)
        {
            var node = stack[i];
            bool isTop = i == stack.Count - 1;

            var text = new FormattedText(
                node.Token,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Cascadia Mono, Consolas", FontStyle.Normal, isTop ? FontWeight.Bold : FontWeight.Normal),
                11,
                isTop ? TextBrush : MutedTextBrush);
            double chipWidth = Math.Max(30, text.Width + 14);
            var rect = new Rect(x, y, chipWidth, 23);

            context.FillRectangle(ChipBrush, rect, 5);
            context.DrawRectangle(isTop ? TopChipPen : ChipPen, rect, 5);
            context.DrawText(text, new Point(
                rect.X + (rect.Width - text.Width) / 2,
                rect.Y + (rect.Height - text.Height) / 2));

            x += chipWidth + 6;
        }
    }
}
