namespace Wtile.Launcher;

/// <summary>One launchable thing: a PATH executable or a Start Menu shortcut.</summary>
public sealed record LauncherEntry(string DisplayName, string LaunchTarget);

/// <summary>
/// Builds the launcher's candidate list from two sources, same as typing into the Windows Run box
/// (PATH) plus what the Start Menu itself would find (Start Menu shortcuts). Rebuilt fresh on
/// every launcher open (see LauncherWindow.Show) rather than cached -- plain filesystem
/// enumeration over a few dozen PATH directories and a few hundred shortcuts is cheap enough not
/// to bother with staleness tracking.
/// </summary>
internal static class AppCatalog
{
    private static readonly string[] DefaultPathExtensions = [".COM", ".EXE", ".BAT", ".CMD"];

    public static List<LauncherEntry> Build()
    {
        var entries = new List<LauncherEntry>();
        AddPathExecutables(entries);
        AddStartMenuShortcuts(entries);

        return [.. entries
            .DistinctBy(e => (e.DisplayName, e.LaunchTarget), EntryComparer.Instance)
            .OrderBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase)];
    }

    private static void AddPathExecutables(List<LauncherEntry> entries)
    {
        string? pathVar = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathVar))
            return;

        string[] extensions = (Environment.GetEnvironmentVariable("PATHEXT") ?? "")
            .Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (extensions.Length == 0)
            extensions = DefaultPathExtensions;

        foreach (string dir in pathVar.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                if (!Directory.Exists(dir))
                    continue;
                foreach (string file in Directory.EnumerateFiles(dir))
                {
                    string ext = Path.GetExtension(file);
                    if (extensions.Any(e => string.Equals(e, ext, StringComparison.OrdinalIgnoreCase)))
                        entries.Add(new LauncherEntry(Path.GetFileName(file), file));
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                // A stale/inaccessible PATH entry is common (removable drives, permission quirks) --
                // skip it and keep building the rest of the catalog.
            }
        }
    }

    private static void AddStartMenuShortcuts(List<LauncherEntry> entries)
    {
        AddShortcutsUnder(entries, Environment.GetFolderPath(Environment.SpecialFolder.Programs));
        AddShortcutsUnder(entries, Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms));
    }

    private static void AddShortcutsUnder(List<LauncherEntry> entries, string root)
    {
        if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
            return;

        try
        {
            foreach (string lnk in Directory.EnumerateFiles(root, "*.lnk", SearchOption.AllDirectories))
                entries.Add(new LauncherEntry(Path.GetFileNameWithoutExtension(lnk), lnk));
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            // Same defensive skip as PATH scanning above -- one inaccessible subfolder shouldn't
            // block the rest of the Start Menu tree.
        }
    }

    private sealed class EntryComparer : IEqualityComparer<(string DisplayName, string LaunchTarget)>
    {
        public static readonly EntryComparer Instance = new();

        public bool Equals((string DisplayName, string LaunchTarget) x, (string DisplayName, string LaunchTarget) y) =>
            string.Equals(x.DisplayName, y.DisplayName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.LaunchTarget, y.LaunchTarget, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string DisplayName, string LaunchTarget) obj) =>
            HashCode.Combine(
                obj.DisplayName.ToUpperInvariant(),
                obj.LaunchTarget.ToUpperInvariant());
    }
}
