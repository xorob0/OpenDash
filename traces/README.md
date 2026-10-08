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
bun run record                 # every scenario that keeps a trace
bun run record green pit       # some of them
```

The bare form records the scenarios `scripts/trace.test.ts` expects a trace for, which is every
scenario the emulator ships except the ones `UNTRACED_SCENARIOS` in
[scripts/emulator.ts](../scripts/emulator.ts) names and says why for. Naming one of those records
it anyway, since that is how a scenario comes off the list, and warns that the trace fails that test
until the name is taken off.

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

In code, `scripts/trace.ts` is the reader: `readTrace(scenario)` and `frame(trace, index)`. The
format itself is `scripts/traceFormat.ts`, which imports nothing from node so that the demo can
bundle it into a page.

## The format

NDJSON, one header line, then one line per property sorted by name, then one line per call sorted
by its text:

```jsonc
{"trace":2,"scenario":"green","frames":200,"hz":10,"ticks":[72721,6],"recorded":"2026-10-09","simHub":"9.12.6","cars":24}
{"p":"DataCorePlugin.GameData.CurrentLapTime","t":"timespan","v":["00:01:38.4120000", "..."]}
{"p":"DataCorePlugin.GameData.Rpms","v":[4530.1, 4602.7]}
{"p":"DataCorePlugin.GameData.TrackName","v":"Spa"}
{"p":"drivername(3)","k":"call","v":"Liam Byrne"}
{"p":"driverrelativegaptoplayer(3)","k":"call","v":[-0.874, -0.9]}
{"p":"getopponentleaderboardposition_aheadbehind(-1)","k":"call","v":3}
{"p":"round(2.675, 2)","k":"call","v":2.68}
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

## The calls, version 2

Half of what the pages read is not a property. `drivername(3)`, `driverrelativegaptoplayer(p)`,
`getopponentleaderboardposition_aheadbehind(-1)`, `driversectorlastlap(p, s, false)`,
`getbestsplittime(1)` and the rest of the opponent family are functions over SimHub's in-memory
leaderboard, so a recording of property values cannot see them, and a version 1 trace drew the
relative, the leaderboard, the opponents, the sectors and lap history's best splits empty (#257).

Version 2 records them as columns. A column with `"k":"call"` is a formula the recorder evaluated
with SimHub's own NCalc engine on every frame, named by its text. The text is the canonical spelling
`packages/generator/src/opponentCalls.ts` defines: the function name in lower case, integer
arguments, `true` or `false` in lower case, arguments joined by a comma and one space. That is the
spelling the browser evaluator looks a call up by once it has computed its arguments, so
`drivername(getopponentleaderboardposition_aheadbehind((repeatindex()) - (4)))` on the fifth row
reads the `getopponentleaderboardposition_aheadbehind(1)` column and then the `drivername(5)` one.
A call with no column, a position past the field or an argument that is not a whole number, is null,
as a position that names no car is in SimHub.

What is asked for is `recordedCalls()` in `scripts/record.ts`: every call `callsRead()` finds in the
packages, with each argument the expression computes enumerated over its kind's domain, and the
probes below. `cars` in the header is the field size that domain was enumerated over, 24: positions
1..24, ahead-and-behind offsets -23..23, sectors 1..3. Most of those columns are a position past the
end of the field, null on every frame, and cost one short line each.

`frame()` returns the calls in the same map as the properties, by their text, and the evaluator reads
an opponent call from that map (`readCall` in `packages/generator/src/ncalc/scope.ts`), so a reader
that hands a frame to the evaluator gets the opponent pages filled without doing anything else.
`propertiesOf()` lists only the properties and `callsOf()` only the calls.

The recorder evaluates the calls inside `IDataPlugin.DataUpdate`. SimHub's PluginManager sets
`NCalcEngineBase.lastData` to the update's `GameData` before it calls any data plugin, so the
leaderboard a call reads there is the finished frame the properties in the same line were read from.
How the recorder keeps a column from freezing is in [its README](../tools/trace-recorder/README.md).

### The probes

A few call columns are not opponent calls but questions. The browser evaluator was written from
NCalc 1.3.8's source and SimHub's decompiled function table, with no SimHub to ask, and where those
could not settle a reading the code says **Unverified**. A probe is a literal formula whose answer
settles one: `max(0, 2.6)` is 3 if `max` takes its left operand's type and 2.6 if not, `abs(7) / 2`
is 3.5 if `abs` returns a decimal, `round(2.5, 0)` is 2 if it rounds to even, `format(-0.04, '0.0')`
says whether .NET Framework writes `-0.0`, `'3' = 3` whether a string against a number compares as
text, and `toshorttime(secondstotimespan(5.2), 1, false, false)` what shape a short time takes under
a minute. A formula that throws in SimHub records null, which is what the dash draws. Null is put in
with `[OpenDashTraceProbe.Unpublished]`, a property nobody publishes, since NCalc 1.3.8 has no null
literal.

The list, each with what it settles, is `PROBE_CALLS` in `scripts/record.ts`.
`scripts/trace.test.ts` holds the evaluator to every recorded answer once a version 2 trace is
committed; a probe that disagrees is a reading in `packages/generator/src/ncalc/` to fix.

### Until the re-record

The nine committed traces are version 1 until they are recorded again on the VM with
`bun run record`. The reader accepts both meanwhile, a version 1 trace reading as a version 2 one with
no calls, and the version 2 tests in `scripts/trace.test.ts` say by name which file they are
waiting for instead of failing. The re-record takes 1 off `READABLE_TRACE_VERSIONS` in
`scripts/traceFormat.ts`. `scripts/fixtures/relative.v2.ndjson` is a version 2 trace written by
hand, seven cars and the relative's calls, which `scripts/traceV2.test.ts` replays through the
relative page to prove the path end to end without the VM.

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

All seven were recorded again on 2026-09-29 for #322, which observed every column they had been
carrying by hand and left none asserted, and the observations bear the entries out where they said
they would. `DataCorePlugin.GameRunning` was 1, `OpenDash.ClockFormat` `24h` and
`OpenDash.DeltaPrecision` `hundredths`, and #109's `PushToPassActive`, `dcHeadlightFlash` and
`EngineStarted` were `false`, `false` and 1, as asserted. The three entries that said they were not
what a recording would write were not: `DataCorePlugin.GameData.BestLapOpponentSameClassPosition` is
6 in the yellow scenario rather than -1, and iRacing's live delta to the last lap,
`DataCorePlugin.GameRawData.Telemetry.LapDeltaToSessionLastlLap`, moves through the lap with its
`_OK` flag true from the first frame rather than standing at 0 and `false`. No replay had read any
of those three, since every trace had been taken with the settings that do not read them, which is
the argument each entry made and the only condition under which a column is worth typing at all.

#503 asserts seventeen in each, typed on top of that recording because the plugin it was taken with
did not publish them yet, and all of them are what a re-record will write. Sixteen are
`OpenDash.Face<size>Zone{B,C}Position`, where the page a zone is showing sits in its cycle, which
the plugin publishes because the panel lets a driver arrange a zone's pages and an expression cannot
read an ordered list. They are null wherever the recording's page is null, which is every face but
the 850x480, and 1 and 15 on the 850x480, which is where pages 0 and 14 sit in a zone whose mask is
full and whose order is the catalogue's -- the plugin's answer for a zone nobody has arranged. The
seventeenth is `OpenDash.FlagsInPitLane` at `true`, the default a plugin nobody has opened
publishes. The next `bun run record` reads all of them from the plugin and drops the entries.

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
