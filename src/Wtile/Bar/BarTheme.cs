using System.Drawing;
using Wtile.Config;

namespace Wtile.Bar;

/// <summary>
/// Mutable color palette shared by the bar and its segments (they hold a reference to the same
/// instance). <see cref="Apply"/> rebuilds it in place on config reload -- segments just read
/// live properties at draw time, no rewiring needed.
/// </summary>
internal sealed class BarTheme
{
    public Color Background { get; private set; }
    public Color Foreground { get; private set; }
    public Color ActiveTag { get; private set; }
    public Color UrgentTag { get; private set; }
    public Color InactiveTagBackground { get; private set; }
    public Color EmptyTagForeground { get; private set; }

    public BarTheme(BarColorsConfig config) => Apply(config);

    public void Apply(BarColorsConfig config)
    {
        Background = ParseOr(config.Background, ColorTranslator.FromHtml("#1e1e2e"));
        Foreground = ParseOr(config.Foreground, ColorTranslator.FromHtml("#cdd6f4"));
        ActiveTag = ParseOr(config.ActiveTag, ColorTranslator.FromHtml("#89b4fa"));
        UrgentTag = ParseOr(config.UrgentTag, ColorTranslator.FromHtml("#f38ba8"));
        // occupiedTag defaults to "" (empty), which ParseOr treats as "use the fallback" -- a
        // derived blend that stays readable against whatever background/foreground is picked.
        // Only actually drawn when bar.occupiedTagIndicator is "background" (see TagsSegment).
        InactiveTagBackground = ParseOr(config.OccupiedTag, Blend(Background, Foreground, 0.15));
        EmptyTagForeground = Blend(Foreground, Background, 0.5);
    }

    private static Color ParseOr(string html, Color fallback)
    {
        try
        {
            return string.IsNullOrWhiteSpace(html) ? fallback : ColorTranslator.FromHtml(html);
        }
        catch (Exception)
        {
            return fallback;
        }
    }

    private static Color Blend(Color a, Color b, double t) => Color.FromArgb(
        (int)(a.R + (b.R - a.R) * t),
        (int)(a.G + (b.G - a.G) * t),
        (int)(a.B + (b.B - a.B) * t));
}
