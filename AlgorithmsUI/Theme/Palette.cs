using System.Reflection;
using Avalonia.Controls;
using Avalonia.Media;

namespace AlgorithmsUI.Theme;

/// <summary>
/// The only place where colors are defined. XAML uses them as <c>{DynamicResource NameBrush}</c>,
/// custom-drawn controls use the fields directly.
/// Base: "Developer Tool / IDE" dark slate palette with a blue focus accent.
/// </summary>
public static class Palette
{
    // Surfaces, from back to front.
    public static readonly Color Sidebar = Color.Parse("#0B1222");
    public static readonly Color Background = Color.Parse("#0F172A");
    public static readonly Color Surface = Color.Parse("#1B2336");
    public static readonly Color SurfaceRaised = Color.Parse("#272F42");
    public static readonly Color Canvas = Color.Parse("#0B1120");
    public static readonly Color Border = Color.Parse("#2A3447");
    public static readonly Color BorderStrong = Color.Parse("#64748B");

    // Text. MutedForeground keeps at least 4.5:1 contrast on every surface above.
    public static readonly Color Foreground = Color.Parse("#F1F5F9");
    public static readonly Color MutedForeground = Color.Parse("#94A3B8");
    public static readonly Color OnHighlight = Color.Parse("#0F172A");

    // Interactive accent (buttons, focus, progress).
    public static readonly Color Accent = Color.Parse("#2563EB");

    // Visualization roles, shared by every canvas so a color means the same thing on every tab.
    public static readonly Color VizActive = Color.Parse("#60A5FA");
    public static readonly Color VizCompare = Color.Parse("#FBBF24");
    public static readonly Color VizWrite = Color.Parse("#FB923C");
    public static readonly Color VizDone = Color.Parse("#4ADE80");
    public static readonly Color VizDoneSurface = Color.Parse("#14332A");
    public static readonly Color VizIdle = Color.Parse("#334155");
    public static readonly Color VizIdleSurface = Color.Parse("#1E293B");
    public static readonly Color VizText = Color.Parse("#E2E8F0");

    public static IBrush Brush(Color color) => new SolidColorBrush(color);

    /// <summary>Adds every color as <c>NameColor</c> and <c>NameBrush</c> resources.</summary>
    public static void Register(IResourceDictionary resources)
    {
        foreach (var field in typeof(Palette).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.GetValue(null) is not Color color)
                continue;
            resources[field.Name + "Color"] = color;
            resources[field.Name + "Brush"] = new SolidColorBrush(color);
        }
    }
}
