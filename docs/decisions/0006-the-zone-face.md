# ADR 0006: The face is zones, not slots

**Date:** 2026-09-11
**Status:** Accepted. Supersedes the slot model described in
[scope-mvp.md](../scope-mvp.md) and amends [ADR 0003](0003-plugin-settings-through-properties.md).

## Context

openDash shipped 0.1.0-rc.2 with a face built from **twelve equal slots**: a hero zone holding the
gear, and beside it a grid of identical rectangles into which the plugin dropped one of thirteen
cards. Every slot was the same size so that every card fitted every slot, a face declared its own
slot count, and a smaller screen simply exposed fewer slots.

It works, it is on a real simulator, and it is the wrong model.

Two passes of the design canvas assumed a grid a user arranges. The first was those twelve equal
slots; the second was cells a card could span, which fixed the "every card is the same size"
problem and kept the underlying assumption. Both assume the same thing: that a driver configures a
dashboard.

**A driver does not configure a dashboard. They change it mid-stint, with a thumb.**

That is what every mature package in this field does, and it is why their screens do not look
interchangeable with each other. It is also what a slot cannot express. A slot assignment is
written by a panel before a session; it has no notion of "the next one", so there is nothing for a
wheel button to advance.

There is a second, quieter problem. Under the slot model a bigger screen buys **more cards of the
same size**. Twelve small readouts at 1920 × 480 is not what a 1920 × 480 screen is for. What it
should buy is **more in each region**: zone C lists seven drivers at 850 × 480 and eighteen at
1280 × 720, which is the same page answering to a bigger box.

## Decision

A rectangular face is **five parts**: the rev bar in a recessed well, a bar of settled values, a
body of zone B, zone A and zone C, and band D across the foot.

Each zone shows **one page at a time from its own catalogue**, and a wheel button cycles it.

- Zone A chooses among four pages: gear with speed and revs, gear alone, speed, or the track.
- Zones B and C choose among the twenty-one pages `packages/dash/src/modules/` already builds for
  the companion and the pit wall. The catalogue stops being companion-only.
- Band D chooses among eight pages that suit a wide short band, and a flag takes the band over
  while one is out.
- The bar is not a zone and does not cycle. It is settled by definition, and that is what earns it
  the space.

**A page is never scaled.** It is laid out for the *shape* of the box it is given — a pair of
bands, width and height, not one ratio — and it sheds its secondary ranks before it shrinks its
numerals.

The full specification, with every rectangle of every face and every page of every catalogue, is
[docs/design/zones.md](../design/zones.md).

## What this costs, and why it is worth it

The settings contract changes shape. Twelve `Slot01`…`Slot12` properties become, per zone, the
page showing, a mask of which pages are enabled, and the page it opens on, plus one quick-glance
property and the bar's four end fields.

The card path is retired. `cards/`, `slots.ts` and `design/rung.ts` have no consumer once every
face is drawn from zones, and the rung in particular cannot survive: it derives a size step from a
slot width on the assumption that every slot of a layout is identical, which is false for zones —
at 1920 a zone is 769 × 314 and at 850 it is 274 × 328, and the same page serves both.

What is *not* lost is the work. The twenty-one pages already exist and already answer to a
rectangle and a density; nineteen of them fit all four canvas shapes unmodified.
`screens/zones.ts` already emits one zone dashboard per distinct rectangle and binds a widget's
screen index to a setting, which is exactly the mechanism a cycling zone needs. The pit wall has
been running the zone model since it shipped. This decision brings the face to where the second
screens already were.

## The amendment to ADR 0003

ADR 0003 makes the plugin inert — it attaches settings as properties and nothing else — and
promises that a package installed without the plugin is a complete product, because every
expression wraps its property read in `isnull()` with the design default.

**Which page a zone is showing is state**, advanced by a button and held by the plugin. So a
package running without the plugin shows each zone's start page and **cannot cycle**.

That is still a complete product by ADR 0003's letter: every binding falls back to its default and
the face is whole and useful. But it is the first feature for which the plugin buys something
material rather than convenience, and that should be written down here rather than discovered by a
user who installed only the `.simhubdash` and wondered why their button did nothing.

It does not reopen ADR 0003. The plugin still does not render, does not read telemetry and does
not compute; holding an integer a button increments is none of those. Whether it may ever compute
is [ADR 0009](0009-does-the-plugin-compute.md).

## Alternatives considered

**Keep the slots and add a page ring per slot.** Twelve slots each cycling a catalogue is twelve
buttons, or one button whose meaning depends on which slot is focused, which needs a focus model a
dash has no way to show. The number of regions has to come down before cycling makes sense, and
once it does the slots are zones.

**Cells a card can span.** This was the second pass and it is the better grid. It fixes the
uniform-size problem and it still answers the wrong question: it makes the arrangement richer
rather than making it changeable at speed.

**Leave the face alone and put the catalogue on the second screens only.** That is where it is
today, and it means the dash in front of the driver is the least capable surface openDash ships,
which is the wrong way round.

## Consequences

### Good

One catalogue serves the face, the companion and the pit wall, so a page is written once and
appears in three places. A bigger screen shows more per zone rather than more zones. A driver can
reach twenty-one pages with a thumb without taking a hand off the wheel. The shape model makes
"does it fit" a property of a page rather than of a layout, which is what lets a face be added
without revisiting every page.

### Bad

Every shipped face is rebuilt. The settings contract, its C# mirror, the plugin panel and the
plugin's tests all change. `OpenDash.Slot01`–`Slot12` are published in `README.md` as properties
an LED profile may read, so they are deprecated for a release rather than deleted (#170). The
work is large and the tree has to stay green throughout, which is why every zone pull request is
additive and the single subtractive one is last.

### Unresolved

What a round face does with zones. The two round artboards are still drawn on the slot model and
the only zone-era word touching them is that a round face uses its ring instead of a band.
`480round.ts` and `800round.ts` read `layout.slots`, which stays optional rather than being
removed, so they keep building throughout and the decision is owed before the card path is
retired rather than before the first zone face. #145.
