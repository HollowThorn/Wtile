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
using Windows.Win32.UI.WindowsAndMessaging;
using Wtile.Bar;
using Wtile.Commands;
using Wtile.Config;
using Wtile.Core;
using Wtile.Hotkeys;
using Wtile.Layouts;

Console.WriteLine("Wtile starting...");

string configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Wtile");
string configPath = Path.Combine(configDir, "config.yaml");
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
manager.SetTaskbarHidden(initial.Config.General.HideTaskbarOnStartup);
manager.SetHideTitlebars(initial.Config.General.HideTitlebars);
CommandRegistry commands = BuiltinCommands.CreateDefault(manager);
using var tracker = new WinEventTracker();

// One bar per monitor, each showing that monitor's own tags/layout -- config (colors/font/
// segments) is shared, but each bar reads its own monitor's live state (see BarWindow ctor).
var bars = new List<BarWindow>();
for (int i = 0; i < manager.Monitors.Count; i++)
    bars.Add(new BarWindow(manager, commands, initial.Config.Bar, manager.Monitors[i], i));

using var hotkeys = new HotkeyManager(commands);
hotkeys.ApplyBindings(initial.Config.Hotkeys);

var applier = new ConfigApplier(manager, bars, hotkeys);
commands.Register(new ReloadCommand(configPath, applier, bars));

manager.Seed();
manager.OnForegroundChanged(PInvoke.GetForegroundWindow()); // seed initial title; the hook only fires on subsequent changes
Console.WriteLine($"Tracking {manager.Windows.Count} window(s). Config: {configPath}. Waiting for events...");

while (true)
{
    int result = PInvoke.GetMessage(out MSG msg, HWND.Null, 0, 0);
    if (result <= 0)
        break; // 0 = WM_QUIT, -1 = error
    PInvoke.TranslateMessage(msg);
    PInvoke.DispatchMessage(msg);
}

manager.SetHideTitlebars(false); // give windows their decorations back before we stop managing them

foreach (BarWindow bar in bars)
    bar.Dispose();

static ConfigLoadResult LoadAndReport(string path)
{
    ConfigLoadResult result = ConfigLoader.LoadFromFile(path);
    foreach (string warning in result.Warnings)
        Console.WriteLine($"[config] warning: {warning}");
    return result;
}
