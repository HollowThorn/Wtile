using System.Text.Json;

namespace Wtile.Core;

/// <summary>Reads/writes state.json (which monitor/tag each window was on -- see
/// WindowManager.CaptureState/ApplySavedState). Never throws: a missing or corrupt file just
/// means "nothing to restore", logged like every other soft-failure in Core (see the
/// [filter]/[blacklist]/[manage] logs in WindowManager.TryAdd).</summary>
public static class WindowStateStore
{
    public static void Save(string path, SavedState state)
    {
        try
        {
            string json = JsonSerializer.Serialize(state, WindowStateJsonContext.Default.SavedState);
            File.WriteAllText(path, json);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"[state] Failed to save {path}: {ex.Message}");
        }
    }

    public static bool TryLoad(string path, out SavedState state)
    {
        state = new SavedState();
        if (!File.Exists(path))
            return false;

        try
        {
            SavedState? loaded = JsonSerializer.Deserialize(File.ReadAllText(path), WindowStateJsonContext.Default.SavedState);
            if (loaded is null)
                return false;
            state = loaded;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            Console.WriteLine($"[state] Failed to load {path}: {ex.Message}");
            return false;
        }
    }
}
