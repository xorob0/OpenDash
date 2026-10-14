# What SimHub publishes for Assetto Corsa, ACC and Le Mans Ultimate

**Last updated:** 2026-10-14 (#1100)
**Confidence:** read from SimHub 9.12.6's decompiled readers, not yet from a drive. Every claim
below cites the class and member it was read from; the few that the source cannot settle are
collected under [Needs a drive](#needs-a-drive) at the end.

1.0 claims Assetto Corsa, Assetto Corsa Competizione and Le Mans Ultimate (#479), and every binding
OpenDash ships was written against iRacing. This page answers once, from the source, what SimHub's
reader for each of the three publishes, so that the capabilities the plugin publishes (#1101), the
Assetto Corsa emulator (#579), the A3 fixes and the table #102 publishes start from the same facts.

## The sources

Decompiled with `ilspycmd` into a scratch directory, never into the repository. The DLLs are the ones
in the VM's `C:\Program Files (x86)\SimHub`, which are byte-identical to `plugin/lib/` where both
exist (`GameReaderCommon.dll`, `SimHub.Plugins.dll`). `ICarsReader.dll` and `iRacingSDK.dll` are the copies
under the share's `simhub-lib-extra`, taken from the same install: the `GameReaderCommon.dll` beside them
is byte-identical to `plugin/lib/`'s.

| Assembly | What it holds |
|---|---|
| `ACSharedMemory.dll` | `ACManager` (Assetto Corsa), `ACCManager` and `ACCSpectatorManager` (ACC), `ACHelper`, the AC and ACC shared-memory structs (`ACSharedMemory.MMFModels`, `ACSharedMemory.ACC.MMFModels`), `ACSharedMemoryGroupReader`, `ACCSharedMemoryGroupReader`, `ACRawData`, `ACCRawData`, `CarPhysicsManager`, `LutManager`, `ACDHelper`, `TrackDesc`, `CarDesc` |
| `RfactorReader.dll` | `LMUManager` (Le Mans Ultimate), `RF2ManagerBase<T>` it derives from, `LMUWrapV2`, LMU's native structs (`RfactorReader.LMU`), and the rFactor 2 plugin reader `CrewChiefV4.rFactor2_V2.RF2SharedMemoryReader` |
| `GameReaderCommon.dll` | `GameManagerBase<,,>`, which turns each reader's `GD_*` answers into `GameData` (`PreParseDataBase`), computes the opponents' derived data (`ComputeOpponentsData`), the sectors, the spotter and the car settings; `StatusDataBase`, `Opponent`, `SectorTimes` |
| `ICarsReader.dll` | `IRacingManager` (iRacing), the reader the other three are measured against: its `GD_*` answers, such as `GD_TyrePressure*` from `LFcoldPressure`, `GD_TyreTemperature*` from `LFtempCM`, `GD_PitLimiterOn` from the `EngineWarnings` limiter bit, `GD_MaxGears` returning 0 and `GD_CarDamage1..5` marked `[NotAvailable]` and returning 0; `GD_Opponents` |
| `SimHub.Plugins.dll` | `DataCorePlugin.DeclareProperties` and `DeclareObject`, which name the `GameData` and `GameRawData` properties; `SupportedGameManager`; the NCalc `driver*()` registry in `NCalcEngineMethodsRegistry` |
| `SimHubWPF.exe` and `SimHubWPF.exe.config` | `GameRegistry.LoadReaders` (the game list), `MainWindow.autodetectTimer_Tick` and `MainModel.GetCurrentGame` (switching), and the `<Code>Processes` application settings (process names) |

A reader is a `GameManagerBase` subclass. Each frame `PollLoop` asks it `IsGameRunning()`, then
`GetNewRawData()`, then `IsGameInRace()` and `IsGamePaused()`; on an unpaused in-race frame it runs
the reader's `PreParseData()` and the base's `PreParseDataBase()`, which calls every `GD_*` method and
writes `data.NewData`. `DataCorePlugin` publishes `data.NewData` as `DataCorePlugin.GameData.*` and the
reader's raw object as `DataCorePlugin.GameRawData.*`.

## How SimHub picks the game, and how each reader decides it is running

SimHub runs one reader at a time: the one for the game selected in its window. Nothing reads
Assetto Corsa while iRacing is selected.

**Selection.** At start `MainWindow.LoadData` selects the last game (`SettingsEx.ContextSettings.LastGame`),
or the detected one when automatic switching is on. Afterwards `autodetectTimer_Tick` runs every three
seconds: `MainModel.GetCurrentGame` takes one process snapshot of every name in every game's
`<Code>Processes` setting, keeps the current game first if its process is running, and otherwise lists
the others ordered by `DetectionPriority`. It switches only when the current game's process is not
running, the current game is not running, the new game allows it (`AutomaticSwitchingAllowed`), the
setting `AutoSwitchGame` is on **and `GlobalLicenseManager.HasBaseLicense` is true**. An unlicensed
SimHub never switches by itself. The command line `-switchgame <Code>` (`MainWindow`, its argument
handler) switches to a game by its code.

The codes and process names, from `GameRegistry.LoadReaders` and `SimHubWPF.exe.config`:

| Game | Code (`DataCorePlugin.CurrentGame`, `GameData.GameName`) | Reader | `<Code>Processes` | Default UDP port |
|---|---|---|---|---|
| iRacing | `IRacing` | `IRacingManager` | `iRacingSim64;iRacingSim;iRacingSimDX11;iRacingSim64DX11;iRacingUI` | none |
| Assetto Corsa | `AssettoCorsa` | `ACManager` | `ACS;ACS_x86;acs_dmm;AssettoCorsa` | none |
| ACC | `AssettoCorsaCompetizione` | `ACCManager` (and `ACCSpectatorManager` as a second reader) | `AC2-Win64-Shipping;acc` | 9000 |
| Le Mans Ultimate | `LMU` | `LMUManager` | `Le Mans Ultimate` | none |

These names decide switching and the "game detected" banner only. Each reader then checks for
itself, in its own order.

### Assetto Corsa: `ACManager.IsGameRunning`

1. **A process named `acs`, `acs_x86`, `acs_pro` or `acs_dmm`** (`ACHelper.acProcessNames`, matched
   case-insensitively by `ACHelper.GetGameProcess`, looked for at most every two seconds). This is the
   reader's own list, not the config's: a process named `AssettoCorsa` is seen by the switcher and not by
   the reader.
2. **Its folder is taken as the AC install** (`ACHelper.GetAsettoCorsaPath`, from the process's path),
   and becomes `GamePath`.
3. **`Local\acpmf_physics`, `Local\acpmf_graphics` and `Local\acpmf_static` open**
   (`ACSharedMemoryGroupReader.TryOpenDebounced`). `Local\acpmf_simhub_v2` is opened too but is optional.
4. Then `ACManager.IsGameInRace`: **`Graphics.Status` is `AC_LIVE` (2) or `AC_PAUSE` (3)**.
   `AC_OFF` (0) and `AC_REPLAY` (1) are not in race. `IsGamePaused` is `Status != AC_LIVE`.

`GetNewRawData` then needs the install folder to look like AC's. When it does not, it throws on every
frame, `PollLoop` logs the exception, and no frame is published:

- `content\tracks` must exist: `TrackDesc.GetFromAPI` lists it with `Directory.GetDirectories`, which
  throws on a missing folder. A subfolder named as `StaticInfo.Track` (lower case) gives the track;
  without one it falls back to `Documents\Assetto Corsa\cfg\race.ini`, and to no track.
- `content\cars\<StaticInfo.CarModel>` must exist, or `race.ini` must name a car: when `CarDesc.FromModel`
  marks the car missing and `ACHelper.GetCarDataFromGameSettings` finds no `race.ini`, `currentCar` is
  null and the next line (`currentCar.CarInfo?.name`) throws.

What the reader reads from those folders: `ui\ui_car.json` (the car's name and class), the car's
`data` folder or `data.acd` for `ai.ini` `[GEARS] UP` (the redline) and `drivetrain.ini` `[GEARS] COUNT`
(the gear count, `CarPhysicsManager.GetPhysic`), `electronics.ini` and its curves for the TC and ABS
presets (`LutManager.GetLut`), and the track's `ui\ui_track.json`, `data\map.ini` and `map.png`
(`TrackDesc`).

Two optional sources add to the shared memory:

- **SimHub's AC app** writes `Local\acpmf_simhub_v2` (`SPageFileCrewChief`: `numVehicles`, `focusVehicle`
  and 128 `acsVehicleInfo`). It is the only source of opponents. SimHub installs it as
  `apps\python\SimHub` and enables it in `Documents\Assetto Corsa\cfg\python.ini` `[SIMHUB] ACTIVE=1`
  (`ACInstallManager`); without it the leaderboard is the player alone (see Opponents below).
- **AC's own UDP interface on port 9996** (`ACUDPReader`), for `isTcInAction`, `isAbsInAction` and
  `isEngineLimiterOn` (`ACManager.GetNewRawData`, `Extensions.UDP*`).

### ACC: `ACCManager.IsGameRunning`

1. **A process named `AC2-Win64-Shipping` or `acc`** (`GameManagerBase.GetGameProcess`, over the names
   `MainWindow` hands the reader through `AddProcessNames` from `AssettoCorsaCompetizioneProcesses`).
2. **`Local\acpmf_physics`, `Local\acpmf_graphics` and `Local\acpmf_static` open**, with ACC's longer
   layouts (`ACCSharedMemoryGroupReader`).
3. **`Graphics.PacketId` has changed within the last ten seconds and `Graphics.Status` is not `AC_OFF`**
   (`ACCManager.CheckGameRunning`).
4. `IsGameInRace`: `Status` is `AC_LIVE` or `AC_PAUSE` **and `Graphics.iCurrentTime` is below
   214748300**. `IsGamePaused` is `Status == AC_PAUSE`.

Opponents, the track's name and the session's real-time data come from ACC's broadcasting protocol:
SimHub connects as a client (`ACCUdpRemoteClient("127.0.0.1", port, "SimHub", "asd", "", 16)`) to the
port `broadcasting.json` names, which `ACCInstallManager.ConfigureGame` writes with the password `asd`.
Without it the leaderboard is the player alone, the same as AC without its app.

### Le Mans Ultimate: `LMUManager.IsGameRunning`

1. **`LMU_Data` opens** (LMU's own shared memory, read twice: as `SharedMemoryGeneric` for its events and
   as `SharedMemoryLayout` for everything else).
2. **The rFactor 2 plugin's buffers open and read** (`RF2ManagerBase.IsGameRunning`, through
   `RF2SharedMemoryReader.ReadGameData`): `$rFactor2SMMP_Telemetry$`, `$rFactor2SMMP_Scoring$`,
   `$rFactor2SMMP_Rules$`, `$rFactor2SMMP_Extended$` and `$rFactor2SMMP_ForceFeedback$` are all required,
   `$rFactor2SMMP_Graphics$` is optional, and each read waits for the buffer's version block to agree
   (`MappedBuffer.GetMappedData`). After a failure it does not retry for two seconds. SimHub installs
   `rFactor2SharedMemoryMapPlugin64.dll` into LMU's `Plugins` folder for this (`installLMU.pak`).
3. **A process named `Le mans ultimate`** (`FastProcessHelper.GetProcesses`, case-insensitive).
4. **`events[0]` is not `SME_EXIT` (1), `SME_SHUTDOWN` (3) or `SME_UNLOAD` (5).**
5. `IsGameInRace` (`RF2ManagerBase`): **the rFactor 2 scoring has a vehicle with `mIsPlayer > 0` whose
   `mID` is among the rFactor 2 telemetry's vehicles.** `IsGamePaused` is
   `LMUNativeTelemetry.scoring.scoringInfo.mCurrentET` unchanged for more than a second.

So LMU needs both pages: the plugin's for "is there a player", LMU's native one for nearly every value.
The player is `telemetry.telemInfo[playerVehicleIdx]` and the scoring entry with the same `mID`
(`LMUManager.PreParseData`).

## How `GameData` and `GameRawData` are built

**`GameData`.** `PreParseDataBase` assigns every member from the reader's `GD_*` method. A method the
reader does not override returns the base's default (`GD_Opponents` an empty list, `GD_PitLimiterSpeed`
and `GD_SpotterCar*` null, `GD_Sector1Time` null, `GD_EngineIgnitionOn` `Rpms > 300`). Then the base
converts: temperatures from °C to the driver's unit, pressures from psi to bar or kPa when chosen
(`OilPressure`, `TyrePressure*`), fuel to gallons, `PitLimiterSpeed` from km/h to the speed unit,
`TyreWear*` through `Map(value, MinimumTyreWear, 100, 0, 100)`, `BrakeBias` rounded to 0.1. A `double`
nobody fills reads 0, never null.

**`[NotAvailable]`.** A reader marks the `GD_*` methods it cannot answer with `[NotAvailable]`, and
`DataCorePlugin.GetPropertySupportStatus(member, readerType, check)`, a public static, turns that into
`SupportStatus.NotSupported` for the property. It is a real per-game capability list, but not a complete
or a correct one: `ACCManager.GD_ABSLevel` is marked and filled, and `ACManager.GD_PitLimiterOn` is not
marked and answers a different question. Where a reader has nothing it usually says so: iRacing's
`GD_CarDamage1..5` are marked and return 0.

**`GameRawData`.** `DataCorePlugin.DeclareProperties` declares it **once, from the reader's
`GetDataSample()`**, not from live data: every public field and property of the sample, recursively,
basic types always, an array as `Name01`, `Name02` and so on (one-based, two digits), and nothing for an
array or object that is null in the sample. An `IExposable` raw object (ACC's) publishes only what its
`GetValues` lists. `[JsonIgnore]` does not hide anything; `[DoNotExpose]` does. So:

- AC (`ACRawData`): `Graphics.*`, `Physics.*` (arrays filled in the sample, so `Physics.CarDamage01` to
  `05`, `Physics.TyreTempI01` to `04` and so on), `StaticInfo.*`, `Opponents.numVehicles` (the vehicle
  array is null in the sample, so no opponent is published raw), `Track.*`, `Car.*` and `Extensions.*`.
- ACC (`ACCRawData.GetValues`): `Graphics.*`, `Physics.*`, `StaticInfo.*`, `Track.*`, `Realtime.*` and
  one boolean per flag, `Graphics.FlagsDetails.IsACC_YELLOW_FLAG` and so on. `GetDataSample` fills the
  physics sample twice and the graphics sample never, so ACC's graphics arrays (`CarCoordinates`,
  `CarIDs`) are not published; the scalars are.
- LMU (`LMUWrapV2`): LMU's native page as `LMUNativeTelemetry.*` (scalars only: the per-vehicle arrays
  are null in the sample), the player's native entries as `PlayerNativeTelemetry.*` and
  `PlayerNativeScoring.*` (scalars), and the rFactor 2 plugin's page as `Data.*`, `CurrentPlayer.*`,
  `CurrentPlayerTelemetry.*` (with `mWheels01` to `04`), `extended.*`, `rules.*` and so on.

A raw name below is written after `DataCorePlugin.GameRawData.`.

## The readings, game by game

Every property OpenDash reads (`propertiesRead()` in `packages/dash/src/properties.ts`, which is also
every trace column), the `driver*()` functions the packages call, and what the plugin reads in
`OpenDash.DataUpdate`. **Filled** means the reader fills it with the meaning iRacing's has; **differs**
that it is filled with another meaning or unit; **empty** that it stays at the default (0, null or
empty); **computed** that the base computes it from other members the same way for every game.

What does not depend on the game at all: `DataCorePlugin.CurrentDateTime`, `DataCorePlugin.GameRunning`,
the units (`FuelUnit`, `TemperatureUnit`, `TyrePressureUnit`, `SpeedLocalUnit`, from SimHub's settings,
`StatusDataBase` constructor), every `OpenDash.*` property (the plugin's settings and the live order,
which `LivePosition.Applies` publishes for iRacing only), and `PersistantTrackerPlugin.*` (SimHub's lap
tracker, computed from the lap history any reader produces).

### Assetto Corsa (`ACManager`)

| Reading | Status | Source and notes |
|---|---|---|
| `SessionTypeName` | differs | `Graphics.Session.ToString().Replace("AC_", "")`: `PRACTICE`, `QUALIFY`, `RACE`, `HOTLAP`, `TIME_ATTACK`, `DRIFT`, `DRAG`, `UNKNOWN`, in capitals. NCalc's `=` is case-sensitive (`EvaluationVisitor.CompareUsingMostPreciseType` uses `Comparer.Default`), so `ucase()` comparisons work and `= 'Race'` does not. |
| `SessionTimeLeft` | filled | `Graphics.SessionTimeLeft` (ms) |
| `TotalLaps`, `RemainingLaps` | filled, computed | `Graphics.NumberOfLaps`; remaining is `TotalLaps - CompletedLaps` |
| `CompletedLaps`, `CurrentLap` | filled, computed | `Graphics.CompletedLaps`; `CurrentLap` is that plus one |
| `CurrentLapTime`, `LastLapTime`, `BestLapTime` | filled | `Graphics.iCurrentTime`, `iLastTime`, `iBestTime` (ms) |
| `Sector1..3LastLapTime`, `Sector1..3BestTime` | computed | `ACManager.PreParseData` keeps `Graphics.LastSectorTime` on entering sector 2 and 3; `GD_Sector2Time` adds the first, so the stored split is cumulative as `SectorTimes` expects |
| `Position` | filled | in a race the app's `carRealTimeLeaderboardPosition` (plus one if any is zero), otherwise `Graphics.Position` |
| `OpponentsCount`, `PlayerClassOpponentsCount` | computed | from `Opponents`; every AC car has a null class, so the class count is the field |
| `BestLapOpponent.BestLapTime`, `BestLapOpponentPosition`, `BestLapOpponentSameClassPosition` | computed | from `Opponents` |
| `TrackName` | differs | `TrackDesc.Track`, the track's folder name in lower case (`ks_red_bull_ring`), not its name; the name is raw `Track.TrackInfo.name`. `StaticInfo.Track` when no track was found. |
| `TrackLength` | filled | `StaticInfo.TrackSPlineLength` (m) |
| `AirTemperature`, `RoadTemperature` | filled | `Physics.AirTemp`, `Physics.RoadTemp` |
| `TrackGripStatus` | empty | not a member of `StatusDataBase` in any game; AC publishes raw `Graphics.surfaceGrip` as a number |
| `Rpms`, `Gear`, `SpeedLocal` | filled | `Physics.Rpms`; `Physics.Gear` less one, 0 as `N` and -1 as `R`; `Physics.SpeedKmh` |
| `Throttle`, `Brake`, `Clutch` | filled | `Physics.Gas`, `Physics.Brake` times 100; `100 - Physics.Clutch * 100` |
| `EngineIgnitionOn`, `EngineStarted` | computed | base default, `Rpms > 300` |
| `CarSettings_MaxGears` | filled | `Extensions.GearCount`, from the car's `drivetrain.ini`; 0 falls back to SimHub's learned count |
| `CarSettings_CurrentGearRedLineRPM`, `RPMShiftLight1/2`, `RPMRedLineReached`, `CurrentDisplayedRPMPercent` | computed | `GameManagerBase.SetRedlineInfos` from `StaticInfo.MaxRpm` and `GD_Redline`, which is the car's `ai.ini` `[GEARS] UP` (`Extensions.GearUpRpm`), used when SimHub's "sync redline from game" setting is on |
| `Fuel`, `Computed.Fuel_*` | filled, computed | `Physics.Fuel` (litres), `StaticInfo.MaxFuel`; the fuel calculator is `DataCorePlugin`'s, the same for every game |
| `OilPressure`, `OilTemperature`, `WaterTemperature` | empty | all three `[NotAvailable]`, 0 |
| `BrakeBias` | filled | `Physics.BrakeBias * 100`, AC's front share, not corrected per car |
| `TCLevel`, `ABSLevel` | differs | the preset index, one-based, found by matching `Physics.TC` or `Physics.Abs` against the car's `electronics.ini` curve (`LutManager.GetLut`); 0 when the amount is 0, 1 when the car has no curve and the system is on |
| `DRSAvailable` | filled | `Physics.DrsAvailable` |
| `PushToPassActive` | empty | `[NotAvailable]`, null |
| `IsInPitLane` | filled | `Graphics.isInPitLane` |
| `IsInPitSince`, `LastPitStopDuration` | computed | from `IsInPit`, which is `Graphics.IsInPit` (the box) |
| `PitLimiterOn` | **differs** | `ACManager.GD_PitLimiterOn` returns `GD_IsInPitLane()`: it is "in the pit lane", not the limiter. AC's own `Physics.PitLimiterOn` is raw only. |
| `PitLimiterSpeed` | empty | not overridden, null |
| `Flag_Yellow`, `Flag_Blue`, `Flag_Black`, `Flag_White`, `Flag_Checkered` | filled | `Graphics.Flags` equal to one value: AC shows one flag at a time |
| `Flag_Green` | empty | `[NotAvailable]`; AC has no green flag |
| `TyrePressure*` | differs | `Physics.WheelsPressure` (psi), the live pressure; iRacing's is the cold one (`LFcoldPressure`) |
| `TyreTemperature*` | differs | `Physics.TyreCoreTemperature`, the core; iRacing's is the middle tread measured in the pits (`LFtempCM`) |
| `TyreWear*` | filled | `Physics.TyreWear` mapped from 93.75..100 to 0..100 (`DefaultTyreWearMinimum` 93.75 in `GameRegistry`); 100 is new, as on iRacing |
| `SpotterCarLeft`, `SpotterCarRight` | computed | `GameManagerBase.SetSpotterData` from the opponents' coordinates: a car within 10 m at 45 to 135 degrees on that side, above 5 km/h and not in reverse; needs the app |
| `driver*()`: name, short name, initials, position, class position, best, last and current lap | filled with the app | the app's `driverName`, `carRealTimeLeaderboardPosition`, `bestLapMS`, `lastLapTimeMS`, `lapCount + 1`; short name and initials computed |
| `driver*()`: class, car number, team name, iRating, front tyre compound | empty | not set on an AC `Opponent` |
| `driver*()`: gaps, relative gap, position gain, pit count, pit duration, laps and time since pit out | computed | `ComputeOpponentsData` from progress and the app's `isCarInPit`, `isCarInPitline` |
| `GameRawData.Telemetry.*`, `SessionData.*` (iRacing's) | empty | the AC equivalents are listed under [The raw blocks](#what-the-raw-blocks-carry-that-gamedata-does-not) |

Without SimHub's AC app `Opponents.vehicle` is null, `GD_Opponents` returns the base's empty list,
and `PreParseDataBase` adds the player alone, **named `Me`**, with the player's position and best lap.

### ACC (`ACCManager`)

| Reading | Status | Source and notes |
|---|---|---|
| `SessionTypeName` | differs | `Graphics.Session.ToString().Replace("AC_", "")`, the same capitals as AC. SimHub's enum stops at `AC_DRAG` (6), so a session ACC numbers beyond it reads as a number. |
| `SessionTimeLeft` | filled | `Graphics.SessionTimeLeft` (ms) |
| `TotalLaps`, `CompletedLaps`, lap times | filled | `Graphics.NumberOfLaps`, `CompletedLaps`, `iCurrentTime`, `iLastTime`, `iBestTime`; the three times read as 0 when they are 214748300 or more, ACC's "no time" |
| `RemainingLaps`, `CurrentLap` | computed | by the base (`PreParseDataBase`), as on AC: `TotalLaps - CompletedLaps` and never below 0, and `CompletedLaps` plus one |
| `Sector*` | computed | as AC, but `GD_Sector2Time` returns the second `LastSectorTime` alone rather than adding the first. Whether ACC's `lastSectorTime` is already cumulative decides whether sector 2 is right; see [Needs a drive](#needs-a-drive). |
| `Position` | filled | `Graphics.Position` |
| `OpponentsCount`, `PlayerClassOpponentsCount`, `BestLapOpponent*` | computed | from the broadcast opponents |
| `TrackName` | filled | the broadcast `TrackData.TrackName`, else `StaticInfo.Track` |
| `TrackLength` | filled | a table of 25 tracks in `ACCManager.trackLength`, else the broadcast `TrackMeters` |
| `AirTemperature`, `RoadTemperature` | filled | `Physics.AirTemp`, `Physics.RoadTemp`, else the broadcast ambient and track temperatures |
| `TrackGripStatus` | empty | not a `GameData` member; ACC's is raw `Graphics.trackGripStatus` (`ACC_GREEN` to `ACC_FLOODED`) |
| `Rpms`, `Gear`, `SpeedLocal`, pedals | filled | as AC |
| `EngineIgnitionOn`, `EngineStarted` | filled | `Physics.ignitionOn`, `Physics.isEngineRunning` |
| `CarSettings_MaxGears` | computed | `GD_MaxGears` returns 0, so it is the highest gear SimHub has seen in that car (`CarManager`, kept between sessions), which is short of the top gear until the driver has used it; ACC publishes no gear count |
| `CarSettings_*` redline and shift lights | computed | from `StaticInfo.MaxRpm`; `GD_Redline` is the base's 0 |
| `Fuel`, `Computed.Fuel_*` | filled, computed | `Physics.Fuel`, `StaticInfo.MaxFuel` |
| `OilPressure`, `OilTemperature` | empty | `[NotAvailable]` |
| `WaterTemperature` | filled | `Physics.waterTemperature` |
| `BrakeBias` | filled | `Physics.BrakeBias * 100`, corrected per car by `LookupTables\AssettoCorsaCompetizione.BrakeBiasCorrection.json` |
| `TCLevel`, `ABSLevel` | filled | `Graphics.TC`, `Graphics.ABS`, the levels the driver dialled. `GD_ABSLevel` carries `[NotAvailable]` although it is filled. |
| `DRSAvailable` | filled | `Physics.DrsAvailable`, 0 on every ACC car |
| `PushToPassActive` | filled | `Physics.P2PActivation > 0`, false on every ACC car |
| `IsInPitLane`, `IsInPitSince`, `LastPitStopDuration` | filled, computed | `Graphics.IsInPitLane`, `Graphics.IsInPit` |
| `PitLimiterOn` | filled | `Physics.PitLimiterOn` |
| `PitLimiterSpeed` | differs | a constant 60 km/h (`ACCManager.GD_PitLimiterSpeed`) on every track |
| `Flag_*` | filled | `Graphics.Flag`, plus `globalChequered` for the chequered, `globalWhite` for the white, the current sector's `globalYellow1..3` for the yellow, and green for a second after a yellow ends; `Flag_Orange` from `ACC_ORANGE_FLAG` |
| `TyrePressure*`, `TyreTemperature*`, `TyreWear*` | differs, differs, filled | as AC: live pressure, core temperature, wear mapped from 93.75 |
| `SpotterCar*` | computed | from the broadcast opponents' coordinates (`Graphics.CarCoordinates` by `CarIDs`) |
| `driver*()`: name, initials, team, car number, class, position, laps, best and last lap | filled with broadcasting | `RealtimeCarUpdate` and its `CarEntry`: `FirstName LastName`, `ShortName`, `TeamName`, `RaceNumber`, the class from `LookupTables\AssettoCorsaCompetizione.CarClasses.csv` |
| `driver*()`: gaps, pit statistics, relative gap | computed | `ComputeOpponentsData`; in the pit is `CarLocation` containing "pit" |
| `driver*()`: iRating, front tyre compound | empty | not set |

### Le Mans Ultimate (`LMUManager`, over `RF2ManagerBase<LMUWrapV2>`)

| Reading | Status | Source and notes |
|---|---|---|
| `SessionTypeName` | differs | from `scoringInfo.mSession`: `Test day` (0), `Practice` (1 to 4), `Qualify` (5 to 8), `Warmup` (9), `Race` (10 to 13) |
| `SessionTimeLeft` | filled | `scoringInfo.mSessionTimeRemaining` |
| `TotalLaps` | filled | `scoringInfo.mMaxLaps` when it is between 1 and 999, else 0 |
| `CompletedLaps` | filled | the player's `mTotalLaps` |
| `RemainingLaps`, `CurrentLap` | computed | by the base (`PreParseDataBase`), as on AC: `TotalLaps - CompletedLaps` and never below 0, and `CompletedLaps` plus one |
| `CurrentLapTime` | filled | `mElapsedTime - mLapStartET` of the native telemetry |
| `LastLapTime`, `BestLapTime` | filled | the player's `mLastLapTime`, `mBestLapTime` (seconds; LMU's -1 for none is clamped to 0 by the base) |
| `Sector*` | computed | `RF2ManagerBase.GD_Sector1Time`, `GD_Sector2Time`: the rFactor 2 plugin's `mCurSector1`, `mCurSector2`, cumulative |
| `Position` | filled | the player's `mPlace` |
| `OpponentsCount`, `BestLapOpponent*` | computed | from LMU's own vehicle list |
| `PlayerClassOpponentsCount` | computed | by `mVehicleClass` |
| `TrackName`, `TrackLength` | filled | `scoringInfo.mTrackName`, `scoringInfo.mLapDist` |
| `AirTemperature`, `RoadTemperature` | filled | `scoringInfo.mAmbientTemp`, `scoringInfo.mTrackTemp` |
| `TrackGripStatus` | empty | not a `GameData` member; LMU's is raw `scoringInfo.mTrackGripLevel` |
| `Rpms`, `Gear`, `SpeedLocal` | filled | `mEngineRPM`, `mGear`, the length of `mLocalVel` |
| `Throttle`, `Brake`, `Clutch` | filled | `mUnfilteredThrottle`, `mUnfilteredBrake`, `mUnfilteredClutch` times 100: the driver's input, not the car's after the aids |
| `EngineIgnitionOn`, `EngineStarted` | filled, computed | `mIgnitionStarter > 0`; started is `Rpms > 300` |
| `CarSettings_MaxGears` | filled | `mMaxGears` |
| `CarSettings_*` redline and shift lights | computed | from `mEngineMaxRPM`; `GD_Redline` is 0 |
| `Fuel`, `Computed.Fuel_*` | filled, computed | `mFuel`, `mFuelCapacity` |
| `OilPressure` | empty | `[NotAvailable]` |
| `OilTemperature`, `WaterTemperature` | filled | `mEngineOilTemp`, `mEngineWaterTemp` |
| `BrakeBias` | filled | `100 - mRearBrakeBias * 100`, the front share |
| `TCLevel`, `ABSLevel` | filled | `mTC`, `mABS` |
| `DRSAvailable`, `PushToPassActive` | empty | `[NotAvailable]` |
| `IsInPitLane` | filled | the player's `mInPits` |
| `IsInPitSince`, `LastPitStopDuration` | **differs** | `GD_IsInPit` is `mInPits` too, so the "in the box" timer runs from the pit entry line and the last stop is the whole pit lane |
| `PitLimiterOn` | filled | `mSpeedLimiter > 0` |
| `PitLimiterSpeed` | empty | `GD_PitLimiterSpeed` calls the base's null |
| `Flag_Blue` | filled | the player's `mFlag == 6` |
| `Flag_Yellow` | differs | any of `scoringInfo.mSectorFlag` is 1: a yellow anywhere on track, not in the driver's sector |
| `Flag_Green` | differs | for five seconds after `mGamePhase` becomes 5 |
| `Flag_Black` | **differs** | the player's `mFinishStatus` is 2 (did not finish) or 3 (disqualified): it lights for a retirement or a disqualification, not for a black flag shown on track |
| `Flag_Checkered`, `Flag_White` | **empty** | `[NotAvailable]`, 0 |
| `TyrePressure*` | differs | `mPressure` (kPa) times 0.145, live |
| `TyreTemperature*` | differs | the mean of the three tread temperatures `mTemperature` less 273.15 |
| `TyreWear*` | filled | `mWear * 100`, 100 new |
| `SpotterCar*` | computed | from the native vehicles' `mPos` |
| `driver*()`: name, team, car number, class, position, laps, best and last lap, compound | filled | `mDriverName`, `mPitGroup` (the team), the number after `#` in `mVehicleName`, `mVehicleClass`, `mPlace`, `mTotalLaps`, `mBestLapTime`, `mLastLapTime`, `mFrontTireCompoundName` through `GetCompound` |
| `driver*()`: gap to leader | filled | `mTimeBehindLeader`, null when a lap or more down with no time |
| `driver*()`: pit statistics | computed | in the pit lane is `mInGarageStall` or bit 31 of `mCurrentSector`; in the box is the lane, `mPitState == 3` and below 5 km/h |
| `driver*()`: iRating | empty | not set |

### iRacing's raw properties, on the other three

Every `GameRawData` property OpenDash reads is iRacing's, so each is null on Assetto Corsa, ACC and
Le Mans Ultimate: their raw blocks have other names (next section). What a null does to the reading:

| Property | What reads it | On the other three | Ticket |
|---|---|---|---|
| `SessionData.DriverInfo.DriverCarGearNumForward` | top gear, the gear ghost | the fallback, which is wrong | #996 |
| `SessionData.DriverInfo.DriverCarSLBlinkRPM`, `SLFirstRPM`, `SLLastRPM`, `SLShiftRPM` | the car's own shift ladder | SimHub's bands | #748 |
| `SessionData.WeekendInfo.WeekendOptions.IncidentLimit`, `Telemetry.PlayerCarMyIncidentCount` | incidents | hidden in the bar and the pit wall's header, `--` in band D and the session module | #567 |
| `Telemetry.SteeringWheelAngle` | the steering dial and trace | `Physics.SteerAngle` on Assetto Corsa and ACC, flat on LMU | #567 |
| `Telemetry.SessionFlagsDetails.Is*` (15) | band D, the flag box, the strips' flag lamps | no flag | #551 |
| `Telemetry.IsOnTrack` | "in the car" for the alerts and notifications | the plugin's `DriverInCar`, and without the plugin unknown, read as not in the car for the car alerts and as in it everywhere else | #561 |
| `Telemetry.LFtempCM`, `RFtempCM`, `LRtempCM`, `RRtempCM` | band D's tyre page | `0 0 0 0` | #556 |
| `Telemetry.PitSvFlags`, `PitSvFuel`, `dpLFTireChange` and the other three | the pit view's service | a stop nobody set | #564 |
| `Telemetry.dcPitSpeedLimiterToggle` | "the car has a limiter", the pit alerts | alerts off | #558 |
| `Telemetry.LapDeltaToSessionLastlLap`, `_OK` | the last-lap delta reference | a level `+0.00` | #575 |
| `Telemetry.WindVel` | the wind on band D and the pit wall header | **`0 km/h`, drawn as a value** | none, reported with this page |
| `Telemetry.SessionTimeOfDay` | the sim clock on the bar and band D | **`00:00`, drawn as a value** | none, reported with this page |
| `Telemetry.SessionTimeTotal` | the untimed-session mark | rests on `SessionTimeLeft` alone | none needed |
| `Telemetry.EngineWarnings` | the engine telltale, the AiM and Porsche engine warnings, the oil pressure LED | dark | none needed |
| `Telemetry.FuelPress`, `ManifoldPress`, `Voltage` | the engine readings page | `--` | none needed |
| `Telemetry.VirtualEnergy`, `VirtualEnergyLaps`, `VirtualEnergyPerLap`, `VirtualEnergyRefuel` | the energy module and band D's energy fields | not available (LMU has it raw) | #962 |
| `Telemetry.PushToPass` | band D's P2P chip | dark, rightly | none needed |
| `Telemetry.dcABS`, `dcTractionControl` | "the car has TC and ABS" | falls back to the level | #549, closed |
| `Telemetry.dcTractionControl2`, `dcThrottleShape`, `dcFuelMixture`, `dcAntiRollFront`, `dcAntiRollRear` | the car settings fields and their change notifications | hidden | none needed |
| `Telemetry.dcHeadlightFlash` | the flash alert, the Porsche strip | never fires | none needed |

`DataCorePlugin.GameData.TrackGripStatus` belongs here too although it is spelled as a `GameData`
member: no reader publishes it, iRacing's included, because `StatusDataBase` has no such member.

The Porsche's low-fuel alarm read `Telemetry.FuelLevel` when this page was written, so it never lit on
the other three. It reads `GameData.Fuel` now, turned back into litres on a gallons profile, which on
iRacing is the same `FuelLevel` (#1126).

## What the raw blocks carry that `GameData` does not

The readings the A3 tickets need, by the raw name SimHub publishes. iRacing's is given first for
reference.

| Reading | iRacing | Assetto Corsa | ACC | Le Mans Ultimate |
|---|---|---|---|---|
| In the car (#561) | `Telemetry.IsOnTrack` | `Graphics.Status` is 2 (`AC_LIVE`); 1 is a replay | `Graphics.Status` is 2; `Graphics.IsSetupMenuVisible` for the setup screen | `LMUNativeTelemetry.scoring.scoringInfo.mInRealtime` (and the plugin's `Data.mInRealtime`) |
| Steering (#567) | `Telemetry.SteeringWheelAngle`, radians, positive left (#1057) | `Physics.SteerAngle`, passed through unconverted | `Physics.SteerAngle`, unconverted; Kunos documents it as the input from -1 to 1, and the car's lock is not published | `PlayerNativeTelemetry.mUnfilteredSteering` (-1 to 1, left to right by the rFactor 2 SDK's description) and `mPhysicalSteeringWheelRange` (degrees, lock to lock); no `GameData` member |
| Pit limiter on (#558) | `GameData.PitLimiterOn` | **`Physics.PitLimiterOn`** (the `GameData` one is the pit lane) | `GameData.PitLimiterOn` | `GameData.PitLimiterOn`; `PlayerNativeTelemetry.mSpeedLimiterActive` |
| The car has a limiter (#558) | `Telemetry.dcPitSpeedLimiterToggle` present | every car | every car | `PlayerNativeTelemetry.mSpeedLimiterAvailable` |
| Pit service (#564) | `Telemetry.PitSvFlags`, `PitSvFuel`, `dp*TireChange` | none | `Graphics.mfdFuelToAdd`, `mfdTyreSet`, `mfdTyrePressureLF` to `RR` | none read by SimHub (the plugin's pit info buffer is not mapped); `PlayerNativeTelemetry.mScheduledStops` |
| Flags (#551) | `Telemetry.SessionFlagsDetails.Is*` | `Graphics.Flags` (one value) | `Graphics.Flag`, `Graphics.FlagsDetails.IsACC_*`, `globalYellow`, `globalYellow1..3`, `globalWhite`, `globalGreen`, `globalChequered`, `globalRed`, `Penalty` | `PlayerNativeScoring.mFlag`, `mUnderYellow`, `mFinishStatus`; `scoringInfo.mGamePhase` (5 green, 6 full course yellow, 8 over), `mYellowFlagState`, `mStartLight`; the plugin's `Data.mSectorFlag01..03` |
| Tyre temperatures (#556) | `Telemetry.LFtempCM` and so on, the same as `GameData.TyreTemperature*` | `Physics.TyreTempI`, `TyreTempM`, `TyreTempO` `01..04`; `GameData.TyreTemperature*Inner/Middle/Outer` swap I and O on the left side | the same, and `Physics.tyreTemp01..04` | `GameData.TyreTemperature*Inner/Middle/Outer` from `mTemperature`; the plugin's `CurrentPlayerTelemetry.mWheels01..04.mTemperature01..03` and `mTireCarcassTemperature` |
| Damage (#582) | none (`GameData.CarDamage*` `[NotAvailable]`, 0) | `Physics.CarDamage01..05` = `GameData.CarDamage1..5`, unscaled | the same, and `Physics.suspensionDamage01..04` | `GameData.CarDamage*` `[NotAvailable]`; the plugin's `CurrentPlayerTelemetry.mDentSeverity01..08`, `mDetached` |
| Incidents (#567) | `Telemetry.PlayerCarMyIncidentCount`, `WeekendOptions.IncidentLimit` | none | none (`Graphics.Penalty` is a typed penalty) | none (`PlayerNativeScoring.mNumPenalties`, `PlayerNativeTelemetry.mTrackLimitsSteps`) |
| Delta to last lap (#575) | `Telemetry.LapDeltaToSessionLastlLap` | none | `Graphics.iDeltaLapTime`, to the best lap, as `GameData.DeltaToSessionBest` | `PlayerNativeTelemetry.mDeltaBest`, to the best lap |
| Gear count (#996) | `DriverInfo.DriverCarGearNumForward` | `Extensions.GearCount`, also `GameData.CarSettings_MaxGears` | none; `CarSettings_MaxGears` is learned | `PlayerNativeTelemetry.mMaxGears`, also `CarSettings_MaxGears` |
| Shift points (#748) | `DriverInfo.DriverCarSL*RPM` | `Extensions.GearUpRpm` (the car's `ai.ini`) | none | none |
| Wind | `Telemetry.WindVel`, `WindDir` | none | `Graphics.WindSpeed`, `Graphics.WindDirection` | `scoringInfo.mWind.x`, `.y`, `.z` |
| Time of day | `Telemetry.SessionTimeOfDay` | none | `Graphics.clock` | `scoringInfo.mTimeOfDay` |
| Session length | `Telemetry.SessionTimeTotal` | none | none | `scoringInfo.mEndET` |
| Track grip | none | `Graphics.surfaceGrip` | `Graphics.trackGripStatus`, `rainIntensity`, `rainIntensityIn10min`, `In30min` | `scoringInfo.mTrackGripLevel`, `mAvgPathWetness`, `mRaining` |
| Engine warnings | `Telemetry.EngineWarnings` | none | none | `PlayerNativeTelemetry.mOverheating` |
| Virtual energy | `Telemetry.VirtualEnergy*` (null on iRacing too) | none | none | `PlayerNativeTelemetry.mVirtualEnergy`, `mSoC`, `mBatteryChargeFraction`, `mRegen` |
| Car settings | `Telemetry.dcTractionControl2`, `dcThrottleShape`, `dcFuelMixture`, `dcAntiRoll*` | none (`Extensions.TCPresetCount`, `ABSPresetCount`) | `Graphics.TCCut`, `Graphics.EngineMap` | `PlayerNativeTelemetry.mTCCut`, `mTCSlip`, `mMotorMap`, `mFrontAntiSway`, `mRearAntiSway`, each with a `Max` |
| Fuel to the end, pit window | none | `StaticInfo.PitWindowStart`, `PitWindowEnd`, `Graphics.MandatoryPitDone` | the same, `Graphics.missingMandatoryPits`, `fuelEstimatedLaps` | none |
| Tyre compound, player | none | `Graphics.TyreCompound` | `Graphics.TyreCompound`, `currentTyreSet` | the player's `driverfronttyrecompound` |
| Headlight flash, push to pass | `Telemetry.dcHeadlightFlash`, `PushToPass` | none | `Graphics.FlashingLights`; `Physics.P2PStatus` | `PlayerNativeTelemetry.mHeadlights` |

## #592's rows, as far as the source answers them

The Assetto Corsa worklist in #592 asked these of the AC column. What remains is gathered under
[Needs a drive](#needs-a-drive).

- **Brake bias.** `Physics.BrakeBias * 100`, so `50.5` and not `0.5`, rounded to 0.1. It is the front
  share by Kunos's description; SimHub applies no per-car correction on AC (it does on ACC).
- **TC and ABS levels.** The preset index the car's `electronics.ini` curve gives for the configured
  amount, 0 when off. A car without the system reads 0, which is what #549's "the car has it" closed on.
- **Sector times.** SimHub computes three: the first two from `lastSectorTime` on entering sectors 2 and
  3, the third from the lap. The three best times are filled.
- **Session.** The names arrive in capitals with underscores (above). `inSession()` works, since none is
  empty. `untimedMark` reads `SessionTimeTotal`, which AC does not have, so it rests on `SessionTimeLeft`.
- **Spotter.** Fires from car coordinates within 10 m beside the car, only with SimHub's AC app running.
- **Grip.** `TrackGripStatus` is not published by SimHub for any game; the band and the pit wall header
  draw `--` everywhere, iRacing included.
- **Fuel.** `Physics.Fuel` in litres and the same calculator as every game.
- **Tyres.** Live pressure in psi, core temperature, wear on SimHub's remaining-tread scale (100 new)
  after mapping AC's 93.75..100. `fronttyrecompound` is null for every AC car, so the field reads `--`;
  the player's compound is raw `Graphics.TyreCompound`.
- **Car and class.** `CarModel` is `ui_car.json`'s `name`, `CarId` the car's folder. SimHub gives no AC
  opponent a class, so the class filter treats the field as one class.
- **Gears.** `CarSettings_MaxGears` is the car's `drivetrain.ini` count; `DriverCarGearNumForward` is
  absent (#996).
- **Shift bands.** `CarSettings_*` from `StaticInfo.MaxRpm`, with the car's `ai.ini` upshift point as the
  game's redline.
- **DRS and P2P.** `DRSAvailable` is AC's own; `PushToPassActive` is null, so the chip stays dark.
- **Opponents.** With SimHub's app: name, car, position, best, last and current lap, pit state and world
  position; no class, number, team, iRating or compound. Without it: one row, the player, named `Me`.
- **Clock and weather.** Air and road temperatures are filled. `WindVel` and `SessionTimeOfDay` are absent,
  and the wind field and the sim clock draw `0 km/h` and `00:00` rather than hiding.
- **Telltales.** `EngineWarnings` is absent, so the engine lamp stays dark. The limiter lamp reads
  `GameData.PitLimiterOn`, which on AC is the pit lane.
- **LED effects.** `TCActive` and `ABSActive` come from AC's UDP interface (`isTcInAction`,
  `isAbsInAction`), so they light only once SimHub's UDP client is connected to the game. The turn
  indicators are `[NotAvailable]`. `ERSPercent` is the KERS or ERS charge over its maximum.

## For #1101: the capabilities the plugin publishes

A dashboard never names a game, so each reading that differs above becomes an `OpenDash.*` property
whose value the plugin derives per game from `GameData.GameName`. The per-game table below is what it
fills; `DataCorePlugin.GetPropertySupportStatus` can seed it but not decide it (it misses AC's limiter,
which is filled with the pit lane, and marks ACC's filled `ABSLevel`).

| Capability | iRacing | Assetto Corsa | ACC | Le Mans Ultimate |
|---|---|---|---|---|
| The car has a pit limiter | `dcPitSpeedLimiterToggle` present | yes | yes | `mSpeedLimiterAvailable` |
| The limiter is on | `GameData.PitLimiterOn` | raw `Physics.PitLimiterOn` | `GameData.PitLimiterOn` | `GameData.PitLimiterOn` |
| Flags, by kind | `SessionFlagsDetails` | `Graphics.Flags`, no green | `Graphics.Flag` and the globals, a sector yellow | `mFlag`, `mGamePhase`, `mSectorFlag`, `mYellowFlagState`; no chequered or white in `GameData` |
| In the car | `IsOnTrack` | `Graphics.Status == 2` | `Graphics.Status == 2` | `mInRealtime` |
| Pit service | yes | no | the MFD's fuel and tyre set | no |
| Incidents | yes | no | no | no |
| Steering angle, positive left, in degrees | `SteeringWheelAngle` in degrees, not negated (#1057) | `Physics.SteerAngle`, its unit and sign for the author's wheel | `Physics.SteerAngle`, an input with no lock | `-mUnfilteredSteering * mPhysicalSteeringWheelRange / 2` |
| A delta to the last lap | yes | no | no (one to the best) | no (one to the best) |
| Damage | no | `CarDamage1..5` | `CarDamage1..5` | no |
| Opponents beyond the player | yes | only with SimHub's AC app | only with broadcasting | yes |
| Track name for people | `TrackName` | raw `Track.TrackInfo.name` | `TrackName` | `TrackName` |
| Track grip | no | a number | a word | a level |
| Wind and time of day | yes | no | yes | yes |
| Pit lane speed limit | yes | no | a constant 60 km/h | no |
| Virtual energy | no | no | no | yes |
| Engine warnings | yes | no | no | overheating only |
| Session names | iRacing's words | capitals, `AC_` stripped | capitals | `Test day`, `Practice`, `Qualify`, `Warmup`, `Race` |
| The pit-box timer is the box | yes | yes | yes | no, it is the lane |

Two readings need no capability, only a different property: the tyre temperature page (#556) can read
`GameData.TyreTemperature*`, which is `LFtempCM` on iRacing and each game's own elsewhere, and the gear
count (#996) can read `GameData.CarSettings_MaxGears`, which AC and LMU fill from the car and SimHub
learns on iRacing and ACC (so a car's first laps there read the highest gear used so far).

## For #579: what an Assetto Corsa emulator must provide

In the order SimHub checks it:

1. **SimHub's current game is `AssettoCorsa`.** Selected in the window, by `-switchgame AssettoCorsa`, or
   by `LastGame` at start. Automatic switching needs a licence, so it cannot be relied on.
2. **A process named `acs.exe`** (or `acs_x86`, `acs_pro`, `acs_dmm`), running from a folder that holds:
   - `content\tracks\` (required) with `<StaticInfo.Track>\` inside, lower case, and optionally its
     `ui\ui_track.json` (`{"name": ...}`), `data\map.ini` and `map.png`;
   - `content\cars\<StaticInfo.CarModel>\` (required unless `Documents\Assetto Corsa\cfg\race.ini` names
     the car), optionally with `ui\ui_car.json` (`{"name": ..., "class": ...}`), and `data\drivetrain.ini`
     (`[GEARS] COUNT=`), `data\ai.ini` (`[GEARS] UP=`) and `data\electronics.ini` with its TC and ABS
     curves, read before any `data.acd`.
3. **`Local\acpmf_physics`, `Local\acpmf_graphics` and `Local\acpmf_static`**, laid out as
   `ACSharedMemory.MMFModels.Physics`, `Graphics` and `StaticInfo` (sequential, `Pack = 4`, Unicode
   strings of 15 and 33 characters in the static page, arrays of 3, 4 and 5 floats in physics).
4. **`Graphics.Status = 2`** for a live session; 3 pauses it, 0 and 1 end it. `StaticInfo.PlayerName` names
   the player in the opponent list.
5. Optionally **`Local\acpmf_simhub_v2`** (`SPageFileCrewChief`: two ints, a 512-byte server name, 128
   `acsVehicleInfo` of `Pack = 4`, a 512-byte path, an int and a 32-byte version), with `isConnected > 0`
   per car and the player's `driverName` equal to `StaticInfo.PlayerName`, for the leaderboard, the
   relative and the spotter.
6. Optionally **a UDP server on 127.0.0.1:9996** speaking AC's `RTCarInfo` protocol, for `TCActive` and
   `ABSActive`.

The shared-memory names are the same for ACC; an ACC emulator differs in the process name
(`AC2-Win64-Shipping`), the longer layouts, a `Graphics.PacketId` that must keep changing, and a
broadcasting server on the UDP port for opponents. LMU needs both LMU's `LMU_Data` page and the
rFactor 2 plugin's five buffers with their version blocks, and a process named `Le Mans Ultimate`.

## Needs a drive

What the source cannot settle, to check in the drives #592, #961 and #962 ask for:

- **Assetto Corsa's steering.** `Physics.SteerAngle` is published as AC writes it, and the dash reads it
  since #567 as the input from -1 to 1 of full lock, positive to the right, which is Marvin's AIRA's
  reading (see [simhub-dash-format.md](simhub-dash-format.md)). The author's wheel test confirms both: a
  right turn moves the dial's mark to the right, and full lock reaches its end.
- **LMU's steering sign.** The structs carry no comment; left to right is the rFactor 2 SDK's
  description of `mUnfilteredSteering`, which LMU inherits, and the same wheel test confirms it.
- **ACC's second sector.** Whether `Graphics.LastSectorTime` is the sector's own time or the split from
  the line: if it is the sector's own, SimHub's `Sector2*` on ACC are wrong.
- **ACC's pit limit.** Whether 60 km/h, SimHub's constant, is the limit on every ACC track.
- **The damage scale.** `CarDamage1..5` are AC's and ACC's numbers unscaled, in the order front, rear,
  left, right, centre by Kunos's ACC documentation; the range a crash reaches is for a drive.
- **LMU's time of day and virtual energy units.** `mTimeOfDay` and `mVirtualEnergy` are floats whose
  units LMU does not document in the structs.
- **ACC's session names past 6.** Which sessions ACC numbers 7 and 8 and what `SessionTypeName` then reads.
- **The track name ACC's broadcast gives.** A display name or the track's code.
