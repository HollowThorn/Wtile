namespace Wtile.Core;

/// <summary>Formats a bytes/sec rate as a short human string (1024-based), e.g. "512B/s", "1.2MB/s"
/// -- spelled out as bytes, not just a bare one-letter unit, so it can't read as megabits/sec at
/// a glance.</summary>
public static class ByteRateFormatter
{
    private const double Kilo = 1024;
    private const double Mega = Kilo * 1024;
    private const double Giga = Mega * 1024;

    public static string Format(double bytesPerSecond)
    {
        double b = Math.Max(0, bytesPerSecond);
        if (b < Kilo)
            return $"{(int)b}B/s";
        if (b < Mega)
            return $"{b / Kilo:0.0}KB/s";
        if (b < Giga)
            return $"{b / Mega:0.0}MB/s";
        return $"{b / Giga:0.0}GB/s";
    }
}
