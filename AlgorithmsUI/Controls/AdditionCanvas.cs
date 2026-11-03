using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AlgorithmsCore.Applications;
using AlgorithmsUI.Theme;

namespace AlgorithmsUI.Controls;

public sealed class AdditionCanvas : Control
{
    private static readonly IBrush CarryBrush = Palette.Brush(Palette.VizWrite);
    private static readonly IBrush DigitBrush = Palette.Brush(Palette.Foreground);
    private static readonly IBrush ResultBrush = Palette.Brush(Palette.VizDone);
    private static readonly IBrush MutedBrush = Palette.Brush(Palette.MutedForeground);
    private static readonly IBrush ActiveColBrush = Palette.Brush(Palette.SurfaceRaised);
    private static readonly IPen DividerPen = new Pen(Palette.Brush(Palette.BorderStrong), 2);
    private static readonly IPen ActiveColPen = new Pen(Palette.Brush(Palette.VizActive), 2);

    private string _numA = "";
    private string _numB = "";
    private string _result = "";
    private List<LongNumberAdder.AdditionStep> _steps = new();
    private int _currentStepIndex = -1;

    public void SetData(string a, string b, string result, List<LongNumberAdder.AdditionStep> steps, int currentStep = -1)
    {
        _numA = a;
        _numB = b;
        _result = result;
        _steps = steps;
        _currentStepIndex = currentStep;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Bounds.Width < 10 || Bounds.Height < 10) return;

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

        // Section 1: "СТОЛБИК" (Column addition)
        int maxLen = Math.Max(_numA.Length, Math.Max(_numB.Length, _result.Length));
        double charWidth = 24;
        double totalBlockWidth = (maxLen + 2) * charWidth;
        double startX = Math.Max(40, (w - totalBlockWidth) / 2);
        double startY = 40;

        var header = new FormattedText(
            "СЛОЖЕНИЕ СТОЛБИКОМ (МЛАДШИЙ РАЗРЯД В ГОЛОВЕ СПИСКА):",
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter, Segoe UI", FontStyle.Normal, FontWeight.SemiBold),
            12,
            MutedBrush);
        context.DrawText(header, new Point(startX, startY - 24));

        // Pad numbers to right align
        string padA = _numA.PadLeft(maxLen + 1);
        string padB = _numB.PadLeft(maxLen + 1);
        string padRes = _result.PadLeft(maxLen + 1);

        // Highlight active column if stepping
        if (_currentStepIndex >= 0 && _currentStepIndex < _steps.Count)
        {
            int colFromRight = _steps[_currentStepIndex].ColumnIndex;
            double colX = startX + (maxLen - colFromRight) * charWidth + 24;
            var colRect = new Rect(colX - 4, startY - 4, charWidth, 140);
            context.FillRectangle(ActiveColBrush, colRect, 4);
            context.DrawRectangle(ActiveColPen, colRect, 4);
        }

        // Draw Carries
        double rowCarryY = startY;
        for (int i = 0; i < _steps.Count; i++)
        {
            if (_currentStepIndex >= 0 && i > _currentStepIndex) break;
            var step = _steps[i];
            if (step.OutputCarry > 0)
            {
                int colFromRight = step.ColumnIndex + 1; // carry applies to next column
                double cx = startX + (maxLen - colFromRight) * charWidth + 24;
                var cText = new FormattedText(
                    step.OutputCarry.ToString(),
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Cascadia Mono, Consolas", FontStyle.Normal, FontWeight.Bold),
                    12,
                    CarryBrush);
                context.DrawText(cText, new Point(cx + (charWidth - cText.Width) / 2, rowCarryY));
            }
        }

        // Draw Number A
        double rowAY = startY + 24;
        for (int i = 0; i < padA.Length; i++)
        {
            if (padA[i] == ' ') continue;
            var txt = new FormattedText(
                padA[i].ToString(),
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Cascadia Mono, Consolas", FontStyle.Normal, FontWeight.SemiBold),
                18,
                DigitBrush);
            context.DrawText(txt, new Point(startX + 24 + i * charWidth + (charWidth - txt.Width) / 2, rowAY));
        }

        // Draw '+' and Number B
        double rowBY = startY + 54;
        var plusTxt = new FormattedText("+", CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface("Cascadia Mono, Consolas", FontStyle.Normal, FontWeight.Bold), 18, CarryBrush);
        context.DrawText(plusTxt, new Point(startX, rowBY));

        for (int i = 0; i < padB.Length; i++)
        {
            if (padB[i] == ' ') continue;
            var txt = new FormattedText(
                padB[i].ToString(),
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Cascadia Mono, Consolas", FontStyle.Normal, FontWeight.SemiBold),
                18,
                DigitBrush);
            context.DrawText(txt, new Point(startX + 24 + i * charWidth + (charWidth - txt.Width) / 2, rowBY));
        }

        // Draw Divider
        double divY = startY + 84;
        context.DrawLine(DividerPen, new Point(startX, divY), new Point(startX + (maxLen + 2) * charWidth, divY));

        // Draw Result
        double rowResY = startY + 92;
        int maxShownDigits = _currentStepIndex >= 0 ? _currentStepIndex + 1 : _steps.Count;
        for (int i = 0; i < padRes.Length; i++)
        {
            if (padRes[i] == ' ') continue;
            int colFromRight = padRes.Length - 1 - i;
            if (colFromRight >= maxShownDigits) continue;

            var txt = new FormattedText(
                padRes[i].ToString(),
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Cascadia Mono, Consolas", FontStyle.Normal, FontWeight.Bold),
                19,
                ResultBrush);
            context.DrawText(txt, new Point(startX + 24 + i * charWidth + (charWidth - txt.Width) / 2, rowResY));
        }

        // Section 2: Explanation of current step
        if (_currentStepIndex >= 0 && _currentStepIndex < _steps.Count)
        {
            var cur = _steps[_currentStepIndex];
            string stepDesc = $"Разряд 10^{cur.ColumnIndex}: {cur.DigitA} + {cur.DigitB} + перенос({cur.InputCarry}) = {cur.Sum}  ➔  цифра в результат: {cur.ResultDigit}, новый перенос: {cur.OutputCarry}";
            var descText = new FormattedText(
                stepDesc,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Cascadia Mono, Consolas", FontStyle.Normal, FontWeight.SemiBold),
                13,
                Palette.Brush(Palette.VizActive));
            context.DrawText(descText, new Point(startX, startY + 140));
        }
    }
}
