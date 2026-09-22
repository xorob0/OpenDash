# The SimHub dashboard format, reverse-engineered

**Last updated:** 2026-09-10
**Confidence:** structure, bindings, widgets, sidecars and packaging verified against real
exports from SimHub 9.2 to 9.12 and against the decompiled 9.12.6 model classes; the spike
items were run on 2026-09-10, see the section at the end.

SimHub's dashboard format is undocumented. What follows was established by reading real
dashboards and the community build pipelines around them, and it is split into what was
observed and what is still inferred.

Samples analysed, none of which is vendored here:

| Package | SimHub version | Base size | Notes |
|---|---|---|---|
| [Pirito10/ETS2-Dashboard](https://github.com/Pirito10/ETS2-Dashboard) | older | 1280 by 720 | Json.NET `$id` references, images inlined as base64 |
| [Blumlaut/simhub-dashes](https://github.com/Blumlaut/simhub-dashes), ADU5 and IC-7 | 9.9.5 | 1280 by 720 | no `$id`, images in `.ressources`, JavaScript and NCalc bindings |
| [andreasdahl1987/DahlDesignDash](https://github.com/andreasdahl1987/DahlDesignDash) | 9.2.6 and later | 1280 by 768 | plugin-driven visibility, colour bindings, integrated LED item |
| Daniel Newman Racing, Endurance, Race Control and Chrono | 9.11 and 9.12 | 1280 by 720, 1920 by 1080, 800 by 480 | commercial; widgets with dashboard variables, adaptive zones. Inspected locally, never to be committed. |

## Verified

### A `.simhubdash` is a zip of a folder

This is the entire packaging story. Blumlaut's release workflow is a single command per dash:

```yaml
- name: Zip Folders
  run: |
      zip -r IC-7.simhubdash IC-7/
      zip -r ADU5.simhubdash ADU5/
```

Double-clicking the file imports it into SimHub, which unpacks it under
`SimHub/DashTemplates/<name>/` and installs the bundled fonts. Our compile step is therefore
one `zip` command, and the whole project strategy rests on this fact.

### What a dashboard folder contains

```
OpenDash/
  OpenDash.djson               the scene graph
  OpenDash.djson.metadata      JSON sidecar, see below
  OpenDash.djson.ressources    a zip of the images the dashboard references (misspelt in the format itself)
  OpenDash.djson.carclasses    JSON sidecar, "[]" in every sample
  OpenDash.djson.png           gallery preview, written by DashStudio on save
  OpenDash.djson.00.png        per-screen previews
  cards.djson                  further .djson files are widgets included by the main one
  _SHFonts/                    bundled .ttf files, installed at import
  JavascriptExtensions/        optional .js files loaded into the JavaScript engine
```

The `.metadata` sidecar duplicates the `Metadata` object of the `.djson`:

```json
{
  "ScreenCount": 4.0,
  "InGameScreensIndexs": [0, 1, 2, 3],
  "IdleScreensIndexs": [0, 1, 2, 3],
  "PitScreensIndexs": [0, 1, 2, 3],
  "MainPreviewIndex": 0,
  "IsOverlay": false,
  "OverlaySizeWarning": true,
  "MetadataVersion": 2.0,
  "EnableOnDashboardMessaging": true,
  "SimHubVersion": "9.9.5",
  "Category": null,
  "Title": "ECUMaster ADU5",
  "Description": "",
  "Author": "blumlaut & T0rqu3_",
  "Width": 1280.0,
  "Height": 720.0,
  "DashboardVersion": ""
}
```

`DashboardVersion` is the field the plugin compares in order to decide whether to reinstall.

### The `.djson` is plain JSON, and modern exports carry no reference tracking

Exports from SimHub 9.9 and later have no `$id` or `$ref` keys and no `$values` wrappers;
`Screens`, `Items` and `Childrens` are plain arrays. Older exports used Json.NET reference
tracking, and SimHub still loads them, so both forms are accepted and the generator emits the
modern one. Top-level keys of a modern export:

```
Variables, Version, Id, BaseHeight, BaseWidth, BackgroundColor, Screens, SnapToGrid,
HideLabels, ShowForeground, ForegroundOpacity, ShowBackground, BackgroundOpacity,
ShowBoundingRectangles, GridSize, Images, Metadata, ShowOnScreenControls, IsOverlay,
EnableClickThroughOverlay, EnableOnDashboardMessaging, UseStrictJSIsolation,
UseStrictJSIsolationWarning
```

`Version` is `2` in every sample. `BaseWidth` and `BaseHeight` define the target resolution;
SimHub scales the dashboard to whatever display it is assigned to, which is why a new size is a
new layout rather than a new file format.

Modern exports omit properties that hold their default value. A `TextItem` from SimHub 9.9.5 is
twenty keys long where the old format wrote forty:

```json
{
  "$type": "SimHub.Plugins.OutputPlugins.GraphicalDash.Models.TextItem, SimHub.Plugins",
  "IsTextItem": true,
  "Font": "Sui Generis", "FontWeight": "Thin", "FontStyle": "Italic", "FontSize": 58.0,
  "Text": "0:00.0", "TextColor": "#FF000000",
  "HorizontalAlignment": 0, "VerticalAlignment": 0,
  "BackgroundColor": "#00FFFFFF",
  "Left": 76.0, "Top": 636.0, "Width": 300.0, "Height": 81.0,
  "Visible": true, "BlinkPhasisInverted": false, "IsFreezed": true,
  "Name": "LAPT", "RenderingSkip": 10, "MinimumRefreshIntervalMS": 100.0
}
```

Type discriminators are Json.NET `$type` strings naming internal SimHub classes, and Json.NET
only honours `$type` when it is the first key of an object, so the generator always writes it
first. Colours are `#AARRGGBB` with the alpha first; `#00FFFFFF` is transparent white, and
writing the alpha last is the most likely early bug. Layout is absolute through `Left`, `Top`,
`Width` and `Height`. `Id` values are GUIDs and `Name` is the label shown in the editor.

### Screens

A screen carries roles and expressions in addition to its items:

```json
{
  "Name": "Main", "ScreenId": "c0e64ce4-ee81-4cb3-a99e-278e8fd2b5db",
  "InGameScreen": true, "IdleScreen": true, "PitScreen": true,
  "AllowOverlays": true, "IsOverlayLayer": false, "IsForegroundLayer": false, "IsBackgroundLayer": false,
  "OverlayTriggerExpression": { "Expression": "" },
  "ScreenEnabledExpression": { "Expression": "" },
  "OverlayMaxDuration": 0, "OverlayMinDuration": 0,
  "BackgroundColor": "#00FFFFFF", "RenderingSkip": 0, "MinimumRefreshIntervalMS": 0.0,
  "Items": []
}
```

`ScreenEnabledExpression` decides whether a screen takes part in navigation, and the Daniel
Newman zones use it to hide the pages a user has disabled. `Items` holds the item tree, where a
`Layer` groups its children under `Childrens`.

### Node types observed

| Type | Seen in | Purpose |
|---|---|---|
| `TextItem` | all | text, the workhorse |
| `RectangleItem` | all | filled rectangle with `BorderStyle`; shift light segments, bars, rules |
| `Layer` | all | grouping, with `Childrens`, `Group`, `Repetitions` |
| `GroupItem` | DNR | container with `ChildsPositioning`, that is to say a stacking layout |
| `ImageItem` | all | image referenced by name from `Images` |
| `ImageFromFileItem` | DNR | image from a user file path |
| `WidgetItem` | Blumlaut, Dahl, DNR | includes another `.djson` by `FileName`, with dashboard variables |
| `LinearGaugeItem` | Blumlaut, DNR | bar gauge with `Minimum`, `Maximum`, `Value`, optional `GaugeImage` |
| `DialGaugeItem` | ETS2, Blumlaut | radial gauge |
| `GradientItem`, `EllipseItem`, `ButtonItem`, `ChartItem`, `RadarItem`, `GeneratedMapItem`, `ScreenCaptureItem`, `IntegratedLedItem` | DNR, Dahl | not needed by the MVP |
| `GearText`, `SpeedText` | ETS2 | legacy purpose-built text; modern dashboards use `TextItem` bound to `[Gear]` |

The MVP needs `TextItem`, `RectangleItem`, `Layer` and `WidgetItem` only.

### Bindings

This was the least understood part of the format, and it is now verified across four packages.
Every item may carry a `Bindings` object keyed by the name of the property being bound. Each
entry holds a `Formula` and a `Mode`; `Mode` 2 is a formula binding and `Mode` 4 is a colour
gradient driven by a formula. NCalc is the default interpreter, and `"Interpreter": 1` selects
JavaScript.

```json
"Bindings": {
  "Text": {
    "Formula": { "Expression": "toshorttime([CurrentLapTime], 1, 0, 1)" },
    "Mode": 2
  },
  "Visible": {
    "Formula": { "Expression": "[DahlDesign.ShowBrakeThrottleGaugesEnabled]" },
    "Mode": 2
  },
  "BackgroundColor": {
    "Formula": { "Expression": "if([PitLimiterOn]=1,'deepskyblue','yellow')" },
    "Mode": 2
  },
  "Value": {
    "Formula": {
      "JSExt": 0, "Interpreter": 1,
      "Expression": "return $prop('Rpms') / $prop('CarSettings_MaxRPM') * 100"
    },
    "Mode": 2
  }
}
```

Several details matter to the generator. Game properties are written in their short form,
`[Rpms]` rather than `[DataCorePlugin.GameData.Rpms]`, and plugin properties as
`[PluginClass.Name]`, where the prefix is the plugin's class name. A `FormatString` key may sit
next to `Formula`, and a `PreExpression` may sit inside it. Bound targets seen in the wild are
`Text`, `Visible`, `Value`, `BackgroundColor`, `TextColor`, `BorderColor`, `Left`, `Top`,
`Image`, `BlinkEnabled`, `Maximum` and `InitialScreenIndex`; `Width`, which the RPM bar needs,
is the one target no sample happened to bind, and it is item 3 of the spike.

`BorderColor` in that list is a mistake kept here because the generator was written against it.
None of SimHub's own bundled dashboards binds it, and an item-level `Bindings.BorderColor` cannot
work; the property is on the `BorderStyle` sub-object, which takes bindings of its own. The next
section is the rule that settles it.

The gradient form, `Mode` 4, maps the value of a formula onto a colour ramp:

```json
"BackgroundColor": {
  "Formula": { "Expression": "if([DahlDesign.PitToggleLF],0,1)" },
  "StartColor": "#FFFF00FF", "StartColorValue": 0.0,
  "EndColor": "#FFC0C0C0", "EndColorValue": 1.0,
  "EnableMiddleColor": false, "MiddleColor": "#FF000000", "MiddleColorValue": 1.0,
  "Mode": 4
}
```

### What a binding can target, and what silently does not (2026-09-13, #124)

The list above is what samples happened to bind. This is the rule underneath it, from
`BindingHelper`, `EditorModel.ApplyBindings` and `PropertyItemWrapper`.

The rule is decompiled. Which targets actually apply at runtime was then checked on the VM as
`OpenDash Probe` ([tools/binding-probe](../../tools/binding-probe/probe.ts)): twelve rows, each
drawing a literal that reads FAIL beside a binding that reads PASS, captured in
[media/xor-73/binding-probe.png](../../media/xor-73/binding-probe.png). Where a claim below was run
there rather than only read, it says so.

**A target is a CLR property name, resolved by reflection, once.** `BindingHelper.InitBinding` does
`item.GetType().GetProperty(propertyName)` and, when that returns null, leaves `ValueGetter` unset.
`Applybindings` returns on exactly that check. So a misspelt or non-existent target is a **silent
no-op**: the item keeps its literal, nothing is logged, and the editor shows nothing wrong. Same
failure shape as the NCalc arity trap below, and the reason `BindingTarget` in
[`packages/generator/src/model.ts`](../../packages/generator/src/model.ts) is a union rather than a
string.

**Only six types can be bound**: `string`, `int`, `double`, `bool`, `Color` and `Brush`. The set
appears in `PropertyItemWrapper`, which decides what the editor offers, and again as the branch list
in `Applybindings`. An enum property cannot be bound at all, which rules out `FontWeight`,
`HorizontalAlignment`, `VerticalAlignment` and `TextWrapping`.

**`Mode` 4 requires a `Color`.** `InitBinding` sets `AllowColorGradient` from
`p.PropertyType == typeof(Color)`, and the gradient branch of `Applybindings` maps the formula's
number through `StartColor`/`MiddleColor`/`EndColor`. `AllowText` is the same test against `string`.

**Bindings nest one level, into sub-objects.** `ApplyBindings` walks
`GetBindableProperties(item.GetType())`, which is every public property whose type implements
`IBindable` and is not itself an item, and recurses into each with the same evaluation. On a
drawable item those are `BorderStyle` and, on text, `TextPadding`; both derive from
`SubPropertyBindingBase`, and the item's `Owner` setter assigns their `Owner` and `ParentItem` as it
is set. So these are real, and were run:

```json
"BorderStyle": {
  "BorderColor": "#FFFF2D46", "BorderTop": 4, "BorderBottom": 4, "BorderLeft": 4, "BorderRight": 4,
  "Bindings": { "BorderColor": { "Formula": { "Expression": "'#FF00D96A'" }, "Mode": 2 } }
}
```

`BorderColor`, `BorderTop`/`Bottom`/`Left`/`Right`, `RadiusTopLeft` and its three siblings, and
`PaddingTop`/`Bottom`/`Left`/`Right` are all bindable **there**. On the VM a bound `BorderColor`
drew green over a red literal, a bound `BorderTop` thickened the top edge, and a bound `PaddingLeft`
moved the text. The same `BorderColor` written at item level left its box red, which is the
reflection rule above doing what it says.

**`Font` and `CharWidth` carry `[NoBinding]`, and both bind anyway.** The attribute is read in one
place, `PropertyItemWrapper`, which is the editor's property grid, and never by `ApplyBindings`. A
binding written into the JSON by hand is therefore applied: on the VM a bound `Font` redrew its text
in Courier New and a bound `CharWidth` widened the monospace cells. Treat them as unsupported all
the same. They are the only two properties on a `TextItem` SimHub marks this way, both are exactly
the properties a text box was measured from, and a behaviour that survives only because nothing
enforces the attribute is one update away from disappearing without a message.

**`ImageFromFileItem.ImagePath` is an ordinary bindable string**, unattributed, which is the path to
an image outside the package. `ImageFromUrlItem.ImageUrl` likewise. Both are read off the type
rather than run, so what SimHub does with a path that does not resolve is still unknown.

**What it costs, per frame.** `ApplyBindings` runs over the rendered screen every frame.

- `Visible` and `Repetitions` are evaluated first, and **an invisible item returns before its other
  bindings are evaluated at all**. Switching a page off is cheap.
- Every other binding of every visible item is evaluated every frame. There is no dirty tracking of
  the formula.
- The **setter** fires only when the value differs from `LastValue`, so a binding whose value is not
  moving costs an evaluation and a comparison and never touches WPF.
- A binding that throws is logged once and **muted for 30 seconds** (`binding.LastError`).
- `double.IsInfinity` on a bound number throws rather than drawing: a zero division in a bound
  `Width` is an error, not a silent zero.

### Widgets and dashboard variables

A `WidgetItem` includes another `.djson` and can pass it variables. This is how the Daniel
Newman dashboards implement their zones: one `ADZ.djson` with fifteen screens, one per page,
included once per zone, with the zone told which side it is on through a variable and the
initial screen bound to a plugin property.

```json
{
  "$type": "SimHub.Plugins.OutputPlugins.GraphicalDash.Models.WidgetItem, SimHub.Plugins",
  "Variables": {
    "DashboardVariables": [
      {
        "VariableName": "ADZSide",
        "EvaluateOnlyOnce": true,
        "OverrideWithParentDashboardVariableWhenAvailable": true,
        "ValueExpression": { "Expression": "'RightADZ'" },
        "EvaluateBeforeScreenRoles": true
      }
    ]
  },
  "FileName": "ADZ.djson",
  "InitialScreenIndex": 2,
  "FreezePageChanges": false,
  "EnableScreenRolesAndActivation": true,
  "IgnoreSavedScreensEx": true,
  "AutoSize": true,
  "Left": 925.0, "Top": 191.0, "Width": 340.0, "Height": 340.0,
  "Name": "RightADZ",
  "Bindings": {
    "InitialScreenIndex": {
      "Formula": {
        "JSExt": 0, "Interpreter": 1,
        "Expression": "return $prop('DNRLEDs.Dash.RoadRightAdz') != null ? $prop('DNRLEDs.Dash.RoadRightAdz') : 2;"
      },
      "Mode": 2
    }
  }
}
```

Inside the widget, a variable is read as `[variable.Name]` in NCalc. OpenDash's slots follow
the same pattern with NCalc expressions and without variables, since the slot property can be
bound on the `WidgetItem` itself.

`InitialScreenIndex` is not only initial, which is what makes the zone face work. Its setter
calls `OnInitialScreenIndexChanged`, which assigns `_Data.Dashboard.SelectedScreen =
GetAvailableScreenAtIndex(InitialScreenIndex)`, so a bound property that changes moves the
widget to that screen on the next frame.

**`FreezePageChanges` must stay false.** When it is true and the dash is not in design mode,
loading the widget deletes every screen but the selected one from the dashboard
(`LinqExtensions.RemoveAll` on `Model.Dashboard.Screens`). A frozen widget cannot be cycled
afterwards, because the pages are not there any more.

### `AddAction`'s release callback is discarded by the extension method (2026-09-12, #144)

`SimHub.Plugins.IPluginExtensions` is the convenient way to register an action:

```csharp
public static void AddAction<T>(this T plugin, string actionName,
    Action<PluginManager, string> actionStart, Action<PluginManager, string> actionEnd = null)
{
    PluginManager.Instance.AddAction(actionName, typeof(T), actionStart, actionEnd = null);
}
```

Read the last argument: `actionEnd = null` is an **assignment**, not a default. The release
callback a caller passes is overwritten with null before it is used, and `AddHiddenAction` does
the same thing. Nothing warns, nothing throws: the action registers, the press fires, the
release never does.

`PluginManager.AddAction(name, type, start, end)` and `PluginManager.AddAction(name, type,
start, end, hidden)` keep both callbacks, and so does `AddInputMapping(name, type, pressed,
released)` — which additionally sets `IsInput = true` and a no-op `PressFallback`, putting the
entry in SimHub's input list rather than its action list.

So an action that has to do something on release is registered through `PluginManager`
directly. OpenDash registers all five that way, the four that need no release included, so
that nobody has to remember which is which.

**And the binding needs press type `During` (3).** `TriggerInputPress` calls `ActionStart` only
for mappings whose `PressType == PressType.During`, and `TriggerInputRelease` calls `ActionEnd`
the same way. Every other press type goes through `TriggerAction`, which calls `ActionStart` and
then `ActionEnd` back to back — so a page held by a button appears and vanishes in one frame.
The binding dialog offers `ShortAndLongPress` by default, which is the wrong one.

The full enum, from `SimHub.Plugins.PressType`: `Default 0`, `ShortPress 1`, `LongPress 2`,
`During 3`, `ShortAndLongPress 4`, `Pressed 5`, `Released 6`, `LongPressNoAutoRepeat 7`.

A binding lives in `PluginsData/PluginManagerSettings.json` as
`{ "Target": "OpenDash.CycleZoneC", "Trigger": "KeyboardReaderPlugin.F9", "PressType": 4,
"GameRestriction": { "SupportedGames": [] } }`, read at startup, which is how `bun run vm bind`
writes one without the dialog.

### Images and fonts

Modern exports keep image bytes out of the `.djson`. The older ETS2 sample inlined them as base64,
where they were 87.6 percent of the file, and nothing SimHub ships today does that.

Read off `DashTemplates/AIM MXS`, which SimHub itself installs, on 2026-09-12. Eighteen of the
dashboards SimHub ships carry a non-empty `Images` list and every one of them has a `.ressources`
sidecar beside it.

A descriptor carries eight fields, two more than were recorded here before:

```json
{"Name":"Aim_MXS_Strada_Car_Dash_Display_with_Icons_1024x1024","Extension":".png",
 "Modified":false,"Optimized":false,"Width":811,"Height":807,
 "Length":194362,"MD5":"fa70de0750a27598b4ccdb3230a8d44f"}
```

`Length` is the uncompressed byte count and `MD5` is of those same bytes. `Width` and `Height` are
the image's own pixels, not the box it is drawn in.

The sidecar is an ordinary zip with one entry per image at its root, named `<Name><Extension>`:

```
AIM MXS.djson.ressources    Aim_MXS_Strada_Car_Dash_Display_with_Icons_1024x1024.png
```

An `ImageItem` references a descriptor by name through `Image`:

```json
{"$type":"SimHub.Plugins.OutputPlugins.GraphicalDash.Models.ImageItem, SimHub.Plugins",
 "Image":"Aim_MXS_Strada_Car_Dash_Display_with_Icons_1024x1024",
 "AutoSize":true,"AutoSizeScale":2.5,"BackgroundColor":"#00FFFFFF",
 "Left":-360.0,"Top":-580.0,"Width":2027.5,"Height":2017.5,
 "Opacity":20.0,"Visible":false,"IsFreezed":true,"Name":"ImageItem0"}
```

`AutoSize` with `AutoSizeScale` sizes the item from the image rather than from `Width` and
`Height`; OpenDash wants the opposite, a fixed box, so it writes `AutoSize` false and sets both.
Verified on the VM on 2026-09-12: one 240 x 180 source drawn by three items into 480 x 180,
240 x 240 and 96 x 72 fills each rect, so the image is stretched to the box rather than letterboxed
inside it. `Opacity` is a percentage, as it is on every other item.

**An image carries no colour of its own.** Read off the committed `plugin/lib/SimHub.Plugins.dll`
on 2026-09-16: `ImageItem` declares `Image`, `AutoSize`, `AutoSizeScale` and `ImageData`, which is
the decoded `ImageSource` and is not serialised, and nothing else. Its whole chain above,
`GraphicalDash.Models.DrawableItem` then `GDashItemBindingBase` then `BindingBase`, offers exactly
one colour, `BackgroundColor`, plus a `BorderStyle`; `BackgroundColor` paints the item's box behind
the picture and not the picture. A lamp that is amber when it lights and grey when it does not is
therefore two files, one per state, and not one file recoloured. `Visible` is declared on that same
`DrawableItem` beside `Left`, `Top`, `Width` and `Height`, so it binds as it does on any other item,
which is how the two files are drawn: two items at the same rect, each hidden by its own
expression.

Fonts are referenced by family name in `Font`, with `FontWeight` taking WPF weight names such
as `Normal`, `SemiBold`, `Bold` and `Black`, and the files are shipped in `_SHFonts/`. `Font`
holds one family rather than a stack, so a fallback such as Arial Narrow or system-ui cannot be
expressed at all: what a package does not ship is resolved by WPF to whatever it can find rather
than to a named alternative, which is why a weight has to be bundled before it may be drawn. Blumlaut
ships `D-DINCondensed-Bold.ttf`; Daniel Newman ships Reddit Mono, Reddit Sans and Inter.

### No letter spacing

No text item in any of the roughly thirty `.djson` files examined carries a letter-spacing,
tracking or kerning property, and the only `Spacing` key found belongs to `GroupItem`. Label
tracking is therefore not expressible on the dashboard face.

### The generated map's widths and radii are doubles (2026-09-16)

`GeneratedStaticMapItem` derives from `GeneratedMapItemBase`, which declares `TrackWidth`,
`TrackBorderWidth`, `MinimumTrackWidth` and `MinimumTrackBorderWidth` as `Double`, and the
`PlayerStyle` it carries for the player and for the opponents declares `DotRadius`,
`DotBorderThickness` and `LabelFontSize` the same way. A fractional width is therefore written and
read without loss, which is why the track module draws the design's 2.5 px outline rather than
rounding it to three. Read from the metadata of the `SimHub.Plugins.dll` committed under
`plugin/lib`, which is the 9.12.6 assembly the spike decompiled.

The same class is where the map's shape limits come from: it holds exactly two styles, one for the
player and one for every opponent, and a style is a dot with a radius and a border. Square markers,
sector markers and a colour for one named car are consequently not expressible;
`OverrideColorsWithCarClassColors` is the only per-car colouring on offer and OpenDash refuses it.

### The class leader has gap functions of its own (2026-09-22, #212)

The opponent providers of the 9.12.6 assembly in `plugin/lib` register, beside `gaptoleader`,
`lapstoleader` and `gaptoleadercombined`, three class twins: `gaptoclassleader`, described as the
driver's gap to his own class leader; `lapstoclassleader`; and `gaptoclassleadercombined`, the
driver's gap to the player's class leader as laps or seconds, spelled the way the overall one is.
They were found while #212 was reviewed, the class gap having been built on the belief that they
did not exist. `carClassRaceGap` in `packages/dash/src/second/values.ts` still derives the figure
from the two gaps to the overall leader, for the reason its comment gives; the three names are here
so that the next reader checks them rather than the belief.

### Community precedent for source in git

Blumlaut commits raw `.djson` and zips in CI, and DahlDesign runs Prettier over `**/*.djson`
on every pull request for diff readability. Both stop short of generating the JSON, which is
where OpenDash goes further.

## Verified in the spike (2026-09-10, SimHub 9.12.6)

The gate items of the scope were run on the Windows VM with a hand-built package
(`odSpike`) and by decompiling `SimHub.Plugins.dll`, `GameReaderCommon.dll`, `ICarsReader.dll`
and `WoteverCommon.dll`. Findings, all now relied upon by the generator:

- A package with only `<name>.djson`, its `.metadata` sidecar, a widget `.djson` and `_SHFonts/`
  imports and renders; the gallery shows an empty thumbnail when no preview PNG is shipped. The
  widget file is not listed as a dashboard of its own.
- Extracting the folder under `DashTemplates/` and copying `_SHFonts/*.ttf` into `DashFonts/`
  is what SimHub's importer does (`ImportDashWindow`), so the plugin can do the same.
- `Width`, `BackgroundColor`, `Visible`, `Text`, `BlinkEnabled` and `InitialScreenIndex`
  bindings all evaluate at runtime. A `WidgetItem` whose `InitialScreenIndex` is bound switches
  screen live, so slots do not need the "every card in every slot" fallback.
- A face resolves by family name with `FontWeight` `Medium`, `SemiBold` and `Bold`, and this entry
  used to say that Barlow and Barlow Condensed both did. They do not, and a release shipped on the
  strength of it. WPF reads the width word out of a family name and files the condensed faces under
  "Barlow" as a stretch, so `Font: "Barlow Condensed"` reached a face about a fifth wider than the
  design, on the dash face, on the second screens and in the plugin's own settings panel. A `.djson`
  carries `Font` and `FontWeight` and nothing for stretch, and `usWidthClass` does not override the
  name, which was tried on the VM. What OpenDash ships is therefore Barlow Condensed with its family
  renamed to one carrying no width word, "openDash Display", so that WPF has nothing to fold; see
  `packages/dash/src/design/fontFiles.ts` and #159. The lesson generalises beyond this font: no
  family OpenDash asks for may contain Condensed, Narrow, Compressed, Extended, Expanded or Wide.
  Their digits are proportional and SimHub cannot request `tnum`, so numerals use
  `UseMonospacedText` with `CharWidth` and `SpecialCharsWidth` cells, which SimHub offers for
  exactly this purpose.
- `Layer` children carry absolute coordinates; a layer's own `Left`, `Top`, `Width`, `Height` and
  `BackgroundColor` are `[JsonIgnore]` in SimHub and are not written.
- `BlinkEnabled`, `BlinkDelay` (half period in ms, default 250) and `BlinkPhasisInverted` live on
  every drawable item. Omitted properties take SimHub's defaults, which the decompiled
  `ShouldSerialize*` methods document: `Opacity` 100, `BlinkDelay` 250, `CharWidth` 40,
  `SpecialCharsWidth` 20, `SpecialChars` `.,:`.
- The metadata object has `SimHubVersion`, `Category`, `Title`, `Description`, `Author`, `Width`,
  `Height`, `DashboardVersion`, `ScreenCount`, the three screen index lists, `MainPreviewIndex`,
  `IsOverlay`, `OverlaySizeWarning`, `MetadataVersion` (2), `EnableOnDashboardMessaging` and
  `PreferredTouchMode`.
- `UseStrictJSIsolation` should be written `true` with `UseStrictJSIsolationWarning` `false`,
  otherwise the editor shows a "legacy Javascript isolation" banner.
- NCalc, as verified live: `format(v, '0.00', true)` adds the sign; `toshorttime(ts, 3, false, true)`
  gives `m:ss.fff`; `timespantoseconds`, `secondstotimespan`, `truncate`, `%`, string `+`
  concatenation with numbers, `and`, `!`, `isnull(v, d)` for absent plugin properties,
  `isnull(v)` for absent raw telemetry, and `driverclassposition(getplayerleaderboardposition())`
  all behave as the generator expects. Game properties are read as
  `[DataCorePlugin.GameData.X]`.
- Unit strings are enum names: `SpeedLocalUnit` `KMH`/`MPH`, `FuelUnit` `Liters`/`Gallons`,
  `TemperatureUnit` `Celcius`/`Fahrenheit`/`Kelvin`, `TyrePressureUnit` `Psi`/`Kpa`/`Bar`.
  `Fuel`, tyre temperatures and pressures are already converted to the user's unit.
- Class position: there is no player class position property. `[PlayerClassOpponentsCount]`
  gives the class car count and `driverclassposition(getplayerleaderboardposition())` the
  position in class.
- iRacing reports TC and ABS levels from `dcTractionControl` and `dcABS`; both are absent, so
  `isnull([DataCorePlugin.GameRawData.Telemetry.dcTractionControl])` is true, on cars without
  the control, which is how the cards show `--`.
- The shift-light properties are band progress values, not bar percentages; see
  [ADR 0004](../decisions/0004-rev-bar-model.md).
- `Flag_Green` is passed through a `GreenLimiter` in `GameManagerBase`, so SimHub reports the green
  flag only for a short time after it is raised; a steady green flag in the sim is not a steady
  `Flag_Green`. The other flags are reported for as long as the sim shows them. The blue flag is
  suppressed while the green flag is up.
- **The six `Flag_*` properties are a lossy summary of what iRacing publishes.** `IRacingManager`
  folds `yellow`, `yellowWaving`, `caution` and `cautionWaving` into one `Flag_Yellow`, and
  `Flag_Black` is only the `black` bit, so a furled black, a disqualification and a meatball are
  all invisible through the normalised properties. The whole bitfield is published separately; see
  below.

### Every iRacing flag bit is a property of its own (2026-09-13, #278)

iRacing's telemetry carries one `SessionFlags` bitfield, and SimHub does not leave it as a number
to be masked. `DataSampleEx` exposes it through `ExposableObject.EnumerateEnum<SessionFlags>`,
which emits one boolean per enum member named `<name>.Is<member>`:

```csharp
new ExposableObject(name + ".Is" + ((T)enumvalue).ToString(), value)   // ExposableObject.cs
```

`DataCorePlugin` declares raw data under `GameRawData`, concatenating `currentName + "." + i.Name`,
so each bit is readable as

```
[DataCorePlugin.GameRawData.Telemetry.SessionFlagsDetails.Is<member>]
```

The member spelling is the enum's own, which is camel case and **not** what the rest of SimHub's
property names look like: `Isdebris`, `Isred`, `IsyellowWaving`, `IsoneLapToGreen`, `IsstartReady`.

The twenty-five members, from `iRacingSDK.SessionFlags`:

| | |
|---|---|
| Race control | `checkered`, `white`, `green`, `yellow`, `red`, `blue`, `debris`, `crossed`, `yellowWaving`, `randomWaving` |
| Caution | `caution`, `cautionWaving`, `oneLapToGreen`, `greenHeld` |
| To go | `tenToGo`, `fiveToGo` |
| Addressed to you | `black`, `disqualify`, `servicible`, `furled`, `repair` |
| Start | `startHidden`, `startReady`, `startSet`, `startGo` |

This is what lets a flag box draw more than the six normalised flags, and it is a boolean per bit
rather than a mask, so no bitwise operator is needed — which matters, because whether SimHub's
NCalc exposes one is not established.

Not verified on a running sim: that every one of these fires when the sim raises it. `servicible`
is spelt that way in the SDK, and `randomWaving` and `crossed` have no documented meaning in
iRacing's own reference. Anything OpenDash draws from this table is drawn only where the meaning
is certain; see `docs/design/flag-box.md`.

Still open: whether `Version` gates anything (every sample says 2, and 2 is what we write), and
verification of the plugin-driven slot switch with the real plugin, which follows the plugin
build.

### NCalc dispatches on the name *and* the argument count (2026-09-11, #134)

`NCalcEngineBase.EvaluateFunction` is a chain of `name == "x" && parameterCount == n` tests. When
neither a name nor an arity matches, **no delegate is attached and the expression evaluates to
nothing**. SimHub reports that nowhere: the item simply draws the empty string. There is no log
line, no red box in the editor, and no way for a dashboard to find out.

That is how `left([Class], 4)` shipped. `left` is a real SimHub function — it is
`left(value, startIndex, maxLength)`, three arguments, backed by `WoteverCommon`'s
`StringExtensions.Left` — so a whitelist of names alone would have passed it. Every class and tyre
chip on both leaderboards drew an empty block from the day the second screens shipped until it was
found by eye, with the expression well formed, the item present, the box the right size and every
test green.

The function table is therefore transcribed into
[`packages/generator/src/ncalcFunctions.ts`](../../packages/generator/src/ncalcFunctions.ts) with
an arity for each name, and the validator rejects a call the engine would not dispatch. It has
three sources, all in `SimHub.Plugins.dll`:

| Source | What it holds |
|---|---|
| `NCalcEngineBase.EvaluateFunction` | 37 named branches, each with its parameter count |
| `NCalcEngineMethodsRegistry.AddMethod` | 42 generic methods the chain falls through to |
| NCalc's own table | `abs`, `round`, `if`, `max`, `min`, `truncate` and the rest of the maths, which SimHub does not intercept |

Three shapes are worth knowing before writing an expression by hand:

- `left` and `right` are `(value, startIndex, maxLength)`. Not `(value, length)`.
- `getbestlapopponentleaderboardposition` and its class-only twin are declared with no parameter
  and their delegates take one anyway, so a dummy `0` is required.
- `driver<name>(position)` and `driversector<name>(position, sector, includePrevious)` are matched
  by prefix against `OpponentsDataProviders`, so a misspelt suffix is an unknown function with the
  right arity — which fails silently like everything else here.

When SimHub is upgraded, the table is re-derived by decompiling rather than edited by hand.

## Sources

- [Blumlaut/simhub-dashes](https://github.com/Blumlaut/simhub-dashes)
- [andreasdahl1987/DahlDesignDash](https://github.com/andreasdahl1987/DahlDesignDash)
- [Pirito10/ETS2-Dashboard](https://github.com/Pirito10/ETS2-Dashboard)
- [SimHub wiki](https://github.com/SHWotever/SimHub/wiki), in particular
  [NCalc scripting](https://github.com/zegreatclan/SimHub/wiki/NCalc-scripting---Introduction)
  and the [JavaScript formula engine](https://github.com/zegreatclan/SimHub/wiki/Javascript-Formula-Engine)
