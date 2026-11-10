using System.Globalization;
using Avalonia;
using Avalonia.Media;
using AlgorithmsCore.Applications;
using AlgorithmsUI.Controls.Animation;
using AlgorithmsUI.Theme;

namespace AlgorithmsUI.Controls;

/// <summary>
/// Column addition. Every digit of the sum pops into place when its column is processed, the carry digits
/// drop in above the next column and the active column glides from one place to the next with a soft glow.
/// </summary>
public sealed class AdditionCanvas : AnimatedControl
{
    private static readonly IBrush CarryBrush = Palette.Brush(Palette.VizWrite);
    private static readonly IBrush DigitBrush = Palette.Brush(Palette.Foreground);
    private static readonly IBrush ResultBrush = Palette.Brush(Palette.VizDone);
    private static readonly IBrush MutedBrush = Palette.Brush(Palette.MutedForeground);
    private static readonly IBrush ActiveColBrush = Palette.Brush(Palette.SurfaceRaised);
    private static readonly IPen DividerPen = new Pen(Palette.Brush(Palette.BorderStrong), 2);
    private static readonly IPen ActiveColPen = new Pen(Palette.Brush(Palette.VizActive), 2);

    private readonly Dictionary<int, SmoothValue> _digits = new();
    private readonly Dictionary<int, SmoothValue> _carries = new();
    private readonly FadeText _description = new();
    private readonly SmoothValue _enter = new(0, 9);
    private readonly SmoothValue _columnX = new(0, 11);
    private readonly SmoothValue _columnAlpha = new(0, 11);

    private string _numA = "";
    private string _numB = "";
    private string _result = "";
    private List<LongNumberAdder.AdditionStep> _steps = new();
    private int _currentStepIndex = -1;
    private string _signature = "";
    private double _time;

    public void SetData(string a, string b, string result, List<LongNumberAdder.AdditionStep> steps, int currentStep = -1)
    {
        string signature = $"{a}|{b}|{steps.Count}";
        if (signature != _signature)
        {
            _signature = signature;
            _enter.SnapTo(0);
            _enter.Target = 1;
            _digits.Clear();
            _carries.Clear();
            _columnAlpha.SnapTo(0);
        }

        _numA = a;
        _numB = b;
        _result = result;
        _steps = steps;
        _currentStepIndex = currentStep;
        SyncTargets();
        UpdateDescription();
        RequestAnimation();
    }

    private void UpdateDescription()
    {
        if (_currentStepIndex >= 0 && _currentStepIndex < _steps.Count)
        {
            var current = _steps[_currentStepIndex];
            _description.Update(
                $"Разряд 10^{current.ColumnIndex}: {current.DigitA} + {current.DigitB} + перенос({current.InputCarry}) = {current.Sum}  ➔  цифра: {current.ResultDigit}, перенос: {current.OutputCarry}");
        }
        else if (_currentStepIndex < 0)
        {
            _description.Update(null);
        }
        else
        {
            _description.Update("Сложение завершено: все разряды обработаны.");
        }
    }

    protected override bool OnAdvance(double deltaSeconds)
    {
        _time += deltaSeconds;
        bool changed = _enter.Advance(deltaSeconds);
        changed |= _columnX.Advance(deltaSeconds);
        changed |= _columnAlpha.Advance(deltaSeconds);
        changed |= _description.Advance(deltaSeconds);

        foreach (var digit in _digits.Values)
            changed |= digit.Advance(deltaSeconds);

        foreach (var carry in _carries.Values)
            changed |= carry.Advance(deltaSeconds);

        return changed;
    }

    /// <summary>Pushes the current step into per-digit / per-carry fade targets.</summary>
    private void SyncTargets()
    {
        int maxLength = Math.Max(_numA.Length, Math.Max(_numB.Length, _result.Length));
        int shownDigits = _currentStepIndex >= 0 ? _currentStepIndex + 1 : _steps.Count;

        for (int column = 0; column <= maxLength; column++)
        {
            bool visible = column < shownDigits;
            if (!_digits.TryGetValue(column, out var smooth))
            {
                smooth = new SmoothValue(visible ? 0 : 0, 22);
                _digits[column] = smooth;
            }

            smooth.Target = visible ? 1 : 0;
        }

        for (int step = 0; step < _steps.Count; step++)
        {
            bool visible = step <= _currentStepIndex && _steps[step].OutputCarry > 0;
            if (!_carries.TryGetValue(step, out var smooth))
            {
                smooth = new SmoothValue(0, 20);
                _carries[step] = smooth;
            }

            smooth.Target = visible ? 1 : 0;
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Bounds.Width < 10 || Bounds.Height < 10)
            return;

        double w = Bounds.Width;
        double h = Bounds.Height;

        if (string.IsNullOrEmpty(_numA) || string.IsNullOrEmpty(_numB))
        {
            var emptyText = new FormattedText(
                "[ ВВЕДИТЕ ДВА ЧИСЛА И НАЖМИТЕ СЛОЖИТЬ ]",
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Inter, Segoe UI", FontStyle.Normal, FontWeight.Medium),
                14,
                MutedBrush);
            context.DrawText(emptyText, new Point((w - emptyText.Width) / 2, h / 2 - emptyText.Height / 2));
            return;
        }

        int maxLen = Math.Max(_numA.Length, Math.Max(_numB.Length, _result.Length));
        // Long numbers shrink the digit cells instead of running outside the canvas.
        double charWidth = Math.Min(24d, Math.Max(11d, (w - 72d) / Math.Max(1, maxLen + 2)));
        double totalBlockWidth = (maxLen + 2) * charWidth;
        double startX = Math.Max(16, (w - totalBlockWidth) / 2);
        double startY = 40;

        double enter = Ease.Clamp01(_enter.Value);
        double enterOffset = (1 - Ease.OutCubic(enter)) * 10;

        using (context.PushOpacity(enter))
        using (context.PushTransform(Matrix.CreateTranslation(0, enterOffset)))
            DrawContent(context, w, startX, startY, maxLen, charWidth);

        _description.Draw(context, w / 2, startY + 150, Palette.Brush(Palette.VizActive), 13, Math.Max(120, w - 40));
    }

    private void DrawContent(DrawingContext context, double width, double startX, double startY, int maxLen, double charWidth)
    {
        double digitSize = Math.Min(18d, charWidth * 0.82);
        const string headerText = "СЛОЖЕНИЕ СТОЛБИКОМ (МЛАДШИЙ РАЗРЯД В ГОЛОВЕ СПИСКА):";
        double headerSize = 12;
        FormattedText header;
        do
        {
            header = new FormattedText(
                headerText,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Inter, Segoe UI", FontStyle.Normal, FontWeight.SemiBold),
                headerSize,
                MutedBrush);
            headerSize -= 0.5;
        }
        while (header.Width > Math.Max(160, width - 32) && headerSize > 8.5);

        context.DrawText(header, new Point(16, startY - 24));

        string padA = _numA.PadLeft(maxLen + 1);
        string padB = _numB.PadLeft(maxLen + 1);
        string padRes = _result.PadLeft(maxLen + 1);

        // Active column highlight follows the current step instead of jumping between columns.
        if (_currentStepIndex >= 0 && _currentStepIndex < _steps.Count)
        {
            int columnFromRight = _steps[_currentStepIndex].ColumnIndex;
            double columnX = startX + (maxLen - columnFromRight) * charWidth + charWidth;
            if (_columnAlpha.Value < 0.02)
                _columnX.SnapTo(columnX);

            SetTarget(_columnX, columnX);
            SetTarget(_columnAlpha, 1);
        }
        else
        {
            SetTarget(_columnAlpha, 0);
        }

        if (_columnAlpha.Value > 0.01)
        {
            double glow = 0.55 + 0.45 * Ease.Pulse(_time, 0.85);
            var columnRect = new Rect(_columnX.Value - 4, startY - 4, charWidth, 140);

            using (context.PushOpacity(_columnAlpha.Value * 0.85))
                context.FillRectangle(ActiveColBrush, columnRect, 4);

            using (context.PushOpacity(_columnAlpha.Value * glow))
                context.DrawRectangle(ActiveColPen, columnRect, 4);
        }

        // Carries: dropped in above the column they flow into.
        double rowCarryY = startY;
        for (int i = 0; i < _steps.Count; i++)
        {
            if (!_carries.TryGetValue(i, out var carryPresence) || carryPresence.Value <= 0.01)
                continue;

            var step = _steps[i];
            if (step.OutputCarry <= 0)
                continue;

            int columnFromRight = step.ColumnIndex + 1;
            double carryX = startX + (maxLen - columnFromRight) * charWidth + charWidth;
            double pop = Ease.OutBack(Ease.Clamp01(carryPresence.Value));

            var carryText = new FormattedText(
                step.OutputCarry.ToString(),
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Cascadia Mono, Consolas", FontStyle.Normal, FontWeight.Bold),
                12 * (0.6 + 0.4 * pop),
                CarryBrush);

            using (context.PushOpacity(Ease.Clamp01(carryPresence.Value)))
                context.DrawText(carryText,
                    new Point(carryX + (charWidth - carryText.Width) / 2, rowCarryY + (1 - pop) * -10));
        }

        DrawNumber(context, padA, startX, startY + 24, charWidth, DigitBrush, FontWeight.SemiBold, digitSize);
        DrawNumber(context, padB, startX, startY + 54, charWidth, DigitBrush, FontWeight.SemiBold, digitSize);

        var plus = new FormattedText("+", CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface("Cascadia Mono, Consolas", FontStyle.Normal, FontWeight.Bold), digitSize, CarryBrush);
        context.DrawText(plus, new Point(startX, startY + 54));

        double dividerY = startY + 84;
        double dividerWidth = (maxLen + 2) * charWidth * Ease.OutCubic(Ease.Clamp01(_enter.Value));
        context.DrawLine(DividerPen, new Point(startX, dividerY), new Point(startX + dividerWidth, dividerY));

        DrawResult(context, padRes, startX, startY + 92, charWidth);
    }

    private static void DrawNumber(
        DrawingContext context,
        string padded,
        double startX,
        double y,
        double charWidth,
        IBrush brush,
        FontWeight weight,
        double fontSize)
    {
        for (int i = 0; i < padded.Length; i++)
        {
            if (padded[i] == ' ')
                continue;

            var text = new FormattedText(
                padded[i].ToString(),
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Cascadia Mono, Consolas", FontStyle.Normal, weight),
                fontSize,
                brush);
            context.DrawText(text, new Point(startX + charWidth + i * charWidth + (charWidth - text.Width) / 2, y));
        }
    }

    private void DrawResult(DrawingContext context, string padded, double startX, double y, double charWidth)
    {
        for (int i = 0; i < padded.Length; i++)
        {
            if (padded[i] == ' ')
                continue;

            int columnFromRight = padded.Length - 1 - i;
            if (!_digits.TryGetValue(columnFromRight, out var presence) || presence.Value <= 0.01)
                continue;

            double pop = Ease.OutBack(Ease.Clamp01(presence.Value));
            var text = new FormattedText(
                padded[i].ToString(),
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Cascadia Mono, Consolas", FontStyle.Normal, FontWeight.Bold),
                Math.Min(19d, charWidth * 0.85) * (0.72 + 0.28 * pop),
                ResultBrush);

            using (context.PushOpacity(Ease.Clamp01(presence.Value)))
                context.DrawText(text, new Point(
                    startX + charWidth + i * charWidth + (charWidth - text.Width) / 2,
                    y + (1 - pop) * 6));
        }
    }
}
