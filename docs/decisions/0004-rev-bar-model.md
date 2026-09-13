# ADR 0004: The rev bar is SimHub's shift-light model, and a plain RPM bar when shift lights are off

**Date:** 2026-09-10
**Status:** Accepted, and amended by [ADR 0014](0014-the-shift-model.md). What this record says
about SimHub's three properties is correct and its rendering is unchanged — but those properties
turned out to be SimHub's *idea* of the car's shift lights, computed from its own per-car settings,
rather than the car's. iRacing publishes the car's own and SimHub reads none of them. Since 0014
this behaviour is the fallback, drawn for a car that publishes no ladder of its own, and the
default is the car's.

## Context

The scope describes the hero's RPM bar as "fill width proportional to the displayed RPM
percentage" with "three colour bands" whose boundaries are SimHub's per-car shift-light values.
The design canvas draws fifteen segments, five green, five amber and five red, with a note that
5/5/5 is only a sample and that the boundaries come from SimHub.

Reading SimHub 9.12's `GameManagerBase` settled what SimHub actually exposes. The per-car values
are not bar percentages. `CarSettings_RPMShiftLight1` is a 0 to 1 progress value through the
first shift band, which starts at `redline - band1 - band2` and ends at `redline - band2`;
`CarSettings_RPMShiftLight2` is the same through the second band, up to the current gear's
redline; `CarSettings_RPMRedLineReached` is a flag. The band widths are SimHub game settings
that are not published as properties, so a bar that spans 0 to 100 percent of the RPM range
cannot know where the coloured bands begin.

## Decision

With `OpenDash.ShiftLights` true (the default), the fifteen segments implement SimHub's own
shift-light model, the one its LED profiles use: the first five light progressively as
`RPMShiftLight1` goes from 0 to 1 and are green, the next five follow `RPMShiftLight2` and are
amber, the last five light together when `RPMRedLineReached` is 1, are red and blink at 8 Hz.
Nothing is lit below the first band. This honours the per-car values exactly and is the
behaviour a driver expects from an LED strip.

With `OpenDash.ShiftLights` false, for DDUs that have physical LEDs, the same fifteen segments
fill in proportion to `CarSettings_CurrentDisplayedRPMPercent` in a neutral colour with no
blink. The plugin copy says "the rev bar stays", and this is what stays.

## Consequences

Two segment layers are emitted, one per mode, with their visibility bound to the setting. The
"band boundary" language in the scope and the tokens now means the per-car band progress
values, not positions along a percentage bar. If SimHub ever exposes the band widths, a
positional rendering can be added as a third mode without touching the cards or the plugin.

**Amended 2026-09-13 (XOR-138).** The setting these layers read is now `OpenDash.RevBar`, whose
three values are `shift`, `rpm` and `off`; `ShiftLights` stays attached as its deprecated alias and
is what `RevBar` falls back through. The two states above are unchanged and are still two layers:
`off` is not a third layer, because drawing nothing is not a layer. On a rectangular zone face it
selects a second arrangement of the whole screen, with the well's room given back to the zones; on
everything that has no such arrangement — the rev arc, the companion's speedo module — it falls back
to the plain RPM bar rather than going dark. The plugin copy no longer says "the rev bar stays".

**Amended again the same day (XOR-230).** [ADR 0014](0014-the-shift-model.md) made the shift model
the car's own four RPMs, with the bands above as the fallback, and the count of layers moved with
it: a rev surface now emits **three**, not two. The amendment above is still right about the thing
it was about, and the two statements are easy to read as a contradiction, so they are reconciled
here.

`off` is **still not a layer**, for the reason given above: drawing nothing is not a layer. What
changed is the other end. The `shift` state, which was one layer, is now **two** — `shiftLights`
for a car that publishes its own ladder and `shiftLightsSimHub` for one that does not — because the
choice between them is made per frame and the honest way to show a per-frame choice is two layers
whose visibility is bound to it. `rpmBar` is the third, and is what `rpm` and `off` both fall to.
So: three layers, two of which are the ladder and one the plain bar, and none of which is `off`.

The rendering of the fallback layer is untouched: it is the segments this record describes, lit by
the same three properties. One expression was tidied when the gear on the flag box came to ask the
same question — the first segment of a band, which compared `progress * n > 0`, now compares
`progress > 0`, which is the same test written the way anything with a single thing to colour has
to ask it. The `.djson` diff is two lines per rev surface and the bar does not move.
