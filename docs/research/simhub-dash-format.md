# The SimHub dashboard format, reverse-engineered

**Last updated:** 2026-09-10
**Confidence:** structure, bindings, widgets, sidecars and packaging verified against real
exports from SimHub 9.2 to 9.12; a short list of behaviours remains to be proven in the spike.

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
openDash/
  openDash.djson               the scene graph
  openDash.djson.metadata      JSON sidecar, see below
  openDash.djson.ressources    a zip of the images the dashboard references (misspelt in the format itself)
  openDash.djson.carclasses    JSON sidecar, "[]" in every sample
  openDash.djson.png           gallery preview, written by DashStudio on save
  openDash.djson.00.png        per-screen previews
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

Inside the widget, a variable is read as `[variable.Name]` in NCalc. openDash's slots follow
the same pattern with NCalc expressions and without variables, since the slot property can be
bound on the `WidgetItem` itself.

### Images and fonts

Modern exports keep image bytes out of the `.djson`. `Images` is a list of descriptors (`Name`,
`Extension`, `Width`, `Height`, `Length`, `MD5`) and the bytes live in the `.ressources` zip,
one file per image. The older ETS2 sample inlined them as base64, where they were 87.6 percent
of the file. The MVP design uses no images at all, so the generator writes an empty `Images`
list and no `.ressources` file; whether SimHub imports a package without that sidecar is item 7
of the spike.

Fonts are referenced by family name in `Font`, with `FontWeight` taking WPF weight names such
as `Normal`, `SemiBold`, `Bold` and `Black`, and the files are shipped in `_SHFonts/`. Blumlaut
ships `D-DINCondensed-Bold.ttf`; Daniel Newman ships Reddit Mono, Reddit Sans and Inter.

### No letter spacing

No text item in any of the roughly thirty `.djson` files examined carries a letter-spacing,
tracking or kerning property, and the only `Spacing` key found belongs to `GroupItem`. Label
tracking is therefore not expressible on the dashboard face.

### Community precedent for source in git

Blumlaut commits raw `.djson` and zips in CI, and DahlDesign runs Prettier over `**/*.djson`
on every pull request for diff readability. Both stop short of generating the JSON, which is
where openDash goes further.

## To be proven in the spike

Whether `InitialScreenIndex` switches the displayed screen when its binding changes at runtime,
or only at load; whether SimHub imports a package that has no `.ressources` and no preview
image, and what it shows as thumbnail; whether a folder extracted under `DashTemplates/` by the
plugin behaves as an imported package, fonts included; whether Barlow Condensed digits are
tabular, since no OpenType feature can be requested; the exact names of the class position,
fuel unit and tyre pressure unit properties; and whether `Version` gates anything, since every
sample says `2`.

## Sources

- [Blumlaut/simhub-dashes](https://github.com/Blumlaut/simhub-dashes)
- [andreasdahl1987/DahlDesignDash](https://github.com/andreasdahl1987/DahlDesignDash)
- [Pirito10/ETS2-Dashboard](https://github.com/Pirito10/ETS2-Dashboard)
- [SimHub wiki](https://github.com/SHWotever/SimHub/wiki), in particular
  [NCalc scripting](https://github.com/zegreatclan/SimHub/wiki/NCalc-scripting---Introduction)
  and the [JavaScript formula engine](https://github.com/zegreatclan/SimHub/wiki/Javascript-Formula-Engine)
