# SimHub plugin SDK

**Last updated:** 2026-09-28
**Confidence:** interfaces, lifecycle, property attachment, settings persistence and project
references verified from the portable SDK demo; not yet built against. The first-start prompt and
the settings backups were read out of the decompiled 9.12.6 `SimHub.Plugins.dll` and seen on the
VM (#475).

## Shape of a plugin

A SimHub plugin is a .NET Framework 4.8 class library dropped into the SimHub install
directory. SimHub asks once, at the next start, whether to enable a newly found DLL, in the prompt
described below. The plugin class is decorated with `[PluginName]`, `[PluginAuthor]` and
`[PluginDescription]` and implements the following interfaces.

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
to the WPF assemblies of the framework. OpenDash commits those files under `plugin/lib/` so
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

One could think that deleting that file resets a plugin. In reality SimHub brings it back.
`ReadCommonSettings` reads `PluginsData\Common\<class>.<key>.json` through
`JsonExtensions.FromJsonFileWithVersionning(path, 5, null)`, and `SaveCommonSettings` writes it
through `JsonExtensions.ToJsonFileWithVersionning(value, path, 10, ...)`; both live in
`WoteverCommon.dll`, which is not in `plugin/lib` and was not decompiled, so what the versioning
does was measured instead. On the VM, with SimHub 9.12.6 during the #459 walk-through, SimHub kept
earlier versions of `OpenDash.GeneralSettings.json` in `PluginsData\Common\_Backups` as
`OpenDash.GeneralSettings_b*.json`, and at the first start after the file itself had been deleted
it restored the file from them, so that the rig it held came back. Resetting a plugin's settings
thus means deleting those copies as well, which is what `plugin/INSTALL.md` says under Uninstall.

## Lifecycle

`PluginManager` is injected before `Init()`, which runs once at startup. `DataUpdate()` runs
every frame at 60 Hz and must not allocate or loop; a plugin that does not need telemetry can
omit `IDataPlugin` altogether. `End()` runs at shutdown and is where settings are saved.

## The first-start prompt (2026-09-28, #475)

SimHub asks about a plugin it has not seen before in a window titled "New plugins have been
detected !". Each plugin is a row carrying its name, its description, "Author : " followed by its
`[PluginAuthor]`, and an enable switch at the right, which is off. Once that switch is on, a second
one, "Show in left main menu", appears under the description, and it is off as well. Pressing
**Ok** with only the first switch on puts the plugin under the left menu's **Additional plugins**,
as a tab named after it; with both on, the plugin has an entry of its own in the left menu. In
neither case does SimHub ask to restart, and the plugin's page works at once. The choice is kept in
`PluginsData\PluginsActivation.json` as `IsEnabled` and `ShowInMainMenu`, which is how
`scripts/vm.ts` activates a plugin without the prompt. All of this was seen on the VM with SimHub
9.12.6, in the #459 walk-through.

**The description line is the class's `[PluginDescription]`.** `PluginFinder.GetDescription`, and
`GetTranslatableDescription` beside it, read it as follows.

```csharp
string value = SLoc.GetValue("PluginDescription_" + pluginType.Name, GetDescriptionInternal(pluginType));
```

`GetDescriptionInternal` returns the `[PluginDescription]` of the class, or the misspelled
`[PluginDescrition]` that SimHub still honours, or null. The key is looked up in SimHub's own
translations first, and the attribute is the default handed to that lookup; handed null, the lookup
returned the key itself, which is why OpenDash's prompt read `PluginDescription_OpenDash` while the
class carried no such attribute. The name is read the same way, through `PluginName_` and
`[PluginName]`. SimHub's own plugins use the same attribute, as in
`[PluginDescription("Shows lap history and statistics.")]` on the Statistics plugin.

**Whether the plugin is in the left menu is the driver's choice, and nothing a plugin declares
presets it.** SimHub's own left-menu entries, Dash Studio and Statistics among them, implement
`IMainMenu`, which adds `Priority` and `IconName` to `IWPFSettingsV2`, and those two carry
`[AutoEnablebyDefaultPlugin]` as well. Both are `internal` to `SimHub.Plugins.dll`, whose
`InternalsVisibleTo` names only `SimhubWPF` and SimHub's own plugin assemblies, so a plugin built
elsewhere can neither implement the one nor apply the other. The public surface has nothing about
the menu either: the public attributes are `PluginName`, `PluginAuthor`, `PluginDescription`,
`PluginDescrition`, `PluginDependsOn` and `SystemInfosProviderName`, and `IWPFSettingsV2`'s
`LeftMenuTitle` and `PictureIcon` are the title and the icon of an entry, not whether there is one.
The prompt itself is in `SimHubWPF.exe`, which is not in `plugin/lib` and has not been decompiled;
that it reads nothing else from a plugin type is therefore an inference from the assembly a plugin
compiles against, not a reading of the prompt's own code. The one public interface of a menu-like shape, `IMainTab`, which adds `Priority` to
`IWPFSettings`, is implemented by nothing in `SimHub.Plugins.dll`, and it is what to look for when
`SimHubWPF.exe` is decompiled. Writing `ShowInMainMenu` into `PluginsActivation.json` from the
plugin would overrule the choice the driver has just made in the prompt, and OpenDash does not do
it; `plugin/INSTALL.md` asks for the second switch instead.

## The OpenDash plugin

One class, `OpenDash`, implementing `IPlugin` and `IWPFSettingsV2`. It does not implement
`IDataPlugin`, and if it ever needs `DataUpdate()` that is a sign scope has crept.

On `Init` it reads the settings, then compares the `DashboardVersion` in the embedded package's
`.metadata` with the one under `DashTemplates/OpenDash/`, and extracts the embedded
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
profile is a plugin property. OpenDash's plugin is the first step on the same path, with the
difference that the dashboard remains usable without it.

## Sources

- [Using the SimHub SDK](https://www.simhubdash.com/community-2/projects/using-the-simhub-sdk/)
- [blekenbleu/SimHubPluginSdk](https://github.com/blekenbleu/SimHubPluginSdk), portable SDK demo
- [SimHub wiki](https://github.com/SHWotever/SimHub/wiki)
