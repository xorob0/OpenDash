# What drives an LED, property by property

**Last updated:** 2026-09-13
**Confidence:** every row was read out of the decompiled SimHub 9.12.6 assemblies — `SimHub.Plugins.dll`
(byte-identical to the VM's install), `GameReaderCommon.dll`, `ICarsReader.dll` and `iRacingSDK.dll` —
rather than from the wiki. Where a value is called "always 0", the method that returns 0 is named.

[simhub-leds-format.md](simhub-leds-format.md) is the file format. This is what goes in it.

The reason this file exists is that an effect is only as honest as the property under it, and half
of what an LED profile can show is not published by every sim. Three answers are possible and this
file keeps them apart: **drawn** (the property is filled on iRacing), **best effort** (the property
exists and iRacing leaves it zero, so the light ships and stays dark there), and **no property at
all** (nothing to bind, in either layer).

The middle one is a deliberate departure from how a *readout* is treated. [scope.md](../scope.md)
says a module reading something iRacing does not publish says so rather than **drawing a zero**, and
that rule is about a screen: `0.00` asserts a measurement nobody took. An LED that stays dark asserts
nothing, so an effect whose property exists is shipped and simply does not light — which is what
makes it useful on a sim that does fill it.

## The trap that decides how every formula is written

`CustomStatusContainer.IsActiveBase` is:

```csharp
return base.NcalcEngine.ParseValueOrDefault(EnabledFormula, 1.0) != 0.0;
```

and `ParseValueOrDefault` wraps the evaluation in `try { … } catch { }` and returns its **default**
on a throw. The default is `1.0`. So **an LED whose formula fails is on**, where a dashboard binding
that fails is merely an unlit segment. Every read therefore goes through `isnull(…, 0)`, which is
ADR 0003's standing rule arriving somewhere it turns out to matter much more.

Two related facts, both verified:

- A bare boolean is fine. `ParseValueOrDefault` has `if (obj is bool) return ((bool)obj) ? 1 : 0;`,
  so a formula does **not** need wrapping in `if(…, 1, 0)`.
- **`CustomStatus` does not test `GameRunning` and every native `Status.*` container does.** So a
  profile built from `CustomStatus` lights up with the sim closed unless something gates it. OpenDash
  wraps each profile in one native `Groups.GameRunningGroup` rather than adding the term to every
  formula.

## What OpenDash draws

| Effect | Property | Notes |
|---|---|---|
| Shift ladder | `GameRawData.SessionData.DriverInfo.DriverCarSL{First,Shift,Last,Blink}RPM` | ADR 0014. One set per car, not per gear |
| Last gear | `…DriverInfo.DriverCarGearNumForward` vs `GameRawData.Telemetry.Gear` | `[Gear]` is a string; the raw one is numeric |
| Shift fallback | `GameData.CarSettings_RPMShiftLight1` / `2`, `CarSettings_RPMRedLineReached` | ADR 0004, for a car publishing no ladder |
| Brake, throttle | `GameData.Brake`, `GameData.Throttle` | 0..100 |
| Fuel gauge | `Computed.Fuel_Percent` | 0..100. Not `GameData.FuelPercent`, which the game reader computes as the sim's litres over a `MaxFuel` already converted to the profile's unit, so it reads 189 for a half tank on a gallons profile. DataCore's divides the two converted figures. #993 |
| Low fuel | `GameData.CarSettings_FuelAlertActive` | What the native `Status.LowFuelRemainingLapsAlert` reads. SimHub computes it, so it works on iRacing |
| ABS active | `GameData.ABSActive`, `OpenDash.WheelLock` | `(BrakeABSactive > 0)`, a real intervention unlike TC, on a car with ABS. Otherwise the plugin's lock-up estimate, while the strip's `LedInferSlip` is on — see below |
| TC | `GameData.TCActive`, `OpenDash.WheelSpin` | The intervention where a sim reports it. On iRacing, the plugin's wheelspin estimate, while the strip's `LedInferSlip` is on — see below |
| DRS | `GameData.DRSAvailable`, `GameData.DRSEnabled` | Carry a stale `[NotAvailable]`, but the reader does fill them from `DRS_Status` |
| Push to pass | `GameData.PushToPassActive`, `GameRawData.Telemetry.PlayerP2P_Count` | Injected per frame from `CarIdxP2P_*[playerCarIdx]` |
| Headlight flash | `GameRawData.Telemetry.dcHeadlightFlash` | The only source. SimHub normalises nothing; absent entirely on a car without the control |
| Spotters | `GameData.SpotterCarLeft` / `SpotterCarRight` | From iRacing's `CarLeftRight`. "Both sides" is the conjunction |
| Turn indicators | `GameData.TurnIndicatorLeft` / `Right` | Best effort; dark on iRacing |
| ERS charge | `GameData.ERSPercent` | Best effort; dark on iRacing. KERS folds in here |
| Flags | `GameData.Flag_{Black,Checkered,Yellow,Blue,White,Green}` | Note SimHub's spelling: `Checkered` |
| Pit lane, limiter | `GameData.IsInPitLane`, `GameData.PitLimiterOn` | The limiter is one bit of iRacing's `EngineWarnings` |
| Pit speeding | composed | See below |
| Water temp warning | `GameRawData.Telemetry.EngineWarnings` bit 1 | |
| Oil pressure warning | `GameRawData.Telemetry.EngineWarnings` bit 4 | |

### Pit speeding is composed, because SimHub publishes none

There is no speeding property at any layer. It is `IsInPitLane` **and** a `PitLimiterSpeed` above
zero **and** a `SpeedLocal` above it by a margin. The margin is what stops the light strobing while
the limiter settles. `PitLimiterSpeedMs` exists on `StatusDataBase` but carries `[DoNotExpose]`, so
it is not a property.

### Wheelspin and lock-up are estimated by the plugin, because SimHub keeps its estimate for ShakeIt

Established on 2026-10-04 by decompiling `GameReaderCommon.dll` and `SimHub.Plugins.dll` 9.12.6.

- iRacing publishes no wheel speeds and no traction-control flag. `GD_TCActive()` returns 0, and so
  does every reader of it: `FeedbackData.TCActive` is copied from `GameData.TCActive` in
  `GameManagerBase.PreParseDataBase`, and ShakeIt's TC Active effect reads `GameData.TCActive` again.
- `StatusDataBase.FeedbackData` holds per-wheel `WheelSlip`, `WheelSpeed` and `WheelRPS`, and carries
  `[DoNotExpose]`, so none of it is a property. That is SimHub's rule for `PitLimiterSpeedMs` above.
- ShakeIt's wheel slip effect (`ShakeItV3.Effects.WheelSlipEffect.GetEffectValue`) picks a source by
  what the sim provides: precalibrated slip, wheel RPS, direct slip, calibrated slip data, wheel
  speeds, and last "RPM vs Speed" (`GetRpmSpeedSlip`, with a "legacy iRacing" variant behind a
  switch). The last one is the rule `plugin/OpenDash/SlipEstimate.cs` applies.
- That rule: nothing below 1 km/h or with the engine stopped on either frame, in neutral, or for 500
  ms after `Gear` changes. Otherwise `|speed₀/rpm₀ − speed₁/rpm₁| × 4000`. Past 20 % brake it is a lock
  weighted by `Offset(brake, 55, 90)`. Past 40 % throttle with the clutch under 5 % it is a spin
  weighted by `Offset(throttle, 70, 100)`. `MathExtensions.Offset` is in none of the vendored
  assemblies; it is taken to be the clamped 0-to-1 position, as its sibling `Map` is.
- The plugin publishes `OpenDash.WheelSpin`, `OpenDash.WheelLock` and `OpenDash.TCInferred`. A strip's
  TC and ABS lamps read the first two behind its `LedInferSlip` switch, which is on by default. ADR
  0018's amendment of 2026-10-04 records why the plugin computes this.
- Unverified on a rig: the threshold (0.2) and the hold (200 ms) are first guesses. The iRacing reader
  itself is not among the vendored assemblies, so that iRacing takes the "RPM vs Speed" path is
  inferred from ShakeIt's code rather than read from the reader.

### `EngineWarnings` is a bitfield, and two of its bits do not exist

`irsdk_EngineWarnings`: 1 water temp, 2 fuel pressure, 4 oil pressure, 8 stalled, 16 pit limiter,
32 rev limiter. The raw dictionary holds a plain `int`, so a bit is tested arithmetically the way
`values.ts` already tests `PitSvFlags`. **There is no oil-temperature bit and no water-pressure
bit.** A lamp for either would have to threshold the temperature itself.

## Best effort: the property exists, and iRacing does not fill it

These ship and are dark on iRacing. On a sim whose reader fills them they light, untested in the
same sense the dashboards are untested on another sim.

The judgement differs from the one a readout gets, and deliberately. `scope.md`'s rule is that a
module reading something iRacing does not publish says so **rather than drawing a zero**, and that is
about a readout: `0.00` on a screen asserts a measurement nobody took. **An LED that stays dark
asserts nothing.**

| Effect | Property | What iRacing does |
|---|---|---|
| TC intervening | `GameData.TCActive` | `GD_TCActive()` is `[NotAvailable] return 0`. The lamp lights there on the plugin's wheelspin estimate instead (above), and is dark without the plugin or with the strip's `LedInferSlip` off |
| Turn indicators | `GameData.TurnIndicatorLeft` / `Right` | Both `[NotAvailable] return 0` — **hard zero, not null**, so `isnull()` cannot tell "off" from "not published" |
| ERS charge, and KERS with it | `GameData.ERSPercent` | The reader overrides neither `GD_ERSMax` nor `GD_ERSStored`, so it is always 0. SimHub has no `KERS` member at all and normalises every hybrid store into this percentage |

## What has no property at all

Not a refusal so much as an absence: there is nothing to bind, in the normalised layer or iRacing's
own. A grep of the decompiled `GameReaderCommon.dll` finds zero of each.

| Effect | Why not | The nearest thing that does exist |
|---|---|---|
| Headlights on/off, beam | No headlight, light or beam member in `StatusDataBase`; no iRacing var. The only "Headlights" string in the assembly is a controller button role | `GameRawData.Telemetry.dcHeadlightFlash`, the flash-to-pass, which **is** shipped |
| Water pressure | No SimHub property and no iRacing var. iRacing publishes oil pressure and water temperature, and no coolant pressure | The water-temperature warning bit, which is shipped, and the raw `WaterLevel` in litres |
| Oil temperature warning | `EngineWarnings` has a water-temp bit and an oil-pressure bit and no oil-temp bit | `GameData.OilTemperature`, thresholded by whoever wants the lamp |
| Distance or time to the pit box | No property and no var; any figure is an estimate rather than a reading | The pit lane and limiter effects, which say where the car is |
| Per-gear shift points | See below — nothing to derive from | The table, `data/shift-points.json` |
| Red, black-and-white flags | No `Flag_Red`; SimHub never normalises iRacing's red bit | `GameRawData.Telemetry.SessionFlagsDetails.Isred` |

## Per-gear shift points: there is nothing to derive

This was the question behind #774, and the answer is flatly no on iRacing.

- **iRacing publishes one ladder for the car.** The `DriverInfo` block holds seventeen `DriverCar*`
  keys and none of them is per gear. There is no `DriverCarGearRatio`, and the four `DriverCarSL*`
  values have no per-gear variant.
- **SimHub's per-gear table is unreachable and, on iRacing, unfilled.**
  `CarSettings.GearSettings[].UpshiftRpm` is blocked twice over: its parent `CarSettings` carries
  `[DoNotExpose]`, and `DeclareObject` returns early for a `List<>` in any case. It is also never
  learned from iRacing: `CarManager.EnsureCar` seeds every gear from `GD_Redline()`, which the
  iRacing reader does not override, so the base returns `0.0` and every gear gets the same estimate.
- **`CarSettings_CurrentGearRedLineRPM` is not per gear on iRacing either.** With stock settings it
  is `DriverCarRedLine * RedLinePercent/100` — one number, identical in every gear.

The one thing that *is* per gear: if the user turns SimHub's per-gear redline on by hand,
`CarSettings_RPMRedLinePerGearOverride` becomes 1 and `CarSettings_CurrentGearRedLineRPM` then does
vary with the gear. That is the only derived per-gear source that exists, and it exists only because
a person typed the numbers in.

So per-gear shift points can come from exactly two places: SimHub's table when the user has filled
it, or a table OpenDash carries. Both are used — see ADR 0014.

## Things that are published but useless on iRacing

Worth knowing, because they validate cleanly and then read zero for ever.

- `SpotterCarLeftDistance`, `SpotterCarRightDistance`, `SpotterCarLeftAngle`, `SpotterCarRightAngle`
  and `DraftEstimate`: `SetSpotterData()` fills these only in the branch taken when the reader
  supplies no spotter value, and the iRacing reader always supplies one.
- `EngineMap`: `GD_EngineMap()` returns null, so the property is always `-1`.
- `CarSettings_FuelAlertFuelRemainingLaps` and `EstimatedFuelRemaingLaps`: both `[DoNotExpose]`, so
  neither is a property. `DataCorePlugin.Computed.Fuel_RemainingLaps` carries the same number.
