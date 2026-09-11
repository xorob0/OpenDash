# irsdk-emulator

A synthetic **iRacing shared-memory telemetry emulator** for SimHub. It creates the same kernel
objects the sim creates — the memory-mapped file `Local\IRSDKMemMapFileName` and the auto-reset event
`Local\IRSDKDataValidEvent` — fills them with a byte-exact irsdk layout (header, var headers, session-info
YAML, rotating telemetry buffers at 60 Hz) and drives the values from a JSON scenario. SimHub's iRacing
reader (`IRacingReader.IRacingManager` on top of the `iRacingSDK` wrapper) cannot tell the difference, so a
dashboard can be verified with real, scripted values on the Windows test VM where no iRacing is installed.

Single net48 console exe, no dependencies besides the .NET Framework 4.8 runtime (no NuGet runtime packages).

## Build

```bash
export PATH="$HOME/.bun/bin:$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"; export DOTNET_ROOT="$HOME/.dotnet"; export DOTNET_CLI_TELEMETRY_OPTOUT=1
cd tools/irsdk-emulator
dotnet build -c Release            # -> bin/Release/net48/IrsdkEmulator.exe (+ scenarios/ copied next to it)
```

`devcheck/IrsdkEmulator.DevCheck.csproj` builds the same sources for net8.0 so `--selfcheck`, `--dry-run`,
`--dump-vars` and `--dump-yaml` can be run on the Linux dev box (`dotnet devcheck/bin/Release/net8.0/IrsdkEmulator.dll ...`).
Named kernel objects are Windows-only, so the real mode only works on Windows.

## Run it with `bun run emulator`

The runner does the two things below that are easy to get wrong, so prefer it to driving the exe
by hand:

```bash
bun run emulator start race --follow   # build, upload, start in the desktop, tail the status line
bun run emulator start yellow --replace
bun run emulator stop | status | tail 20
```

It builds and uploads the exe with its `scenarios/` folder, always launches into the interactive
desktop, refuses to start while another copy is running, and stops through the stop file so
SimHub sees a disconnect. Ctrl-C under `--follow` stops the emulator on the VM before returning.

The section below is what the runner does, and is what to read when it breaks.

## Run on the Windows VM

1. Copy `bin/Release/net48/IrsdkEmulator.exe` **and the `scenarios/` folder** next to each other, e.g. to
   `/opt/winvm/shared/irsdk-emulator/` (= `Z:\irsdk-emulator\`) or `C:\Temp\irsdk-emulator\`.
2. Start it **in the interactive desktop session** (the one SimHub runs in), *not* from an SSH/PowerShell
   session: `Local\` kernel objects are per logon session, an emulator started over SSH (session 0) is invisible
   to SimHub on the desktop. With the `winvm` tools that is `run_in_desktop`, e.g.

   ```text
   run_in_desktop(command="C:\Temp\irsdk-emulator\IrsdkEmulator.exe",
                  arguments="race --log C:\Temp\irsdk-emulator\emulator.log --stop-file C:\Temp\irsdk-emulator\stop.txt",
                  working_dir="C:\Temp\irsdk-emulator")
   ```

   Exact command lines (cmd.exe / PowerShell on the desktop):

   ```text
   IrsdkEmulator.exe                       # scenarios\race.json next to the exe
   IrsdkEmulator.exe notc                  # scenarios\notc.json
   IrsdkEmulator.exe C:\path\to\my.json    # any scenario file
   IrsdkEmulator.exe race --log C:\Temp\emulator.log --stop-file C:\Temp\stop.txt
   IrsdkEmulator.exe --selfcheck           # layout self-test, exit code 0 == pass
   ```

   Run the emulator and SimHub as the same (non-elevated) user.
3. In SimHub pick the game **iRacing** (left menu *Games* → *iRacing*; SimHub 9 selects it automatically when the
   iRacing "connection" appears). SimHub logs `Connected to iRacing application`, the game shows as running and
   the properties under `DataCorePlugin.GameData.*` / `GameRawData.Telemetry.*` / `GameRawData.SessionData.*`
   carry the scripted values. Open the dash in *Dash Studio* or the web renderer as usual.
4. Stop: `Ctrl+C` in the emulator console, or create the `--stop-file` (e.g. `run_powershell('ni C:\Temp\stop.txt')`),
   or `--duration <s>`. On exit the header status is set to 0, so SimHub sees a disconnect (game "not running"
   after its 4 s timeout). A hard `taskkill` also works: the tick counter stops and SimHub times out the same way.

The emulator prints one line per second:

```text
[   61.0s] tick 3660 | t 1241.0 rem 1459 | rpm 4528 G2 63 km/h | lap 13/14 cur 97.14 last 98.412 best 97.905 | fuel 36.6 | flags 0x00000000 | pos 3/2 | TC 5 ABS 2 | warn 0x00000000 | tyre 86C
```

plus log lines for lap completions, flag changes, timeline events and session-info updates. `TC -- ABS --` means the
variable is absent from the scenario.

### Options

| option | meaning |
|---|---|
| `scenario` | path, or a name resolved as `scenarios/<name>.json` next to the exe (default `scenarios/race.json`) |
| `--duration <s>` | stop after `<s>` simulated seconds |
| `--stop-file <path>` | stop cleanly once the file exists (checked once per second, file is deleted) |
| `--log <file>` | append all console output to a file (useful with `run_in_desktop`) |
| `--quiet` | no per-second status line |
| `--dry-run` [`--fast`] | simulate without creating the shared memory/event (`--fast` = do not pace to real time) |
| `--dump-vars` | print the effective variable table (name, type, count, unit, buffer offset, value, pinned) and exit |
| `--dump-yaml` | print the rendered session YAML and exit |
| `--selfcheck` | layout/JSON/YAML self-test + a 3 s in-memory simulation of the scenario (exit 0 == pass) |

## How the shared memory is laid out

Exactly `irsdk_defines.h`, cross-checked against SimHub's decompiled `iRSDKHeader`, `VarBuf`, `VarHeader`
(`Marshal.PtrToStructure` sizes 112 / 16 / 144 bytes):

```text
0        irsdk_header: ver=2, status=1 (connected), tickRate=60, sessionInfoUpdate, sessionInfoLen,
         sessionInfoOffset, numVars, varHeaderOffset, numBuf=3, bufLen, pad[2], varBuf[4]{tickCount, bufOffset, pad[2]}
112      var headers, 144 bytes each: int type, int offset, int count, bool countAsTime+pad, name[32], desc[64], unit[32]
         types: 0 char, 1 bool (1 byte), 2 int, 3 bitField, 4 float, 5 double; array elements are packed
~43 KB   session-info YAML, NUL terminated, inside a fixed reserve ("sessionInfoCapacity", default 128 KB);
         sessionInfoLen == the reserve, sessionInfoUpdate is incremented whenever the text changes
~175 KB  numBuf telemetry buffers of bufLen bytes; each tick writes buffer (tick % numBuf), then its tickCount
         in the header, then sets the data-valid event. SimHub picks the buffer with the highest tickCount.
```

Map size defaults to 2 MB (`"mapSize"`). If the mapping already exists (SimHub keeps its view open after an earlier
emulator run) the emulator attaches to it instead of failing; the layout is rewritten from scratch with status=0 first.

## Scenario format

JSON with `//` comments and trailing commas allowed. See `scenarios/race.json` (fully commented) and
`scenarios/notc.json` (uses `extends`).

```jsonc
{
  "name": "...",
  "extends": "race.json",              // optional: merge on top of another scenario
  "tickRate": 60, "numBuf": 3, "sessionInfoCapacity": 131072, "mapSize": 2097152, "seed": 12345,
  "sessionYaml": "race-session.yaml",  // template, relative to this file
  "yaml": { "RaceSessionLaps": "30" }, // values for {{placeholders}} in the template
  "includeCatalog": true,              // start from the built-in catalogue (all standard iRacing vars, defaults)
  "exclude": ["dcTractionControl"],    // drop variables (they are then absent from the var headers)
  "variables": [                       // add / override; type,count,unit,desc optional for catalogue vars
    {"name": "FuelLevel", "value": 38.4},
    {"name": "PlayerCarPosition", "value": 3, "pin": true},          // pinned: drivers cannot change it
    {"name": "CarIdxGear", "value": {"4": 5, "17": 3}},              // array elements by index
    {"name": "CarIdxLapDistPct", "value": [-1, 0.31, 0.97]},         // elements 0..n-1
    {"name": "SessionFlags", "value": "green|startHidden"},          // flag names or numbers / "0x.." strings
    {"name": "MyVar", "type": "float", "count": 1, "unit": "x", "desc": "custom", "value": 1}
  ],
  "drivers": [ {"type": "rpmSweep", "min": 3000, "max": 8000, "period": 6}, ... ],
  "timeline": [ {"at": 15, "set": {"dcBrakeBias": 55.0}}, ... ]
}
```

Values: number, `true`/`false`, `"0x10"`, session-flag names (`"green|blue"`), list, or `{"index": value}`.
Array references `"Name[12]"` are accepted in `set`/`pin`/`unpin`. With `extends`, `variables` and `exclude` are
appended, `yaml` is merged, everything else is replaced by the child.

### Drivers

Run in listed order every tick; the session clock (`SessionTime`, `SessionTick`, `SessionTimeRemain` if not
unlimited, `SessionTimeOfDay`) always runs first. Parameters are re-read every tick, so the timeline can change them.
Driver writes never touch pinned elements.

| type | parameters (defaults) | writes |
|---|---|---|
| `rpmSweep` | `min` 3000, `max` 8000, `period` 6 s, `var` RPM, `slFirst`, `slBlink` | RPM (ease-out ramp, then drop), Engine0_RPM, ShiftIndicatorPct; signals `rpmSweep.cycle/phase` |
| `gearFromRpm` | `minGear` 2, `maxGear` 6, `redline` 9000, `speedAtRedline` [km/h per gear], `rpmVar` | Gear (+1 per sweep, cycling), Speed & VelocityX (m/s) from gear x rpm, CarIdxGear/CarIdxRPM[player] |
| `lapTimer` | `lapTime` 98, `jitter` 0.5, `trackLength` 7000 | LapCurrentLapTime (counts up, resets), Lap, LapCompleted, LapLastLapTime, LapBestLapTime, LapBestLap, LapDistPct, LapDist, LapDeltaTo*Lap(+_DD/_OK), CarIdx* of the player; signals `player.*` |
| `fuelBurn` | `litersPerLap` 2.9, `maxLiters` 100, `kgPerLiter` 0.75, `minLiters` 0.5 | FuelLevel (refills at `minLiters`), FuelLevelPct, FuelUsePerHour |
| `flagCycle` | `period` 8, `sequence` [none, yellow, blue, white, green, black, checkered], `base` (always OR'd), `var` | SessionFlags |
| `pitLimiterToggle` | `period` 5, `bit` 0x10, `var` EngineWarnings | toggles the PitSpeedLimiter bit, dcPitSpeedLimiterToggle |
| `field` | `cars` [{idx, class, pace, pct, lap, pit, last, best, bestLap}], `pace` 98, `jitter` 0.5, `resultsInterval` 5, `yamlIndent` 3, `pitCars` [idx...] | all CarIdx* arrays for the listed cars (lap, completed, pct, surface, pit road, position, class position, class, F2Time gap to leader, EstTime, last/best lap, gear/rpm), RaceLaps, SessionLapsRemain(Ex), PlayerCarPosition/ClassPosition; the player car follows `lapTimer`; renders `{{ResultsPositions}}` / `{{ResultsFastestLap}}` into the YAML |
| `sine` | `var`, `index` 0, `min`, `max`, `period`, `phase` | any variable follows a sine |
| `toggle` | `var`, `index` 0, `period` 2, `on` 1, `off` 0 | any variable toggles |
| `clock` | – | explicit placement of the session clock |

Positions are computed from distance covered, so with equal `pace` per car the running order is stable;
give cars different paces if you want overtakes. Cars in `pitCars` (or `"pit": true`) sit in their pit stall
(`CarIdxTrackSurface` 1, `CarIdxOnPitRoad` true) and drop back.

### Timeline

`{"at": seconds, ...}` events, applied after the drivers on the tick they become due:

| key | effect |
|---|---|
| `log` | text to print |
| `set` | `{"Var": value, "Arr[3]": value}` written once (a driver owning the variable overwrites it next tick) |
| `pin` / `unpin` | write and lock (`pin`), release (`unpin`: list of references) |
| `yaml` | template placeholder values → session YAML re-rendered, `sessionInfoUpdate` incremented |
| `sessionYaml` | switch to another template file |
| `driver` | `{"type"/"name": ..., params..., "enabled": false}` merges parameters into a driver (adds it if unknown) |

### Session YAML template

`scenarios/race-session.yaml` is a real-looking iRacing session string (1-space indentation, `---`/`...` markers,
`\n` line endings, ASCII). Placeholders: `{{RaceSessionLaps}}`, `{{RaceSessionTime}}`, `{{ResultsPositions}}`,
`{{ResultsFastestLap}}`, `{{TcSetting}}`, `{{AbsSetting}}`. It contains everything SimHub reads:

* `WeekendInfo`: TrackName, TrackID, TrackLength (`"7.00 km"`, SimHub parses the km suffix), TrackDisplayName,
  TrackConfigName, TrackPitSpeedLimit (`"60.00 kph"`), EventType, NumCarClasses, HeatRacing, WeekendOptions (NumStarters ...), TelemetryOptions
* `SessionInfo.Sessions[0..2]`: Practice / Lone Qualify / Race, each with SessionNum, SessionLaps, SessionTime,
  SessionType, ResultsPositions (Position, ClassPosition (0-based like iRacing), CarIdx, Lap, Time, FastestLap,
  FastestTime, LastTime, LapsLed, LapsComplete, LapsDriven, Incidents, ReasonOutId/Str), ResultsFastestLap, Results*
* `DriverInfo`: DriverCarIdx 12, PaceCarIdx 0, DriverCarIdleRPM, DriverCarRedLine 9000, DriverCarFuelKgPerLtr,
  DriverCarFuelMaxLtr 100, DriverCarMaxFuelPct, DriverCarSLFirstRPM 7800 / SLShiftRPM 8400 / SLLastRPM 8800 / SLBlinkRPM 9000,
  DriverCarIsElectric, DriverPitTrkPct, DriverCarEstLapTime, DriverSetupName, and `Drivers[]` (pace car + 24 cars) with
  CarIdx, UserName, AbbrevName, Initials, UserID, TeamID, TeamName, CarNumber, CarNumberRaw, CarPath, CarClassID
  (2708 GT3 / 2709 GT4), CarID, CarIsPaceCar, CarIsAI, CarScreenName, CarScreenNameShort, CarClassShortName,
  CarClassRelSpeed, CarClassLicenseLevel, CarClassMaxFuelPct, CarClassWeightPenalty, CarClassColor, CarClassEstLapTime,
  IRating, LicLevel, LicSubLevel, LicString, LicColor, IsSpectator, Car/Helmet/Suit/CarNumberDesignStr, CarSponsor_1/2
* `SplitTimeInfo.Sectors` (3 sectors), `CameraInfo`, `RadioInfo`, `CarSetup.Chassis.InCarDials` (AbsSetting,
  TractionControlSetting, BrakePressureBias — SimHub reads these when `dcABS`/`dcTractionControl` telemetry is missing)

The rendered YAML was checked with YamlDotNet through a port of SimHub's `DataFeed.EscapeMalformedData` +
typed `SessionData` deserialisation + the untyped dictionary/`ExtractVars` path.

## Variables provided

`includeCatalog: true` pulls in ~300 standard iRacing variables (see `Catalog.cs`, `--dump-vars` lists the effective
table). All arrays are 64 elements like the sim. Highlights, grouped:

* session: SessionTime (double), SessionTick, SessionNum, SessionState, SessionUniqueID, SessionFlags (bitField),
  SessionTimeRemain (double), SessionLapsRemain(Ex), SessionTimeTotal, SessionLapsTotal, SessionTimeOfDay,
  RadioTransmit*, DisplayUnits, DriverMarker, PushToTalk/Pass, IsOnTrack, IsReplayPlaying, ReplayFrameNum(End),
  IsDiskLogging*, FrameRate, CpuUsage*, PaceMode, PitsOpen
* player: PlayerCarIdx, PlayerCarPosition, PlayerCarClassPosition, PlayerCarClass, PlayerTrackSurface(Material),
  PlayerCar*IncidentCount, PlayerCarWeightPenalty, PlayerCarPowerAdjust, PlayerCarTowTime, PlayerCarInPitStall,
  PlayerCarPitSvStatus, PlayerTireCompound, PlayerFastRepairsUsed, OnPitRoad, CarLeftRight
* CarIdx arrays: Lap, LapCompleted, LapDistPct, TrackSurface, TrackSurfaceMaterial, OnPitRoad, Position, ClassPosition,
  Class, F2Time, EstTime, LastLapTime, BestLapTime, BestLapNum, TireCompound, QualTireCompound(Locked), FastRepairsUsed,
  SessionFlags, PaceLine, PaceRow, PaceFlags, Steer, RPM, Gear, P2P_Status, P2P_Count
* driving: SteeringWheelAngle(+Max/Torque/Pct*), Throttle, Brake, Clutch, *Raw, HandbrakeRaw, Gear, RPM, Speed,
  VelocityX/Y/Z, Yaw, YawNorth, Pitch, Roll, *Rate, Lat/Long/VertAccel, Lap, LapCompleted, LapDist, LapDistPct,
  RaceLaps, LapBestLap, LapBestLapTime, LapLastLapTime, LapCurrentLapTime, Lap*NLap*, LapDeltaTo{Best,Optimal,SessionBest,SessionOptimal,SessionLastl}Lap(+_DD,_OK),
  ShiftIndicatorPct, ShiftPowerPct, ShiftGrindRPM, BrakeABSactive, EngineWarnings (bitField), Tire??_RumblePitch
* car: FuelLevel, FuelLevelPct, FuelUsePerHour, FuelPress, WaterTemp, WaterLevel, OilTemp, OilPress, OilLevel, Voltage,
  ManifoldPress, Engine0_RPM, dcBrakeBias, dcABS, dcTractionControl, dcFuelMixture, dcThrottleShape, dcDashPage,
  dcStarter, dcPitSpeedLimiterToggle, dcHeadlightFlash, dc*Wipers, dp* pit-stop settings, PitSv*, PitRepairLeft,
  PitOptRepairLeft, PitstopActive, FastRepair*, *TiresUsed/Available, TireSets*
* tyres/suspension: {LF,RF,LR,RR}tempCL/CM/CR, {..}wearL/M/R, {..}coldPressure (kPa), {..}brakeLinePress, {..}shockDefl/shockVel
* weather: TrackTemp, TrackTempCrew, AirTemp, AirDensity, AirPressure, WindVel, WindDir, RelativeHumidity, FogLevel,
  Precipitation, Skies, TrackWetness, Solar*, WeatherDeclaredWet
* camera/replay/misc: Cam*, ReplayPlaySpeed, ReplayPlaySlowMotion, ReplaySessionTime/Num, IsOnTrackCar, IsInGarage,
  IsGarageVisible, EnterExitReset, OkToReloadTextures, LoadNumTextures, VidCap*, DC*, Mem*, Chan*

Everything `IRacingManager.cs`/`Telemetry.cs` dereference with a throwing cast is present, and the scenario self-test
checks the traps SimHub has: RPM must not be 0 while on track and LR wear must not be all zero (else the sample is
treated as corrupt), Voltage > 0 and no EngineStalled bit (ignition/engine on), player `CarIdxTrackSurface` != -1
(else "spectating"), SessionTime or SessionUniqueID non-zero (else the sample is ignored).

## Scenarios shipped

* `race.json` + `race-session.yaml`: Spa GP, 24 cars (12 GT3 class 2708, 12 GT4 class 2709), race session 2 of 3,
  30 laps with 12 done, 2700 s session time with 1520 s remaining and ticking; player CarIdx 12 in a Porsche 911 GT3 R
  (992) (redline 9000, shift lights 7800/8400/8800/9000, 100 l tank) on overall P3 / class P2 (P1 GT3 #25, P2 GT4 #56);
  tyre carcass temps 84–87 °C, cold pressures ~190 kPa, wear ~97 %, dcTractionControl 3, dcABS 2, brake bias 54.5,
  FuelLevel 38.4 l burning 2.9 l/lap; RPM sweep 3000→8000 over 6 s with gears 2..6 and matching speed; flags cycling
  every 8 s; pit limiter every 5 s; steering/throttle/brake/lateral-g sines; one car parked in the pits; timeline with
  brake-bias change, spotter, ABS pulse, TC change including a session-info update, leader pit stop, flag cycle stop.
* `notc.json`: extends `race.json`; `dcTractionControl`/`dcABS` removed from the variable list (SimHub then reports
  TC/ABS level 0, dash should show "--"), `CarSetup` TC/ABS entries blank, timed race (`SessionLaps: unlimited`,
  `SessionTime: 1800.0000 sec`, SessionLapsTotal/Remain 32767, 1130 s remaining).

## Self-test

`IrsdkEmulator.exe --selfcheck [scenario]` (exit 0 == pass): checks `Marshal.SizeOf` of SimHub's struct
declarations (112/16/144), renders the full catalogue plus custom vars into a byte array and reads header, all var
headers and every value back with `Marshal.PtrToStructure` / the SimHub accessor conversions (including a 0x80000004
bit field, -1 floats, doubles, packed bool arrays), verifies no variable overlaps, the NUL-terminated YAML, the JSON
reader, the template engine, then loads the scenario, simulates 3 s in memory and checks the SimHub traps listed above.

## Not verified here

Everything above was verified on Linux through the net8.0 devcheck build (dry run, in-memory image, YamlDotNet
parse of the session string). Not verifiable without the VM: creating/opening the named mapping and event on Windows,
SimHub actually connecting (`Connected to iRacing application` in its log), the attach-to-existing-mapping path after a
restart while SimHub still holds the section, and the 60 Hz pacing on the 2-vCPU VM (`timeBeginPeriod(1)` is used;
if SimHub logs `IRacing missing sample`, that is only a warning).
