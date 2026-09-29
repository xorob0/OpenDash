# Telemetry traces

One file per emulator scenario, holding what SimHub saw while that scenario played. Anything that
wants to draw a dashboard without Windows, SimHub and the shared memory reads one of these: the
preview renderer, the per-pull-request video and the pixel goldens all replay a trace rather than
asking for a VM of their own.

A trace is recorded once and committed. The Windows VM is therefore the origin of a trace and not a
dependency of it, which is the point: recording happens when a scenario changes or when a package
starts reading a property that was never recorded, and every pull request afterwards replays the
committed file.

## Recording

```bash
bun run record                 # every scenario
bun run record green pit       # some of them
```

The command claims the VM, installs [the recorder plugin](../tools/trace-recorder/README.md) into
SimHub, runs each scenario past it once and writes the file here. `--frames`, `--hz` and
`--warm-up` exist for working on the recorder itself; leave them alone for anything that is
going to be committed, since a trace recorded at a different rate is not comparable with the
others.

It installs the OpenDash plugin before it records anything, because every `[OpenDash.*]` property a
face reads is published by that plugin, and a trace recorded without it would be missing a third of
its columns.

Read the diff before committing. A trace is a reviewed artefact rather than build output, and the
diff is the only place a scenario change shows itself in numbers.

## Reading

```bash
bun run trace list             # what is committed, and how much of each one moves
bun run trace show green 0     # one frame as a property map
bun run trace check            # parse every trace
```

In code, `scripts/trace.ts` is the reader: `readTrace(scenario)` and `frame(trace, index)`.

## The format

NDJSON, one header line and then one line per property, sorted by name:

```jsonc
{"trace":1,"scenario":"green","frames":200,"hz":10,"ticks":[72721,6],"recorded":"2026-09-13","simHub":"9.12.6","asserted":["DataCorePlugin.GameRunning"]}
{"p":"DataCorePlugin.GameData.CurrentLapTime","t":"timespan","v":["00:01:38.4120000", "..."]}
{"p":"DataCorePlugin.GameData.Rpms","v":[4530.1, 4602.7]}
{"p":"DataCorePlugin.GameData.TrackName","v":"Spa"}
```

A property that never moves is one short line carrying one value; a property that moves carries one
value per frame. That is columnar rather than one record per frame, and it is that way because the
file is committed. A frame-shaped file repeats two hundred property names on every line and puts
everything that moves onto the same line, so a scenario that changes the fuel load rewrites every
line of the file and none of them can be read afterwards. Here it changes the fuel line and nothing
else. Most of what SimHub publishes holds still for the length of a twenty second scenario, which is
also why the transposed file is small enough to keep in the repository.

`t` appears only where a JSON scalar would lose the type: a TimeSpan travels as `hh:mm:ss.fffffff`
and a DateTime as ISO 8601, and without `t` a reader could not tell either from a property that
really is text. `ticks` is the emulator grid the frames were taken on, as first tick and step, and
the trace also carries `DataCorePlugin.GameRawData.Telemetry.SessionTick` as an ordinary column, so
that the tick each frame actually came from can be read off the file.

Numbers are rounded to four decimals, which is past anything a dashboard draws and short of the
noise in a double. Without that a re-recording of an unchanged scenario would differ on every line.

## A column that was typed rather than observed says so

`asserted` in the header names the columns of that file that were written by hand. Everything else
came out of a real SimHub and `recorded` and `simHub` say which one and when; a hand-written column
has none of that behind it, so it is listed rather than left to look like the rest.

It exists because "it carries every property any binding of any package reads" fails the moment a
package starts reading a property no trace holds, and the honest answers to that are a re-record or
this. A re-record is the better one and is what removes an entry: `toTrace` builds the header from
scratch, so the next recording of a scenario drops the list, and `recordedProperties()` derives what
it asks SimHub for from `propertiesRead()`, so a property a binding reads is picked up without
anybody adding it anywhere. Until then the entry is a claim, `scripts/trace.test.ts` holds it to
being a constant -- nobody hand-writes two hundred frames of a moving value honestly -- and a reader
replaying the file can see which one line of it nobody watched.

The seven traces committed on 2026-09-18 carry one such column, `DataCorePlugin.GameRunning` at a
constant 1. Every one of them was recorded with the emulator attached and a session live, which is
what 1 means, so a re-record will write the same value; it is the provenance and not the number that
this field is about.

They carry a second since #433, `DataCorePlugin.GameData.BestLapOpponentSameClassPosition` at a
constant -1, and this one is not what a re-record will write. It is the leaderboard row of the
fastest car of the player's own class, which the session best reads when `OpenDash.PositionMode` is
`class`, and the emulator's field is two classes of twelve with best laps drawn from a seeded random
generator, so which row holds it is something only SimHub watching the scenario can say. -1 is the
value SimHub itself publishes before it has one, "nobody yet", which in a replay draws the class
session best as the empty placeholder rather than as a car nobody observed. Every trace was recorded
with `PositionMode` at `overall`, so no replay of them reads the column at all; the next
`bun run record` picks the property up by itself and drops the entry.

Every trace asserts `OpenDash.ClockFormat` since #324, at a constant `24h`, which is what the plugin
publishes until a driver changes it and so what a re-record will write. It was added by hand because
the idle screen every package carries reads it, and a trace that lacks a property a binding reads
fails the check above.

Three more since #109, because the alert catalogue and the pit family read them, and none is a guess
about the value. `DataCorePlugin.GameData.PushToPassActive` is `false` in all seven: SimHub's iRacing
reader fills it from `CarIdxP2P_Status` at the player's index and hands it through as a `bool?` rather
than as 1 or 0, and the emulator publishes that array all `false`.
`DataCorePlugin.GameRawData.Telemetry.dcHeadlightFlash` is `false`: a raw telemetry boolean the
emulator publishes as `false`, the same type and default as `dcPitSpeedLimiterToggle`, which every
trace recorded as `false`. `DataCorePlugin.GameData.EngineStarted` is 1: the reader computes it from
its own ignition and the stalled bit of `EngineWarnings`, and every trace recorded `EngineIgnitionOn`
at a constant 1 and an `EngineWarnings` that never carries the stalled bit, 8. They are asserted
because the VM was held by another session when they were needed; the next recording of each
scenario replaces them with what SimHub says.

## Why a recording waits two minutes first

Frame one is taken two minutes after SimHub first reports the game running. A few seconds would be
enough for it to connect and settle; the wait is there for the fuel figures under
`DataCorePlugin.Computed.*`, which are averages SimHub builds by watching laps complete and which
read zero until it has seen one. The scenarios lap in ninety-eight seconds. A trace taken before
that carries a fuel card showing nothing, and a video made from it would look like a dash whose fuel
calculation is broken rather than one recorded too early.

The wait is counted from what the recorder sees rather than from a tick written into the request.
The emulator's `SessionTick` starts at the scenario's own session time, which is above seventy
thousand in a race half run and around forty thousand in a timed one, so no fixed tick could serve
every scenario; the first one in the header is therefore the tick the first frame actually came
from.

## What a re-recording does not reproduce

The emulator is a function of its tick, so the telemetry at a given tick is the same on every run.
Four things are not. SimHub publishes the wall clock, which the pit wall draws. The delta and fuel
properties are computed from a history SimHub builds after it connects, so they depend on how long
it had been running. `PersistantTrackerPlugin.PreviousLap_NN` is a store the plugin keeps between
sessions, so the lap history in a trace is the test VM's own rather than the scenario's. And SimHub
polls at its own rate, so a frame is taken at the first tick at or after the one asked for, which is
occasionally a tick later than last time.

None of that stops a trace from being reproducible in the sense that matters, which is that
re-recording an unchanged scenario produces the same picture. It does mean that a trace diff is
never empty, and that the lines worth reading in it are the ones belonging to properties the
scenario actually touches.
