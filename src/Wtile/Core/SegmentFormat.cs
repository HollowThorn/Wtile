using System.Globalization;

namespace Wtile.Core;

/// <summary>Applies a user-configurable format string to segment values. Measure/Draw run on
/// every bar repaint, so a malformed format string (wrong placeholder count/syntax) must not
/// throw out of the paint path and take the bar down -- it falls back to the built-in default.</summary>
public static class SegmentFormat
{
    public static string Apply(string format, string fallback, params object[] args)
    {
        try
        {
            return string.Format(CultureInfo.InvariantCulture, format, args);
        }
        catch (FormatException)
        {
            return string.Format(CultureInfo.InvariantCulture, fallback, args);
        }
    }
}
