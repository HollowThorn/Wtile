// Hotkeys are global (low-level keyboard hook, see Hotkeys/HotkeyManager.cs), dispatch through
// the same CommandRegistry the bar's tag clicks use, and come from hotkeys: in
// %APPDATA%\Wtile\config.yaml. Config is NOT watched live -- there's no background
// FileSystemWatcher -- instead press the "reload" hotkey (Win+Shift+R by default) after editing
// config.yaml, or after connecting/disconnecting a monitor (it also unconditionally refreshes
// bar geometry against current screen metrics, independent of whether the file changed).
// Per-tag layout state (nmaster/mfact/gap) is independent per tag, like dwm/bug.n.

using System.Reflection;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;
using Windows.Win32.UI.WindowsAndMessaging;
using Wtile.Bar;
using Wtile.Commands;
using Wtile.Config;
using Wtile.Core;
using Wtile.Hotkeys;
using Wtile.Launcher;
using Wtile.Layouts;

// Wtile is a Windows-subsystem exe (see OutputType in Wtile.csproj) so launching it from Explorer,
// a shortcut, or Startup never flashes a console -- but that also means Console.WriteLine below
// silently goes nowhere by default. Attaching to an already-running parent console (i.e. we were
// launched from a terminal, as with `-v` below) restores that output there without ever creating
// a console of our own.
PInvoke.AttachConsole(PInvoke.ATTACH_PARENT_PROCESS);

// Checked before anything else (no hooks/COM/windows touched yet) so `-v`/`--version` is a cheap,
// side-effect-free way to prove which build an exe at some install path actually is -- see
// GenerateBuildInfo in Wtile.csproj for how GitCommit/BuildTimeUtc get embedded at build time.
if (args is ["-v" or "--version"])
{
    Console.WriteLine($"Wtile {Wtile.BuildInfo.GitCommit} (built {Wtile.BuildInfo.BuildTimeUtc} UTC)");
    return;
}

Console.WriteLine("Wtile starting...");

// COM must be initialized on this thread before any IMMDeviceEnumerator/IAudioEndpointVolume use
// (see VolumeStats.cs). Cheap and safe even if the "volume" segment isn't configured.
unsafe { PInvoke.CoInitializeEx(null, COINIT.COINIT_APARTMENTTHREADED); }

string configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Wtile");
string configPath = Path.Combine(configDir, "config.yaml");
string statePath = Path.Combine(configDir, "state.json");
Directory.CreateDirectory(configDir);
if (!File.Exists(configPath))
{
    using Stream resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("Wtile.default-config.yaml")
        ?? throw new InvalidOperationException("Embedded default config resource not found.");
    using var reader = new StreamReader(resource);
    File.WriteAllText(configPath, reader.ReadToEnd());
    Console.WriteLine($"[config] No config found; wrote defaults to {configPath}");
}

ConfigLoadResult initial = LoadAndReport(configPath);
LayoutConfig masterStackConfig = initial.Config.Layouts.Find(l => l.Name == "master-stack")
    ?? new LayoutConfig { Name = "master-stack", Nmaster = 1, Mfact = 0.55, Gap = 0 };
var defaultLayoutParams = new Dictionary<string, double>
{
    ["nmaster"] = masterStackConfig.Nmaster,
    ["mfact"] = masterStackConfig.Mfact,
    ["gap"] = masterStackConfig.Gap,
};

LayoutRegistry layouts = LayoutRegistry.CreateDefault();
var manager = new WindowManager(layouts, initial.Config.General.DefaultLayout, defaultLayoutParams, initial.Config.General.TagCount);
manager.InitializeMonitors();
manager.SyncInitialTaskbarState(); // before BarWindow/Arrange: recognize an already-hidden taskbar from a previous run
manager.SetHideTitlebars(initial.Config.General.HideTitlebars);
manager.SetBlacklist(WindowRuleCompiler.CompileBlacklist(initial.Config.Blacklist));
manager.SetTagRules(WindowRuleCompiler.CompileTagRules(initial.Config.TagRules));
manager.SetRememberState(initial.Config.General.RememberState);
StartupRegistration.ApplyConfig(initial.Config.General.LaunchOnBoot);
CommandRegistry commands = BuiltinCommands.CreateDefault(manager);
using var tracker = new WinEventTracker();

// One bar per monitor, each showing that monitor's own tags/layout -- config (colors/font/
// segments) is shared, but each bar reads its own monitor's live state (see BarWindow ctor).
var bars = new List<BarWindow>();
for (int i = 0; i < manager.Monitors.Count; i++)
    bars.Add(new BarWindow(manager, commands, initial.Config.Bar, manager.Monitors[i], i));
commands.Register(new ToggleBarCommand(bars));

using var focusBorder = new FocusBorderWindow(manager, initial.Config.General.FocusedBorderWidth, initial.Config.General.FocusedBorderColor);

using var hotkeys = new HotkeyManager(commands);
hotkeys.ApplyBindings(initial.Config.Hotkeys);

using var launcher = new LauncherWindow(manager, initial.Config.Bar);
commands.Register(new AppLauncherCommand(launcher));

var applier = new ConfigApplier(manager, bars, hotkeys, focusBorder, launcher);
commands.Register(new ReloadCommand(configPath, applier, bars, manager, statePath));

using var tray = new TrayIcon(commands);

manager.Seed();
if (manager.RememberState && WindowStateStore.TryLoad(statePath, out SavedState savedState))
    manager.ApplySavedState(savedState);
manager.OnForegroundChanged(PInvoke.GetForegroundWindow()); // seed initial title; the hook only fires on subsequent changes
Console.WriteLine($"Tracking {manager.Windows.Count} window(s). Config: {configPath}. Waiting for events...");

// autostart: spawned last, once everything above is in place, so tagRules: apply to whatever these
// open and rememberState has already claimed the windows that were open before us. Goes through
// the same "spawn" command a hotkey uses. Reload never re-runs this (ConfigApplier doesn't touch
// autostart) -- editing config shouldn't relaunch your apps.
if (initial.Config.Autostart.Count > 0)
{
    manager.BeginAutostart(initial.Config.Autostart.Select(e => e.ProcessName));
    foreach (AutostartEntry entry in initial.Config.Autostart)
        commands.TryExecute("spawn", entry.Spawn);
}

while (true)
{
    int result = PInvoke.GetMessage(out MSG msg, HWND.Null, 0, 0);
    if (result <= 0)
        break; // 0 = WM_QUIT, -1 = error
    PInvoke.TranslateMessage(msg);
    PInvoke.DispatchMessage(msg);
}

if (manager.RememberState)
    WindowStateStore.Save(statePath, manager.CaptureState());

manager.RestoreAllWindows(); // give windows on other tags back before we stop managing them
manager.SetHideTitlebars(false); // give windows their decorations back before we stop managing them
TaskbarController.SetVisible(true); // give the real taskbar back before we stop managing it

foreach (BarWindow bar in bars)
    bar.Dispose();

PInvoke.CoUninitialize();

static ConfigLoadResult LoadAndReport(string path)
{
    ConfigLoadResult result = ConfigLoader.LoadFromFile(path);
    foreach (string warning in result.Warnings)
        Console.WriteLine($"[config] warning: {warning}");
    return result;
}
