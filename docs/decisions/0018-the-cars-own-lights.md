# ADR 0018: The car's own lights, from a table OpenDash does not carry

**Date:** 2026-09-16
**Status:** Accepted. Amends [ADR 0014](0014-the-shift-model.md), which stands: its two ladders are
still what a car without a table gets, and its rule that one definition drives every surface is the
reason this record exists rather than a strip-only patch. Reopens
[ADR 0009](0009-does-the-plugin-compute.md) on the terms ADR 0009 itself set out, and extends
[ADR 0012](0012-update-checks.md) to a second host.

Amended by itself twice, and both amendments are at the foot of the Decision.

On 2026-09-21 for [#789](https://github.com/xorob0/OpenDash/issues/789), on one point that reaches
into part 1 below: **the fetch is a button now.** Nothing about the second host, the one archive or
the 386 KB changed, but the sentences below saying the copy is refreshed weekly and that the
update-check switch governs it were true until that ticket and are not now.

On 2026-09-27 for [#353](https://github.com/xorob0/OpenDash/issues/353), on the question this record
left open: **a screen takes the car's thresholds and keeps OpenDash's colours.** Part 3's rung 1 is
no longer strips-only, and the Unresolved section below is answered rather than standing.

On 2026-10-04, on part 2's "for nothing else yet": **the plugin also estimates wheelspin and
lock-up**, because iRacing publishes neither and SimHub publishes its own estimate nowhere. The
amendment of that date, at the foot of this record, gives the reasoning.

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

**The pattern is not in telemetry, at any layer, and no project gets it from there.** A measured
table is the only source, and the large ones are closed; the Fanatec App carries its own closed
set; [Car Data](https://github.com/Lovely-Sim-Racing/lovely-car-data) carries 85 iRacing cars in
the open under CC BY-NC-SA 4.0. Everyone else falls back to the four RPMs, which is where OpenDash
is. A measured table is not a shortcut somebody took: it is the only known way.

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
credits the dataset's authors in the lights panel.

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

**Amended 2026-09-21 ([#789](https://github.com/xorob0/OpenDash/issues/789)).** Part 1's fetch is
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

The licence one is that a copy the *user* made, having been shown the project, the licence, the
size and the host, is a better answer to CC BY-NC-SA than a copy a background thread made on their
behalf during startup. It does not change what the licence permits — OpenDash still redistributes
nothing, which was always the load-bearing fact — and it is worth being honest that it is a
strengthening of a position rather than the establishing of one. The thing that would actually
settle OpenDash's use of this data is asking the dataset's authors directly, which has not been
done and is not what this amendment is.

Both the privacy argument in part 1 and the attribution now appear on the page. Before this, "one
archive of every car, rather than one car at a time" was reasoning a user never saw: it was in the
source and in this record, and the panel said only that the tables followed the update check.

**Amended 2026-09-27 ([#353](https://github.com/xorob0/OpenDash/issues/353)).** Part 3's rung 1 is no
longer the strips'. **A screen lights at the instants the car's own bar lights, in OpenDash's
colours.** The rev bar, the rev arc, the companion's speedo bar and the Redline printed beside it all
take the car's measured thresholds, per gear, and draw them in `design/tokens.json`; the strip is
unchanged and stays a literal copy of the car's bar, colours and all.

The ticket listed three answers and this is the first of them. It was chosen against **taking the
car's colours too** — the most faithful, and the one that makes a photograph of the rig look like the
car — because a face is OpenDash's drawing of the car's state rather than a copy of a bar. A strip is
the same kind of object as the thing it mirrors: sixteen lamps in a row, and the only reason it is
not the car's own bar is that it is bolted to a different wheel. A rev bar is not; it is fifteen
segments in a well, in a face whose every other colour comes from the tokens, and a cornflower-blue
over-rev inside it reads as a rendering fault rather than as a Porsche. Timing is the half a driver
learns and colour is the half they read, and adopting the timing is what stops two surfaces in one
rig saying different things about the same engine.

It was also chosen against **colour behind the same `car` style the strips obey**, which is the shape
[ADR 0011](0011-personalisation.md) would expect and is a setting for a picture nobody has asked for.
The one thing taken from that answer is its gate: the *timing* is behind the rev light style rather
than unconditional. A driver whose strips are on `car` gets the car everywhere, and a driver who chose
`leftToRight`, `meetInMiddle` or `f1` on them has said whose lights they want and gets the derived
ladders on the screens as they did before. That is a departure from answer 1 as the ticket wrote it —
"the bar takes the car's thresholds" full stop — and it is deliberate: the style is the rig's one
answer to whose lights these are, and a screen that ignored it would be a second answer. No new
setting was added, which the ticket's third answer would have needed.

**The gate is a question only the plugin can answer, and that is why it is published.** The style is
chosen *per strip*: the Lights tab writes it onto the bar, and each installed strip profile has the
rig-wide property name rewritten to that bar's own. A screen has no strip, and the bars are a list the
driver adds to at runtime, so their property names cannot appear in an expression a package was built
with. So the plugin reduces the list to one boolean, `OpenDash.CarLadderChosen` — any strip asking for
the car's own lights, and the rig-wide value only for a rig that has no strips at all — and every
surface hangs on that: the four rev-bar layers, the Redline readout, and the flag box's digit behind
its own switch. It is the same reduction that decides whether the tables are walked, so a surface
cannot be gated on an answer that leaves the numbers it reads unfilled.

This is the correction to what #353 first shipped, and it is recorded rather than quietly fixed
because the first version of this amendment claimed the opposite. That version gated the screens on
`OpenDash.LedRpmStyle` itself — the rig-wide field a strip with no opinion of its own falls back to —
and that field has had no writer in the panel since the styles went per bar. A driver who set their
one strip to F1 left it at its default, `car`, and got the car's measured instants on every screen
beside an F1 pattern on their strip; a rig carrying `f1` in a settings file written before that change
got the reverse. Neither is what the paragraph above says, and both are what it now does.

**The screens read a number, never the table.** The plugin already walks the table every frame for
the strips, so it publishes three more values out of the same walk: how many of the car's own lamps
are lit now, how many that gear's ladder has, and the RPM the top third of it lights at. A bar lights the segment
`local` of its band `b`, out of that band's `m`, when `lit * 3m > (b * m + local) * lamps` — the same
cross-multiplication the published ladder's bands use, and asked per band for the same reason they are.
So the fraction of the bar that is lit is the fraction of the car's bar that is lit, and the first
segment of a band lights on `lit * 3 > b * lamps`, which is exactly where the digit reaches that band.
That holds at any segment count and however the thirds divide, which the first cut of this amendment did
not: it asked for segment `k` of the whole bar, which coincides with the digit only where the top band
begins at two thirds exactly. Fifteen does and fourteen does not, and nothing said so.
`CarLadderTopRpm` is what the Redline readout prints, so the number beside the bar is still the number
the bar goes red at, which is what `shift.ts` promises.

**The flash is the car's own where the car has one, and OpenDash's where it has not.** `CarLadderOverRev`
already existed for the digit and is the table's own redline for the gear, at the car's own blink
interval. But 47 of the 85 measured cars publish no flash at all and say so with a zero, and for them
that property is false at any RPM. On a strip that is right — a strip is a copy of the car's bar, and a
flash the car never gives is not one to invent. On a screen it is not: OpenDash's own top band has
flashed at redline since [ADR 0004](0004-rev-bar-model.md), on every car, and going dark there on more
than half the cars anybody has measured would be adopting the car's *look* while claiming to take only
its timing. So the plugin publishes `CarLadderFlashes` — whether this car and gear have a flash to give
at all, which is not whether they are giving one — and where it is false the bar, the arc and the digit
flash on the published threshold, exactly as they did before the tables reached them. Where it is true
nothing falls back, because a car that flashes above the published threshold would otherwise flash
early and the two would fight. The first cut of this amendment said "the flash is `CarLadderOverRev`,
which already existed for the digit" and left those 47 cars solid at the limit on every surface.

The fallback is unchanged and total, and there is now a fourth way to land on it: no plugin, no
tables, no row for this car, a row that would not read, or a rig on one of OpenDash's own styles — and,
for the flash alone, a car that publishes none.
All of them leave the screens on rungs 3 and 4, drawing exactly what they drew before — and which one
a car is on is which layer of the bar is visible in Dash Studio, as it has been since ADR 0014.

**The two seams this leaves, said plainly.** The first is a rig with two strips set differently: one
on `car` and one on `f1` asks for the car's own lights, so the screens take the car's instants and the
F1 strip keeps its own pattern. Any strip is enough on purpose — the mirror is one computation feeding
every strip, and a rig that has asked for the car's instants anywhere has asked for them — but a driver
who wanted that split on the strips did not necessarily mean it for their faces.

The second is a rig with **no strips at all**: a wheel with a flag box on it and no RGB anywhere, or a
screen and nothing else. There is no bar to carry a style, so the rig-wide value answers, and on a rig
that has never held an older version that value is the default, `car`. Such a driver can still turn
the bar off or to plain revs per face, and their flag box has a switch of its own, but they cannot ask
for OpenDash's derived ladders while keeping the bar. That is the argument for the Lights tab offering
the rig's answer beside the strips', and it is not offered here because a setting nobody has asked for
is what answer 3 was rejected for — the next person to want it should add the row rather than discover
the gap.

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
from the Car Data repository, on the same terms — nothing about the user is sent, it is switchable
off, and a failure is silent and falls back. The promise is unchanged; the list of hosts grew by
one and this record is where that is written down.

**OpenDash depends on an upstream it does not control.** Coverage is partial (85 cars), the schema
can move (it is versioned `v2.0.0`), and one of the 85 files is already malformed. The parser
skips what it cannot read rather than throwing, and a car OpenDash cannot parse is a car on rung 3.

**The strip and the screen now disagree about colour.** The strip mirrors the car's palette; the rev
bar and the rev arc still draw OpenDash's tokens at OpenDash's thresholds. This is the divergence
`shiftPoints.ts` warns about, arriving for real, and it is the first thing to close
([#353](https://github.com/xorob0/OpenDash/issues/353)): the same computation that fills a strip can
fill a bar, and rung 1 should not stay strips-only for long.

### Answered, 2026-09-27

Whether the rev bar should adopt the car's colours at all, or whether a screen is a place where
OpenDash's palette should win and only the *timing* should mirror. It was the reason rung 1 was
strips-only here rather than pushed through `shift.ts` where the screens would have picked it up for
free, and [#353](https://github.com/xorob0/OpenDash/issues/353) decided it: **the timing mirrors and
the palette wins.** The amendment at the foot of the Decision says so and names the two answers it
was chosen against. Rung 1 now reaches the rev bar, the rev arc, the companion's speedo and the
Redline beside it; it still does not reach them as colour, and no part of it is read twice.

## Amended, 2026-10-02: the style is a switch, and the Lights tab is the LEDs page (#369, #791)

**What moved.** Part 5 kept three styles beside `car`. The LEDs page offers one switch per strip,
"Car's own rev lights" ([#369](https://github.com/xorob0/OpenDash/issues/369)): on writes `car`, off
writes `leftToRight`. `meetInMiddle` and `f1` stay in the contract's set so an older settings file
reads, and a strip carrying either reads as off. The gate in the amendment of 2026-09-27 is unchanged:
`OpenDash.CarLadderChosen` is any strip asking for the car's own lights.

**Where it lives.** The Lights tab is gone ([#791](https://github.com/xorob0/OpenDash/issues/791)).
The switch is in the strip's Rev lights. The download, the car count, the attribution and the
licence are in Every strip at the foot of the LEDs page, under the name Car Data. The download is
still user-initiated and still the only thing that fetches. The Matrix page's digit switch reads
"Car-specific shift points".

**What the panel now says.** The car line under the switch names the car and whether Car Data has
it, and says nothing outside iRacing, since the plugin reads iRacing's data only. When nothing was
ever downloaded it points at Every strip. A failed download beside a working copy keeps the count
and age of that copy in the line.

## Amended, 2026-10-04: the plugin estimates wheelspin and lock-up

**What was missing.** On iRacing the strips' traction control lamp never lit, and neither did the ABS
lamp on a car without ABS. iRacing publishes no wheel speeds and no traction-control flag, and SimHub's
`GameData.TCActive` is a hard 0 there. SimHub does estimate slip, but only for ShakeIt. The decompiled
9.12.6 assemblies show where:

- `GameData.FeedbackData` carries per-wheel slip and is `[DoNotExpose]`, so it never becomes a
  property.
- Its `TCActive` is copied from `GameData.TCActive`, so it is the same 0.
- ShakeIt's wheel slip effect falls back to an "RPM vs Speed" estimate for a sim without wheel speeds
  (`WheelSlipEffect.GetRpmSpeedSlip`), and hands the result to a shaker.

No profile and no screen can read any of it.

**What the plugin does.** `SlipEstimate.cs` applies ShakeIt's rule as it is: the change in speed over
RPM between two frames, ignored at rest, in neutral and for half a second after a gear change, and
weighted by throttle for a spin or by brake for a lock. It publishes three booleans,
`OpenDash.WheelSpin`, `OpenDash.WheelLock` and `OpenDash.TCInferred` (a spin while the TC dial is above
zero). The lamp threshold and the 200 ms hold are OpenDash's own, a first guess for a rig to correct.

**Why this passes part 2's test.** It is the derivation [ADR 0009](0009-does-the-plugin-compute.md)
named: it needs the last frame, and no NCalc expression keeps one. The datum is not a SimHub property
either. And a profile without the plugin is unchanged: the estimates read as null there, and each lamp
lights on the sim's own report alone, as it did before.

**What a strip does with it.** The traction control lamp lights on `TCActive` or on `WheelSpin`, and
the ABS lamp on `ABSActive` or on `WheelLock`. Each strip has a switch, "Infer wheel spin and wheel lock"
(`LedInferSlip`), on by default, which a driver turns off to have the lamps light only on what the sim
reports. The estimate is the car's and rig-wide; the switch is the strip's.

