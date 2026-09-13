# ADR 0014: The shift model is the car's own, not SimHub's

**Date:** 2026-09-13
**Status:** Accepted. Amends [ADR 0004](0004-rev-bar-model.md), which is not superseded: its
behaviour is still what a car that publishes no ladder of its own draws, and its reading of what
SimHub exposes was correct. What changed is that SimHub is no longer the only thing to read.

## Context

[ADR 0004](0004-rev-bar-model.md) built the rev bar on `CarSettings_RPMShiftLight1`,
`CarSettings_RPMShiftLight2` and `CarSettings_RPMRedLineReached`, having read SimHub 9.12's
`GameManagerBase` and established that they are band *progress* values rather than bar
percentages. That reading was right, and the bar it produced is honest about what those three
properties mean.

What was not known then is where they come from. [XOR-224](../research/simhub-leds-format.md)
went looking, and the answer changes what the bar should be built on.

**iRacing publishes the car's own shift-light RPMs.** The session string carries, per car,
`DriverCarSLFirstRPM` (the first light comes on), `DriverCarSLShiftRPM` (the sim is telling you to
shift), `DriverCarSLLastRPM` (the last light comes on) and `DriverCarSLBlinkRPM` (the lights blink:
over-rev), beside `DriverCarRedLine` and `DriverCarIdleRPM`. They are typed `double` on
`iRacingSDK.SessionData._DriverInfo`, so an expression reads them at
`DataCorePlugin.GameRawData.SessionData.DriverInfo.*` — the same nested raw-data path
`incidentLimit` already uses.

**Nothing in SimHub reads them.** Scanning all 323 assemblies of a 9.12.6 install for
`DriverCarSLFirstRPM` finds it twice: in `iRacingSDK.dll`, which declares it, and in an embedded
sample-data resource inside `ICarsReader.dll`. No code path consumes either. The iRacing reader
takes exactly one engine value out of that block:

```csharp
protected override double GD_MaxRpm()
{
    return base.NewData.Raw.SessionData.DriverInfo.DriverCarRedLine;
}
```

So the three `CarSettings_*` properties are computed by SimHub from its own per-car settings —
`MaxRpm`, `Redline`, `OverrideRedline`, `EnablePerGearRedline` and a `GearSettings[].UpshiftRpm`
table it learns through `SetAutoGearRedline` — seeded from the redline and otherwise from defaults
the user tunes in SimHub's Car Settings page. They are SimHub's idea of the car's lights. They are
not the car's lights.

This matters beyond the screen. The LED work of [ADR 0013](0013-lighting-hardware.md) needs a shift
model too, and if the strip and the face each pick their own, a rig will show two different answers
to the same question. Every competitor closes that gap with hand-tuned per-car segment tables —
Daniel Newman Racing carries them for 88 iRacing cars and several hundred more across six other
sims — because that is what supporting seven sims costs. openDash supports one, and that one
answers the question itself.

## Decision

**openDash's shift model is the four RPMs the sim publishes for the car, and SimHub's bands are
the fallback for a car that publishes none. One definition, read by the rev bar, the rev arc and
every generated LED profile.**

**The ladder.** Nothing is lit below `First`. The first band runs `First` to `Shift`, the second
`Shift` to `Last`, the third lights together at `Last`, and the whole top band flashes at `Blink`
(except in the last gear, below).
Each of the four values maps to exactly one thing a driver can see, which is the property that
makes it a mirror rather than an approximation. A band lights segment by segment on the same rule
ADR 0004 used — the band's progress times its segment count passed the segment's index — so the
rendering is unchanged and only its source moved.

**Where the definition lives: an expression, in one module.** `packages/dash/src/shift.ts` is the
only place the four property names appear, and a test fails if a second file mentions
`DriverCarSL*RPM`. The generator emits the same expressions into the `.djson` and, when the LED
work lands, into the `.ledsprofile`, because XOR-224 established that a profile's formulas are the
same NCalc with the same `isnull()`.

This is deliberately *not* a plugin property, and [ADR 0009](0009-does-the-plugin-compute.md) is
why it does not have to be. Four numbers compared against RPM is arithmetic over properties that
exist, done in the expression — the exact shape ADR 0009 permits — rather than a state machine
needing memory between frames. Making it a property would also break the standing rule that a
package is a complete product on its own, since the bar would then go dark without the plugin.

**The fallback, chosen per frame rather than per install.** A car qualifies for its own ladder when
it publishes a first light above zero, a last light above that, and a shift RPM between the two.
Another sim, a session that has not started, and a car that publishes zeros all fail that test and
get ADR 0004's behaviour unchanged. The test is evaluated once per layer rather than once per
segment, which is also what keeps the per-segment expressions short.

A band may still be zero-width — a car whose shift RPM equals its last light is ordinary — and its
segments then light together rather than never lighting. The comparison is written as a
cross-multiplication rather than a division, so a zero-width band divides by nothing.

**Which ladder is in use is visible.** The two are two layers, `revBar.shiftLights` and
`revBar.shiftLightsSimHub`, whose visibility is bound to the test. Whichever is visible in Dash
Studio is the one in use, which is how somebody debugging a car finds out which ladder it is on
without reading an expression. It costs nothing at runtime: the hidden layer is not drawn.

**One ladder per car, and the last gear is the exception.** iRacing publishes one set of four RPMs
for the car, not one per gear, and that is what the car's own lights show — so openDash shows the
same, with no per-gear offset and no table of cars in this repository. The one place a single
ladder is plainly wrong is the last gear, where there is nothing to shift into: a flashing bar is
an instruction that cannot be followed. So **in the last gear the top band stays lit but stops
flashing.** The bar still says the engine is at its limit; it just stops asking for a shift that
does not exist. SimHub's own `RPMSegments` container carries `BlinkOnLastGear` for the same reason,
which is the precedent for treating this as a choice rather than an oversight.

The last gear is found from `DriverCarGearNumForward`, in the same DriverInfo block as the four
RPMs, against iRacing's numeric `Telemetry.Gear` rather than `[Gear]`, which is a string. A car
that does not publish a gear count keeps flashing exactly as before, because the test requires a
count above zero.

This is where a hand-measured table beats the mirror: in a car whose power band moves with the
ratio, the useful upshift is not the same in second as in fifth, and Daniel Newman Racing measures
shift points per gear for six hundred cars. **This record first said openDash "does not, and will
not" carry such a table. That was reversed the same day — see the amendment at the end.**

**Thresholds are mirrored; colour never is.** The sim publishes when a light comes on and no
colour sequence at all — there is none anywhere in the session string. So a mirror is a mirror of
*behaviour*. Colours stay in `design/tokens.json`, and a car's own colour sequence belongs to Car
themes rather than here.

**`Telemetry.ShiftIndicatorPct` does not earn its place.** It is one number where the four are a
ladder, it cannot say where a band begins, and it would need its own fallback. The four are
sufficient and it adds nothing they do not already carry.

## What would reopen this

**A sim that publishes a ladder under another name.** The fallback is keyed on iRacing's property
names. Supporting a second sim's ladder is a third branch, not a change to this one, and it should
wait until somebody has actually driven that sim.

**Per-gear shift points.** Settled above for XOR-233: one ladder, the last gear excepted. What
would reopen it is a car where the single ladder is visibly wrong in a gear that is not the last —
the Mercedes-AMG GT4, whose manual says the first LED moves with the selected gear and quotes its
ladder for third only, is the known candidate, and XOR-170 hits the same question from the theme
side. If that car cannot be reproduced faithfully without per-gear thresholds, the two tickets
should agree on one answer before either moves.

**Evidence that a car's published ladder is wrong.** The whole decision rests on the sim's numbers
being better than SimHub's defaults. If a car is found whose published values do not match its
in-car lights, that is worth knowing, and the answer is probably still to mirror it and report the
sim's bug rather than to hand-tune a table.

## Consequences

### Good

A car released this morning mirrors correctly, with no table, no release and nobody measuring
anything. That is the thing the comparison cannot copy cheaply, and it falls out of supporting one
sim rather than seven.

The strip and the screen light at the same instant for the same reason, because they read one
definition. That was the reason to settle this before the LED profiles rather than after.

`Shift` becomes visible for the first time. SimHub's model has no equivalent — its two bands are
derived from the redline — so the point at which the sim is actually telling you to shift was not
previously on the bar at all.

### Bad

The per-segment expressions are longer and read four raw-data properties instead of one game
property. They are still arithmetic and still evaluated on properties SimHub has already
published, but the `.djson` is bigger and the diff of a rev-bar change is harder to scan.

There are now two ladders to keep working and only one of them is exercised by a typical test.
The fallback is the one that will rot quietly, because a developer with iRacing running never sees
it.

A third layer per face is emitted where there were two, so every face grew by fifteen rects that
are hidden most of the time. The three are `shiftLights`, `shiftLightsSimHub` and `rpmBar`: the
shift state is what became two, and `off` is still not a layer. [ADR 0004](0004-rev-bar-model.md)
carries the reconciliation, because it is that record's count that moved.

### Unresolved

**`design/tokens.json` now disagrees with the code and has deliberately not been changed.**
`shiftLights.stages[].from` names `CarSettings_RPMShiftLight1`, `CarSettings_RPMShiftLight2` and
`CarSettings_CurrentGearRedLineRPM`, and `stagesNote` says the boundaries are SimHub's per-car
values. Those fields are documentation inside the design source rather than anything the build
reads — only `segments`, `height` and `flashHz` reach code — so nothing is broken, but they are now
wrong about the default path. `design/` is the author's canvas and is not edited from code, so this
is recorded here rather than quietly fixed.

**Whether the flash should move to `Shift` rather than `Blink`.** `Blink` is over-rev, which is
late; a driver who wants to be told to shift is told by the second band beginning. Both are
defensible and the current choice mirrors what the sim itself does.


---

## Amended, 2026-09-13: a per-gear table, overriding a ladder that stays derived by default

The paragraph above refused a per-car table outright. That is reversed, and it is recorded here
rather than in a new record because it changes one clause of this one.

**What changed.** openDash now carries `data/shift-points.json`, a per-car, per-gear table that
**overrides** the derived ladder where an entry exists. Everything else stands: a car not in the
table gets iRacing's own four RPMs exactly as before, and the table is empty on the day it ships.

**Why the refusal did not survive.** The investigation behind it was incomplete in one direction. It
established that the sim publishes the car's ladder, which is true. It did not establish that the
sim publishes **nothing per gear**, which
[simhub-led-sources.md](../research/simhub-led-sources.md) has since shown:

* iRacing's `DriverInfo` block has seventeen `DriverCar*` keys and not one is per gear. There is no
  `DriverCarGearRatio`, and the four `DriverCarSL*` values have no per-gear variant.
* SimHub's `GearSettings[].UpshiftRpm` is unreachable from an expression — its parent `CarSettings`
  carries `[DoNotExpose]`, and `DeclareObject` skips a `List<>` regardless — and on iRacing it is
  never learned: `CarManager.EnsureCar` seeds every gear from `GD_Redline()`, which the iRacing
  reader does not override, so every gear gets the same estimate.
* `CarSettings_CurrentGearRedLineRPM` is, with stock settings, `DriverCarRedLine * 95/100` — one
  number, identical in every gear.

So "derive it rather than table it" was never an option. The choice was a table or nothing, and
nothing means a car whose shift point moves with the ratio is wrong in most gears.

**The precedence, highest first.** The table for this car and gear; then SimHub's own per-gear
redline when the user has turned it on by hand (`CarSettings_RPMRedLinePerGearOverride` is 1, and
`CarSettings_CurrentGearRedLineRPM` then does vary with the gear — their numbers, not ours); then
the car's own published ladder; then SimHub's bands. A profile carries one conditional group per car
and gear the table covers, composed over the derived ladder, so an empty table costs nothing at all:
no entries, no containers, no change to any profile. **Two of those four rungs are built; the
second is not, and the first is built on the strips alone. The correction below says which.**

**The table ships empty, and that is the honest state rather than an unfinished one.** openDash does
not carry measurements it has not made. A competitor's tables are theirs and will not be copied, and
inventing numbers would put a shift light in the wrong place with total confidence, which is worse
than not having one. What ships is the mechanism, the schema, the validator and the contribution
rules: an entry needs a traceable `source` and needs `first <= shift <= last <= blink`, and
`bun run check` fails if it does not. A measured car is then a reviewable pull request.

**What this costs.** The thing the original refusal protected — that a car released this morning
mirrors correctly with no table, no release and nobody measuring anything — is still true for every
car not in the table, which is every car today. What is given up is the claim that openDash *never*
needs one. It does, for a minority of cars, and the table is bounded by what somebody has actually
driven and written down.

---

## Corrected, 2026-09-13: which of this is built, and which of it was only written down

A review of XOR-233 read the two amendments above against the code and found three statements in
the present tense that nothing implemented. Nothing is being decided here and no behaviour changes:
this section replaces description with fact, so that the next reader is not misled the same way.
Where a rung is not built, it says so plainly rather than describing it as though it were.

**The four-rung precedence, as built.**

| Rung | Where | Built? |
| --- | --- | --- |
| 1. The per-gear table | `packages/dash/src/leds/shiftPoints.ts` | **Yes, on the RPM strips only.** `tabledStageLit` and `tabledOverRev` have exactly one caller in the build, `leds/rpmStrip.ts`; `packages/dash/test/leds.test.ts` calls them too, which is a test rather than a surface. No screen surface and not the flag box reads the table. |
| 2. SimHub's per-gear redline override | `simhubPerGear` | **No.** `simhubPerGear` is written and nothing calls it, and `CarSettings_RPMRedLinePerGearOverride` appears in no generated `.ledsprofile` and no generated `.simhubdash`. The rung's other property, `CarSettings_CurrentGearRedLineRPM`, **is** in the build — see the paragraph below — but not as this rung. |
| 3. The car's own published ladder | `packages/dash/src/shift.ts` | **Yes, on every surface.** |
| 4. SimHub's bands | `packages/dash/src/shift.ts` | **Yes, on every surface**, as the other half of the same per-frame choice. |

**"One definition, read by the rev bar, the rev arc and every generated LED profile" holds for
rungs 3 and 4 and for nothing else.** It is the claim in the Decision above, and it is still the
claim: rungs 3 and 4 are one module and every surface reads it. Rung 1 is the exception and is a
strip-only rung until somebody extends it; rung 2 does not exist. A car measured into
`data/shift-points.json` today would move a strip and leave the face, the arc, the companion's
speedo and the flag box on the derived ladder.

**`CarSettings_CurrentGearRedLineRPM` is in the build, and it is not rung 2.** The row above used
to say the property appeared in nothing the build writes, and that was false on the day it was
written: the companion's speedo printed it, unconditionally, in a field labelled *Redline*, under a
rev bar whose top band lights at `DriverCarSLLastRPM`. Two answers to one question, side by side, of
exactly the kind this record exists to prevent. That is fixed: `redlineRpm` in
`packages/dash/src/shift.ts` prints the rung-3 number, `Last`, wherever the car publishes a ladder,
and falls back to `CarSettings_CurrentGearRedLineRPM` only where it does not — because rung 4 is two
band *progress* values and a `RedLineReached` flag, and has no RPM to print. So the property is
still emitted, as the fallback half of one shared definition, in two of the twenty-two packages a
clean `bun run build` writes: `openDash.simhubdash` and `openDash Companion.simhubdash`, the two
that give the speedo page a `wide` box. The other shapes do not carry it at all, because
`modules/shedding.ts` makes the Redline field the first thing that page drops. It is never gated on `CarSettings_RPMRedLinePerGearOverride`, so it is never per gear,
which is the whole of what rung 2 would have added. `simhubRedlineRpm` in `shift.ts` is its one
body; `leds/shiftPoints.ts` carried a byte-identical second one, `simhubGearRedline`, and that is
the duplicate the speedo was reading.

**The speedo reads rungs 3 and 4, with one honest seam.** Its bar is rungs 3 and 4 exactly as the
face's is. Its printed number is rung 3 where the car publishes a ladder, and SimHub's redline
where it does not, because rung 4 publishes no such number. The seam is a property of what SimHub
exposes rather than a second model.

**Nothing diverges today, and that is exactly why this went unnoticed.** `data/shift-points.json`
ships empty, so rung 1 emits no containers anywhere and every surface in the build is on rungs 3
and 4 — the same expressions, on the same frame. The first measured car is also the first divergence,
and closing it is work rather than a property of the model.

**The ladder clause was not being honoured by the flag box, and now is.** The Decision says the top
band "flashes at `Blink` (except in the last gear)". The box's gear digit flashed on the *band*
instead — the moment `Rpms >= LastRPM`, and in the last gear too — because the shared band model
carried a boolean saying *that* the band flashes rather than an expression saying *when*. The model
now carries the expression (`ShiftBand.blink` in `components/revSegments.ts`, from `overRevEither`
in `shift.ts`), the digit reads it, and a test in `packages/dash/test/flagBox.test.ts` compares the
digit's emitted flash expression against the rev bar's top segment's, string for string.

**One definition means one definition.** `leds/shiftPoints.ts` also carried a second, byte-identical
copy of the rung-3 gate, exported under another name with a doc claiming callers and a test that did
not exist. It is deleted; `mirrorAvailable` in `shift.ts` is the only spelling of that condition.

It carried a second one of those, and that one was not dead. `simhubGearRedline` there and
`gearRedline` in `second/values.ts` serialised to the identical string under two names; the first
was documented as unread, the second was what the companion's speedo printed. Both are deleted, and
`simhubRedlineRpm` in `shift.ts` is the only spelling of that number. The readout that prints it
asks `redlineRpm`, which is the same question the rev bar's top band asks and gets the same answer;
`packages/dash/test/expressions.test.ts` compares the two, string for string, the way the flag box's
flash is compared above. `tabledCars` in `shiftPoints.ts` went the same way for a smaller reason: it
was an export with no caller anywhere at all.
