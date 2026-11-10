using Avalonia;
using Avalonia.Media;

namespace AlgorithmsUI.Controls.Animation;

/// <summary>
/// A one-line caption that cross-fades whenever its text changes, instead of replacing the text instantly.
/// Used by every canvas for the "what just happened" line at the bottom of the drawing.
/// </summary>
public sealed class FadeText
{
    private readonly SmoothValue _opacity = new(1, 12);
    private readonly SmoothValue _shift = new(0, 12);

    public string? Text { get; private set; }

    public double Opacity => _opacity.Value;

    public double Shift => _shift.Value;

    /// <summary>Changes the caption; returns <c>true</c> when a new animation was started.</summary>
    public bool Update(string? text, double enterShift = 6)
    {
        if (Text == text)
            return false;

        Text = text;
        if (string.IsNullOrEmpty(text))
        {
            _opacity.Target = 0;
        }
        else
        {
            _opacity.SnapTo(0);
            _shift.SnapTo(enterShift);
            _opacity.Target = 1;
            _shift.Target = 0;
        }

        return true;
    }

    public bool Advance(double deltaSeconds) => _opacity.Advance(deltaSeconds) | _shift.Advance(deltaSeconds);

    /// <summary>Draws the caption horizontally centred at <paramref name="centerX"/>.</summary>
    public void Draw(DrawingContext context, double centerX, double top, IBrush brush, double fontSize = 12, double maxWidth = 0)
    {
        if (string.IsNullOrEmpty(Text) || _opacity.Value <= 0.01)
            return;

        var formatted = new FormattedText(
            Text!,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Cascadia Mono, Consolas", FontStyle.Normal, FontWeight.SemiBold),
            fontSize,
            brush);

        if (maxWidth > 20)
            formatted.MaxTextWidth = maxWidth;

        using (context.PushOpacity(_opacity.Value))
        using (context.PushTransform(Matrix.CreateTranslation(0, _shift.Value)))
            context.DrawText(formatted, new Point(centerX - formatted.Width / 2, top));
    }
}
