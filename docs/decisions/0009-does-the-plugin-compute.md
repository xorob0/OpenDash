# ADR 0009: Does the plugin compute?

**Date:** 2026-09-11
**Status:** Accepted. Refines [ADR 0003](0003-plugin-settings-through-properties.md) and moves the
"computed telemetry of our own" line in [scope.md](../scope.md).

Amended on 2026-10-13 for [#1063](https://github.com/xorob0/OpenDash/issues/1063): **the refusal is
now a rule with one way through it.** The plugin does not compute, unless a feature needs a value
that cannot be had any other way. The exceptions this record had been collecting one at a time, and
ADR 0018's, are that rule applied; the Decision below says it, and the section on the race as it
stands on the road is the case that made it a rule rather than a fourth exception.

## Context

[ADR 0003](0003-plugin-settings-through-properties.md) made the plugin deliberately inert: it
installs packages, attaches settings as properties, and does nothing else. It does not render, does
not read telemetry and does not compute. [scope.md](../scope.md) restated that as a standing
refusal.

The zone face forces the question, because the catalogue draws values the refusal appears to
forbid. `ZoneCatalogue.dc.html` shows `Per lap`, `Avg 5`, `Est. laps`, `Window L14–18`,
`Last stop`, `Runs out` and `SoF` as ordinary page content, not as aspirations. A scene graph
cannot keep state across laps, so on the face of it about a third of the catalogue needs somewhere
else to live.

Answered after the pages are built, that third ships as not-available states and is then rebuilt.
So it is answered first.

## Investigation

We went and looked rather than reasoning from the refusal. SimHub 9.12.6 was decompiled and its
attached properties enumerated.

**SimHub already computes most of it.** `DataCorePlugin.Computed` publishes the whole fuel family:
`Fuel_LitersPerLap`, `Fuel_RemainingLaps`, `Fuel_RemainingTime`, `Fuel_LastLapConsumption`,
`Fuel_CurrentLapConsumption`, `Fuel_Percent`. `PersistantTrackerPlugin` publishes the delta family
and the lap history: `LiveDeltaSeconds`, `SessionBestLiveDeltaSeconds`,
`AllTimeBestLiveDeltaSeconds`, `EstimatedLapTime` on three different bases, `SessionBest`,
`AllTimeBest`, and `PreviousLap_00` through `PreviousLap_NN` with their deltas.

**The lap history is the important find.** `PreviousLap_00` … `PreviousLap_04` are five ordinary
properties. A five-lap average is therefore *arithmetic over properties that already exist*, and
NCalc can do arithmetic. It needs no state, no plugin and no memory.

That generalises. Going through the catalogue field by field:

| Field | Where it comes from |
|---|---|
| Per lap | `Computed.Fuel_LitersPerLap` |
| Est. laps | `Computed.Fuel_RemainingLaps` |
| Runs out | `Computed.Fuel_RemainingTime` |
| Last stop | `driverpitlastduration(position)` |
| Avg 5 | arithmetic over `PreviousLap_00`…`_04` |
| Delta, estimate, session best, all-time best | `PersistantTrackerPlugin` |
| Window L14–18 | arithmetic: laps remaining, tank capacity, `Fuel_LitersPerLap` |
| Fuel to energy | Le Mans Ultimate only; iRacing publishes no virtual energy |
| SoF | not published by SimHub in any form we could find |

So the third of the catalogue that looked like it needed a computing plugin is, on inspection,
**two fields**: one that only exists in a sim OpenDash does not support, and one that no plugin of
ours could compute either, because the datum is not there to compute from.

## Decision

**The plugin does not compute, unless a feature needs a value that cannot be had any other way.**

The default is the refusal, and it is cheap to keep. Where a value needs deriving, it is derived
**in the expression**, from properties SimHub already publishes. The scene graph stays a pure function of properties, the package stays complete without
the plugin — a derived value falls back exactly as a read one does — and there is no second place
where a number can be wrong.

The way through is narrow, and all three of its conditions hold before the plugin computes anything:

1. **A feature needs the value.** Something a driver asked for cannot be drawn correctly without it.
   Computing a number because it would be tidier to have one is not that.
2. **Nothing else can provide it.** SimHub does not publish it, the sim does not publish it, and it
   cannot be derived where it is drawn: no expression and no JavaScript binding can produce it
   correctly, because what it is derived from is not published, or because deriving it needs memory
   between frames, or because it has to be one answer that many items agree on and an expression
   reads the frame SimHub is still building.
3. **The package stays complete without it.** The plugin publishes the value as a property, and
   every dashboard reads it with SimHub's own answer, or the placeholder, as its `isnull` fallback.
   A package installed without the plugin draws what it drew before the value existed.

A computation that passes is written in a pure class the net8.0 tests compile, fed from the frame
SimHub has finished in `DataUpdate`, and recorded below or in its own ADR with the reason each
condition holds. One that fails any of the three is refused however useful it looks.

Two consequences follow for the catalogue:

- **Fuel to energy** is drawn and marked as Le Mans Ultimate only, exactly as `energy` already is
  in the module catalogue. A page that reads what iRacing does not publish says so rather than
  drawing a zero, which is the rule `second-screens.md` already applies.
- **Strength of field** leaves the bar's catalogue of end fields until somebody finds where it
  comes from. Ten fields, not eleven. Drawing a field that can never have a value is worse than
  not offering it.

### State that is SimHub's own

One could think that the refusal above forbids every value which depends on what happened a moment
ago. In reality it forbids only a place of our own in which such a value is kept. SimHub's
`changed(ms, value)` reports that a property has moved within a window, and it is dispatched by the
engine and declared in `ncalcFunctions.ts`; the window is state, but it is SimHub's state, kept and
aged by SimHub, and an expression that asks for it is reading a property exactly as
`Fuel_RemainingLaps` is read. Thus an alert which must stand for a few seconds after a count moves,
of which the incident notice is the first, is written with `changed()` rather than waiting on
#167, and the package remains complete on its own, since a dashboard installed without the
plugin evaluates the same window.

The allowance is for reading what the host already remembers and does not extend to `setvalue` and
`getvalue`, with which an expression would keep state of its own. Those would be a computing plugin
written in NCalc, which is the thing this record refuses, and they remain unused.

### A value SimHub keeps and does not publish

Added 2026-09-28 for #454. SimHub works out the best lap of the player's class on every frame and
keeps the car as `BestLapSameClassOpponent`, but it never becomes a property: `DataCorePlugin`
declares `GameData` from an empty frame at startup, and on that frame the car is null. Its field
twin, `BestLapOpponent`, is declared because the empty frame carries a blank car there.

An expression can reach the class car's time only through `driverbestlap()`, and that function
reads the frame SimHub is still building rather than the one it last published. SimHub builds each
frame in place on the object the dashboard reads, so a dashboard that renders mid-build finds no
leaderboard and gets nothing, which a rig reported as the session best flashing. The race is read
from SimHub's code; the emulator did not produce it on the test VM. The plugin therefore copies the
time out of the finished frame in `DataUpdate` and publishes it as `OpenDash.ClassBestLap`.

That is not a computation, and it does not reopen the decision above: SimHub chose the car and the
time, and the plugin only moves SimHub's own answer to where SimHub would have published it. It is
the same shape of exception as ADR 0018, which reads telemetry because no property exists to derive
from, and the package stays complete without the plugin, falling back on the row lookup, which is
right on every frame SimHub is not mid-build.

### A delta SimHub does not publish and iRacing does

Added 2026-09-29 for #322. The delta family above is SimHub's live delta to the session best and to
the all-time best; `PersistantTrackerPlugin`'s `*LastLapDelta` properties are the finished lap's,
written at the line, and no property carries a running comparison with the lap before this one. The
ticket asked for one on the belief that SimHub published it, and SimHub 9.12.6, decompiled, does not.

iRacing does. Its telemetry carries `LapDeltaToSessionLastlLap`, the second `l` being iRacing's own,
with `_OK` saying whether there is a last lap to compare against, and SimHub passes raw telemetry
through as `DataCorePlugin.GameRawData.Telemetry.*`. The last-lap reference therefore reads that
property in the expression, gated on its `_OK`, which is the decision above applied rather than an
exception to it: a published property, read where it is drawn, with the package complete without the
plugin.

Computing the same number ourselves would have been the thing this record refuses. A live delta is
the current lap's time at this point of the track against the previous lap's time at the same point,
so it needs the previous lap kept by distance, frame after frame, which is memory between frames and
the second of the conditions below.

### A position in a list the driver arranges

Added 2026-09-29 for #791. One number moved to the plugin, and on this record's own terms. The panel
lets a driver arrange the order of a zone's pages, and where the page showing sits in that order is a
question about a list an expression cannot read, however long it is allowed to be. The plugin
publishes it as `Face<size>Zone<X>Position`; the face reads it with the catalogue-order count as its
`isnull` fallback, so a package with no plugin still counts, in the catalogue's order, which is the
order it cycles in without one. The length of the cycle stays a popcount in the expression.

### The race as it stands on the road

Added 2026-10-13 for #1063, and the case that turned this record's exceptions into its rule. iRacing
moves a car's place when it crosses the start/finish line and at no other time. SimHub passes that
place on as it is and sorts its leaderboard on it, and every `driver*` function takes an index into
that leaderboard, so a pass in the first corner reached every place, every list and every gap on a
dashboard a lap later. The place as it stands on the road is the order of the distances the cars
have covered, and SimHub publishes every car's distance as `drivercurrentlaphighprecision()`.

The three conditions, one by one:

1. **A feature needs it.** The ticket is the feature: the place, and the lists and gaps beside it,
   have to move when a car is passed.
2. **Nothing else can provide it.** SimHub does not publish a live order for iRacing: its leaderboard
   would sort on `Opponent.LivePosition`, and the iRacing reader leaves it empty. An expression could
   count the cars ahead of one car, one comparison per car of the field, up to sixty-four, but a list
   needs the inverse, the car in each place, which is a sort, and no expression sorts. A JavaScript
   binding could sort, but it would sort again in every cell of every row of every list, each reading
   the frame SimHub is still building, and two cells of one row could disagree about which car the row
   is. One ranking of the finished frame, read by every item, is the only way the numbers agree.
3. **The package stays complete.** Every one of the four families is read with SimHub's own answer
   as its fallback, so a package with no plugin, and every frame on which the plugin publishes
   nothing, draws the sim's order exactly as before.

So the plugin ranks the finished frame once in `DataUpdate` (`plugin/OpenDash/LivePosition.cs`) and
publishes, for sixty-four rows and places, each leaderboard row's place over the field and in its
class, the row in each place over the field, and the row in each place of the player's class. Every
place a dashboard draws, the order every list is drawn in, the leader the gap is measured from and the
car ahead the interval is measured to are read through them. It publishes nothing outside an iRacing
race, after the chequered flag, or before the sim has placed anybody, and where a car cannot be
compared on distance it keeps the sim's order for it. It is a switch on the panel, Live positions,
on by default: a driver who would rather read the official order turns it off, and the plugin then
publishes nothing, which every dashboard already reads its way past.

The cost is a frame. The rows the plugin publishes are SimHub's rows on the frame it finished, and a
dashboard hands them to `driver*` functions that read the frame being built. The two are the same list
except on the frame on which a car crosses the line and the sim reorders it, and on that frame a row
can name its neighbour's car; the next frame puts it right. A list a lap behind is the defect; a row
wrong for a frame at a line crossing is the price of mending it, and it is accepted here.

### What the running sim can report

Added 2026-10-14 for #1101, and recorded in full in [ADR 0021](0021-a-dashboard-never-names-a-game.md).
1.0 claims three sims beside iRacing, and a few questions a dashboard asks have an answer that differs by
sim in a way no expression can see: whether the car has a pit limiter, whether it is on, whether the sim
reports damage, and whether the driver is in the car. Answering them in the expression would mean naming
the game, which the author turned down, so the plugin answers them from a table with a row per game and
publishes one property per question. It is this record's rule applied: a feature needs each answer,
SimHub publishes the game's code and nothing that says what the game can report, and every dashboard
reads each answer with iRacing's own reading as its fallback, so a package with no plugin draws what it
drew before.

## Where the line falls in practice

An expression is not a good place for arithmetic that is long, shared between many items, or
stateful, and the first two will happen before the third. Neither is, on its own, the second
condition of the Decision: each has a move to try first.

- **When the same derivation appears in more than about three items**, it belongs behind one name.
  The first move then is a JavaScript binding (`"Interpreter": 1`, which the second screens already
  use), not the plugin: it keeps the package complete on its own, which is the property worth
  defending.
- **When something genuinely needs memory between frames** — a value that shows for three seconds
  and then stops, a rolling figure over a window SimHub does not keep — the question is whether a
  JavaScript binding can hold state between frames at all. That is #167, and its answer is the
  strongest argument this record could ever be reopened with. If a binding cannot, the plugin is
  the only place such a thing can live, and four alert tickets change shape with it.

Until the three conditions of the Decision hold, a plugin that computes buys nothing and costs the
guarantee that a `.simhubdash` on its own is a complete product.

## Consequences

### Good

Zones B and C can be built now, and every page of the catalogue has a real source. ADR 0003 holds
unchanged, so the standalone promise holds. No number has two implementations: where the plugin
computes one, the expression reads it and falls back to SimHub's, and does not compute it again.

### Bad

Some expressions get longer — a five-lap average is five property reads and a division. That is a
readability cost in the NCalc helpers rather than a correctness one, and `ncalcFunctions.ts` now
checks that whatever is written actually dispatches.

Two catalogue entries shrink: the bar offers ten end fields rather than eleven, and the energy
family is honestly labelled rather than quietly empty.

### Unresolved

Whether a JavaScript binding can hold state between frames (#167). The answer decides nothing
in this record but sets the terms on which it could be revisited.
