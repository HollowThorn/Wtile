using System.Runtime.InteropServices.ComTypes;
using Windows.Win32;
using Windows.Win32.NetworkManagement.IpHelper;
using Windows.Win32.System.Power;
using Windows.Win32.System.SystemInformation;

namespace Wtile.Core;

/// <summary>
/// Shared Win32 polling for the status-bar's system-stat segments (cpu/memory/battery/network).
/// Static and process-lifetime-scoped (mirrors WindowInspector) rather than per-segment: every
/// monitor's bar has its own segment instances, recreated on every config reload, but they all
/// read the same one set of "last sample" state here -- so reload doesn't reset delta tracking,
/// and multiple monitors don't each independently re-poll and jitter against each other.
/// </summary>
internal static unsafe class SystemStats
{
    private const long MinSampleIntervalMs = 500;

    public readonly record struct BatteryStatus(bool HasBattery, int Percent, bool IsCharging, bool IsOnAc);
    public readonly record struct NetworkRate(double DownBytesPerSec, double UpBytesPerSec);

    // --- CPU ---
    private static ulong _lastIdle, _lastKernel, _lastUser;
    private static long _lastCpuSampleTicks = -1;
    private static double _lastCpuPercent;

    /// <summary>0-100. Throttled to one real sample per MinSampleIntervalMs -- Measure and Draw
    /// both call this within the same repaint, and repaints also fire off manager.Changed (focus/
    /// tag/window events, which can burst faster than 1Hz); without throttling, in-repaint calls
    /// would diff microseconds apart (~0%) and bursts would resample far more than once a second.</summary>
    public static double GetCpuPercent()
    {
        long now = Environment.TickCount64;
        if (_lastCpuSampleTicks >= 0 && now - _lastCpuSampleTicks < MinSampleIntervalMs)
            return _lastCpuPercent;

        if (!PInvoke.GetSystemTimes(out FILETIME idleFt, out FILETIME kernelFt, out FILETIME userFt))
            return _lastCpuPercent;

        ulong idle = ToUInt64(idleFt);
        ulong kernel = ToUInt64(kernelFt); // includes idle time
        ulong user = ToUInt64(userFt);

        if (_lastCpuSampleTicks >= 0)
        {
            ulong idleDelta = idle - _lastIdle;
            ulong sysDelta = (kernel - _lastKernel) + (user - _lastUser);
            _lastCpuPercent = sysDelta == 0 ? 0 : 100.0 * (sysDelta - idleDelta) / sysDelta;
        }
        // else: first-ever sample, no prior baseline to diff against -- leave _lastCpuPercent at
        // its default 0; the very next throttled sample corrects it.

        (_lastIdle, _lastKernel, _lastUser, _lastCpuSampleTicks) = (idle, kernel, user, now);
        return _lastCpuPercent;
    }

    private static ulong ToUInt64(FILETIME ft) => ((ulong)(uint)ft.dwHighDateTime << 32) | (uint)ft.dwLowDateTime;

    // --- Memory ---
    /// <summary>0-100, straight from MEMORYSTATUSEX.dwMemoryLoad -- Windows already computes this
    /// as a percent; unlike CPU/network, a single point-in-time call, no delta/throttle needed.</summary>
    public static double GetMemoryPercent()
    {
        var status = new MEMORYSTATUSEX { dwLength = (uint)sizeof(MEMORYSTATUSEX) };
        return PInvoke.GlobalMemoryStatusEx(ref status) ? status.dwMemoryLoad : 0;
    }

    // --- Battery ---
    private const byte BatteryFlagNoSystemBattery = 128;
    private const byte Unknown = 255;

    /// <summary>HasBattery is false on a desktop (no system battery) or when the status is
    /// unknown -- callers must render nothing in that case, not a garbage percent.</summary>
    public static BatteryStatus GetBattery()
    {
        if (!PInvoke.GetSystemPowerStatus(out SYSTEM_POWER_STATUS status))
            return default;
        if (status.BatteryFlag == BatteryFlagNoSystemBattery || status.BatteryFlag == Unknown
            || status.BatteryLifePercent == Unknown)
            return default;

        bool isOnAc = status.ACLineStatus == 1;
        bool isCharging = (status.BatteryFlag & 8) != 0; // SYSTEM_POWER_STATUS.BatteryFlag bit 3 = charging
        return new BatteryStatus(HasBattery: true, status.BatteryLifePercent, isCharging, isOnAc);
    }

    // --- Network ---
    private static ulong _lastRxBytes, _lastTxBytes;
    private static long _lastNetSampleTicks = -1;
    private static NetworkRate _lastNetRate;

    private const uint IfTypeSoftwareLoopback = 24;
    private const uint IfOperStatusUp = 1;

    /// <summary>Same throttle/cache reasoning as GetCpuPercent. Sums InOctets/OutOctets across
    /// every non-loopback, operationally-up adapter.</summary>
    public static NetworkRate GetNetworkRate()
    {
        long now = Environment.TickCount64;
        if (_lastNetSampleTicks >= 0 && now - _lastNetSampleTicks < MinSampleIntervalMs)
            return _lastNetRate;

        (ulong rx, ulong tx) = SampleInterfaceTotals();

        if (_lastNetSampleTicks >= 0)
        {
            double elapsedSeconds = (now - _lastNetSampleTicks) / 1000.0;
            if (elapsedSeconds > 0)
            {
                double down = (rx - _lastRxBytes) / elapsedSeconds;
                double up = (tx - _lastTxBytes) / elapsedSeconds;
                _lastNetRate = new NetworkRate(Math.Max(0, down), Math.Max(0, up));
            }
        }

        (_lastRxBytes, _lastTxBytes, _lastNetSampleTicks) = (rx, tx, now);
        return _lastNetRate;
    }

    private static (ulong Rx, ulong Tx) SampleInterfaceTotals()
    {
        MIB_IF_TABLE2* table = null;
        try
        {
            if (PInvoke.GetIfTable2(&table) != 0 || table is null)
                return (0, 0);

            ulong rx = 0, tx = 0;
            // MIB_IF_TABLE2.Table is a C trailing flexible-array member; CsWin32 projects it as a
            // VariableLengthInlineArray<T> wrapper rather than a plain array -- AsSpan(count) is
            // its intended accessor, not raw pointer arithmetic off the field.
            foreach (ref MIB_IF_ROW2 row in table->Table.AsSpan((int)table->NumEntries))
            {
                if ((uint)row.Type == IfTypeSoftwareLoopback || (uint)row.OperStatus != IfOperStatusUp)
                    continue;
                rx += row.InOctets;
                tx += row.OutOctets;
            }
            return (rx, tx);
        }
        finally
        {
            if (table is not null)
                PInvoke.FreeMibTable(table);
        }
    }
}
