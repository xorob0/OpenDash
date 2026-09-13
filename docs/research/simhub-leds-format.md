# The SimHub LED profile format, reverse-engineered

**Last updated:** 2026-09-13
**Confidence:** container model, profile shape, storage and the expression doors read from the
decompiled SimHub 9.12.6 `RGBDriver` assemblies and confirmed against the files of a real 9.12.6
install on the test VM. The iRacing findings were confirmed by decompiling `ICarsReader.dll` and
`iRacingSDK.dll` and by scanning all 323 assemblies of the install for the property names.

SimHub's LED profiles are as undocumented as its dashboards. This is what the format is, what
drives it, and the one fact that decides how openDash should approach it:
**SimHub never reads the shift-light values that iRacing publishes for the car.**

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

## Not verified

- Whether a profile can be installed by writing into `PluginsData/Common/*.json` while SimHub is
  running, or whether it has to be imported through the UI. The driver writes that file itself,
  which suggests the first is unsafe and the answer is either to write before SimHub starts or to
  drive the import.
- Which output plugins a generated profile has to be produced for, and whether `ForcedLedCount`
  and `Groups.RemapGroup` are enough to cover the devices people own with one tree.
- What `S1`, `S2` and `Raw` mean in a device's `BuiltInLedsProfiles` folders.
- Whether `ScriptedContent` is fast enough at 60 Hz for a whole strip, and what an expression
  error does to the rest of the profile.
