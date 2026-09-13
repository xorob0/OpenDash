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
`Shift` to `Last`, the third lights together at `Last`, and the whole top band flashes at `Blink`.
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

**Per-gear shift points.** iRacing publishes one ladder for the car, and some cars genuinely want a
different one in first gear or at the top of the box. XOR-233 owns that question; this record
assumes one ladder per car and is what that ticket would amend.

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
are hidden most of the time.

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
