# ADR 0015: What a car theme is, and the scope line it moves

**Date:** 2026-10-06
**Status:** Accepted. Moves the "Personalisation that changes the layout" line in
[scope.md](../scope.md) for a shipped car theme, adds a row to the table in
[ADR 0011](0011-personalisation.md), and narrows [ADR 0006](0006-the-zone-face.md), whose five parts
become the default theme's anatomy rather than every face's. It is the record
[#193](https://github.com/xorob0/OpenDash/issues/193) asked for, written when the Porsche theme
([#205](https://github.com/xorob0/OpenDash/issues/205)) gave it a first consumer to be measured against.
How the packages it multiplies reach a rig is [ADR 0016](0016-themed-package-distribution.md).

## Context

The Car themes project wants a face that a driver recognises as the car's own: the Porsche 992 display
with its sixteen dots, its outlined setting boxes and its grey and black cells, drawn at the sizes
OpenDash ships. Every ticket in that project fell under a refusal in [scope.md](../scope.md), and the
refusal it fell under changed shape while the tickets waited. When #193 was written, scope.md refused
theming outright; [ADR 0011](0011-personalisation.md) has since reversed that for colour and left a
narrower line behind, "Personalisation that changes the layout", on the ground that a typeface, a size,
a spacing and a position are consumed by a layout decision in TypeScript, and that nothing arriving
after the build can re-run it.

That ground is exactly where a car theme stands on the right side. One could think that a theme which
moves the gear, rounds every box and redraws the shift lights is the very layout change the line
refuses. In reality the line refuses a layout decided after the build, by a user, at runtime, which
no test can measure; a car theme is decided before the build, by a contributor, in a pull request, and
it is measured by `textFit` and `secondScreens` like every other package. The argument of ADR 0011
therefore does not need to be reversed here, only applied to a case it did not consider.

ADR 0011 also rejected variant packages, in the paragraph on typefaces, because ten faces times three
typefaces times four themes is "a release nobody can navigate and a dashboard manager nobody can read".
A car theme is a variant package, so the rejection has to be answered rather than ignored. It is
answered by the two facts that distinguish the cases: a car theme is chosen per car by a driver who
owns that car, which a typeface is not, and since [ADR 0017](0017-a-screen-is-an-instance.md) a rig
holds only the screens its owner added, so a variant that nobody picks never reaches the dashboard
list. ADR 0016 makes the second fact a rule for themed packages.

## Decision

**A car theme is a build-time variant: one package per size per theme, resolved into literal values in
the `.djson` exactly as the default look is. There is no `OpenDash.Theme` property, and nothing at
runtime selects a theme.**

A runtime selector was considered and rejected for three reasons. ADR 0011 settles that geometry may
not be bound, and a theme that could not move a rectangle would be a palette, which ADR 0011 already
provides. Moreover a package carrying two looks behind a switch is two scene graphs in one folder,
whose bindings are all evaluated every frame for the look that is not drawn. Finally, choosing which
dashboard a display shows is SimHub's decision, and SimHub already makes it per car through its
playlist ([#199](https://github.com/xorob0/OpenDash/issues/199)), so a theme selected at runtime would
be a second matching engine competing with the one the driver already configures.

### What a theme may change

A theme may change four things, listed in the order of what they cost.

**The palette.** A theme carries a token overlay on the `color` and `purpose` layers of
`design/tokens.json`, which stays the base and is not edited for a theme. The overlay may remap a
semantic or purpose token and may add the values its car needs, the Porsche's greys for instance, and
every colour value a theme introduces is written in its overlay and nowhere else. The rule that no
colour literal appears in `packages/dash/src` stands, since the overlay is a token file and the code
beside it reads it as `tokens.ts` reads the base.

**The type**, bounded by what can be measured and what may ship. A theme draws only faces that are in
`FACE_FONT_FILES` in `packages/dash/src/dashboard.ts` and measured into
`packages/dash/src/design/advances.ts`, so a face a theme needs is added and measured before it is
drawn, and it is OFL or MIT because it travels in `_SHFonts/` (the trade dress section of scope.md).

**The chrome**: frames, rules, fills, corner radii and the treatment of a readout's tray. The
brand's square corners and one-pixel rules are the default theme's chrome rather than a law over
every theme, which is what lets the Porsche round its boxes.

**The anatomy**, which is the expensive one. A theme declares its anatomy as a function from a size
to named regions, and the zone anatomy of `packages/dash/src/zones/` and the layouts becomes the
default implementation of that function rather than the shape of every face
([#196](https://github.com/xorob0/OpenDash/issues/196)). A theme may thus move where a page is drawn,
which page a zone opens on, and which regions a transient state takes over, as the Porsche's limiter
takes zones B, A and C together; it may add a page of its own to a catalogue, as the Porsche's row of
boxes, tyres and bias is one more page of band D's. An added page goes at the end of the catalogue,
since a zone's setting is the index of its page and a page moved from its index would open another
driver's zone on a page they did not choose; the conformance test of #196 therefore refuses a page
removed or moved and lets an added one through by name only.

A theme does not get a page that selects itself. The Hyundai TCR cluster was raised as the case for
one, and the answer, decided on 2026-10-06, is that an automatic page selector is out of scope for
v2.0: the contract has no such page, adding one would be a contract change under ADR 0003 rather than
an anatomy, and the Hyundai themes take the wheel button like every other.

### What a theme may never do

**A theme may never remove a page from a catalogue, and it may never drop a field.** This is the line
that separates a theme from a skin, and it is stated in those words so that it can be applied without
asking. Every zone the default face cycles still cycles in the theme, from the same wheel button,
through the same catalogue. A theme that cannot fit a value in the region it gives it sheds exactly as
a page already sheds, through `fitFields` and `fieldsThatFit` in `packages/dash/src/second/field.ts`
and the order recorded in `packages/dash/src/modules/shedding.ts`, and never by leaving the value out
of what it builds.

A theme may decline a size, which is a different act. Declining means that no package of that theme
is built at that size, so a driver with that screen keeps the default face, and the theme catalogue
in the contract ([#202](https://github.com/xorob0/OpenDash/issues/202)) says which sizes each theme
supports, so that the plugin offers only what exists. A theme that claims a size, however, claims all
of it: every page at every shape its regions produce, with the conformance harness of
[#200](https://github.com/xorob0/OpenDash/issues/200) passing for each.

### What a theme is, as a file

A theme is a module under `packages/dash/src/themes/<id>/`, holding its anatomy and its token overlay
together. The two halves are of different kinds and the record says which is which: the overlay is
data, in the shape of the layers of `design/tokens.json` it overlays, and the anatomy is TypeScript,
since it has to compute rectangles from a size. They live in one folder because a reviewer reads them
together, and because a theme is one decision about one car rather than two files that happen to share
a name.

**`default` is a theme like any other.** It lives under `packages/dash/src/themes/default/`, its overlay
changes nothing, its anatomy is the zone anatomy, and it is built by the same path as every other
theme. There is no separate code path for "no theme", because a second path is a second thing to keep
correct, and the default one would be the path every user runs.

### The requirement that makes it reviewable

**The packages the default theme builds are byte-identical to what `main` builds before the theme
layer exists.** Every pull request that builds the machinery is reviewed against that property, and
proves it by building on `main` and on its branch into two directories and diffing the generated
`.djson` files. A refactor that moves the face into a theme and changes one byte of the default
package has changed the product, and the diff says where.

## What it costs

**Packages multiply, and that is the price of the decision.** A theme that claims every rectangular
face is up to eight packages, and the project holds a ticket per car, several of which cover a family
of cars on one display. The multiplication reaches the build, CI, the plugin's resources and the
dashboard list a driver sees in SimHub. This record takes it knowingly and hands it to
[ADR 0016](0016-themed-package-distribution.md), which decides that a themed package is embedded and
written only when it is picked.

**The anatomy is code a contributor has to write per theme.** A theme with an anatomy of its own owes
a drawing at every size it claims and a function that produces it, and the cheapest themes will be
those that change the palette and the chrome over the default anatomy. That is a cost to the
contributor rather than to the driver, and it is the right side for it to fall on.

**Two rules of the house become the default theme's rules.** The brand's square corners and the
principle that colour is reserved for state are kept by the default theme and may be departed from by
a car theme where the car does, which the trade dress section of scope.md bounds. A reader of
[brand.md](../design/brand.md) should read its form rules as the default theme's from now on.

## Unresolved

**Whether a theme may change how a value is formatted.** The 992.2 Cup draws its brake bias signed
([#211](https://github.com/xorob0/OpenDash/issues/211)), and that ticket put the question to this
record. Formatting sits close to meaning, closer than colour or position, and this record does not
answer it: the Porsche theme draws `BrakeBias` as the percentage OpenDash already draws, and the signed
form waits for the theme that needs it to argue the case.

**Whether the portrait face may ship under a car's name.** The Porsche declares an arrangement of its
own for 600 x 686, although no Porsche display is portrait. The rule above permits a theme to claim
the size or to decline it; which of the two the Porsche does is #205's question rather than this
record's.
