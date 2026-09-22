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
{"trace":1,"scenario":"green","frames":200,"hz":10,"ticks":[72721,6],"recorded":"2026-09-13","simHub":"9.12.6"}
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
