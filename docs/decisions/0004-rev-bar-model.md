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
