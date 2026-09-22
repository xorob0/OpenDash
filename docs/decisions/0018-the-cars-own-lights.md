# ADR 0018: The car's own lights, from a table OpenDash does not carry

**Date:** 2026-09-16
**Status:** Accepted. Amends [ADR 0014](0014-the-shift-model.md), which stands: its two ladders are
still what a car without a table gets, and its rule that one definition drives every surface is the
reason this record exists rather than a strip-only patch. Reopens
[ADR 0009](0009-does-the-plugin-compute.md) on the terms ADR 0009 itself set out, and extends
[ADR 0012](0012-update-checks.md) to a second host.

Amended by itself on 2026-09-21 for [#366](https://github.com/xorob0/OpenDash/issues/366), and on
one point that reaches into part 1 below: **the fetch is a button now.** Nothing about the second
host, the one archive or the 386 KB changed, but the sentences below saying the copy is refreshed
weekly and that the update-check switch governs it were true until that ticket and are not now. The
amendment is at the foot of the Decision and says what replaced them.

## Context

[ADR 0014](0014-the-shift-model.md) made OpenDash mirror the car's shift *behaviour*: the four
`DriverCarSL*` RPMs iRacing publishes and SimHub ignores. It was explicit about the half it could
not do — "what the sim does not publish is colour… a mirror is a mirror of behaviour" — and sent
colour to the Car themes project.

That half is what a driver sees. A 992 Cup lights from both ends inwards; a Next Gen stock car runs
green to amber to red, left to right; a W13 finishes with five blue LEDs in one block; a Ferrari
296 GT3 has six LEDs and a Formula Vee has one. Mirroring when they light and not how leaves the
strip saying the right thing in the wrong language, and it leaves a driver who changes car unable
to read their own wheel without re-learning it.

Getting the other half right runs into four standing refusals at once, which is why this is a
record and not a ticket:

- **[ADR 0009](0009-does-the-plugin-compute.md): the plugin does not compute.**
- **`data/shift-points.json`**: OpenDash does not carry measurements it has not made, and
  "copied from another product" is explicitly not an acceptable source.
- **[ADR 0012](0012-update-checks.md)**: the network is GitHub releases, and nothing else.
- **[ADR 0014](0014-the-shift-model.md)**: colour is OpenDash's tokens, not the car's.

## Investigation

[iracing-led-patterns.md](../research/iracing-led-patterns.md) is the whole of it. Three findings
decide this record.

**The pattern is not in telemetry, at any layer, and no project gets it from there.** Daniel
Newman Racing carries 600+ cars measured per gear behind a membership; the Fanatec App carries its
own closed set; [Lovely Car Data](https://github.com/Lovely-Sim-Racing/lovely-car-data) carries 85
iRacing cars in the open under CC BY-NC-SA 4.0. Everyone else falls back to the four RPMs, which is
where OpenDash is. A measured table is not a shortcut somebody took: it is the only known way.

**The whole vocabulary is one data shape.** A threshold and a colour per LED, plus a blink colour
and interval, express left-to-right, meet-in-the-middle, blocks, one-at-a-time, all-red,
green-yellow-red, gaps in the bar and a single shift lamp — all of them, with no case analysis
anywhere. Counted over the 85 cars: 49 ascending, 30 symmetric, 38 with blocks, 12 with gaps, LED
counts from 1 to 16, and **32 whose table changes with the gear**, which the four RPMs structurally
cannot express.

**SimHub has a door that fits.** `DynamicColorContainer.SetResultBase` evaluates its `ColorFormula`
as a string and hands it to `ColorConverter`, so one container per LED reading one property per LED
puts the entire decision outside the profile. (`RPMSegments` would also work — it tests each
segment against its own `StartValue` with no monotonicity assumed — but it blinks on SimHub's
redline rather than the table's, and its blink interval is a constant in the file.)

## Decision

**OpenDash mirrors the car's own bar — thresholds, colours, blink and gear — where a table for that
car exists, and mirrors its behaviour exactly as before where one does not.**

Five parts, each of which is the answer to one of the refusals above.

### 1. OpenDash carries no table. The plugin fetches one.

The data is CC BY-NC-SA 4.0 and this repository is MIT; vendoring it would put non-commercial
share-alike numbers into an MIT tree and into every generated profile, and it would break the rule
`data/shift-points.json` states about measurements OpenDash has not made. So OpenDash ships none of
it. The plugin fetches it onto the user's machine instead, caches it under its own folder, and
credits Lovely Sim Racing in the lights panel.

This is better than a snapshot on its own terms as well: a car measured next month works without a
release, and the numbers stay the upstream's to correct.

**One archive of every car, rather than one car at a time.** The obvious shape is to ask for the
car the driver just got into, and it is the wrong one: it would tell a CDN which car this user is
driving and when, every time they tried a new one. That is a session detail leaving the machine,
which is the line [ADR 0012](0012-update-checks.md) exists to keep, and no amount of "it is only a
car name" makes it a thing OpenDash should send. One archive asks the question every other user
asks and discloses nothing about this one. It also fails better: a car works the first time it is
driven, offline, in a session that never reaches the network at all. It costs 386 KB, once,
refreshed at most weekly, and the user's existing update-check switch governs it.

`data/shift-points.json` is unchanged and keeps its own purpose — a car **OpenDash itself** has
measured, contributed as a reviewable pull request.

### 2. The plugin computes, for this and for nothing else yet.

[ADR 0009](0009-does-the-plugin-compute.md) named exactly what would reopen it: a derivation an
expression cannot do, or one that needs memory between frames. This is both, and not marginally. No
NCalc expression can hold an 85-car table, none can fetch one, and the over-rev flash needs a clock
the expression engine does not have. There is no SimHub property to derive from, because the datum
is not in SimHub.

What ADR 0009 was defending — that a package alone is a complete product — survives intact, and
that is the test applied here rather than the letter of the rule. **The profile without the plugin
is not broken; it is the profile OpenDash ships today.** The mirror is one more rung on top of a
ladder that already falls back twice.

### 3. The precedence, top to bottom

Extending the list in `leds/shiftPoints.ts`, which is the file that has to stay true:

1. **The car's own bar**, per gear, per LED, from the fetched table. *New. The strips only.*
2. A measured entry in `data/shift-points.json`. Unchanged, still empty, still the strips only.
3. The car's published ladder, the four `DriverCarSL*` RPMs. Unchanged, everywhere.
4. SimHub's bands, for a car that publishes nothing. Unchanged, everywhere.

A car falls to rung 3 for any of: the plugin absent, the table not fetched yet, no entry for the
car, a malformed entry, or the driver preferring one of OpenDash's own styles. All five look the
same from the profile's side — one property is false — which is what keeps the failure quiet and
the fallback total.

### 4. The pattern is a function of a rectangle, the way a module is

A car's bar is 1 to 16 LEDs; a strip's run is 8 to 25. The renderer takes the table and the run
length and returns a colour per LED:

- **run longer than the bar**: each car LED is repeated to fill the run (`stretch`, the default), or
  drawn once at its true length in the middle of the run (`exact`, a setting).
- **run shorter than the bar**: nearest-neighbour resampling over normalised position, which
  preserves the ends, the symmetry and the blocks — a resampled meet-in-the-middle still meets in
  the middle.
- a transparent LED stays dark, so a car's gaps survive both.

### 5. The three styles stay

`leftToRight`, `meetInMiddle` and `f1` are unchanged and are what a driver who wants one look in
every car chooses. The new value is a fourth, `car`, and it is the default: OpenDash's opinion is
that the car is right and the driver may disagree.

**Amended 2026-09-21 ([#366](https://github.com/xorob0/OpenDash/issues/366)).** Part 1's fetch is
**user-initiated**. It used to happen on its own: `LoadInBackground` ran at startup, `IsStale` decided
a week had passed, and the archive came down gated by `Settings.CheckForUpdates`. Now a button on the
Lights tab is the only thing that fetches, `CheckForUpdates` does not govern the tables at all, and
starting SimHub makes no request whatever the settings say. `MaxAge` survives as a sentence rather
than a trigger — a copy over a week old is mentioned beside the button and refetched only if the
driver presses it.

Two reasons, and the record is the place to say that the second one is the weaker of them.

The plain one is that a driver could not find out why their lights were generic. `car` is the default
style (part 5) and the fallback is deliberately total and silent (part 3): the plugin absent, the
tables not fetched, no entry for the car, a malformed entry and a driver who chose another style all
look identical from the profile's side. That is right for the profile and wrong for the panel, and a
button with a car count beside it turns the commonest of the five into something a person can see.

The licence one is that a copy the *user* made, having been shown the project, the licence, the size
and the host, is a better answer to CC BY-NC-SA than a copy a background thread made on their behalf
during startup. It does not change what the licence permits — OpenDash still redistributes nothing,
which was always the load-bearing fact — and it is worth being honest that it is a strengthening of a
position rather than the establishing of one. The thing that would actually settle OpenDash's use of
this data is asking Lovely Sim Racing directly, which has not been done and is not what this
amendment is.

Both the privacy argument in part 1 and the attribution now appear on the page. Before this, "one
archive of every car, rather than one car at a time" was reasoning a user never saw: it was in the
source and in this record, and the panel said only that the tables followed the update check.

## Alternatives considered

**Bake the tables into the profiles at build time.** One `RPMSegments` per car and gear under a
`CarId` group; deduplicating identical gears takes 755 gear rows to 258, which is a few hundred KB
per profile and one comparison per car per frame — genuinely viable, and it would need no plugin.
Rejected because it cannot be reconciled with part 1: a table OpenDash does not carry cannot be
baked into a file OpenDash builds. It is also frozen at release, and it cannot blink at the car's
interval.

**One `ScriptedContent` per run, with the table as embedded Javascript.** One container instead of
one per LED. Rejected for now: it needs a Javascript body in a profile that is otherwise NCalc and
therefore unvalidatable by the generator, and it rests on a marshalling assumption
(`ToColorArray` requires `object[]` of strings) that has not been tested on the VM. Recorded in the
research file as the collapse to make if the per-LED property family proves too noisy.

**Vendor the data anyway, with attribution.** Legally possible — a mixed-licence repository is not
unusual — but it taints every generated profile with a non-commercial clause in a project that is
MIT, for no benefit over fetching.

**Measure our own 85 cars.** Honest and unaffordable: it needs someone to own and drive each car
and watch its bar. The mechanism for it already exists and stays.

## Consequences

### Good

A driver sees their own car's lights, in the car's colours, at the car's RPMs, in the car's gear —
on a strip, a brow or a button box OpenDash has never heard of, because the renderer is a function
of the run length. The pattern vocabulary needs no code: adding a car is upstream's business, and
OpenDash learns it without a release. And the taxonomy that looked like six features is one
renderer and a table.

### Bad

**The mirror needs the plugin.** A profile on its own falls to rung 3, which is what it does today,
but "install the plugin" is now a real answer to "why do my lights look generic".

**A second host on the network.** ADR 0012 promised GitHub and a User-Agent. It now also fetches
from the Lovely Car Data repository, on the same terms — nothing about the user is sent, it is
switchable off, and a failure is silent and falls back. The promise is unchanged; the list of hosts
grew by one and this record is where that is written down.

**OpenDash depends on an upstream it does not control.** Coverage is partial (85 cars), the schema
can move (it is versioned `v2.0.0`), and one of the 85 files is already malformed. The parser
skips what it cannot read rather than throwing, and a car OpenDash cannot parse is a car on rung 3.

**The strip and the screen now disagree about colour.** The strip mirrors the car's palette; the rev
bar and the rev arc still draw OpenDash's tokens at OpenDash's thresholds. This is the divergence
`shiftPoints.ts` warns about, arriving for real, and it is the first thing to close
([#353](https://github.com/xorob0/OpenDash/issues/353)): the same computation that fills a strip can
fill a bar, and rung 1 should not stay strips-only for long.

### Unresolved

Whether the rev bar should adopt the car's colours at all, or whether a screen is a place where
OpenDash's palette should win and only the *timing* should mirror. That is a design question rather
than a mechanical one, and it is the reason rung 1 is deliberately strips-only in this record
instead of being pushed through `shift.ts` where the screens would have picked it up for free.
[#353](https://github.com/xorob0/OpenDash/issues/353) is where it is decided, and it lists the three
answers rather than assuming one.
