# What drives an LED, property by property

**Last updated:** 2026-09-13
**Confidence:** every row was read out of the decompiled SimHub 9.12.6 assemblies — `SimHub.Plugins.dll`
(byte-identical to the VM's install), `GameReaderCommon.dll`, `ICarsReader.dll` and `iRacingSDK.dll` —
rather than from the wiki. Where a value is called "always 0", the method that returns 0 is named.

[simhub-leds-format.md](simhub-leds-format.md) is the file format. This is what goes in it.

The reason this file exists is the rule in [scope.md](../scope.md): a module that reads something
iRacing does not publish **says so rather than drawing a zero**. On a screen that rule protects a
readout; on a strip it matters more, because an LED that never lights reads as *"not happening"*
rather than *"not known"*, and a driver cannot tell the two apart at 200km/h.

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
  profile built from `CustomStatus` lights up with the sim closed unless something gates it. openDash
  wraps each profile in one native `Groups.GameRunningGroup` rather than adding the term to every
  formula.

## What openDash draws

| Effect | Property | Notes |
|---|---|---|
| Shift ladder | `GameRawData.SessionData.DriverInfo.DriverCarSL{First,Shift,Last,Blink}RPM` | ADR 0014. One set per car, not per gear |
| Last gear | `…DriverInfo.DriverCarGearNumForward` vs `GameRawData.Telemetry.Gear` | `[Gear]` is a string; the raw one is numeric |
| Shift fallback | `GameData.CarSettings_RPMShiftLight1` / `2`, `CarSettings_RPMRedLineReached` | ADR 0004, for a car publishing no ladder |
| Brake, throttle | `GameData.Brake`, `GameData.Throttle` | 0..100 |
| Fuel gauge | `GameData.FuelPercent` | 0..100 |
| Low fuel | `GameData.CarSettings_FuelAlertActive` | What the native `Status.LowFuelRemainingLapsAlert` reads. SimHub computes it, so it works on iRacing |
| ABS active | `GameData.ABSActive` | `(BrakeABSactive > 0)`. Real intervention, unlike TC |
| TC set | `GameData.TCLevel` | The dial. **Not** `TCActive` — see below |
| DRS | `GameData.DRSAvailable`, `GameData.DRSEnabled` | Carry a stale `[NotAvailable]`, but the reader does fill them from `DRS_Status` |
| Push to pass | `GameData.PushToPassActive`, `GameRawData.Telemetry.PlayerP2P_Count` | Injected per frame from `CarIdxP2P_*[playerCarIdx]` |
| Headlight flash | `GameRawData.Telemetry.dcHeadlightFlash` | The only source. SimHub normalises nothing; absent entirely on a car without the control |
| Spotters | `GameData.SpotterCarLeft` / `SpotterCarRight` | From iRacing's `CarLeftRight`. "Both sides" is the conjunction |
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

### `EngineWarnings` is a bitfield, and two of its bits do not exist

`irsdk_EngineWarnings`: 1 water temp, 2 fuel pressure, 4 oil pressure, 8 stalled, 16 pit limiter,
32 rev limiter. The raw dictionary holds a plain `int`, so a bit is tested arithmetically the way
`values.ts` already tests `PitSvFlags`. **There is no oil-temperature bit and no water-pressure
bit.** A lamp for either would have to threshold the temperature itself.

## What openDash refuses to draw, and why

Each of these is a light a competitor shows. On iRacing every one of them could only ever be dark.

| Effect | Why not |
|---|---|
| TC intervening | `IRacingManager.GD_TCActive()` is `[NotAvailable] { return 0; }`. Always 0. The TC light shows the dial instead |
| Turn indicators | `GD_TurnIndicatorLeft/Right()` are `[NotAvailable] { return 0; }` — **hard zero, not null**, so `isnull()` cannot even detect the absence. The native `Status.TurnIndicator*` containers are dead on iRacing |
| ERS, battery charge | `ERSPercent`, `ERSStored`, `ERSMax` exist; the iRacing reader overrides neither `GD_ERSMax` nor `GD_ERSStored`, so all three are always 0. `LedsGameData` has no ERS member at all |
| KERS | Does not exist in SimHub 9.12.6, in any sim, under any spelling |
| Headlights on/off, beam | No SimHub field and no iRacing var. The only "Headlights" string in the assembly is a controller button role |
| Water pressure | No SimHub property and no iRacing var. iRacing publishes oil pressure and water temperature, and no coolant pressure |
| Distance or time to the pit box | No property and no var; any figure is an estimate rather than a reading |
| Per-gear shift points | See below — nothing to derive from |
| Red, black-and-white flags | No `Flag_Red`. iRacing's `SessionFlags.red` bit exists and SimHub never normalises it; reachable as `GameRawData.Telemetry.SessionFlagsDetails.Isred` |

## Per-gear shift points: there is nothing to derive

This was the question behind XOR-233, and the answer is flatly no on iRacing.

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
it, or a table openDash carries. Both are used — see ADR 0014.

## Things that are published but useless on iRacing

Worth knowing, because they validate cleanly and then read zero for ever.

- `SpotterCarLeftDistance`, `SpotterCarRightDistance`, `SpotterCarLeftAngle`, `SpotterCarRightAngle`
  and `DraftEstimate`: `SetSpotterData()` fills these only in the branch taken when the reader
  supplies no spotter value, and the iRacing reader always supplies one.
- `EngineMap`: `GD_EngineMap()` returns null, so the property is always `-1`.
- `CarSettings_FuelAlertFuelRemainingLaps` and `EstimatedFuelRemaingLaps`: both `[DoNotExpose]`, so
  neither is a property. `DataCorePlugin.Computed.Fuel_RemainingLaps` carries the same number.
