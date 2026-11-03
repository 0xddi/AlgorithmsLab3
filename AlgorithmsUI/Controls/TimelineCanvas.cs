using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AlgorithmsCore.Applications;
using AlgorithmsUI.Theme;

namespace AlgorithmsUI.Controls;

public sealed class TimelineCanvas : Control
{
    private static readonly IBrush TextBrush = Palette.Brush(Palette.Foreground);
    private static readonly IBrush MutedBrush = Palette.Brush(Palette.MutedForeground);
    private static readonly IPen AxisPen = new Pen(Palette.Brush(Palette.BorderStrong), 2);
    private static readonly IPen BlockBorderPen = new Pen(Palette.Brush(Palette.Border), 1.5);
    private static readonly IPen ActivePen = new Pen(Palette.Brush(Palette.VizCompare), 2);

    private static readonly Color[] ProcessColors =
    [
        Color.Parse("#2563EB"), // Blue
        Color.Parse("#D97706"), // Amber/Orange
        Color.Parse("#16A34A"), // Green
        Color.Parse("#9333EA"), // Purple
        Color.Parse("#0891B2"), // Cyan
        Color.Parse("#E11D48"), // Rose
    ];

    private List<RoundRobinScheduler.ExecutionSlice> _slices = new();
    private List<SimProcess> _processes = new();
    private int _currentSliceIndex = -1;

    public void SetData(List<RoundRobinScheduler.ExecutionSlice> slices, List<SimProcess> processes, int currentSlice = -1)
    {
        _slices = slices;
        _processes = processes;
        _currentSliceIndex = currentSlice;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Bounds.Width < 10 || Bounds.Height < 10) return;

        double w = Bounds.Width;
        double h = Bounds.Height;

        if (_slices.Count == 0)
        {
            var emptyText = new FormattedText(
                "[ НАСТРОЙТЕ ПРОЦЕССЫ И ЗАПУСТИТЕ СИМУЛЯЦИЮ ]",
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Inter, Segoe UI", FontStyle.Normal, FontWeight.Medium),
                14,
                MutedBrush);
            context.DrawText(emptyText, new Point((w - emptyText.Width) / 2, h / 2 - emptyText.Height / 2));
            return;
        }

        double startX = 40;
        double startY = 40;
        double totalTime = _slices.Last().EndTime;
        double availableWidth = w - startX * 2;
        double timeScale = availableWidth / Math.Max(1, totalTime);
        double blockHeight = 44;

        var header = new FormattedText(
            "ДИАГРАММА ГАНТА (ВЫДЕЛЕНИЕ КВАНТОВ ВРЕМЕНИ ПО FIFO):",
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter, Segoe UI", FontStyle.Normal, FontWeight.SemiBold),
            12,
            MutedBrush);
        context.DrawText(header, new Point(startX, startY - 24));

        int shownCount = _currentSliceIndex >= 0 ? _currentSliceIndex + 1 : _slices.Count;

        // Draw Gantt blocks
        for (int i = 0; i < shownCount; i++)
        {
            var slice = _slices[i];
            double x1 = startX + slice.StartTime * timeScale;
            double x2 = startX + slice.EndTime * timeScale;
            double sliceWidth = Math.Max(2, x2 - x1);

            int pIdx = _processes.FindIndex(p => p.Name == slice.ProcessName);
            var color = ProcessColors[Math.Abs(pIdx) % ProcessColors.Length];
            var fillBrush = new SolidColorBrush(color);

            var rect = new Rect(x1, startY, sliceWidth, blockHeight);
            context.FillRectangle(fillBrush, rect, 4);

            bool isActive = (i == _currentSliceIndex);
            context.DrawRectangle(isActive ? ActivePen : BlockBorderPen, rect, 4);

            // Process name inside block if fits
            if (sliceWidth > 24)
            {
                var label = new FormattedText(
                    slice.ProcessName,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Inter, Segoe UI", FontStyle.Normal, FontWeight.Bold),
                    12,
                    TextBrush);
                context.DrawText(label, new Point(x1 + (sliceWidth - label.Width) / 2, startY + (blockHeight - label.Height) / 2));
            }

            // Time tick under block end
            var tick = new FormattedText(
                slice.EndTime.ToString(),
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Cascadia Mono, Consolas", FontStyle.Normal, FontWeight.Normal),
                10,
                MutedBrush);
            context.DrawText(tick, new Point(x2 - tick.Width / 2, startY + blockHeight + 6));
        }

        // Time 0 tick
        var zeroTick = new FormattedText(
            "0",
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Cascadia Mono, Consolas", FontStyle.Normal, FontWeight.Normal),
            10,
            MutedBrush);
        context.DrawText(zeroTick, new Point(startX - zeroTick.Width / 2, startY + blockHeight + 6));

        // Time axis line
        context.DrawLine(AxisPen, new Point(startX, startY + blockHeight + 2), new Point(w - startX, startY + blockHeight + 2));

        // Section 2: Active Slice & Queue status
        if (_currentSliceIndex >= 0 && _currentSliceIndex < _slices.Count)
        {
            var cur = _slices[_currentSliceIndex];
            double statusY = startY + blockHeight + 40;

            var curText = new FormattedText(
                $"Текущий шаг t={cur.StartTime}..{cur.EndTime}: Выполняется {cur.ProcessName} (осталось {cur.RemainingAfter})",
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Cascadia Mono, Consolas", FontStyle.Normal, FontWeight.Bold),
                13,
                Palette.Brush(Palette.VizActive));
            context.DrawText(curText, new Point(startX, statusY));

            string qStr = cur.QueueAfter.Count > 0 ? string.Join(" ──► ", cur.QueueAfter) : "[пусто]";
            var queueText = new FormattedText(
                $"Состояние MyQueue:  {qStr}",
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Cascadia Mono, Consolas", FontStyle.Normal, FontWeight.Normal),
                12,
                MutedBrush);
            context.DrawText(queueText, new Point(startX, statusY + 24));
        }
    }
}
