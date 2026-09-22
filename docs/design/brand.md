# Brand and visual direction

**Last updated:** 2026-09-22
**Status:** direction agreed; layouts in progress on the design canvas.

Token values live in [`design/tokens.json`](../../design/tokens.json), which is the source of
truth. This document explains the reasoning, so that future changes are made for reasons rather
than taste. What OpenDash *sounds* like is [voice.md](voice.md).

## Reference

The Porsche 911 GT3 R and 963 race dash: flat, functional, high-contrast, blocky numerals, no
ornament. Not the 992 road-car cluster, which is too decorative, and not the Taycan, which is
too soft.

Not Lovely either. Their licence covers their UI design; see
[../research/competitors.md](../research/competitors.md#licence-restriction).

## The governing principle

> The brand is achromatic. Colour is reserved for meaning.

Real race dashes are white numerals on black, where the only chroma is functional: shift
lights, warnings, flags. A driver at 200 km/h reads colour as state, instantly and without
parsing, and every decorative use of colour erodes that reflex.

This is why OpenDash's accent is `#33D9F2` ice cyan and never appears on the dash face. An
amber accent would collide with shift stage 2, and green or red would collide with flags and
alarms. Cyan sits outside the functional palette entirely, so it can be the identity colour in
the logo, the plugin panel and the documentation without ever being misread at speed. It is a
defensible position rather than a preference: legibility first, identity second.

## Palette

### Dash face

| Role | Token | Value |
|---|---|---|
| Background | `color.surface.base` | `#0A0B0D` |
| Zone fill | `color.surface.zone` | `#14161A` |
| Separator | `color.surface.raised` | `#1C1F24` |
| Primary numerals | `color.text.primary` | `#F5F7FA` |
| Supporting values | `color.text.secondary` | `#8A9099` |
| Field labels | `color.text.label` | `#5A6069` |
| Inactive segments | `color.text.dim` | `#33383F` |

Background is near-black rather than pure black, because pure black blooms on the IPS and OLED
panels used in DDUs and reduces perceived contrast rather than increasing it.

### Functional colour, state only

| Meaning | Token | Value |
|---|---|---|
| Shift 1, delta faster, within limits | `color.good.primary` | `#00D96A` |
| Shift 2, caution | `color.caution.primary` | `#FFB300` |
| Shift 3, delta slower, alarm | `color.danger.primary` | `#FF2D46` |
| Blue flag | `color.info.primary` | `#2E7BFF` |
| Session best | `color.best.primary` | `#B14BFF` |
| White flag, pit limiter | `color.neutral.primary` | `#FFFFFF` |

Purple for session best follows established sim-racing convention, and breaking it in order to
be distinctive would cost drivers real recognition time.

### Brand, identity surfaces only

| | Token | Value |
|---|---|---|
| OpenDash cyan | `color.brand.primary` | `#33D9F2` |

Logo, plugin panel, documentation, website. Never on the dash face. Which surfaces of the panel
may carry it is enumerated in [plugin.md](plugin.md), where the enumeration on the canvas and the
one the panel draws are recorded as disagreeing.

## Typography

| Role | Family | Licence |
|---|---|---|
| Numerals and data | Barlow Condensed | SIL OFL 1.1 |
| Labels and plugin UI | Barlow | SIL OFL 1.1 |

One family, two widths. It is DIN-adjacent, which is the right register for a race dash, and
the condensed width buys horizontal room for large numerals on a 4:1 screen.

The licence is not incidental. SimHub bundles fonts into `_SHFonts/` inside the package, so
shipping the dashboard means redistributing the font files, and anything not under the OFL or
MIT is disqualified whatever its merits. This ruled out several obvious DIN choices.

Labels are uppercase, small and dim; they should be findable but never compete with values.
Numerals are weight 600 to 700. Two constraints come from the renderer rather than from taste.
SimHub text items expose no letter-spacing property, so the tracking tokens apply to the plugin
panel and to documentation only, and label spacing on the dash face is obtained through case
and size alone.

The second is answered. SimHub exposes no OpenType features and Barlow Condensed's digits are
proportional, so a ticking value would jitter. Every value is therefore drawn in SimHub's own
monospace cells, measured from the bundled files to hold the widest ink a value can draw, and
recorded in `font.cell`. **Only what fits a cell may be drawn in one**, which is why `#`, `%`,
`&`, `@`, `M`, `W`, `m` and `w` are never part of a value: a car number is drawn bare and its hash
belongs to the label. That is rule 19 on the canvas.

One more thing the renderer decides rather than taste: the condensed faces ship under the family
name `openDash Display`. WPF reads the width word out of a family name and files "Barlow
Condensed" as a *stretch* of Barlow, which a `.djson` has no way to ask back, so every numeral
came out about a fifth too wide. Same outlines, renamed on the way into a package; the vendored
files are untouched.

A package carries the faces it is drawn in rather than every weight the two families offer, which
means Barlow Medium for labels and the display family at SemiBold and Bold for numerals, to which
the second screens add Light for the pit wall wordmark. The restriction follows from correctness
rather than from package size, since a weight drawn without its file is resolved by WPF to whatever
it can find, and every advance in `packages/dash/src/design/advances.ts` then measures a face that
never shipped. The build therefore refuses a package that draws a weight it does not carry, so that
a further weight has to be added to `FACE_FONT_FILES` or `SCREEN_FONT_FILES`, and measured, prior
to being drawn.

## Form

- Hard edges. Radius 0 by default, since race dashes are not rounded.
- No gradients, no gloss, no skeuomorphism.
- Zones separated by 1 px rules, not by boxes or cards.
- Hierarchy through size, aggressively: gear is 260 px and labels are 15 px, and a 17:1 ratio
  is correct here because timid hierarchy is unreadable at a glance.
- Negative space is functional. It is what makes a value findable by position, which is how
  drivers actually read a dash, by muscle memory rather than by scanning.

## Logo

Wordmark: `Open` in a light weight, `Dash` in bold. Condensed, technical. The two weights are the
whole of it; there is no second typeface and no letter spacing.

The mark is **the face's own layout**: a full width strip across the top, a full width strip across
the bottom, and three zones between them, the middle one wider than its neighbours because the gear
is. It is what an OpenDash face looks like from far enough away to stop reading the values. It is
`media/logo.svg`, drawn in a 32 unit square in `purpose.ui.accent` and nothing else.

It is the third mark. The first was a rev bar, rejected because segments on a shared baseline read
as a measurement whatever else is done to them, so it looked like a signal-strength icon at every
size. The second was the dash as an instrument, a housing closed over the top by a half circle with
a tachometer needle swept up and to the right inside it, and what removed it was not taste but a
screenshot. SimHub's own Dash Studio entry sits five rows above OpenDash in the same left menu and
is drawn as a gauge with a needle cut out of it, so the mark was reading as a second copy of a
neighbour in the one place where most people meet it. The obvious repair, inverting it into an
outlined bezel with a solid needle inside, was drawn and measured against that same screenshot: it
lands on Dash Studio exactly rather than away from it, because Dash Studio is already an outline
with a solid needle in it. A layout is the one thing a round instrument cannot be.

**This reverses an argument the second mark rested on.** The rev bar was dismissed on the grounds
that the dash "is a silhouette rather than an arrangement", and the present mark is unambiguously an
arrangement. The distinction that survives is narrower than the original claim: a row of equal
segments on one baseline is a bar chart, whereas five zones in the face's own proportions are the
product's structure, and nothing else in the category is shaped that way.

**Wider than it is tall**, because that is the shape of the thing. Every face OpenDash draws is a
landscape rectangle, from the 1920 by 480 reference down to the 800 by 286 nano, and a mark shaped
like a gauge pod would be standing for something the project does not make.

**Every corner is square.** The second mark spent the one curve the brand allows itself on the half
circle over its top; this one spends it nowhere and is `radius.none` throughout, which is the form
rule the rest of the product already keeps. An arc reintroduced here would be the shape drifting
back towards the instrument it stopped being, and `MarkTests` refuses one outright.

**The bands run the full width and only the middle is divided**, because that is how the face is
built. A vertical separation running the whole height would draw three columns of a table instead,
and the top and bottom rows of the face are not columns of anything.

**One shape with four holes.** The housing is one subpath, each separation another, and `fill-rule`
`evenodd` cuts them out of it, so a separation is the background showing through rather than a
second colour. That is what keeps the mark to one brush and lets it sit on the near-black panel and
on the README's white without being drawn twice.

The proportions are the face stylised rather than the face measured, and the two departures are
deliberate. On the 850 by 480 face the rules are one pixel wide and the three columns are near
enough equal thirds; here the gaps are 2.5 units so that they survive being drawn small, and the
middle column is 10.5 against 7.25 either side so that the gear zone is legibly the dominant one,
which is what it is on the face. The header is thicker than the footer, as it is on the face, since
it carries the rev bar and the session row against the footer's single row of fuel figures.

**It does not survive 16 px**, and that was accepted rather than overlooked. At half a pixel per
unit the five zones close up and the mark reads as a striped rectangle, so the browser tab is the
one place where it is weaker than what it replaced. It is held to 24 px instead, which is the size
SimHub's left menu draws it at and the size at which it is most often seen; at three quarters of a
pixel per unit, nothing in it may fall under one and a half pixels. It is still read monochrome,
which it survives intact: take the fill away and the layout is still the layout, and SimHub in fact
keeps only the alpha and repaints the menu icon white, so monochrome is not hypothetical.

It is drawn twice, because WPF cannot render an SVG and Markdown cannot render a `Canvas`. The two
copies are **one string**: `MarkShape.PathData` is the SVG's own `d`, character for character,
`Ui.Mark` hands it to `Geometry.Parse` behind an `F0` for the same even-odd rule, and `MarkTests`
compares them. The rev bar was held together by four numbers per bar instead, and it still drifted,
since the rectangles matched and the corner radius did not, a corner being neither a position nor a
size. `MarkTests` also holds the rules above rather than only the numbers, so the shape cannot
quietly stop being a dash. Change the SVG first.

Where it goes: the settings panel's header beside the wordmark, and the top of `README.md`. Not on
the face, which is a driver's instrument and not a billboard.

## Open questions

Shift-light thresholds are settled, and no longer the way this section first said. They are the
four RPMs **the sim publishes for the car** — `DriverCarSLFirstRPM`, `DriverCarSLShiftRPM`,
`DriverCarSLLastRPM` and `DriverCarSLBlinkRPM` — and SimHub's own per-car values
(`CarSettings_RPMShiftLight1`, `CarSettings_RPMShiftLight2` and the current gear redline) are the
fallback for a car that publishes none. [ADR 0014](../decisions/0014-the-shift-model.md) is why:
nothing in SimHub reads the car's own, so its bands are its idea of the car's lights rather than the
car's. The on-dash shift lights can still be switched off from the plugin for DDUs with physical
LEDs, and the same thresholds light those, since the generated LED profiles read the one model.

Thresholds are mirrored and **colour never is**: the sim publishes when a light comes on and no
sequence at all, so the three band colours stay in `design/tokens.json` with everything else.

Whether tyre temperature uses a continuous colour ramp or discrete bands remains open. Bands
are more glanceable, ramps carry more information, and the current leaning is bands. The
question matters less for the MVP than it first seemed, since iRacing only refreshes tyre data
in the pit stall.
