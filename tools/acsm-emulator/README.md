# acsm-emulator

A synthetic **Assetto Corsa shared-memory telemetry emulator** for SimHub, the AC twin of
[irsdk-emulator](../irsdk-emulator/README.md). It creates the three memory-mapped files the game
creates, `Local\acpmf_physics`, `Local\acpmf_graphics` and `Local\acpmf_static`, and the one SimHub's
own AC app writes from inside the game, `Local\acpmf_simhub_v2`, fills them byte for byte as SimHub's
Assetto Corsa reader (`ACSharedMemory.ACManager`) reads them, and drives the values from a JSON
scenario at 60 Hz. The VM has no Assetto Corsa and no licence for one, and SimHub cannot tell the
emulator from the game, so an AC dashboard can be checked with real, scripted values (#579).

Single net48 console exe, no dependencies besides the .NET Framework 4.8 runtime. The scenario reader
is the irsdk emulator's `Json.cs`, compiled in from there.

## Why the executable is called acs.exe

SimHub's AC reader does not start from the shared memory. `ACManager.IsGameRunning` first looks for a
process named `acs`, `acs_x86`, `acs_pro` or `acs_dmm` (`ACHelper.acProcessNames`), takes the folder it
runs from as the game's install, and only then opens the three mappings. Every frame it then reads the
car from `content\cars\<StaticInfo.CarModel>` and lists `content\tracks` under that folder, and when
either is missing it throws on every frame and publishes nothing (the research of #1100 has the whole
sequence, class by class).

So the emulator is that process: its assembly is named `acs`, and the build copies a fake install,
`content\`, beside it. That is the least invasive way to satisfy the reader. Nothing else on the VM is
renamed, nothing is installed where the game would be, and stopping the emulator is the game exiting.
The fake install holds only the files the reader opens, written for OpenDash rather than taken from the
game:

| | What SimHub reads from it |
|---|---|
| `content/cars/ks_porsche_911_gt3_r_2016/ui/ui_car.json` | the car's name, published as `GameData.CarModel` |
| `.../data/drivetrain.ini` | `[GEARS] COUNT`, published as `CarSettings_MaxGears` |
| `.../data/ai.ini` | `[GEARS] UP`, the redline SimHub syncs its shift lights to |
| `.../data/electronics.ini`, `tc.lut`, `abs.lut` | the TC and ABS curves `Physics.TC` and `Physics.Abs` are matched against, published as `TCLevel` and `ABSLevel` |
| `content/cars/ks_mazda_mx5_cup/...` | a second car with five gears and a lower redline, for `shiftlights`; no `electronics.ini`, so its levels read 0 |
| `content/tracks/spa/ui/ui_track.json` | the track's name, raw `Track.TrackInfo.name`; `GameData.TrackName` is the folder, `spa` |

The car folder names are AC's own codes so that the values look like the game's; the files in them
are not the game's.

## Selecting the game

SimHub runs one game's reader at a time, the one selected in its window, and switches by itself only
when automatic switching is on, the game's process is in its list **and** it holds a licence
(`MainWindow.autodetectTimer_Tick`). The VM's SimHub cannot be relied on for any of that, so
`bun run dev --sim ac` chooses: while SimHub is stopped for the install it writes `LastGame` in
`PluginsData\ContextSimhubSettings.json`, which is what SimHub opens on (`MainWindow.LoadData`), and
starts it. Every other start the loop makes writes `IRacing` there, so a run of iRacing after one of AC
gets iRacing whatever was left selected. Selecting **Assetto Corsa** by hand in SimHub's *Games* list
does the same.

SimHub has an installer for its AC app (`ACInstallManager`) and may offer it when AC is selected; there
is no game to install it into, and the emulator's `acpmf_simhub_v2` stands in for the app, so such an
offer can be dismissed. Whether it appears at all is for the first run on the VM to say.

## Run it with `bun run emulator`

```bash
bun run emulator start green --sim ac --follow   # build, upload with content\, start in the desktop
bun run emulator stop --sim ac
bun run dev --sim ac --scenario yellow             # the whole loop, with SimHub switched to AC
```

The runner (`scripts/emulator.ts`) does what [the irsdk README](../irsdk-emulator/README.md) describes:
it uploads the exe with `scenarios\` and `content\` to `C:\Temp\acsm-emulator`, launches it in the
interactive desktop (the `Local\` names are per logon session, and one created over SSH is invisible to
SimHub), refuses to start a second copy and stops through the stop file, which sets `Graphics.Status`
to `AC_OFF` before the process exits.

By hand, from a desktop prompt:

```text
acs.exe                          # scenarios\race.json next to the exe
acs.exe yellow                   # scenarios\yellow.json
acs.exe green --log C:\Temp\acsm-emulator\emulator.log --stop-file C:\Temp\acsm-emulator\stop.txt
acs.exe --selfcheck pit          # layout and install self-test, exit code 0 == pass
```

It prints one line per second:

```text
[   61.0s] packet 3660 | AC_LIVE AC_RACE left 1459 s | rpm 4528 G2 63 km/h | lap 13/30 cur 97.14 last 98.412 best 97.905 | fuel 36.6 | flag AC_NO_FLAG | pos 3/24 | TC 0.06 ABS 0.2 | pit 00 limiter 0 | tyre 86C
```

and log lines for completed laps (`lap 13 completed in 98.000 s`, the same line the irsdk emulator
writes, which `bun run shots` counts), flag changes and timeline events.

| option | meaning |
|---|---|
| `scenario` | a path, or a name resolved as `scenarios/<name>.json` next to the exe (default `race`) |
| `--duration <s>` | stop after `<s>` simulated seconds |
| `--stop-file <path>` | stop cleanly once the file exists |
| `--log <file>` | append all console output to a file |
| `--quiet` | no per-second status line |
| `--dry-run` [`--fast`] | simulate without the shared memory (`--fast` does not pace to real time) |
| `--dump` | print every value of the three pages after one tick and exit |
| `--selfcheck` | the self-test below |

## The pages

Laid out by declaring the very structs SimHub declares, field for field and attribute for attribute,
copied from SimHub 9.12.6's decompiled `ACSharedMemory.dll` into [Structs.cs](Structs.cs), and written
with `Marshal.StructureToPtr`, which is the inverse of the `Marshal.PtrToStructure` SimHub reads them
with. Sequential, `Pack = 4`, Unicode strings of 15 and 33 characters, a C# `bool` as a four-byte BOOL:

```text
Local\acpmf_physics    Physics             580 bytes   Rpms @20, TyreCoreTemperature @152, CarDamage @224, PitLimiterOn @248, BrakeBias @564
Local\acpmf_graphics   Graphics            288 bytes   Status @4, CompletedLaps @132, TyreCompound @176, Flags @268, isInPitLane @276
Local\acpmf_static     StaticInfo          684 bytes   CarModel @68, MaxRpm @412, TrackSPlineLength @520, PitWindowEnd @680
Local\acpmf_simhub_v2  SPageFileCrewChief  30252 bytes  128 acsVehicleInfo of 228 bytes from @520
```

Each mapping is the struct rounded up to whole 4 KB pages, created or, when SimHub still holds one from
an earlier run, opened.

**Both `PacketId`s are the tick.** SimHub publishes `Graphics.PacketId` as
`DataCorePlugin.GameRawData.Graphics.PacketId`, and the trace recorder keys its frames on it for an AC
recording, as it keys iRacing's on `SessionTick`.

## Scenario format

JSON with comments and trailing commas, like the irsdk emulator's. A value is named as SimHub publishes
it after `DataCorePlugin.GameRawData.`, so a scenario reads like the property list a dashboard author
sees:

```jsonc
{
  "name": "...",
  "extends": "race.json",                 // optional: merged on top of another scenario
  "tickRate": 60, "seed": 12345,
  "app": true,                            // false: SimHub's AC app is not running, no acpmf_simhub_v2
  "values": {                             // written once at the start
    "StaticInfo.CarModel": "ks_porsche_911_gt3_r_2016",
    "Graphics.Status": "AC_LIVE",         // an enum by its name, with or without AC_
    "Physics.TyreCoreTemperature": [85.4, 85.9, 84.8, 85.1],
    "Physics.CarDamage[0]": 12.5,         // one element; SimHub calls it Physics.CarDamage01
  },
  "pin": { "Graphics.Flags": "AC_YELLOW_FLAG" },   // written once and held: no driver moves it
  "drivers": [ {"type": "rpmSweep", "min": 3000, "max": 8000, "period": 6}, ... ],
  "timeline": [ {"at": 15, "set": {"Physics.BrakeBias": 0.55}}, ... ],
  "loop": 0                               // seconds after which the timeline starts again
}
```

With `extends`, `values` and `pin` are merged key by key and everything else is replaced. Timeline
events take `log`, `set`, `pin`, `unpin` (a list of names) and `driver` (one object or a list), as the
irsdk emulator's do.

### Drivers

The clock runs first every tick: both `PacketId`s, and `Graphics.SessionTimeLeft` counting down in
milliseconds while it is above zero. A driver never writes a pinned value.

| type | parameters (defaults) | writes |
|---|---|---|
| `rpmSweep` | `min` 3000, `max` 8000, `period` 6 s | `Physics.Rpms`, ease-out then a drop |
| `gearFromRpm` | `minGear` 2, `maxGear` 6, `redline` 9000, `speedAtRedline` [km/h per gear] | `Physics.Gear` in AC's numbering (0 R, 1 N, 2 first), `Physics.SpeedKmh`, `Physics.LocalVelocity[2]` |
| `lapTimer` | `lapTime` 98, `jitter` 0.5, `trackLength` (`StaticInfo.TrackSPlineLength`) | `Graphics.iCurrentTime`, `iLastTime`, `iBestTime` and their text, `CompletedLaps`, `NormalizedCarPosition`, `DistanceTraveled`, `CurrentSectorIndex` and `LastSectorTime` by thirds of the lap, `CarCoordinates` round a circle, `Physics.Heading` |
| `fuelBurn` | `litersPerLap` 2.9, `minLiters` 0.5 | `Physics.Fuel`, refilled to where it started |
| `flagCycle` | `period` 8, `sequence` [NO_FLAG, YELLOW_FLAG, BLUE_FLAG, WHITE_FLAG, BLACK_FLAG, CHECKERED_FLAG] | `Graphics.Flags`, one value at a time |
| `pitLimiterToggle` | `period` 5 | `Physics.PitLimiterOn` |
| `field` | `cars` [{name, car, lap, pct, best, last, pit, pace}], `pace` 98, `jitter` 0, `pitCars` [names] | `Local\acpmf_simhub_v2`, `Graphics.Position`, `StaticInfo.NumCars` |
| `sine`, `ramp`, `toggle` | `var` (a value's name), `index`, as the irsdk emulator's | any value |

The `field` is everything SimHub's AC reader knows of the other cars, which is what its AC app
publishes: a name, a car, laps, three lap times, a position on the spline and in the world, and two pit
states. No class, number, team, rating or tyre compound, which is thinner than iRacing's session string
and is exactly what a leaderboard on AC has to draw without. The player is the car named
`StaticInfo.PlayerName`, which is how SimHub finds it, and follows the lap timer through the graphics
page. In a race SimHub takes the place from the zero-based running order the field publishes, and
otherwise from the order of best laps.

## Scenarios shipped

[scenarios/README.md](scenarios/README.md) says what each one holds still and why. In short: `race`
(the base, everything moving), `green`, `yellow` and `pit` (the capture scenarios, mirroring the irsdk
ones where AC publishes the state), `quali`, `notc`, `shiftlights` and `noapp` (the field without
SimHub's app, which iRacing has no equivalent of).

## Self-test

`acs.exe --selfcheck [scenario]` (exit 0 == pass) checks the size of every struct and the offsets of a
few fields against numbers worked out by hand from SimHub's declarations, simulates the scenario for
three seconds in memory, reads every page back with `Marshal.PtrToStructure` as SimHub does and checks
what SimHub's reader needs: `Graphics.Status` `AC_LIVE`, revs above 300 so `EngineStarted` is true, the
player named, the track length and the maximum revs set, the car and the track present in the fake
install, the car's files parsing the way SimHub parses them (and the TC and ABS levels SimHub will read
from them), and, with a field, every car connected, exactly one of them the player, and a running order
with no gap. CI runs it on every scenario through the net8.0 build in `devcheck/`:

```bash
export PATH="$HOME/.dotnet:$PATH"; export DOTNET_ROOT="$HOME/.dotnet"
dotnet build tools/acsm-emulator/devcheck -c Release
cd tools/acsm-emulator
dotnet devcheck/bin/Release/net8.0/acs.dll --selfcheck yellow
```

## Not verified here

Everything above was verified on Linux through the devcheck build. Not verifiable without the VM:
SimHub finding `acs.exe` and taking `C:\Temp\acsm-emulator` as the install (its log says `AC process
found`), opening the mappings created in the desktop session, switching to Assetto Corsa from
`LastGame`, and the values arriving under `DataCorePlugin.GameData.*` and `GameRawData.Physics.*`.
SimHub's AC reader also listens on UDP 9996 for AC's own telemetry, the only source of `TCActive` and
`ABSActive`; the emulator does not speak it, so those two stay 0.
