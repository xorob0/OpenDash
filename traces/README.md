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

What a recording asks SimHub for is `propertiesRead()` in
[packages/dash/src/properties.ts](../packages/dash/src/properties.ts): every property read by any
expression of any package of any theme a release ships, the item bindings, the border colours, the
screens' enabled expressions and the dashboard variables alike, plus the lap-history names no scan
can see. Until #581 the scan read only the item bindings of the default theme, so the engine chip's
border, the companion's module switches, the pit wall's page and everything the Porsche reads on its
own were in no trace, and nothing said so.

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

## Traces per sim

```bash
bun run record --sim ac green yellow pit   # Assetto Corsa's, from tools/acsm-emulator
bun run trace list --sim ac
```

Each sim has a folder of its own: iRacing's traces are here, where the nine of them always were, and
Assetto Corsa's are in `ac/`, one per scenario of [tools/acsm-emulator](../tools/acsm-emulator/README.md)
and under the same names as their iRacing twins. A folder rather than a suffix, so that `green` is
`green` on either sim and everything that lists this folder for iRacing's traces goes on seeing exactly
those. [scripts/sims.ts](../scripts/sims.ts) holds each sim's list of scenarios that have no trace,
with the reason for each.

Both are held to the same list of properties, everything any package reads, because a package reads
the same properties whichever sim is driven. An Assetto Corsa trace therefore carries iRacing's raw
`Telemetry.*` and `SessionData.*` columns, and they are null: SimHub's AC reader publishes no such
thing, and that null is what a dashboard on AC is handed, which is the point of replaying one. On top
of that, `scripts/trace.test.ts` checks what is particular to a sim:

* the header's `game`, which the recording reads off `DataCorePlugin.CurrentGame` rather than taking
  from the command line, names the sim; a trace from before the field, all nine of iRacing's, reads as
  `IRacing`;
* the sim's own tick column is there, `Telemetry.SessionTick` for iRacing and `Graphics.PacketId` for
  Assetto Corsa, which the emulator steps once a tick in the same way;
* no other sim's raw column holds a value, since a value there means SimHub was reading another game
  than the header says.

When the plugin publishes per-reading `OpenDash.*` properties where the games differ (#1101), those
enter `propertiesRead()` like any other and every trace of every sim has to carry them.

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

It exists because "it carries every property any expression of any package of any shipped theme
reads" fails the moment a package starts reading a property no trace holds, and the honest answers to
that are a re-record or this. A re-record is the better one and is what removes an entry: `toTrace` builds the header from
scratch, so the next recording of a scenario drops the list, and `recordedProperties()` derives what
it asks SimHub for from `propertiesRead()`, so a property a binding reads is picked up without
anybody adding it anywhere. Until then the entry is a claim, `scripts/trace.test.ts` holds it to
being a constant -- nobody hand-writes two hundred frames of a moving value honestly -- and a reader
replaying the file can see which one line of it nobody watched.

All seven were recorded again on 2026-09-29 for #322, which observed every column they had been
carrying by hand and left none asserted, and the observations bear the entries out where they said
they would. `DataCorePlugin.GameRunning` was 1, `OpenDash.ClockFormat` `24h` and
`OpenDash.DeltaPrecision` `hundredths`, and #762's `PushToPassActive`, `dcHeadlightFlash` and
`EngineStarted` were `false`, `false` and 1, as asserted. The three entries that said they were not
what a recording would write were not: `DataCorePlugin.GameData.BestLapOpponentSameClassPosition` is
6 in the yellow scenario rather than -1, and iRacing's live delta to the last lap,
`DataCorePlugin.GameRawData.Telemetry.LapDeltaToSessionLastlLap`, moves through the lap with its
`_OK` flag true from the first frame rather than standing at 0 and `false`. No replay had read any
of those three, since every trace had been taken with the settings that do not read them, which is
the argument each entry made and the only condition under which a column is worth typing at all.

#791 asserts seventeen in each, typed on top of that recording because the plugin it was taken with
did not publish them yet, and all of them are what a re-record will write. Sixteen are
`OpenDash.Face<size>Zone{B,C}Position`, where the page a zone is showing sits in its cycle, which
the plugin publishes because the panel lets a driver arrange a zone's pages and an expression cannot
read an ordered list. They are null wherever the recording's page is null, which is every face but
the 850x480, and 1 and 15 on the 850x480, which is where pages 0 and 14 sit in a zone whose mask is
full and whose order is the catalogue's -- the plugin's answer for a zone nobody has arranged. The
seventeenth is `OpenDash.FlagsInPitLane` at `true`, the default a plugin nobody has opened
publishes. The next `bun run record` reads all of them from the plugin and drops the entries.

That recording was 2026-10-09, for #581, which is also when the nine traces first carried the
twenty-seven properties the scan had never seen: the engine chip's border reads the warnings
bitfield, the companion's enables read its twenty-one module switches and its forced module, the pit
wall's enables read its page, and the Porsche's face reads the raw fuel level, the pit limiter speed
and the crest. All seventeen #791 entries were observed and the lists are gone. `FlagsInPitLane` is
true as asserted; the zone positions are on a different face than predicted, null on the 850x480 and
1 and 16 on the 1280x480, because that is the screen the VM's rig carried that day and zone C's
cycle had grown a page. The rig's state is the recording's, like the lap history, not the scenario's.

The same day, a second recording, because #800 had re-recorded the nine on `main` at the same
time for its engine-readings page and the two recordings could not be spliced: a trace is one
timeline, and a column from another recording in it is a frame that never happened. The second pass
was taken from the merged tree and carries both sides' columns and two more that neither had. The
AiM theme (#204) and its backlight setting (#715) had landed between the first recording and the
merge, and a scan that reads every shipped theme sees the AiM's face reading its setting: the plugin
publishes the chosen look or the default, and `white`, the default in
`packages/dash/src/themes/aim/settings.json`, is what the rig nobody has set published. The
companion's twenty-second switch, #800's page, published true like the rest. #800's six engine
readings stand still in every scenario, oil at 81.22 and 103.2, water at 88.5, fuel pressure 4.3,
manifold 1.02 and 13.9 volts, which is the emulator's steady engine rather than anything the
scenarios drive. No entry is asserted in any of the nine.

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
sessions, so the lap history in a trace is the test VM's own rather than the scenario's; the dashes
draw only the slots whose `_IsCurrentSession` is 1, which are the laps of the session the trace
records (#1011). And SimHub polls at its own rate, so a frame is taken at the first tick at or after
the one asked for, which is occasionally a tick later than last time.

None of that stops a trace from being reproducible in the sense that matters, which is that
re-recording an unchanged scenario produces the same picture. It does mean that a trace diff is
never empty, and that the lines worth reading in it are the ones belonging to properties the
scenario actually touches.

#1063 asserts two hundred and fifty-six in each, typed on top of that recording because the plugin it
was taken with did not publish them yet: `OpenDash.LivePosition<row>`, `LiveClassPosition<row>`,
`LiveOrder<place>` and `LiveClassOrder<place>` for sixty-four cars, the race as it stands on the road,
which the plugin works out because iRacing moves its own order only at the line. They are all null,
which is not what a recording of the race scenarios will write, and says so here: there the plugin
publishes the order the emulator gives, and the emulator ranks its field by distance on every tick, so
every one of them is the sim's own answer that the dashboards fall back to when it is null. The replay
draws the same places and the same rows either way. Qualifying, where the plugin publishes nothing, and
`notc`, whose chequered flag falls 132 frames in and after which it publishes nothing, would be null for
some or all of the recording anyway. The next `bun run record` reads them from the plugin and drops the
entries.

#1017 asserts one more in each, `DataCorePlugin.GameRawData.Telemetry.SessionTimeTotal`, the length
iRacing declares for the session. It was recorded on #1017's branch while #993's re-recording landed
on main, and the two could not both be kept, so the traces are main's recording with that one column
carried over from the branch's: 2700 in the race scenarios, 1800 in `notc` and 900 in `quali`, the
emulator's own scenario lengths, which do not move within a session or between two recordings of one.
The next `bun run record` reads it from SimHub and drops the entry.

#1101 asserts one more in each, `OpenDash.CarHasPitLimiter`, whether the car has a pit limiter to engage,
which the plugin answers per sim so that no dashboard names a game (ADR 0021). The pit family reads it in
place of iRacing's `dcPitSpeedLimiterToggle`, with that test as its fallback. On iRacing the plugin's answer
is whether the toggle is there at all, and it is there on every frame of all nine, so the column is `true`
in each: a constant, and the value a recording with the plugin installed will write. The next
`bun run record` reads it from the plugin and drops the entry.

That recording was 2026-10-14, for #555, whose track page reads `OpenDash.TrackMapRecorded`: whether
SimHub holds an outline of the track the car is on. It is `true` on every frame of all nine, because
the test VM has lapped the emulator's track before and SimHub keeps an outline per track and
layout between sessions, so like the lap history it is the rig's state rather than the scenario's,
and the `false` a fresh install shows before its first whole lap is in no trace. `CarHasPitLimiter`
was `true` as asserted, and that recording left no entry asserted in any of the nine. The raw
`Telemetry.FuelLevel` column is gone, since #1126 moved the Porsche's low-fuel alarm onto the tank
SimHub fills on every sim and nothing reads the raw one any more.

#1123 recorded the nine the same day on its own branch, when the track state moved from
`GameData.TrackGripStatus`, which SimHub publishes for no game, to iRacing's `Telemetry.TrackWetness`
(#1128). That column was 1 on every frame of all nine, iRacing's dry and the emulator's constant. #555's
recording reached main first, so when the two met the nine kept #555's recording with #1123's
`TrackWetness` column carried over from its own, and the `TrackGripStatus` column nothing reads any
more dropped. The value was observed, but in a different recording than the one each file holds, so
each asserts the column: it was carried in by hand, not recorded with the frames beside it. The next
`bun run record` reads it from SimHub and drops the entry.

#582, which landed on main while that recording was on #555's branch, asserts five more in each, carried
onto the recorded traces when the two met. The damage page reads `OpenDash.DamageReported`, whether the sim reports
the car's damage at all, which the plugin answers per sim, and on a sim that does it reads
`DataCorePlugin.GameData.CarDamage1` to `4`, the front, rear, left and right members. iRacing reports none:
the plugin's answer is `false` on every frame of all nine, and SimHub's iRacing reader marks the four
members not available and returns 0 for each (`docs/research/sims.md`). Both are constants, and the values
a recording with the plugin installed will write. The next `bun run record` reads them from SimHub and the
plugin and drops the entries.

#1124 asserts two more in each, the raw track names the track module's header reads:
`SessionData.WeekendInfo.TrackDisplayShortName` as `Spa`, the short name the emulator's
race-session.yaml gives every traced scenario, and AC's `Track.TrackInfo.name` as null, since iRacing's
reader has no raw `Track` block. Both were carried onto the recorded traces with #582's, and the next
`bun run record` reads them from SimHub and drops the entries.

Assetto Corsa's seven assert `OpenDash.TrackMapRecorded` as `true`, carried in from #555 when it met
main: they were recorded before the plugin published it. The emulator completes laps at the same Spa
in every recording and SimHub keeps an outline per track and layout, so `true` is the rig's state as
it is on iRacing; the next `bun run record --sim ac` observes it and drops the entry.

They were also recorded before #1123 read `Telemetry.TrackWetness`, so each asserts that column as
null, which is what SimHub hands a dashboard on AC for any raw iRacing `Telemetry.*` column. The next
`bun run record --sim ac` reads it and drops the entry.

#1129 asserts four more in each, `OpenDash.YellowFlagShown`, `OpenDash.BlackFlagShown`,
`OpenDash.WhiteFlagShown` and `OpenDash.ChequeredFlagShown`, which flag the plugin says the driver is
being shown where SimHub's own `Flag_*` summary is wrong. Band D, the flag box, the strips and the round
faces' ring read each of them ahead of the summary it stands for. Only Le Mans Ultimate's row answers
them; iRacing's leaves all four null so that its dashboards read what they read before, and the column is
null on every frame of all nine: a constant, and the value a recording with the plugin installed will
write. The next `bun run record` reads them from the plugin and drops the entries.

Assetto Corsa's seven in `ac/` assert the same four. Its row leaves them null too, since ACManager's
`Flag_*` read `Graphics.Flag`, the one flag the game shows the driver, so the column is null on every
frame of all seven for the same reason. The next `bun run record --sim ac` reads them from the plugin and
drops the entries.

#561 asserts one more in each of the sixteen, `OpenDash.DriverInCar`, whether the driver is in the car with
the physics running, which the plugin answers per sim (ADR 0021). The car alerts, the change notifications,
the AiM's engine warning and the Porsche's fuel alarm read it ahead of iRacing's `Telemetry.IsOnTrack`. On
iRacing the plugin's answer is `IsOnTrack` itself, which is `true` on every frame of all nine; on Assetto
Corsa it is `Graphics.Status` at `AC_LIVE`, which is what the emulator writes on every tick of every
scenario until it stops, so the column is `true` on every frame of all seven too. Both are constants, and
the value a recording with the plugin installed will write. The next `bun run record` and
`bun run record --sim ac` read it from the plugin and drop the entries.

#1122 asserts one more in each, `OpenDash.PitLimiterOn`, whether the limiter is on, which the plugin
answers per sim because on Assetto Corsa SimHub's own `PitLimiterOn` is the pit lane (ADR 0021). Every
limiter reading reads it, with SimHub's `PitLimiterOn` as its fallback. On iRacing the plugin's answer is
that member itself, so the column is `true` in `pit` and `pitspeeding` and `false` in `green`,
`settingchange` and `shiftlights`, where the member is constant, and the value a recording with the plugin
installed will write. In `race`, `yellow`, `quali` and `notc` the member moves, and an asserted column is a
constant, so there it is null: the readings fall back to the recorded member, which is the plugin's answer
frame for frame, and the replay draws the same limiter either way. The next `bun run record` reads it from
the plugin and drops the entry.

Assetto Corsa's seven in `ac/` assert it too. There the plugin answers with the raw `Physics.PitLimiterOn`,
which the emulator pins in four of them: on in `pit`, so the column is `true`, and off in `green`, `noapp`
and `yellow`, so it is `false`. In `race`, `notc` and `quali` the emulator toggles the raw limiter every five
seconds, which no constant can say, so there the column is null and the readings fall back to SimHub's
`PitLimiterOn`, the pit lane, which is off on track: the replay of those three draws the limiter off where a
dash with the plugin blinks it on and off. That is the one place a replay and the VM differ, and the next
`bun run record --sim ac` records the plugin's column and drops the entry.

#1130 asserts two more in each of the nine and each of Assetto Corsa's seven, `OpenDash.PitStopSeconds` and
`OpenDash.LastPitStopSeconds`, the stop in the pit box the plugin times where SimHub's own `IsInPitSince` and
`LastPitStopDuration` count something else. The pit view reads them ahead of SimHub's two. Only Le Mans
Ultimate's row answers, since its reader's `IsInPit` is the whole pit lane; iRacing's and Assetto Corsa's
`IsInPit` are the box, so their rows leave it to SimHub and the plugin publishes null for both, on every frame
of all sixteen: a constant, and the value a recording with the plugin installed will write. The next
`bun run record` and `bun run record --sim ac` read them from the plugin and drop the entries.

#1177 asserts one more in each of the nine and each of Assetto Corsa's seven,
`DataCorePlugin.GameRawData.Graphics.TyreCompound`, the compound AC writes for the player, which band D's
Compound and the tyres module's chip read where SimHub's `fronttyrecompound` is empty, as it is for every AC
car. On Assetto Corsa it is `Slick Medium (M)` in all seven: the emulator's `race.json` writes it once in its
values and every other scenario extends that file without touching it, so it is a constant, and the value a
recording will write. iRacing's reader has no raw `Graphics` block, so in the nine it is null. The next
`bun run record` and `bun run record --sim ac` read it from SimHub and drop the entries.
