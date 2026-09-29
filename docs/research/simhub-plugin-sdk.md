# SimHub plugin SDK

**Last updated:** 2026-09-29
**Confidence:** interfaces, lifecycle, property attachment, settings persistence and project
references verified from the portable SDK demo; not yet built against. The first-start prompt and
the settings backups were read out of the decompiled 9.12.6 `SimHub.Plugins.dll`, `SimHubWPF.exe`,
`WoteverLocalization.dll` and `WoteverCommon.dll`, and seen on the VM (#475).

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

One could think that deleting that file resets a plugin. In reality the plugin reads it back from
a copy. `ReadCommonSettings` reads `PluginsData\Common\<class>.<key>.json` through
`JsonExtensions.FromJsonFileWithVersionning(path, 5, null)` in `WoteverCommon.dll`, which tries the
file itself and then its copies `_b1` to `_b5` in `PluginsData\Common\_Backups`, as in
`OpenDash.GeneralSettings_b1.json`, and returns the first one it can read. `SaveCommonSettings`
writes through `ToJsonFileWithVersionning(value, path, 10, ...)`, which leaves the file alone when
its content has not changed and otherwise shifts the copies up by one, up to `_b10`, before moving
the file to `_b1` and writing the new one. A deleted settings file is therefore read from `_b1` at
the next start and written back at the plugin's next save, which OpenDash makes in `Init`. On the
VM, with SimHub 9.12.6 and the #475 branch, deleting only `OpenDash.GeneralSettings.json` brought a
rig of one screen back at the next start, whereas deleting it together with every
`OpenDash.GeneralSettings_b*.json` left the Rig tab empty. Resetting a plugin's settings thus means
deleting those copies as well, which is what `plugin/INSTALL.md` says under Uninstall.

## Lifecycle

`PluginManager` is injected before `Init()`, which runs once at startup. `DataUpdate()` runs
every frame at 60 Hz and must not allocate or loop; a plugin that does not need telemetry can
omit `IDataPlugin` altogether. `End()` runs at shutdown and is where settings are saved.

## The first-start prompt (2026-09-28, #475)

SimHub asks about a plugin it has not seen before in a window titled "New plugins have been
detected !", which is `SimHubWPF.Controls.NewPlugins`. Each plugin is a row carrying its name, its
description, "Author : " followed by its `[PluginAuthor]`, and an enable switch at the right, which
is off. Once that switch is on, a second one, "Show in left main menu", appears under the
description, and it is off as well. Pressing **Ok** with only the first switch on puts the plugin
under the left menu's **Additional plugins**, as a tab named after it; with both on, the plugin has
an entry of its own in the left menu. In neither case does SimHub ask to restart, since `MainWindow`
answers **Ok** by loading the current game's plugins again, and the plugin's page works at once.
The choice is kept in `PluginsData\PluginsActivation.json` as `IsEnabled` and `ShowInMainMenu`,
which is how `scripts/vm.ts` activates a plugin without the prompt. The same two switches are under
**Add/remove features**, at the foot of the left menu, where switching the second on later moves
the plugin into the left menu at once. SimHub's Settings window, on the other hand, has no Plugins
tab in 9.12.6, although the prompt's last line sends the reader to "the simhub settings". All of
this was seen on the VM with SimHub 9.12.6, in the #459 walk-through and again on 2026-09-29 with
the #475 branch.

Which plugins the prompt lists is decided by `MainWindow` as it starts. A plugin is new when no
entry of `PluginsActivation.json` carries its full class name or the last segment of it, so that a
class named `OpenDash` in any namespace counts as known. A new plugin marked
`[AutoEnablebyDefaultPlugin]` is enabled without being asked about, one marked
`[NoAutoEnablePlugin]` is neither enabled nor listed, and every other one is listed with its switch
off. SimHub writes the
file back from the plugins it found, so that a start without the DLL drops the plugin's entry, and a
plugin that is removed and later put back is asked about again, which the VM showed.

**The description line is the class's `[PluginDescription]`.** `PluginFinder.GetDescription`, and
`GetTranslatableDescription` beside it, read it as follows.

```csharp
string value = SLoc.GetValue("PluginDescription_" + pluginType.Name, GetDescriptionInternal(pluginType));
```

`GetDescriptionInternal` returns the `[PluginDescription]` of the class, or the misspelled
`[PluginDescrition]` that SimHub still honours, or null. The key is looked up in SimHub's own
translations first, and the attribute is the default handed to that lookup. `SLoc.GetValue` and the
`TranslatableString` the prompt draws, both in `WoteverLocalization.dll`, end in
`GetValue(key, defaultText) ?? defaultText ?? key`, so a key SimHub has no translation for is drawn
as the default, and as the key itself when the default is null. That is why OpenDash's prompt read
`PluginDescription_OpenDash` while the class carried no such attribute; with it, the prompt and the
Add/remove features list both draw the sentence, which the VM showed. The name is read the same
way, through `PluginName_` and `[PluginName]`. SimHub's own plugins use the same attribute, as in
`[PluginDescription("Shows lap history and statistics.")]` on the Statistics plugin.

**Whether a plugin starts in the left menu is decided by an interface that a plugin built elsewhere
cannot implement.** For every plugin it finds, `MainWindow` sets both the switch and the default
that the "Restore default menu state" link of Add/remove features returns to from one test, and
from nothing else.

```csharp
item.ShowInMainMenuDefault = typeof(IMainMenu).IsAssignableFrom(item.Type);
```

`IMainMenu`, which adds `Priority` and `IconName` to `IWPFSettingsV2`, is what SimHub's own
left-menu entries implement, Dash Studio and Statistics among them. It is `internal` to
`SimHub.Plugins.dll`, whose `InternalsVisibleTo` names only `SimhubWPF` and SimHub's own plugin
assemblies, so the compiler refuses a class of another assembly that implements it; the only way
around that would be to take the name of one of SimHub's own assemblies, which is no contract at
all and not something OpenDash does. `[AutoEnablebyDefaultPlugin]` is internal as well, and it only
enables. The public surface has nothing about the menu: the public attributes are `PluginName`,
`PluginAuthor`, `PluginDescription`, `PluginDescrition`, `PluginDependsOn` and
`SystemInfosProviderName`, the `LeftMenuTitle` and `PictureIcon` of `IWPFSettingsV2` are the title
and the icon of an entry rather than whether there is one, and the public `IMainTab` is read by
nothing in `SimHubWPF.exe`. The second switch itself is drawn for any plugin that implements
`IWPFSettings` (`CanToggleMainMenu`). Writing `ShowInMainMenu` into `PluginsActivation.json` from
the plugin would overrule the choice the driver has just made in the prompt, and OpenDash does not
do it; `plugin/INSTALL.md` asks for the second switch instead.

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
