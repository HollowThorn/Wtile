namespace Wtile.Core;

/// <summary>Formats a bytes/sec rate as a short human string (1024-based), e.g. "512B", "1.2M".
/// slstatus-flavored brevity -- one-letter unit, no "iB".</summary>
public static class ByteRateFormatter
{
    private const double Kilo = 1024;
    private const double Mega = Kilo * 1024;
    private const double Giga = Mega * 1024;

    public static string Format(double bytesPerSecond)
    {
        double b = Math.Max(0, bytesPerSecond);
        if (b < Kilo)
            return $"{(int)b}B";
        if (b < Mega)
            return $"{b / Kilo:0.0}K";
        if (b < Giga)
            return $"{b / Mega:0.0}M";
        return $"{b / Giga:0.0}G";
    }
}
