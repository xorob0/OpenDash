# The SimHub LED profile format, reverse-engineered

**Last updated:** 2026-09-13
**Confidence:** container model, profile shape, storage and the expression doors read from the
decompiled SimHub 9.12.6 `RGBDriver` assemblies and confirmed against the files of a real 9.12.6
install on the test VM. The iRacing findings were confirmed by decompiling `ICarsReader.dll` and
`iRacingSDK.dll` and by scanning all 323 assemblies of the install for the property names. The
matrix half was read from the 9.12.6 `RGBMatrixDriver` assemblies, `ProfilesCommon` and
`WoteverCommon.JsonExtensions`; it has not yet been round-tripped through a real matrix.

SimHub's LED profiles are as undocumented as its dashboards. This is what the format is, what
drives it, and the one fact that decides how openDash should approach it:
**SimHub never reads the shift-light values that iRacing publishes for the car.**

There are two of these formats, not one. A strip is the `RGBDriver`; an 8x8 flag box is the
`RGBMatrixDriver`, and they differ in the place it is easiest to get wrong. The first two thirds
of this file are the strip; [the matrix](#the-matrix-is-a-second-driver-not-a-second-device)
starts below the iRacing section.

## Where a profile lives

Every LED output plugin owns one settings file, and every profile the user has made is inside it:

```
SimHub/PluginsData/Common/ArduinoRGBLedsSettings.json     RGB strips on an Arduino
SimHub/PluginsData/Common/NextionRGBLedsSettings_1.json   Nextion
SimHub/PluginsData/Common/ArduinoTM1638LedsSettings.json  TM1638 modules
```

The file is a `LedsSettings`: `Profiles` (an array of whole profiles), `activeProfileId`,
`LastUsedProfile`, `LastGameProfiles`, `ProfileSwitchingMode`, `ProfilesCycleMode`,
`CurrentProfileGame`, `GlobalBrightness`, `ForcedLedCount` and `UseBuiltInProfiles`.
`RGBLedsDriver` reads it with `JsonExtensions.FromJsonFileWithVersionning` at startup and writes
it back on change, so a file edited underneath a running SimHub is overwritten.

A single profile is also an exchange format: **`.ledsprofile`**, the same JSON object written on
its own, offered in the UI as `Leds effects profile (*.ledsprofile)|*.ledsprofile`. Devices ship
theirs alongside the definition:

```
SimHub/DevicesDefinitions/Embedded/<Device>/BuiltInLedsProfiles/{S1,S2,Raw}/<guid>.ledsprofile
SimHub/DevicesBuiltInProfiles/<device guid>/S2/<guid>.ledsprofile
```

There is no LED equivalent of `.simhubdash`: a profile is not part of a dashboard package, and
importing one is a separate act from importing a dash.

## What a profile is

```json
{
  "CarChoices": [], "CarChoice": null, "GameCode": null,
  "UseStrictJSIsolation": false, "EmbeddedJavascript": null,
  "GlobalBrightness": 100.0, "GlobalBrightnessPreset": { "CurrentMode": 0, "Brightness": 100.0 },
  "LedContainers": [ ... ],
  "TestLedsGameData": { ... },
  "Name": "Default Profile", "ProfileId": "<guid>", "UseProfileBrightness": false
}
```

`LedContainers` is a tree: a container that holds others carries its own `LedContainers`, and a
leaf paints LEDs. Each node names its class in `ContainerType`, and `LedContainerJsonConverter`
resolves it by trimming `SimHub.Plugins.DataPlugins.RGBDriver.LedsContainers.` from the front of
the type name and `Container` from the end, so `Flags.YellowFlagContainer` is written
`Flags.YellowFlag` and `RPMContainer` is written `RPM`. A `ContainerType` it cannot resolve
becomes an `UnknownContainer` that keeps the original JSON, so an unknown effect survives a load
and save rather than being dropped.

Each effect declares how many LEDs it occupies (`LedCount`) and where it starts
(`StartPosition`), and the engine composes them in order onto one strip.

### The containers that exist

| Family | `ContainerType` |
|---|---|
| RPM | `RPM` (gradient over a range), `RPMSegments` (explicit segments) |
| Inputs and car | `Brake`, `Speed`, `Turbo`, `Fuel`, `Gap.Delta`, `Status.Gear` |
| Status | `Status.AbsActive`, `Status.AbsOn`, `Status.TCActive`, `Status.TCOn`, `Status.BrakeActive`, `Status.DrsAvailable`, `Status.DrsOn`, `Status.SpeedLimiter`, `Status.SpeedLimiterAnimation`, `Status.SpotterCarLeft`, `Status.SpotterCarRight`, `Status.TurnIndicatorLeft`, `Status.TurnIndicatorRight`, `Status.LowFuelRemainingLapsAlert` |
| Flags | `Flags.GreenFlag`, `Flags.YellowFlag`, `Flags.BlueFlag`, `Flags.WhiteFlag`, `Flags.BlackFlag`, `Flags.RedlineReached` |
| Colour | `StaticColor`, `StaticGradient`, `CustomGradient`, `DynamicColor`, `CustomStatus`, `Animation` |
| Script | `ScriptedContent` |
| Grouping | `Base.Group`, `Base.ConditionnalGroup`, `Base.IncludeProfileGroup` |
| Conditions | `Groups.GameRunningGroup`, `Groups.GameNotRunningGroup`, `Groups.CurrentGameGroup`, `Groups.GameCarModelGroup`, `Groups.GameCarStatedGroup`, `Groups.GameCarInPitLaneGroup`, `Groups.GameCarSpeedLimiterGroup`, `Groups.CustomConditionalGroup` |
| Transforms | `Groups.RemapGroup`, `Groups.MirrorGroup`, `Groups.RepeatGroup`, `Groups.ScrollGroup`, `Groups.KeepXOfYGroup`, `Groups.FormulaShiftGroup`, `Groups.BrightnessGroup`, `Groups.BrightnessFormulaGroup`, `Groups.BreathGroup` |

`Groups.RemapGroup` is how one set of effects drives differently wired hardware: it reorders
physical LED positions into a logical order, which is what lets the same effect tree serve a
3/9/3 strip and a 4/14/4 one.

### The two RPM containers

`RPM` is a gradient: `RpmMode`, `PercentMin`/`PercentMax`, `RPMMin`/`RPMMax`, `StartColor`,
`EndColor`, `GradientOnAll`, `RightToLeft`, `FillAllLeds`, `UseLedDimming`, `RelativeToRedline`,
`OverrideMaxRPMWithRedlineValue`, `BlinkEnabled`, `BlinkDelay`, `BlinkOnLastGear`.

`RPMSegments` is a strip described segment by segment, which is the shape of a real car's LED
bar. Each `LedSegment` is `{ StartValue, NormalColor, BlinkingColor, UseBlinkingColor, LedCount }`,
and `RpmMode` says what `StartValue` means:

```
RpmMode.Percent        percent of max RPM
RpmMode.RedlinePercent percent of the redline
RpmMode.Rpms           an absolute RPM
```

With `RpmMode.Rpms` a segment table *is* a car's shift-light table — but the numbers are baked
into the profile, so a profile that must suit many cars needs either one table per car under a
`Groups.GameCarModelGroup`, or a value that is computed at run time.

## The two doors that take an expression

**`ScriptedContent`** holds one `ContentFormula`, evaluated by the profile's own NCalc/JavaScript
engine, and expects an array of colours:

```csharp
Color[] array = base.ParentProfile.NcalcEngine.ParseValueOrDefault(ContentFormula, new Color[0]);
result.Fill(LedCount, Color.Transparent);
for (int i = 0; i < array.Length && i < LedCount; i++) result[i] = array[i];
```

It is the same expression engine the dashboards use, `$prop('...')` included, and the profile
carries its own `EmbeddedJavascript` (with `UseStrictJSIsolation`) for shared functions. A
profile can therefore compute every LED from any SimHub property, per frame, with no per-car
tables at all.

**Formula behaviours** (`LedFormulaEditor`, `Groups.CustomConditionalGroup`,
`Groups.FormulaShiftGroup`, `Groups.BrightnessFormulaGroup`, `DynamicColor`, `CustomStatus`) put
a formula on one attribute of an ordinary effect — its condition, its offset, its brightness or
its colour — which is the cheap version of the same idea.

## What the engine hands an effect

`SetResultBase(LedsGameData data, LedResult result, List<Profile> includedFrom)`. `LedsGameData`
is deliberately generic, and the test-data block of any profile lists it in full: `CarId`,
`CarModel`, `Gear`, `GearEx`, `Rpm`-ish values (`RPMPercent`, `RPMRedlineReached`, `MaxRpm`,
`RPMSMax`), `SpeedKmh`, `SpeedMph`, `Fuel`, `FuelMax`, `LowFuelAlert`, `Brake`, `Turbo`,
`TurboPercent`, `AbsEnabled`, `AbsActive`, `TCEnabled`, `TCActive`, `DRSEnabled`, `DRSAvailable`,
`IsInPitLane`, `PitLimiterOn`, `SpotterCarLeft`, `SpotterCarRight`, the five flags,
`SessionBestDelta`, `AllTimeBestDelta`, `GameRunning`, `GameName`, `CarStartedTime`.

There is **nothing per-car about shift points in it**: `RPMRedlineReached` and `RPMPercent` are
as far as it goes. Anything finer has to come from a property, through a formula.

## A plugin can add its own effect

`SimHub/PluginSdk/User.LedEditorEffect/` is a complete sample: a class deriving
`LedsContainerBase` with `[ContainerMetadata(order, "Name", "Description", "Category", ...)]` is
discovered by the RGB driver, appears in the effect picker, supplies its own WPF editor through
`MainLedsEditor`, is serialised into the profile under its own `ContainerType`, and may read
anything, `PluginManager.GetInstance().GetPropertyValue(...)` included. The comment in the sample
asks that effects use the `LedsGameData` argument where they can, so that the test-data panel
keeps working.

This is the second way openDash could drive LEDs, and it has the obvious cost: the effect only
exists where the openDash plugin is installed, and the profile is not portable without it.

## iRacing publishes the car's own shift lights, and SimHub ignores them

The iRacing session string carries, per car, the values the sim's own shift light and the car's
LED bar use:

| Property | Meaning |
|---|---|
| `DriverCarSLFirstRPM` | the first light comes on |
| `DriverCarSLShiftRPM` | the sim is telling you to shift |
| `DriverCarSLLastRPM` | the last light comes on |
| `DriverCarSLBlinkRPM` | the lights blink: over-rev |
| `DriverCarRedLine`, `DriverCarIdleRPM` | redline and idle |
| `Telemetry.ShiftIndicatorPct` | the sim's own shift indicator, 0 to 1 |

They are typed `double` on `iRacingSDK.SessionData._DriverInfo`, so in an expression they are
`DataCorePlugin.GameRawData.SessionData.DriverInfo.DriverCarSLFirstRPM`, the nested raw-data path
openDash already uses for `WeekendInfo.WeekendOptions.IncidentLimit`.

Scanning all 323 assemblies of a 9.12.6 install for `DriverCarSLFirstRPM` finds it twice: in
`iRacingSDK.dll`, which declares it, and in an **embedded sample-data resource** inside
`ICarsReader.dll`. No code path reads it. The iRacing reader takes exactly one engine value from
that block:

```csharp
protected override double GD_MaxRpm()
{
    return base.NewData.Raw.SessionData.DriverInfo.DriverCarRedLine;
}
```

So `CarSettings_RPMShiftLight1`, `CarSettings_RPMShiftLight2` and `CarSettings_RPMRedLineReached`
— the values [ADR 0004](../decisions/0004-rev-bar-model.md) built the rev bar on — are computed
by SimHub from its own per-car settings (`GameSettings.Car.CarSettings`: `MaxRpm`, `Redline`,
`OverrideRedline`, `EnablePerGearRedline` and a `GearSettings[].UpshiftRpm` table it learns
through `SetAutoGearRedline`), seeded from the redline and otherwise from defaults the user
tunes. They are not the car's lights. They are SimHub's idea of them.

## What this means for openDash

Every competitor solves the gap the same way: Daniel Newman Racing's RPM profiles carry
hand-tuned segment tables, often per gear, for 88 iRacing cars and several hundred more across
six other sims, keyed by car model. That is the only way to do it for seven sims.

openDash supports one sim, and that sim publishes the answer. One `ScriptedContent` container, or
one plugin-provided effect, reading the four `DriverCarSL*` values, mirrors the car the driver is
actually sitting in — every car, including one released this morning, with no table to maintain.
The same four values can drive the rev bar and the rev arc on screen, so the strip and the screen
light at the same instant for the same reason.

What the sim does **not** publish is colour. There is no per-car LED colour sequence in the
session string, so a mirror is a mirror of *behaviour*: when the first light comes on, when it
says shift, when it blinks. Colours stay openDash's tokens, and a car's own colour sequence
belongs to the Car themes project, not here.

## The matrix is a second driver, not a second device

Everything above is `RGBDriver`: a strip, one dimension, effects that own a run of LEDs. An 8x8
flag box is **`RGBMatrixDriver`**, a separate assembly with its own container namespace, its own
JSON converter and its own settings file. The two share `ProfilesCommon`, `Animation`,
`ExpressionValue` and the `.ledsprofile` extension, and share almost nothing else. A profile
written for one will not load into the other: the container vocabularies do not overlap.

openDash's flag box is the matrix driver, so the rest of this file is about it.

```
SimHub/PluginsData/Common/ArduinoRGBMatrixSettings.json   a matrix on an Arduino, D6
```

That file is a `MatrixSettings`, the matrix twin of `LedsSettings`, and it holds the same
`Profiles` array of whole profiles. It is read by `FromJsonFileWithVersionning` when the driver is
constructed and written back on change, so the warning above applies unchanged: a file edited
underneath a running SimHub is overwritten.

The commercial **iFlag** from SYM Projects is a registered device rather than a bare Arduino
(`SymProjectsIFlagDevice` constructs `new RGBMatrixDriver(settings, DeviceKind.Matrix8x8, allowMultileResults: false)`),
and keeps its profiles inside its own device settings blob. Same profile format, different file.
A printed box on an Arduino is the Arduino path and is what openDash targets.

### `ContainerType` is spelt differently here, and this is the trap

The strip's converter trims `SimHub.Plugins.DataPlugins.RGBDriver.LedsContainers.` from the front
of the type name and `Container` from the end, which is why a strip profile says
`Flags.YellowFlag`. **The matrix converter does neither.** `LedContainerJsonConverter` in
`RGBMatrixDriver.Utilities` registers each type under `type.Name` alone:

```csharp
ActionTypes[typeName ?? type.Name] = new ActionDescriptor { ActionType = type };
```

So a matrix profile says `AnimationContainer`, `FlagsContainer`, `GroupContainer` — the bare
class name, `Container` suffix included. There is one alias, `OpacityPulseGroupContainer`, mapped
to `GroupContainer` for old files. A `ContainerType` the matrix converter cannot resolve is not
tolerated the way the strip tolerates it: there is no `UnknownContainer` fallback, and
`Activator.CreateInstance(ActionTypes[...])` throws on a name it does not know, which takes the
whole profile with it.

### The containers that exist

| Family | `ContainerType` |
|---|---|
| Effects | `AnimationContainer`, `FlagsContainer`, `GearContainer`, `SpotterContainer` |
| Status | `AbsActiveContainer`, `AbsOnContainer`, `BrakeActiveContainer`, `DrsAvailableContainer`, `DrsOnContainer`, `LowFuelRemainingLapsAlertContainer`, `RedlineReachedContainer`, `SpeedLimiterContainer`, `SpotterCarLeftContainer`, `SpotterCarRightContainer`, `TCActiveContainer`, `TCOnContainer`, `TurnIndicatorLeftContainer`, `TurnIndicatorRightContainer` |
| Flags | `BlackFlagContainer`, `BlueFlagContainer`, `GreenFlagContainer`, `WhiteFlagContainer`, `YellowFlagContainer` |
| Grouping | `GroupContainer`, `IncludeProfileGroupContainer` |
| Conditions | `CustomConditionalGroupContainer`, `GameRunningGroupContainer`, `GameNotRunningGroupContainer`, `CurrentGameGroupContainer`, `GameCarModelGroupContainer`, `GameCarStatedGroupContainer`, `GameCarInPitLaneGroupContainer`, `GameCarSpeedLimiterGroupContainer`, `BrakeGroupContainer` |
| Brightness | `BrightnessGroupContainer`, `BrightnessFormulaGroupContainer` |
| Script | `ScriptedContentContainer` |

There is no `RemapGroup`, no `MirrorGroup`, no `ScrollGroup`: the strip's transform family does
not exist on the matrix, because a picture is already addressed by coordinate.

### Every container carries these

```json
{
  "ContainerType": "AnimationContainer",
  "ContainerId": "<guid>",
  "Description": "Green flag",
  "IsEnabled": true,
  "StartPositionMatrix": 1,
  "StartPositionX": 1,
  "StartPositionY": 1,
  "DeviceKind": 2
}
```

`StartPositionMatrix`, `StartPositionX` and `StartPositionY` are **one-based** and all default to
1. `Description` is omitted when null; the others are always written. `ContainerType` is a
read-only property computed from the class, so it is written on save and read on load, and the
generator simply emits it.

A group writes its own `StartPositionX`/`StartPositionY` under the names **`StartPositionXEx`**
and **`StartPositionYEx`** — `GroupContainerBase` overrides them with `[JsonProperty("StartPositionXEx")]`
— and holds its children in `LedContainers`. A leaf uses the unsuffixed names. Getting this wrong
silently places a group at the origin.

### `DeviceKind` is a flags enum, and 8x8 is 2

```
Matrix8x8 = 2   (8 rows, 8 columns)   Default = 2
PSP       = 4   (5 rows, 4 columns)
Matrix4x4 = 8
Matrix5x4 = 0x10 (4 rows, 5 columns)
Matrix5x7 = 0x20 (7 rows, 5 columns)
Any       = int.MaxValue
```

`DeviceMetadata` is `(folder, rows, columns)`, so `Matrix8x8` is 8 by 8 and the ambiguous names
above are rows-by-columns the other way round from their spelling. `GearContainer` declares
`(DeviceKind)2147483643`, which is `Any` with `PSP` cleared: a gear glyph is offered on every
matrix except the switch panel.

### How a 64-pixel picture is stored

An `AnimationContainer` holds one `Animation`, which is `Columns`, `Rows`, `PenColor` and an
array of `Frames`. A `Frame` is a duration and a pixel set, and the pixel set is serialised by
`Frame.FrameColorsConverter` into **one string**:

```json
{
  "FrameDuration": 100,
  "Colors": "0,0,#FFCC00;0,1,#FFCC00;1,0,#FFCC00"
}
```

Each `;`-separated triple is `row,column,#colour`. The order is row first: the backing type is
`Dictionary<int, Dictionary<int, Pixel>>` and `GetPixel(x, y)` reads `Colors[y][x]`, so the outer
key is y. These coordinates are **zero-based**, unlike the container's `StartPositionX`/`Y`.
Absent pixels are transparent, so a glyph only lists what it lights, and `#RRGGBB` gains a
leading alpha pair only when alpha is not `FF` — `ColorHelper.ToHtml` writes `#AARRGGBB` in that
case, which is the same spelling the `.djson` uses.

An animation is therefore frames with per-frame durations, and a still picture is one frame. A
blink is two frames, and `FrameDuration` is milliseconds.

Shared animations also exist as `.ledanimation` files under `MatrixAnimations\8x8\` with a
`UserOverrides` folder beside them, but an `AnimationContainer` carries its frames inline and
needs none of that.

### Four matrices

`MultiMatrixResult` holds `MatrixResult[4]`: **four is a hard cap in the type, not a setting**.
Which matrix a container paints is its `StartPositionMatrix`, 1 to 4. `MatrixSettings` carries
`AllowMultipleResults`, which the Arduino path sets from the device and the iFlag device sets
false, and `CurrentResultMatrixCount` reports how many the current profile actually paints.

`Rotation` lives on `MatrixSettings`, and `CanRotate` is hard-coded false for the matrix driver,
so rotation is applied by the device rather than chosen in the profile. Serpentine wiring appears
nowhere in SimHub's managed code at all: it is a firmware and device-settings concern, decided by
the corner the data cable enters. Neither belongs in a generated profile, and neither belongs in
openDash's settings panel.

### The condition door is the same `ExpressionValue` the dashboards use

`CustomConditionalGroupContainer` — "When formula is true" — carries one `TriggerFormula`, and
`ScriptedContentContainer` carries one `ContentFormula`. Both are
`SimHub.Plugins.OutputPlugins.Dash.GLCDTemplating.ExpressionValue`: **the same class, from the
same assembly, that every binding in a `.djson` uses.** It serialises the same way, `Expression`
plus an optional `PreExpression`, with `Interpreter: 1, JSExt: 0` for JavaScript and NCalc by
default.

This settles the question #275 said the whole block rests on. `isnull([OpenDash.ShiftLights], true)`
evaluates inside a matrix profile exactly as it does inside a dashboard, the generator's existing
`buildFormulaObject` emits it unchanged, and a setting changed in the panel reaches the screen and
the box through one property and one expression language.

`GameCarStatedGroupContainer` ("after the car is started") adds a `Duration` in seconds;
`BrightnessGroupContainer` adds an integer `Brightness` percent; `ConditionnalGroupContainer`
adds `ClearBackgroundWhenActive`, omitted when false.

### What this means for openDash

A matrix profile is generatable, on the same terms as a `.djson` and with the same tools. It is
plain indented JSON — `JsonExtensions.ToJsonFile(profile, path, preserveReferences: false)` is
`JsonConvert.SerializeObject(item, Formatting.Indented)` with default settings, so there are no
`$id` or `$ref` references and no `$type` discriminators to reproduce. The glyphs are the only
new thing: a grid of tokens compiled to a `row,col,#hex` string, which is a pure function and
belongs in a snapshot test.

What it is not is a `.simhubdash`. A profile installs into a settings file the driver owns rather
than into a folder SimHub imports, so the install story is genuinely different from the packages'
and is ADR 0013's problem, not the generator's.

## Not verified

- Whether a profile can be installed by writing into `PluginsData/Common/*.json` while SimHub is
  running, or whether it has to be imported through the UI. The driver writes that file itself,
  which suggests the first is unsafe and the answer is either to write before SimHub starts or to
  drive the import. This is the same question for the strip and the matrix and it is ADR 0013's.
- Everything in the matrix section, on a real matrix. It was read from the assemblies, not seen:
  no 8x8 panel is plugged into the test VM, and SimHub's own matrix preview is the substitute
  until one is. The specific claims to check first are the bare-`type.Name` `ContainerType`
  spelling, the `StartPositionXEx`/`StartPositionYEx` rename on groups, and whether a generated
  profile loads at all.
- Whether `FrameDuration` is honoured at the value asked for, or quantised to the driver's own
  tick the way `BlinkDelay` is suspected to be on the dash side.
- Which output plugins a generated profile has to be produced for, and whether `ForcedLedCount`
  and `Groups.RemapGroup` are enough to cover the devices people own with one tree.
- What `S1`, `S2` and `Raw` mean in a device's `BuiltInLedsProfiles` folders.
- Whether `ScriptedContent` is fast enough at 60 Hz for a whole strip, and what an expression
  error does to the rest of the profile.
