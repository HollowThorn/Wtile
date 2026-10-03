// Hotkeys are global (low-level keyboard hook, see Hotkeys/HotkeyManager.cs), dispatch through
// the same CommandRegistry the bar's tag clicks use, and come from hotkeys: in
// %APPDATA%\Wtile\config.yaml. Config is NOT watched live -- there's no background
// FileSystemWatcher -- instead press the "reload" hotkey (Win+Shift+R by default) after editing
// config.yaml, or after connecting/disconnecting a monitor (it also unconditionally refreshes
// bar geometry against current screen metrics, independent of whether the file changed).
// Per-tag layout state (nmaster/mfact/gap) is independent per tag, like dwm/bug.n.

using System.Diagnostics;
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
// launched from a terminal) restores that output there without ever creating a console of our
// own -- gated below on -d/--debug so a plain terminal launch stays quiet.
bool launchedFromTerminal = PInvoke.AttachConsole(PInvoke.ATTACH_PARENT_PROCESS);
bool debug = args.Contains("-d") || args.Contains("--debug");

// Checked before anything else (no hooks/COM/windows touched yet) so `-v`/`--version` is a cheap,
// side-effect-free way to prove which build an exe at some install path actually is -- see
// GenerateBuildInfo in Wtile.csproj for how GitCommit/BuildTimeUtc get embedded at build time.
if (args is ["-v" or "--version"])
{
    Console.WriteLine($"Wtile {Wtile.BuildInfo.GitCommit} (built {Wtile.BuildInfo.BuildTimeUtc} UTC)");
    return;
}

// PowerShell (unlike cmd.exe, which checks the PE subsystem and doesn't wait for GUI-subsystem
// exes) blocks the prompt until a directly-invoked process exits -- so `wtile` from a terminal
// would otherwise hold that terminal hostage for as long as Wtile keeps running, since Wtile never
// opens a window of its own to Alt-F4 away from. Sidestep it the way any Windows background tool
// does: relaunch as a fresh, independent process the shell was never waiting on, and let this
// short-lived one exit immediately. Only relevant when actually launched from a terminal
// (launchedFromTerminal -- Explorer/shortcut/Startup never blocks anything) and only when the
// user hasn't asked to watch it live with -d/--debug, which implies staying attached is fine.
if (launchedFromTerminal && !debug)
{
    PInvoke.FreeConsole();
    var relaunch = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = true };
    foreach (string a in args)
        relaunch.ArgumentList.Add(a);
    Process.Start(relaunch);
    return;
}

// Nothing stops a second `wtile` launching alongside a first, and two would each install their
// own keyboard hook, tray icon, and bars, and fight over the same windows. A named mutex
// (unprefixed, so it lives in this session's own namespace -- each interactive user session gets
// its own, same as the tray icon/hotkeys already implicitly do) is the standard single-instance
// idiom: held for the whole process lifetime via the `using` below, so it's released
// automatically on exit, clean or not, rather than needing its own cleanup path alongside
// CleanUpForExit.
using var singleInstanceMutex = new Mutex(initiallyOwned: true, "Wtile-SingleInstance", out bool isFirstInstance);
if (!isFirstInstance)
{
    Console.WriteLine("Wtile is already running.");
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

using var tray = new TrayIcon(commands, manager);

// Crash-safety net: keeps state.json fresh (debounced) during activity, not just at quit/reload,
// so TryRecoverHidden has something recent to work with after an unclean exit -- see
// WindowManager.ScheduleSafetySave. Always on, independent of general.rememberState (which only
// controls whether ApplySavedState restores full tag/monitor/floating/pinned placement below).
manager.Changed += manager.ScheduleSafetySave;
manager.SafetySaveRequested += () => WindowStateStore.Save(statePath, manager.CaptureState());

bool cleanedUp = false;

// Un-hides whatever Wtile is currently keeping SW_HIDE'd for a tag switch and saves state.json,
// same as a normal "quit". Idempotent and safe to call more than once (WM_QUERYENDSESSION can be
// delivered once per top-level window, and a crash could reach both the exception handler and the
// normal end of Main). This is a best-effort safety net for the cases a graceful "quit" command
// can't reach on its own: an unhandled exception, or a logoff/shutdown/restart ending the session.
// It does NOT and cannot cover Task Manager's "End Task" against an unresponsive process -- that's
// a raw TerminateProcess, which runs no cleanup code in any process, on any OS, ever; the real
// defense there is not freezing in the first place (see WindowManager.OnWindowShown's stale-event
// guard) plus WindowManager.TryRecoverHidden re-adopting anything still left stranded at next launch.
void CleanUpForExit()
{
    if (cleanedUp)
        return;
    cleanedUp = true;

    WindowStateStore.Save(statePath, manager.CaptureState()); // always -- see the crash-safety note above

    manager.RestoreAllWindows(); // give windows on other tags back before we stop managing them
    manager.SetHideTitlebars(false); // give windows their decorations back before we stop managing them
    TaskbarController.SetVisible(true); // give the real taskbar back before we stop managing it
}

AppDomain.CurrentDomain.UnhandledException += (_, _) => CleanUpForExit();
AppDomain.CurrentDomain.ProcessExit += (_, _) => CleanUpForExit();

// Loaded before Seed() (and threaded through it) rather than after: a window state.json still
// remembers but that's currently OS-hidden or titlebar-stripped from an earlier run that didn't
// exit cleanly needs to be recovered as part of Seed()'s own enumeration -- see
// WindowManager.TryRecoverHidden. Always attempted, independent of rememberState, since that
// recovery is a correctness safety net, not the placement-memory preference rememberState
// controls -- only applying the rest of savedState (tag/monitor/floating/pinned) below stays
// gated on it.
SavedState? savedState = WindowStateStore.TryLoad(statePath, out SavedState loaded) ? loaded : null;
manager.Seed(savedState);
if (savedState is not null && manager.RememberState)
    manager.ApplySavedState(savedState);
manager.OnForegroundChanged(PInvoke.GetForegroundWindow()); // seed initial title; the hook only fires on subsequent changes
Console.WriteLine($"Tracking {manager.Windows.Count} window(s). Config: {configPath}. Waiting for events...");

while (true)
{
    int result = PInvoke.GetMessage(out MSG msg, HWND.Null, 0, 0);
    if (result <= 0)
        break; // 0 = WM_QUIT, -1 = error

    // The session ending may never post WM_QUIT at all, so act as soon as Windows announces it
    // rather than waiting for one -- still translate/dispatch the message itself afterwards so
    // DefWindowProc's default (allow the session to end) still runs.
    if (msg.message == PInvoke.WM_QUERYENDSESSION || msg.message == PInvoke.WM_ENDSESSION)
        CleanUpForExit();

    PInvoke.TranslateMessage(msg);
    PInvoke.DispatchMessage(msg);
}

CleanUpForExit();

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
