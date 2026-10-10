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
| Three packages inspected locally | 9.11 and 9.12 | 1280 by 720, 1920 by 1080, 800 by 480 | commercial; widgets with dashboard variables, adaptive zones. Inspected locally, never to be committed. |

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
  OpenDash.djson.png           gallery thumbnail, written by DashStudio on save; see below
  OpenDash.djson.00.png        per-screen previews, written on save and read by nothing in 9.12.6
  cards.djson                  further .djson files are widgets included by the main one
  _SHFonts/                    bundled .ttf files, installed at import
  JavascriptExtensions/        optional .js files loaded into the JavaScript engine
```

Every sidecar is found by the main dashboard's file name, and by nothing else. Decompiled from 9.12.6
on 2026-09-28 for #456: SimHub keeps no list of them, and each one is read as the `.djson`'s own path
with a suffix appended, the `.metadata` by `GraphicalDashItem`, the `.jpg` or `.png` by `LoadPreview`,
the `.ressources` by `DashboardImage.GetImageFromRessources` and the `.carclasses` by
`EditorModel.LoadCarClassOverrides`. `EditorModel.CleanDir` reads a file the same way, taking whatever
precedes `.djson.` as the dashboard it belongs to, and deletes a sidecar whose dashboard is not beside
it. A copy of a dashboard under another name has therefore to rename every `<name>.djson.*` with it,
since a sidecar left under the old name raises no error and is simply never read. Nothing inside a
sidecar names the dashboard, on the other hand: an image is looked up inside the `.ressources` zip by
its own `Name` and `Extension`.

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

### The gallery thumbnail is `<dashboard>.djson.png`, and a package has to bring its own

Decompiled from 9.12.6 on 2026-09-22, which is what #410 and `packages/dash/previews/` rest on.

`GraphicalDashItem.LoadPreview` is the whole of it: it takes the `.djson` path, looks for that path
plus `.jpg`, then that path plus `.png`, and returns null when it finds neither. A null is an empty
box in the list. Nothing derives the picture from the scene graph at list time, so a generated
package that ships no PNG is listed blank for ever, beside hand-drawn dashboards that all have one
because `EditorModel.Save` writes it on every save.

Four facts follow from the code and decide what a package ships:

- **The name is the main dashboard's.** `EditorModel.CleanDir` walks the folder and deletes any
  `<x>.djson.png` whose base name is not the folder's own name, so a widget cannot carry a
  thumbnail and the file cannot be called anything else.
- **The size DashStudio writes is 300 px tall.** `ImageCapturer.SaveToPng(visual, file, 300)`
  scales by `min(1, 300 / height)`, so the picture is the dashboard's aspect at 300 px tall.
- **The list decodes it at 200 px wide.** `new GraphicalDashItemThumbnail(path, 200)` sets
  `BitmapImage.DecodePixelWidth`, so anything wider than 200 is resampled down and anything
  narrower is stretched up.
- **A decoded thumbnail is cached on disk, keyed by the file's own write time.**
  `Thumbnails\<md5 of full path + LastWriteTime + 200>.png`. `ZipArchiveEntry.ExtractToFile`
  stamps the extracted file with the zip entry's timestamp, and OpenDash's zips are reproducible
  and therefore stamp every entry with the same fixed date. Left alone, that would give every
  release of a package the same cache key at the same path, and an update that redrew the face
  would keep showing the picture the first install left behind. `previewMtime` in
  `packages/generator/src/package.ts` is the answer: the thumbnail entry, alone among the entries,
  is stamped from a hash of its own bytes, so the key moves when the picture does and not
  otherwise.

The per-screen `<dashboard>.djson.NN.png` files are written on save by the same code
(`GetScreenShotFilename`, at 100 px tall) and are read by nothing in 9.12.6: `ScreensOverview`
renders live. OpenDash does not ship them.

### A dashboard is reopened by the spelling of its folder, case included (2026-09-28, #467)

SimHub remembers the dashboard it had open in `DashStudioSettings_2.json`, under `LayoutsV2`, as a
path such as `DashTemplates\OpenDash 850x480\OpenDash 850x480.djson`. One could think that the match
made at startup is the filesystem's, which on Windows ignores case. In reality it is made with regard
to case, which was measured rather than decompiled: on the VM, with SimHub 9.12.6 and that path
remembered, the log said "Starting dashboard" at each start while the folder was spelled
`OpenDash 850x480`, said nothing in two and a half minutes once the same folder had been renamed to
`openDash 850x480`, and said it again at the next start once only the folder had been renamed back.

The spelling of a folder is thus part of what a driver has set up, even where Windows does not tell
two spellings apart, and whatever writes a dashboard folder has to keep the one SimHub last read. For
a stock screen that is the package's own, which the installer writes whatever the settings say.
Whether a display assigned to a hardware device is lost in the same way was not tested, since the VM
has none.

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

#### How the three roles actually pick a screen

Decompiled from 9.12.6 for #763, since the order of the two filters is the whole of what an idle
screen depends on.

- **The expression is applied first and the role second.** `EditorModel.CheckGameModeScreen` calls
  `UpdateScreenEnabledStatus`, which sets `ScreenEnabled` from the expression (an empty or missing
  expression is `true`; a non-empty one is enabled while it parses above zero), and then works only
  with the screens where `ScreenEnabled && !IsLayer`. A screen whose expression is false is not a
  candidate for any role.
- **The mode is one of four.** Game when `lastData.GameRunning` **and** some candidate is an
  `InGameScreen`; Pit when that holds, `IsPitlimiterOrPitLane()` is true **and** some candidate is a
  `PitScreen`; Idle when neither and some candidate is an `IdleScreen`; Indeterminate otherwise,
  which picks from every candidate. So a role no enabled screen carries does not blank the display,
  it falls through -- which is why an idle screen must be enabled in every configuration a package
  has, or a package at rest goes back to showing its first screen.
- **Each mode remembers its own screen.** `FindModeScreen` keeps the current screen while the mode
  has not changed, then the screen that mode was last on, then the first candidate carrying the role.
- **Navigation filters by role only when the roles differ.** `Dashboard.GetActiveScreens`, which
  `SelectNextScreen`, `SelectPreviousScreen` and the simple touch mode walk, builds
  `$"{PitScreen};{InGameScreen};{IdleScreen}"` for every enabled screen and, if every screen produces
  the same string, keeps them all. Otherwise it keeps the pit screens (falling back to the in-game
  ones when there are none or the car is not in the pits) while a game runs, and the idle screens
  when none is. A dashboard whose screens all carry the same roles is therefore fully navigable in
  every mode, which is what OpenDash's companion relied on before it had an idle screen.
- **`MainPreviewIndex` is not ours to choose freely.** `EditorModel.UpdateMetadatas` writes the index
  of the first `InGameScreen`, falling back to the first `IdleScreen`, and rewrites the three
  `*ScreensIndexs` lists from the per-screen booleans. Put the idle screen last and the generated
  metadata agrees with what SimHub would have written.

SimHub's own samples say the same thing in JSON: `ControlCenter.djson` carries an `Idle` screen with
`"InGameScreen": false, "IdleScreen": true, "PitScreen": false` and three racing screens with
`"IdleScreen": false`, and `MobileDashWithRelativeTimings` is the same shape with one of each.

#### A dashboard can remember which screen it was on (2026-09-29, #362)

Decompiled from 9.12.6 for the companion's quick glance, which has to go back to a module SimHub
chose and never told anybody about.

- **One frame is three steps, in this order.** `EditorModel.UpdateData` runs
  `UpdateVariables(Dashboard, …, beforeScreens: true)`, which evaluates the dashboard variables marked
  `EvaluateBeforeScreenRoles`; then `CheckGameModeScreen`, which evaluates every screen's enabled
  expression and chooses the screen; then `UpdateDataInternal`, which evaluates the other variables
  and applies the chosen screen's bindings. Only a before-screen-roles variable reaches an enabled
  expression on the frame it was computed. Frames run on SimHub's data loop
  (`GraphicalDashPlugin.DataUpdate` hands each one to the dashboards), which is ten a second on the
  free edition and up to sixty licensed; a frame is skipped when the dash's refresh setting says so
  (`RefreshSpeed_WPFRenderer`) or while the interface is still drawing the previous one.
- **Forcing one screen selects it.** With the current screen disabled, `FindModeScreen` tries the
  screen the mode last remembered -- the same one, so also disabled -- and then takes the first
  enabled screen carrying the role. When exactly one is enabled that is the one, and when the others
  re-enable SimHub stays, because the current screen is enabled.
- **`rootdashboardscreenname()` is the screen drawn last frame, when a variable asks.** It answers
  from `ScreenNamesStack`, which `ApplyBindings` sets to the chosen screen's name while it applies the
  items' bindings, in step three. A before-screen-roles variable in step one therefore reads the
  previous frame's screen. The engine is per `EditorModel`, so two devices showing the same dashboard
  each have their own answer.
- **A variable reading itself reads its last value, and that is legal.** `[variable.X]` returns
  `CurrentValue` from the dashboard's own `Variables` (`EditorModel.GetVariable`, case-insensitive);
  nothing evaluates it at the point of reading. The "self referencing variable" exception is for a read
  nested inside a read of the same variable, which evaluating an expression is not. So
  `if(cond, [variable.X], value)` holds its value for as long as `cond` is true. Variables are
  evaluated in list order, so one reads those above it as they are this frame.
- **Nothing that reads a variable is cached across dashboards.** `ExpressionValue.HashCode` marks any
  expression containing `variable.` (or `activescreenname`) as not cacheable, so every dashboard
  evaluates its own. An expression with neither is shared through a global per-frame cache keyed by its
  text.
- **The JSON is the widget's shape, at the root.** `Dashboard.Variables` is its first declared member,
  a `VariablesContainer` with `DashboardVariables`, written only when non-empty.

The companion's three variables, and a frame-by-frame model of all of this, are in
`packages/dash/src/contract.ts` (`companionVariables`) and `packages/dash/test/secondScreens.test.ts`.

**Seen on the VM, 2026-09-29.** SimHub 9.12.6, free edition, `OpenDash Companion` windowed on the
race scenario, SimHub's `NextScreen` bound to F5 and `CompanionHoldQuickGlance` to F6 as a hold. F5
twice took it from Lap times to Sectors; F6 held showed Track; released, it went back to Sectors,
which the plugin had never been told about; F5 then paged on to Speedo, so the way back had let go.
The same from Speedo with a one-second hold came back to Speedo. SimHub logged nothing. The captures
are in `media/362/`.

### Node types observed

| Type | Seen in | Purpose |
|---|---|---|
| `TextItem` | all | text, the workhorse |
| `RectangleItem` | all | filled rectangle with `BorderStyle`; shift light segments, bars, rules |
| `Layer` | all | grouping, with `Childrens`, `Group`, `Repetitions` |
| `GroupItem` | inspected | container with `ChildsPositioning`, that is to say a stacking layout |
| `ImageItem` | all | image referenced by name from `Images` |
| `ImageFromFileItem` | inspected, OpenDash's Porsche badge (#714) | image from a user file path, fitted keeping its proportions |
| `WidgetItem` | Blumlaut, Dahl, inspected | includes another `.djson` by `FileName`, with dashboard variables |
| `LinearGaugeItem` | Blumlaut, inspected | bar gauge with `Minimum`, `Maximum`, `Value`, optional `GaugeImage` |
| `DialGaugeItem` | ETS2, Blumlaut | radial gauge |
| `GradientItem`, `EllipseItem`, `ButtonItem`, `ChartItem`, `RadarItem`, `GeneratedMapItem`, `ScreenCaptureItem`, `IntegratedLedItem` | inspected, Dahl | not needed by the MVP |
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
an image outside the package. `ImageFromUrlItem.ImageUrl` likewise, beside an `ImageRefreshIntervalSeconds`
that defaults to 60.

**Verified on the VM on 2026-10-08** (#714), on SimHub 9.12.6, with a hand-built 1280 x 480 dashboard of
both items, each over a grey frame, its path or address bound or literal, opened windowed and captured at
1:1, and read against the decompiled types:

- **A path that resolves draws, fitted to the rect with its proportions kept.** A 274 x 364 PNG in a
  54 x 62 item stood 62 high and about 47 wide, centred; in a 160 x 60 item it stood 60 high, centred.
  This is not `ImageItem`, which stretches to fill its box. A 274 px source, a 120 px and a 60 px one
  came out alike at 54 x 62: SimHub decodes through `DashboardImage.LoadImage`, which caches the bitmap
  with linear scaling, and the downscale is clean.
- **A path that does not resolve, and an empty path, draw nothing and log nothing.** `ImageData` is
  `File.Exists(ImagePath) ? decode : null`, inside a `try` that also gives null. The item's own
  `BackgroundColor` still paints, so a transparent item over something else lets it show.
- **A relative path was not tried.** The type passes `ImagePath` to `File.Exists` as it stands, so a
  relative one resolves against SimHub's working directory and not the dashboard's folder; OpenDash
  publishes a full path.
- **A bound `ImagePath` that changes at run time is followed.** A JavaScript binding cycling an existing
  file, a missing one and `''` every six seconds drew, blanked and drew again on cue. The setter raises
  `ImageData` on a change, and the getter reloads when the path differs from the one it last read.
- **A file that appears, or is rewritten, at an unchanged path is picked up within a second.**
  `UpdateData` compares the file's write time once a second and reloads on a change; a path that did not
  exist when the dash opened drew as soon as the file was copied there. A writer should therefore
  replace the file in one move rather than write it in place, so the item never reads half of one.
- **`ImageFromUrlItem` fetches again every `ImageRefreshIntervalSeconds` for as long as the dash is
  open.** Twelve requests in two minutes at 10 s, from a bare `WebClient` that sends no User-Agent. So
  Wikimedia's `upload.wikimedia.org`, which answers 403 to a request without one, drew nothing, while the
  same file served from the host drew. An unresolvable host drew nothing and logged nothing. With the
  server stopped, the last image stayed on screen: `LoadImage`'s `catch` nulls the field, but the
  `ImageData = null` that follows raises no change when the field is already null.

So a picture OpenDash may not ship is drawn from a file: the plugin downloads it once with a User-Agent,
names it in a property, and the item reads it from disk, offline and without polling. `ImageFromUrlItem`
is the wrong tool for anything fetched from a host that minds being asked once a minute per rig.

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
        "Expression": "return $prop('VendorLEDs.Dash.RightZone') != null ? $prop('VendorLEDs.Dash.RightZone') : 2;"
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

Fonts are referenced by family name in `Font`, with `FontWeight` taking WPF weight names such as
`Normal`, `SemiBold`, `Bold` and `Black`, and the files are shipped in `_SHFonts/`. `Font` holds
one family rather than a stack, so a fallback such as Arial Narrow or system-ui cannot be expressed
at all: what a package does not ship is resolved by WPF to whatever it can find rather than to a
named alternative, which is why a weight has to be bundled before it may be drawn. Blumlaut ships
`D-DINCondensed-Bold.ttf`; another ships Reddit Mono, Reddit Sans and Inter.

### A font copied into a running SimHub is not drawn until it restarts (2026-09-28, #441)

One could think that `FontHelper.RefreshFonts()`, which SimHub's importer calls after copying a
package's fonts and which the plugin reaches by reflection, loads what was copied. In reality it does
not, and nothing a plugin can do in-process does. `FontHelper` builds its list with
`Fonts.GetFontFamilies(new Uri(new FileInfo("DashFonts\\").FullName))`, and WPF answers that with a
DirectWrite font collection created through the shared DirectWrite factory, keyed by the folder's
URI. The factory keeps that collection for the life of the process, so the refresh, which asks for
the same URI, is handed the collection built the first time the folder was read. Above it, WPF keeps
each resolved family in `MS.Internal.FontCache.TypefaceMetricsCache`, keyed by a family identity that
compares the location without regard to case.

Measured with PresentationCore 4.8.4682 on the VM, in a process that had read a folder holding the
Bold and SemiBold faces of openDash Display: after `openDashDisplay-Light.ttf` and
`Barlow-SemiBold.ttf` were copied in, the folder listed only openDash Display, without Barlow, and a
Light `Typeface` resolved to `openDashDisplay-SemiBold.ttf`. Emptying `TypefaceMetricsCache` and
running two full collections changed nothing. The same folder under a URI spelled in lower case was
enumerated afresh and listed Barlow, yet Light still resolved to SemiBold, because the family
identity found the stale entry. In SimHub itself, a face written by Reinstall into a running SimHub
was not drawn by the dashboard after it was closed and reopened, and was drawn after a restart; the
update of run A on the ticket met the same thing.

Two consequences follow. A face has to be in `DashFonts` before SimHub first reads the folder, which
the plugin's first start after an update achieves: on the VM it wrote Light about a quarter of a second
after it started, and SimHub opened its dashboard twelve seconds later, drawing Light. And whatever
writes a font into a running SimHub has to ask for a restart rather than a reopen, which is what
`UpdateWording.RestartToSee` is for. A face in use is not locked against writing, on the other hand:
one an open dashboard was drawing from was overwritten, and moved out and back, while SimHub ran.

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
did not exist. `carClassRaceGap` in `packages/dash/src/second/values.ts` still derives the seconds
from the two gaps to the overall leader, which is what `GaptoClassLeader` is for a car of the
player's class; the three names are here so that the next reader checks them rather than the belief.

What the laps count was read from `GameManagerBase` in the 9.12.6 `GameReaderCommon.dll` on
2026-10-10 (#1023). `LapsToLeader` is `(int)Math.Truncate(leader.CurrentLapHighPrecision -
car.CurrentLapHighPrecision)`, the leader being the first of `Opponents`, and `LapsToClassLeader` the
same against the first opponent whose `CarClass` is the player's, which is the car
`getopponentleaderboardposition_playerclassonly(1)` names. `CurrentLapHighPrecision` is the laps
begun and the fraction of the lap run, `CurrentLap - 1` plus the track position where the reader
does not set it itself, so both count whole laps behind by distance and stay 0 for
a car on the leader's lap whose lap counter is one lower because the leader has crossed the line and
it has not. Neither is set on the leader's own row, which is null. The combined strings are built by
`Opponent.GetCombinedGapAsString`: the laps as `+1 lap` or `+2 laps` when above 0, and otherwise the
seconds formatted `0.00` with a `+` when positive, so a car on the lead lap reads two decimals. The
Gap columns read the counts and spell both cases themselves. The NCalc names are `driverlapstoleader`
and `driverlapstoclassleader`, registered as `Lapstoleader` and `Lapstoclassleader` and lower-cased.

### Per-car playlists belong to a display device, and match the iRacing CarPath (2026-10-07, #199)

SimHub switches a display's dashboard by car through a playlist that belongs to the display device and
not to Dash Studio: the section "Dashboard playlists and car assignment" on the Dash page of a
`DashMonitorDevice` (a monitor added as a device), a `BitmapDisplayDevice<T>` (the USB screens) or a
`WebDashDevice`. A dashboard opened in a Dash Studio window has no playlist at all. The classes are in
`SimHub.Plugins.OutputPlugins.GraphicalDash.DashPlaylist`, and what follows was read from the 9.12.6
decompile of `plugin/lib/SimHub.Plugins.dll` and of the guest's `ICarsReader.dll` with ilspycmd
9.1.0.7988 on 2026-10-06.

**The key is `GameData.NewData.CarId`, compared with `==`**, ordinal and case-sensitive
(`DashPlaylistManager.UpdateState`). On iRacing that is the player's `CarPath`, `porsche992rgt3`, since
`IRacingManager.GD_CarId` returns the `CarPath` of the driver whose `CarIdx` is `PlayerCarIdx`;
`CarScreenName` is `CarModel` and plays no part in the match. A car-path column in the theme
catalogue (`iracingCarPaths` in `contract.ts`, `IracingCarPaths` in `Contract.cs`) is therefore the
whole of what the plugin needs. Stability across iRacing updates was not measured; a car iRacing
re-releases as a new model gets a new path, which is a new car for this purpose.

**On disk** a device's playlist lives in its own file,
`PluginsData\Common\Devices\<InstanceId>\settings.json`, under
`DashPlaylistSettings.GamePlaylists.IRacing.CarDashes[]`, one entry being
`{"Cars":[{"CarId":"porsche992rgt3","CarName":"…"}],"Items":[{"Id":"<guid>","DashboardSelection":"OpenDash Porsche 1280x480"}],"Id":"<guid>","IsActive":false}`.
`DashboardSelection` is serialised as the dashboard's `Code`, which is the main `.djson`'s file name
without its extension and so the folder's name. `DevicesPlugin` reads every device file once at
startup and rewrites all of them from memory in `SaveSettings()`, which runs on a clean exit, so an
edit to the file while SimHub runs is lost and the plugin works on the running objects instead.

**Three behaviours matter to a writer.** The first entry naming a car wins, so an entry of ours
placed before the driver's own for the same car hides theirs. A car with no entry keeps whatever the
display was showing, unless the game playlist's `DefaultMainDash` is enabled with a selection
("Load this dashboard when car model has no defined playlist") and `UseDefaultMainDashForUnkownCars`
is true, both of which are the driver's settings. And an entry naming a dashboard SimHub does not list
sets the display's dashboard to null when the car loads, which blanks it, so an entry must never
outlive its folder or precede SimHub's listing of it.

**What the plugin reaches** (`plugin/OpenDash/CarPlaylists.cs`), every member public and every one of
them listed in `PanelCarPlaylist.Reached`, which `PanelCarPlaylistTests` holds to the assembly in
`plugin/lib` and which the plugin looks up before it touches anything, logging the missing ones by name
and writing nothing when one has gone:

| | |
|---|---|
| `DevicesPlugin.DevicesPluginSettings.Devices`, `DeviceInstance.GetInstances()` | every device, composites flattened, as `LedTargets.cs` walks them |
| `IBitmapDisplayDevice.GetDashboard()` | the dashboard a display shows |
| `BitmapDisplayDevice<T>.Settings.DashPlaylistSettings` | a USB screen's playlist; public, read by name only because `T` is not known |
| `DeviceInstance.GetSettingsControls()`, `DeviceSettingControl.Control`, then `DashMonitorDeviceSettingsControl.DashMonitorSettings.DashPlaylistSettings` or `WebDashDeviceSettingsControl.DashMonitorSettings.DashPlaylistSettings` | a monitor's and a web dash's, whose `Settings` are private: the first tab of the settings page holds the same live object, the route #686 takes for a wheel's LEDs |
| `DashPlaylistSettings.CurrentGameCode`, `UpdateCurrentGame(string)`, `CurrentGamePlaylist` | the iRacing playlist, `GamePlaylists` being private; the game it pointed at before is put back at once |
| `GameDashPlaylist.CarDashes`, `DefaultMainDash.Enabled`, `DefaultMainDash.Selection.Dashboard`, `UseDefaultMainDashForUnkownCars` | the entries, and whether a car without one falls back, read and never written |
| `CarDash.Cars[].CarId`, `DashPlaylist.Id`, `DashPlaylist.Items[].Id`, `PlaylistItem.DashboardSelection.Dashboard` | an entry, read; the dashboard is the one thing written in place |
| `GraphicalDashPlugin.GetSettings().Items[].Code` | the dashboards SimHub lists |
| `DevicesPlugin.SaveSettings()` | every device file written now, so an entry outlives a SimHub that is killed |

A new entry is built with Newtonsoft from the JSON shape above, as `DevicesPlugin` builds every entry it
reads, because `DashPlaylist.Id`, `PlaylistItem.Id` and `CarSelection.CarId` have private setters marked
`[JsonProperty]`. `DashPlaylistManager.ReapplyCarSettings()`, which would make a running display
re-evaluate the car at once, is internal and is not called: a new entry takes effect at the next car
change or the next start, and a themed screen is only bound once SimHub lists it, which is the next
start in any case.

**Verified on the VM on 2026-10-07**, on SimHub 9.12.6 with the Sim-Lab Dash SD43-LED (a
`BitmapDisplayDevice`, not connected) and the emulator's `cars` scenario. The entries the plugin wrote
appeared in the device's `settings.json` at once, in the shape above, and in SimHub's own "Per car
playlists for IRacing" list; they survived a clean exit; the display's main dashboard followed the
player's `CarPath` from the default face to the themed one and back, falling back to the driver's
`DefaultMainDash` in the Ferrari; an entry the driver had made for `porsche992rgt3` was left as it was
and only `porsche992cup` was written beside it; and removing the screen took out the plugin's entries
and nothing else. The monitor and web dash routes were not exercised, since the VM has neither device.

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
  position in class. That is `PositionInClass`, which `GameManagerBase` numbers itself, from 1
  within each class, in the order of the overall `Position` with a 0 sorted last, so a class
  place is never 0 and whether a car is placed is asked of `driverposition` (#1014). The class
  places gained, `driverpositiongainclass`, are counted from the class place of the first frame
  SimHub saw the car, so a car first seen unplaced counts them from a place SimHub made up, and no
  package reads them. The overall `driverpositiongain` waits for the car's first real place, and
  it is the class count too when the field is a single class (#1022). `drivergaptoleader` does not
  wait: before the sim has placed anyone it is published for every car, measured from the first
  car of the driver list and negative for some, as seen on the test VM with every iRacing position
  at 0, so the Gap and Int columns ask `driverposition` of both cars as well (#1028). Nor is it
  published for the car every gap is measured from: `ComputeOpponentsData` in 9.12.6 calls
  `UpdateGapToLeader` for every car but the first of `Opponents`, which is `driver*(1)`, and sets
  that first car's `GaptoLeaderSimHub` to 0 only when it is the player. So `drivergaptoleader(1)` is
  null whenever somebody else leads, as seen on the test VM in the `green` scenario, and a difference
  taken from it is null too. SimHub's own `GaptoClassLeader` subtracts 0 in that case, and the Gap
  and Int columns read it as 0 the same way, on that car only (#1041).
- `drivergaptoleader` and `driverlapstoleader` are distance in every session. `UpdateGapToLeader`
  in `GameManagerBaseGapHelpers` takes the first car's `CurrentLapHighPrecision` less the car's,
  times the reference best lap or through the first car's delta store, and `LapsToLeader` truncates
  the same difference; the iRacing reader sets neither itself, and nothing asks the session type. In
  a race that is the gap. In practice and qualifying `Opponents` is sorted by `Position` (the iRacing
  reader sets no `LivePosition`), which it takes from the session results in best-lap order, so the first car is the fastest and the
  distance says how long each car has been out: a car tenths off the best lap reads `+5L` or a
  negative three-digit gap. Per car SimHub publishes `driverdeltatobest`, the car's best lap less the
  fastest of the whole field, floored at 0, which no class list or interval can use; `driverbestlap`
  is a TimeSpan from the results' `FastestTime`, left at zero for a car with none. So outside a race
  the Gap and Int columns subtract two best laps themselves (#1040). `SessionTypeName` is iRacing's
  `SessionType` verbatim: `Practice`, `Open Qualify`, `Lone Qualify`, `Offline Testing`, `Warmup`
  and `Race` are the names the dash knows; how another sim spells them has not been read.
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

### Every iRacing flag bit is a property of its own (2026-09-13, #769)

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

### `timespantoseconds` of a number is null, and `null > 0` throws

Measured for #454 against the `NCalc.dll` SimHub 9.12.6 ships, with SimHub's `isnull` and
`timespantoseconds` reproduced from the decompile.

- `timespantoseconds(x)` answers a TimeSpan's seconds and **null for anything else**, the number `0`
  included. So `timespantoseconds(isnull(t, 0))` is null whenever `t` is.
- NCalc's `null > 0` throws `ArgumentNullException`, and a throwing expression draws the empty string.
- The guard therefore wraps the conversion: `isnull(timespantoseconds(t), 0) > 0` is false for a
  null, a zero and a missing time alike, and `hasTime` in `second/values.ts` is written that way.

### A function reads a different frame from a property

Established for #454 by decompiling `GameManagerBase`, `PluginManager` and `DataCorePlugin`.

- **SimHub builds each frame in place.** Every tick starts with `data.GameNewData = new StatusData(...)`
  and fills that object over the rest of the tick: `Opponents` is null until `GD_Opponents()`
  returns, then briefly unsorted, and `BestLapOpponentPosition` is -1 until `ComputeOpponentsData`
  has run. `NCalcEngineBase.lastData` is the same `GameData` object, so **every function that reads
  `lastData?.NewData` sees the frame under construction** on a dashboard that renders mid-tick. That
  is the whole `driver*` family, the `getbest*` family, `getplayerleaderboardposition` and the rest.
- **A `GameData.*` property reads the last finished frame.** `DataCorePlugin` publishes `GameData` from
  its own `lastData`, which it sets to `data.NewData` in its `DataUpdate`, after the frame is built.
- So the two can disagree for a frame, and a function can answer nothing where the property beside it
  has a value. A field that must not blink reads properties only.
- `GameData` is declared once, by reflection, from an empty `StatusData(InitializeArrays: true)`.
  A member that is null on that frame is never declared, whatever it holds later: `BestLapOpponent`
  is initialised there and declared, hidden, with all its `Opponent` members
  (`GameData.BestLapOpponent.BestLapTime`); `BestLapSameClassOpponent` is not initialised and has no
  property at all.

### The live delta to the last lap is iRacing's, not SimHub's (2026-09-29, #322)

Established by decompiling `PersistantTrackerPlugin` in SimHub 9.12.6.

- **SimHub publishes two live deltas and no third.** `SessionBestLiveDeltaSeconds` and
  `AllTimeBestLiveDeltaSeconds` run through the lap. The `*LastLapDelta` properties beside them are
  the finished lap's, written once at the line, and nothing compares the lap in progress with the one
  before it. #322 was written believing otherwise.
- **iRacing publishes it as raw telemetry**, and SimHub passes that through:
  `[DataCorePlugin.GameRawData.Telemetry.LapDeltaToSessionLastlLap]`. The second `l` in `Lastl` is
  iRacing's own spelling and has to be kept; `LapDeltaToSessionLastLap` is a property nobody
  publishes, and reading it draws the fallback without a word.
- Beside it are `LapDeltaToSessionLastlLap_OK`, a boolean that is true once there is a last lap to
  compare against, and `LapDeltaToSessionLastlLap_DD`, iRacing's rate of change of the delta, which
  nothing reads. The value is not meaningful while `_OK` is false, so `lastLapDelta` in
  `packages/dash/src/second/values.ts` reads it only behind `isnull(..._OK, false)` and draws a level
  delta otherwise, as SimHub's two draw 0 when they have no lap to compare against.
- **A raw iRacing float is a boxed System.Single, and `format` does not sign one.** iRacing publishes
  the delta as an irsdk_float; the SDK reads it with `ReadSingle`, the raw telemetry is a dictionary
  of objects that SimHub exposes unchanged, and NCalc's `if` and SimHub's `isnull` hand their
  argument on unchanged too. `format(v, pattern, true)` (`NCalcEngineBase.Function_Format_Core`)
  writes its `+` only when the value is a double, a decimal or an int, so a Single comes out as .NET
  formats it alone: `0.21` for a slower delta where the session best draws `+0.21`, `0.00` for a
  level one, and `0.00` for a small negative one where a double draws `-0.00`. `abs` and the
  comparisons promote it, so a colour is right where the figure is not. The fix is to make it a
  double before it is formatted, by `* 1.0` and not `* 1`: NCalc parses `1` as an Int32, and a Single
  times an Int32 is still a Single, where a Single times the double `1.0` is a double.
  `lastLapDelta` does that.
- The emulator writes all three (`tools/irsdk-emulator/Drivers.cs`, `SetDelta`), and SimHub exposes
  them under these names: the traces recorded on the VM on 2026-09-29 carry the delta moving through
  the lap, 0.3622 to -0.0246 across the race scenario, and `_OK` as `true`.
- **A format's pattern is written as a literal, so a precision is an `if` around two formats.** The
  pattern `format(v, '0.00', true)` was verified with is a string literal, and `fmt` and `signed` in
  `packages/generator/src/ncalc.ts` quote whatever they are given: a pattern passed to them as an
  expression is emitted as a string literal holding the expression's text, and .NET then reads that
  text as a custom pattern: its `.` and `0` become placeholders and its quotes and commas vanish, so
  the delta draws a mangled copy of the expression rather than a number. So the delta's
  precision is `if(<thousandths>, format(v, '0.000', true), format(v, '0.00', true))`, as
  `referenceDeltaText` writes it. Whether SimHub's `format` would take a bound pattern at all has not
  been tried, and nothing needs it to.
- **`format` rounds a half away from zero.** .NET Framework, which SimHub runs on, formats a double
  with a custom pattern by first taking fifteen significant digits and then rounding the last place
  half away from zero, so 0.005 to two places is `0.01`, and 9.9995 to three is `10.000` although the
  double is a hair under the half. That is why the band inside which the delta is drawn level is
  strictly under half a unit of the last place: at the half the figure has already gained a digit.
  The evaluator in `packages/dash/test/ncalcEval.ts` formats with JavaScript's `toFixed`, which rounds
  the double itself, and so parts from the dash where a reading written as a decimal half is stored
  as a double just under it: 1.005 to two places is `1.00` in the evaluator and `1.01` on the dash,
  and 9.9995 to three is `9.999` against `10.000`. At 12.345 the double is just over the half, so both
  draw `12.35`. This is from the .NET reference source and has not been measured on the VM.

## Sources

- [Blumlaut/simhub-dashes](https://github.com/Blumlaut/simhub-dashes)
- [andreasdahl1987/DahlDesignDash](https://github.com/andreasdahl1987/DahlDesignDash)
- [Pirito10/ETS2-Dashboard](https://github.com/Pirito10/ETS2-Dashboard)
- [SimHub wiki](https://github.com/SHWotever/SimHub/wiki), in particular
  [NCalc scripting](https://github.com/zegreatclan/SimHub/wiki/NCalc-scripting---Introduction)
  and the [JavaScript formula engine](https://github.com/zegreatclan/SimHub/wiki/Javascript-Formula-Engine)
