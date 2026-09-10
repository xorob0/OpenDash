# SimHub plugin SDK

**Last updated:** 2026-09-10
**Confidence:** interfaces, lifecycle, property attachment, settings persistence and project
references verified from the portable SDK demo; not yet built against.

## Shape of a plugin

A SimHub plugin is a .NET Framework 4.8 class library dropped into the SimHub install
directory. SimHub asks once, at the next start, whether to enable a newly found DLL. The plugin
class is decorated with `[PluginName]` and `[PluginAuthor]` and implements the following
interfaces.

| Interface | Purpose |
|---|---|
| `IPlugin` | lifecycle: `Init(PluginManager)` and `End(PluginManager)` |
| `IDataPlugin` | telemetry: `DataUpdate(PluginManager, ref GameData)` at 60 Hz |
| `IWPFSettingsV2` | `GetWPFSettingsControl(PluginManager)`, `LeftMenuTitle`, `PictureIcon` |

The SDK demo, [blekenbleu/SimHubPluginSdk](https://github.com/blekenbleu/SimHubPluginSdk),
reduces to this:

```csharp
[PluginAuthor("Author")]
[PluginName("SDK plugin")]
public class Plugin : IPlugin, IDataPlugin, IWPFSettingsV2
{
    public Settings Settings;
    public PluginManager PluginManager { get; set; }
    public ImageSource PictureIcon => this.ToIcon(Properties.Resources.sdkmenuicon);
    public string LeftMenuTitle => "SDK plugin";

    public void Init(PluginManager pluginManager)
    {
        Settings = this.ReadCommonSettings<Settings>("GeneralSettings", () => new Settings());
        this.AttachDelegate("CurrentDateTime", () => DateTime.Now);
        this.AddAction("IncrementSpeedWarning", (a, b) => { /* ... */ });
    }

    public void End(PluginManager pluginManager)
    {
        this.SaveCommonSettings("GeneralSettings", Settings);
    }

    public System.Windows.Controls.Control GetWPFSettingsControl(PluginManager pluginManager)
    {
        return new SettingsControl(this);
    }

    public void DataUpdate(PluginManager pluginManager, ref GameData data) { }
}
```

The project references SimHub's own assemblies from the install directory through a
`SIMHUB_INSTALL_PATH` property: `SimHub.Plugins.dll`, `GameReaderCommon.dll`,
`SimHub.Logging.dll`, `log4net.dll`, `MahApps.Metro.dll` and `InputManagerCS.dll`, in addition
to the WPF assemblies of the framework. openDash commits those files under `plugin/lib/` so
that GitHub's Windows runner can build without a SimHub install, as many plugin repositories
do; if SimHub's author objects, the references switch back to an install path.

## Properties

`AttachDelegate(name, getter)` publishes a property that every dashboard, LED profile and
other plugin can read. The property is named after the plugin class, so a class named
`OpenDash` attaching `PositionMode` produces `[OpenDash.PositionMode]` in NCalc and
`$prop('OpenDash.PositionMode')` in JavaScript. Dotted names are allowed, and Daniel Newman
uses `DNRLEDs.Dash.RoadRightAdz`. The getter runs whenever the property is read, so a getter
that returns a field of the settings object is sufficient, and there is nothing to push.

This is verified from the dashboards themselves, where
`[DahlDesign.ShowBrakeThrottleGaugesEnabled]` and the `DNRLEDs` properties are read exactly
this way.

## Settings

`ReadCommonSettings<T>(key, factory)` and `SaveCommonSettings(key, value)` persist a plain
serialisable class under SimHub's settings folder. The settings control is a WPF `UserControl`;
SimHub ships its own styles (`SHButtonPrimary`, `SHToggleButton`, section titles) in
`SimHub.Plugins.Styles`, and using them is what makes a panel look native.

## Lifecycle

`PluginManager` is injected before `Init()`, which runs once at startup. `DataUpdate()` runs
every frame at 60 Hz and must not allocate or loop; a plugin that does not need telemetry can
omit `IDataPlugin` altogether. `End()` runs at shutdown and is where settings are saved.

## The openDash plugin

One class, `OpenDash`, implementing `IPlugin` and `IWPFSettingsV2`. It does not implement
`IDataPlugin`, and if it ever needs `DataUpdate()` that is a sign scope has crept.

On `Init` it reads the settings, then compares the `DashboardVersion` in the embedded package's
`.metadata` with the one under `DashTemplates/openDash/`, and extracts the embedded
`.simhubdash` when the installed one is missing or older. The package is an embedded resource
produced by the dash build and copied into the plugin project by CI, so that the plugin and the
dashboard of one release are always the same bytes. It then attaches one property per setting.

| Property | Getter returns |
|---|---|
| `OpenDash.ShiftLights` | `bool` |
| `OpenDash.PositionMode` | `"overall"` or `"class"` |
| `OpenDash.DeltaReference` | `"session"` or `"alltime"` |
| `OpenDash.SessionProgress` | `"auto"`, `"laps"` or `"time"` |
| `OpenDash.Slot01` to `OpenDash.Slot12` | `int`, a card number |

The settings class is a plain object with those fields and the defaults from the scope
document. The panel writes to it directly, and since the getters read the object, a change is
visible to the dashboard on the next frame, without a save or a restart.

## What the plugin does not do

It does not open the dashboard on a display, because no public API does that; the user assigns
the dashboard to a screen in SimHub as with any other. It does not check for updates online,
and it does not read telemetry or compute anything. Computed telemetry, that is fuel prediction
and stint estimates, is where a plugin genuinely earns a `DataUpdate()`, and it is post-MVP.

## Precedent

Lovely ships a plugin DLL plus installer with a Dashboard Manager that browses, installs and
updates its dashes from inside SimHub. Daniel Newman Racing goes further: its dashboards refuse
to display anything until the plugin is detected, and every option of every dashboard and LED
profile is a plugin property. openDash's plugin is the first step on the same path, with the
difference that the dashboard remains usable without it.

## Sources

- [Using the SimHub SDK](https://www.simhubdash.com/community-2/projects/using-the-simhub-sdk/)
- [blekenbleu/SimHubPluginSdk](https://github.com/blekenbleu/SimHubPluginSdk), portable SDK demo
- [SimHub wiki](https://github.com/SHWotever/SimHub/wiki)
