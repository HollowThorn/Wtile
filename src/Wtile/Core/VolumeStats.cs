using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Media.Audio;
using Windows.Win32.Media.Audio.Endpoints;
using Windows.Win32.System.Com;

namespace Wtile.Core;

/// <summary>
/// Reads the default audio endpoint's master volume via the Core Audio API. Caches the resolved
/// IAudioEndpointVolume for the process lifetime rather than re-resolving the default device on
/// every poll -- a mid-session default-device swap (e.g. unplugging headphones) won't be picked
/// up without IMMNotificationClient, an accepted simplification for now. Requires CoInitializeEx
/// to have been called on this thread already (see Program.cs).
/// </summary>
internal static unsafe class VolumeStats
{
    private static IAudioEndpointVolume? _volume;
    private static bool _resolutionFailed;

    /// <summary>False when no default audio device could be resolved -- caller renders nothing,
    /// same "hide on absence" contract as SystemStats.GetBattery.</summary>
    public static bool TryGetVolume(out int percent, out bool muted)
    {
        percent = 0;
        muted = false;

        if (!TryResolveVolume(out IAudioEndpointVolume? volume))
            return false;

        try
        {
            volume.GetMasterVolumeLevelScalar(out float level);
            volume.GetMute(out BOOL isMuted);
            percent = (int)Math.Round(level * 100);
            muted = isMuted;
            return true;
        }
        catch (COMException)
        {
            // The cached endpoint died (e.g. device disconnected) -- drop it and try to
            // re-resolve on the next poll instead of failing permanently for the rest of the run.
            _volume = null;
            return false;
        }
    }

    private static bool TryResolveVolume([NotNullWhen(true)] out IAudioEndpointVolume? volume)
    {
        if (_volume is not null)
        {
            volume = _volume;
            return true;
        }
        if (_resolutionFailed)
        {
            volume = null;
            return false;
        }

        try
        {
            IMMDeviceEnumerator enumerator = MMDeviceEnumerator.CreateInstance<IMMDeviceEnumerator>();
            enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out IMMDevice device);

            var iid = typeof(IAudioEndpointVolume).GUID;
            device.Activate(&iid, CLSCTX.CLSCTX_ALL, null, out object volumeObj);

            _volume = (IAudioEndpointVolume)volumeObj;
            volume = _volume;
            return true;
        }
        catch (COMException)
        {
            _resolutionFailed = true; // e.g. no audio device at all -- don't retry every poll
            volume = null;
            return false;
        }
    }
}
