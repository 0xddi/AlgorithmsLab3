using System.Globalization;
using Avalonia;
using Avalonia.Media;
using AlgorithmsUI.Controls.Animation;
using AlgorithmsUI.Theme;

namespace AlgorithmsUI.Controls;

/// <summary>
/// The Undo/Redo model drawn as two real stacks on the main canvas. Every edit pushes a chip onto the undo
/// stack; an undo lifts the top chip, flies it across the canvas and drops it onto the redo stack (redo does
/// the opposite). The document preview underneath shows the text those two stacks describe.
/// </summary>
public sealed class UndoRedoCanvas : AnimatedControl
{
    private const double TravelSeconds = 0.46;
    private const double Pad = 20;
    private const double HeaderHeight = 62;
    private const double DocumentHeight = 104;
    private const double ChipHeight = 29;
    private const double ChipGap = 6;

    private static readonly IBrush PanelBrush = Palette.Brush(Palette.Surface);
    private static readonly IBrush PanelBorderBrush = Palette.Brush(Palette.Border);
    private static readonly IBrush ChipBrush = Palette.Brush(Palette.SurfaceRaised);
    private static readonly IBrush ChipTopBrush = Palette.Brush(Palette.SurfaceRaised);
    private static readonly IBrush TextBrush = Palette.Brush(Palette.Foreground);
    private static readonly IBrush MutedBrush = Palette.Brush(Palette.MutedForeground);
    private static readonly IBrush UndoBrush = Palette.Brush(Palette.VizActive);
    private static readonly IBrush RedoBrush = Palette.Brush(Palette.VizDone);
    private static readonly IPen PanelPen = new Pen(PanelBorderBrush, 1);
    private static readonly IPen UndoPen = new Pen(UndoBrush, 1.5);
    private static readonly IPen RedoPen = new Pen(RedoBrush, 1.5);
    private static readonly IPen CardPen = new Pen(PanelBorderBrush, 1);

    private readonly List<Chip> _undo = new();
    private readonly List<Chip> _redo = new();
    private readonly List<string> _lastUndo = new();
    private readonly List<string> _lastRedo = new();
    private readonly SmoothValue _undoCounter = new(0, 12);
    private readonly SmoothValue _redoCounter = new(0, 12);
    private readonly SmoothValue _documentFlash = new(1, 10);
    private double _time;

    private string _document = "";
    private Traveler? _traveler;

    public string DocumentText
    {
        get => _document;
        set
        {
            if (_document == value)
                return;

            _document = value ?? "";
            _documentFlash.SnapTo(0.2);
            _documentFlash.Target = 1;
            RequestAnimation();
        }
    }

    /// <summary>Feeds the canvas the current stacks (top of stack first). All motion is derived from the diff.</summary>
    public void SetStacks(IReadOnlyList<string> undo, IReadOnlyList<string> redo)
    {
        string? flyingText = null;
        bool toRedo = false;

        // An undo moves exactly one action from the top of undo to the top of redo; redo is the mirror.
        if (TryDetectTransfer(_lastUndo, _lastRedo, undo, redo, out string? detected, out bool direction))
        {
            flyingText = detected;
            toRedo = direction;
        }

        Sync(_undo, undo, "u");
        Sync(_redo, redo, "r");

        _lastUndo.Clear();
        _lastUndo.AddRange(undo);
        _lastRedo.Clear();
        _lastRedo.AddRange(redo);

        _undoCounter.Target = undo.Count;
        _redoCounter.Target = redo.Count;

        if (flyingText != null)
        {
            var source = toRedo ? _undo : _redo;
            var chip = source.Find(c => c.Text == flyingText && c.Index == 0);
            if (chip != null)
            {
                chip.Flying = true;
                chip.Flight = 0;

                var destination = toRedo ? _redo : _undo;
                var landed = destination.Find(c => c.Text == flyingText && c.Index == 0);
                if (landed != null)
                {
                    landed.HoldUntil = _time + TravelSeconds;
                    landed.Presence.SnapTo(0);
                    landed.Presence.Target = 1;
                }

                _traveler = new Traveler(flyingText, toRedo);
            }
        }

        RequestAnimation();
    }

    /// <summary>
    /// Recognises "one action moved from the top of one stack to the top of the other", which is exactly what
    /// undo and redo do, so the canvas can animate a chip flying across instead of two unrelated fades.
    /// </summary>
    private static bool TryDetectTransfer(
        List<string> oldUndo,
        List<string> oldRedo,
        IReadOnlyList<string> newUndo,
        IReadOnlyList<string> newRedo,
        out string? text,
        out bool toRedo)
    {
        text = null;
        toRedo = true;

        // undo -> redo: undo lost its top, redo gained that same action on top.
        if (oldUndo.Count == newUndo.Count + 1 &&
            oldRedo.Count + 1 == newRedo.Count &&
            newUndo.Count + oldRedo.Count > 0 &&
            IsSuffix(newUndo, oldUndo, 1) &&
            IsSuffix(oldRedo, newRedo, 1) &&
            oldUndo[0] == newRedo[0])
        {
            text = oldUndo[0];
            toRedo = true;
            return true;
        }

        // redo -> undo: the mirror image.
        if (oldRedo.Count == newRedo.Count + 1 &&
            oldUndo.Count + 1 == newUndo.Count &&
            newRedo.Count + oldUndo.Count > 0 &&
            IsSuffix(newRedo, oldRedo, 1) &&
            IsSuffix(oldUndo, newUndo, 1) &&
            oldRedo[0] == newUndo[0])
        {
            text = oldRedo[0];
            toRedo = false;
            return true;
        }

        return false;
    }

    /// <summary>True when <paramref name="longer"/> starting at <paramref name="start"/> equals <paramref name="shorter"/>.</summary>
    private static bool IsSuffix(IReadOnlyList<string> shorter, IReadOnlyList<string> longer, int start)
    {
        if (longer.Count - start != shorter.Count)
            return false;

        for (int i = 0; i < shorter.Count; i++)
        {
            if (longer[start + i] != shorter[i])
                return false;
        }

        return true;
    }

    private void Sync(List<Chip> chips, IReadOnlyList<string> source, string prefix)
    {
        var occurrences = new Dictionary<string, int>();
        var alive = new HashSet<Chip>();

        for (int i = 0; i < source.Count; i++)
        {
            string text = source[i];
            occurrences.TryGetValue(text, out int seen);
            occurrences[text] = seen + 1;
            string key = $"{prefix}:{text}#{seen}";

            var chip = chips.Find(c => c.Key == key);
            if (chip == null)
            {
                chip = new Chip(key, text);
                chip.Presence.SnapTo(0);
                chip.Slot.SnapTo(-0.8);
                chips.Add(chip);
            }
            else
            {
                chip.Text = text;
            }

            chip.Index = i;
            chip.Presence.Target = 1;
            chip.Slot.Target = i;
            alive.Add(chip);
        }

        foreach (var chip in chips)
        {
            if (alive.Contains(chip))
                continue;

            chip.Presence.Target = 0;
            chip.Slot.Target = chip.Slot.Value;
        }
    }

    protected override bool OnAdvance(double deltaSeconds)
    {
        _time += deltaSeconds;
        bool changed = _undoCounter.Advance(deltaSeconds);
        changed |= _redoCounter.Advance(deltaSeconds);
        changed |= _documentFlash.Advance(deltaSeconds);
        changed |= AdvanceChips(_undo, deltaSeconds);
        changed |= AdvanceChips(_redo, deltaSeconds);

        if (_traveler != null)
        {
            _traveler.Progress += deltaSeconds / TravelSeconds;
            if (_traveler.Progress >= 1)
                _traveler = null;

            changed = true;
        }

        return changed;
    }

    private bool AdvanceChips(List<Chip> chips, double deltaSeconds)
    {
        bool changed = false;
        for (int i = chips.Count - 1; i >= 0; i--)
        {
            var chip = chips[i];

            if (chip.Flying)
            {
                chip.Flight += deltaSeconds;
                if (chip.Flight >= TravelSeconds)
                    chips.RemoveAt(i);

                changed = true;
                continue;
            }

            if (_time < chip.HoldUntil)
            {
                // Waiting for its twin to land from the other stack.
                changed = true;
                continue;
            }

            changed |= chip.Presence.Advance(deltaSeconds);
            changed |= chip.Slot.Advance(deltaSeconds);

            if (chip.Presence.Target == 0 && chip.Presence.Settled)
                chips.RemoveAt(i);
        }

        return changed;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Bounds.Width < 10 || Bounds.Height < 10)
            return;

        double w = Bounds.Width;
        double h = Bounds.Height;

        var header = new FormattedText(
            "UNDO / REDO НА ДВУХ СТЕКАХ",
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter, Segoe UI", FontStyle.Normal, FontWeight.SemiBold),
            12,
            MutedBrush);
        context.DrawText(header, new Point(Pad, Pad - 2));

        var subtitle = new FormattedText(
            "Каждое действие — push в стек отмен. Undo поднимает верхнее действие и переносит его в стек возврата, Redo — обратно.",
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter, Segoe UI", FontStyle.Normal, FontWeight.Normal),
            12,
            MutedBrush)
        {
            MaxTextWidth = Math.Max(120, w - Pad * 2)
        };
        using (context.PushOpacity(0.85))
            context.DrawText(subtitle, new Point(Pad, Pad + 18));

        double columnsTop = Pad + HeaderHeight;
        double columnsBottom = Math.Max(columnsTop + 120, h - Pad - DocumentHeight - 16);
        double gap = 16;
        double columnWidth = (w - Pad * 2 - gap) / 2;

        var undoRect = new Rect(Pad, columnsTop, columnWidth, columnsBottom - columnsTop);
        var redoRect = new Rect(Pad + columnWidth + gap, columnsTop, columnWidth, columnsBottom - columnsTop);

        DrawStack(context, undoRect, _undo, "UNDO — ОТМЕНИТЬ", UndoBrush, UndoPen, _undoCounter.Value);
        DrawStack(context, redoRect, _redo, "REDO — ПОВТОРИТЬ", RedoBrush, RedoPen, _redoCounter.Value);

        if (_traveler != null)
            DrawTraveler(context, undoRect, redoRect);

        DrawDocument(context, new Rect(Pad, h - Pad - DocumentHeight, w - Pad * 2, DocumentHeight));
    }

    private void DrawStack(
        DrawingContext context,
        Rect rect,
        List<Chip> chips,
        string title,
        IBrush accent,
        IPen accentPen,
        double counter)
    {
        context.FillRectangle(PanelBrush, rect, 12);
        context.DrawRectangle(PanelPen, rect, 12);

        var titleText = new FormattedText(
            title,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter, Segoe UI", FontStyle.Normal, FontWeight.Bold),
            10.5,
            accent);
        context.DrawText(titleText, new Point(rect.X + 12, rect.Y + 11));

        // Count badge, counting smoothly towards the real number of actions.
        int shownCount = (int)Math.Round(Math.Max(0, counter));
        var badgeText = new FormattedText(
            shownCount.ToString(),
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Cascadia Mono, Consolas", FontStyle.Normal, FontWeight.Bold),
            11,
            accent);
        var badgeWidth = Math.Max(20, badgeText.Width + 12);
        var badgeRect = new Rect(rect.Right - 12 - badgeWidth, rect.Y + 8, badgeWidth, 18);
        using (context.PushOpacity(0.18))
            context.FillRectangle(accent, badgeRect, 9);
        context.DrawText(badgeText, new Point(
            badgeRect.X + (badgeRect.Width - badgeText.Width) / 2,
            badgeRect.Y + (badgeRect.Height - badgeText.Height) / 2));

        double areaTop = rect.Y + 38;
        double areaHeight = rect.Height - 50;
        if (areaHeight < 30)
            return;

        int maxVisible = Math.Max(1, (int)((areaHeight + ChipGap) / (ChipHeight + ChipGap)));

        var visibleChips = chips
            .Where(c => !c.Flying)
            .OrderByDescending(c => c.Presence.Target == 0 ? -1 : c.Index)
            .ThenBy(c => c.Index)
            .ToList();

        if (visibleChips.Count == 0)
        {
            var empty = new FormattedText(
                "[ пусто ]",
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Cascadia Mono, Consolas", FontStyle.Italic, FontWeight.Normal),
                12,
                MutedBrush);
            context.DrawText(empty, new Point(
                rect.X + (rect.Width - empty.Width) / 2,
                areaTop + areaHeight / 2 - empty.Height / 2));
            return;
        }

        int drawn = 0;
        int hidden = 0;
        foreach (var chip in visibleChips)
        {
            double presence = Ease.Clamp01(chip.Presence.Value);
            if (presence <= 0.01)
                continue;

            if (chip.Presence.Target == 1 && chip.Index >= maxVisible)
            {
                hidden++;
                continue;
            }

            double y = areaTop + chip.Slot.Value * (ChipHeight + ChipGap);
            double x = rect.X + 10;
            double width = rect.Width - 20;

            if (y + ChipHeight < areaTop - 4 || y > rect.Bottom - 10)
                continue;

            bool isTop = chip.Index == 0 && chip.Presence.Target == 1;
            DrawChip(context, new Rect(x, y, width, ChipHeight), chip.Text, presence, isTop, accentPen);
            drawn++;
        }

        if (hidden > 0)
        {
            var more = new FormattedText(
                $"+{hidden} ещё",
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Cascadia Mono, Consolas", FontStyle.Normal, FontWeight.Normal),
                10.5,
                MutedBrush);
            context.DrawText(more, new Point(rect.Right - 12 - more.Width, rect.Bottom - 22));
        }
    }

    private void DrawChip(DrawingContext context, Rect rect, string text, double presence, bool isTop, IPen accentPen)
    {
        double scale = 0.9 + 0.1 * Ease.OutCubic(presence);
        var center = new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);

        using (context.PushOpacity(presence))
        using (context.PushTransform(
                   Matrix.CreateTranslation(-center.X, -center.Y)
                   * Matrix.CreateScale(scale, scale)
                   * Matrix.CreateTranslation(center.X, center.Y)))
        {
            context.FillRectangle(ChipTopBrush, rect, 7);
            context.DrawRectangle(isTop ? accentPen : CardPen, rect, 7);

            var label = new FormattedText(
                text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Cascadia Mono, Consolas", FontStyle.Normal, isTop ? FontWeight.SemiBold : FontWeight.Normal),
                11.5,
                isTop ? TextBrush : MutedBrush)
            {
                MaxTextWidth = Math.Max(20, rect.Width - 20),
                Trimming = TextTrimming.CharacterEllipsis
            };

            context.DrawText(label, new Point(rect.X + 10, rect.Y + (rect.Height - label.Height) / 2));
        }
    }

    private void DrawTraveler(DrawingContext context, Rect undoRect, Rect redoRect)
    {
        if (_traveler == null)
            return;

        var from = new Point(undoRect.X + undoRect.Width / 2, undoRect.Y + 38 + ChipHeight / 2);
        var to = new Point(redoRect.X + redoRect.Width / 2, redoRect.Y + 38 + ChipHeight / 2);
        if (!_traveler.ToRedo)
        {
            from = new Point(redoRect.X + redoRect.Width / 2, redoRect.Y + 38 + ChipHeight / 2);
            to = new Point(undoRect.X + undoRect.Width / 2, undoRect.Y + 38 + ChipHeight / 2);
        }

        double progress = Ease.Clamp01(_traveler.Progress);
        double eased = Ease.InOutCubic(progress);
        var control = new Point((from.X + to.X) / 2, Math.Min(from.Y, to.Y) - 58);

        // Quadratic Bezier for the flight path, plus a short fading trail.
        var path = new StreamGeometry();
        using (var geometry = path.Open())
        {
            geometry.BeginFigure(from, false);
            geometry.QuadraticBezierTo(control, to);
        }

        var trailBrush = _traveler.ToRedo ? RedoBrush : UndoBrush;
        using (context.PushOpacity(0.25))
            context.DrawGeometry(null, new Pen(trailBrush, 1.5), path);

        double position = eased;
        var point = new Point(
            (1 - position) * (1 - position) * from.X + 2 * (1 - position) * position * control.X + position * position * to.X,
            (1 - position) * (1 - position) * from.Y + 2 * (1 - position) * position * control.Y + position * position * to.Y);

        double width = Math.Min(250, undoRect.Width - 24);
        var rect = new Rect(point.X - width / 2, point.Y - ChipHeight / 2, width, ChipHeight);
        var accent = _traveler.ToRedo ? RedoBrush : UndoBrush;

        context.FillRectangle(ChipBrush, rect, 8);
        context.DrawRectangle(new Pen(accent, 1.5), rect, 8);

        var label = new FormattedText(
            _traveler.Text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Cascadia Mono, Consolas", FontStyle.Normal, FontWeight.SemiBold),
            11.5,
            TextBrush)
        {
            MaxTextWidth = Math.Max(20, width - 20),
            Trimming = TextTrimming.CharacterEllipsis
        };
        context.DrawText(label, new Point(rect.X + 10, rect.Y + (rect.Height - label.Height) / 2));
    }

    private void DrawDocument(DrawingContext context, Rect rect)
    {
        context.FillRectangle(PanelBrush, rect, 12);
        context.DrawRectangle(PanelPen, rect, 12);

        var label = new FormattedText(
            "ДОКУМЕНТ",
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter, Segoe UI", FontStyle.Normal, FontWeight.SemiBold),
            10,
            MutedBrush);
        context.DrawText(label, new Point(rect.X + 14, rect.Y + 12));

        string text = _document.Length == 0 ? "—" : _document;
        double maxWidth = rect.Width - 44;
        var body = new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Cascadia Mono, Consolas", FontStyle.Normal, FontWeight.Normal),
            15,
            TextBrush)
        {
            MaxTextWidth = Math.Max(20, maxWidth),
            MaxTextHeight = rect.Height - 46,
            Trimming = TextTrimming.CharacterEllipsis
        };

        double flash = Ease.Clamp01(_documentFlash.Value);
        using (context.PushOpacity(0.55 + 0.45 * flash))
            context.DrawText(body, new Point(rect.X + 14, rect.Y + 34));

        // Blinking caret at the end of the text, hinting that the document is live.
        double caretX = Math.Min(rect.Right - 16, rect.X + 14 + Math.Min(body.Width, maxWidth) + 2);
        if (Ease.Pulse(_time, 0.55) > 0.35)
        {
            using (context.PushOpacity(0.7))
                context.FillRectangle(TextBrush, new Rect(caretX, rect.Y + 34, 1.5, 18), 1);
        }
    }

    private sealed class Chip
    {
        public Chip(string key, string text)
        {
            Key = key;
            Text = text;
        }

        public string Key { get; }

        public string Text { get; set; }



        public int Index { get; set; }

        public bool Flying { get; set; }

        public double Flight { get; set; }

        public double HoldUntil { get; set; }

        public SmoothValue Presence { get; } = new(0, 15);

        public SmoothValue Slot { get; } = new(-1, 10);
    }

    private sealed class Traveler
    {
        public Traveler(string text, bool toRedo)
        {
            Text = text;
            ToRedo = toRedo;
        }

        public string Text { get; }

        public bool ToRedo { get; }

        public double Progress { get; set; }
    }
}
