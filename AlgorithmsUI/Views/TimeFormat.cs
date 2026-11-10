namespace AlgorithmsUI.Views;

internal static class TimeFormat
{
    public static string Format(TimeSpan elapsed) => elapsed.TotalMilliseconds < 0.1
        ? $"{elapsed.TotalMicroseconds:F1} мкс"
        : $"{elapsed.TotalMilliseconds:F2} мс";
}
