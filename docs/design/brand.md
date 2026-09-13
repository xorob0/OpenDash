# Brand and visual direction

**Last updated:** 2026-09-13
**Status:** direction agreed; layouts in progress on the design canvas.

Token values live in [`design/tokens.json`](../../design/tokens.json), which is the source of
truth. This document explains the reasoning, so that future changes are made for reasons rather
than taste.

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

Logo, plugin panel, documentation, website. Never on the dash face.

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

The mark is **the dash itself**: a housing wider than it is tall, square across the bottom and
closed over the top by a half circle, with a tachometer needle swept up and to the right inside it.
It is `media/logo.svg`, drawn in a 32 unit square in `purpose.ui.accent` and nothing else.

It replaced a rev bar, which was the right idea argued the wrong way round. The rev bar is a part
of the face rather than the face, and drawn as segments it could not escape being a bar chart:
segments on a shared baseline read as a measurement whatever else is done to them, so the mark
looked like a signal-strength icon at every size it was put at. The dash is the whole product, it
is a silhouette rather than an arrangement, and a needle says *instrument* in a way no row of
anything does.

**Wider than it is tall**, because that is the shape of the thing. Every face OpenDash draws is a
landscape rectangle, from the 1920 by 480 reference down to the 800 by 286 nano, and a mark shaped
like a gauge pod would be standing for something the project does not make.

**The top is one half circle, not two rounded corners.** Its radius is exactly half the width, so a
single arc spans the whole of it and the straight sides are what is left underneath. Anything less
leaves a flat run across the top, and the silhouette stops reading as a dash and starts reading as
a box with the corners taken off. The floor stays square, which is `radius.none`: this is the one
curve the brand allows itself, and it is the one doing all the work.

**The needle is a needle**: a round hub, and a blade running from that hub's own *tangent points*
out to a single point. Tangents matter more than they sound. A wedge whose base is a chord across
the hub steps off it and the whole thing reads as a comma at 24 px; tangent lines leave the circle
smoothly, which is the difference between a needle and a blob. It pivots at the half circle's own
centre, so the needle and the roof share a centre of curvature and the point aims at the arc
wherever it is swept to. Up and to the right is a tachometer under load, which is the only state
worth drawing.

**One shape with one hole.** The housing is one subpath, the needle another, and `fill-rule`
`evenodd` cuts the second out of the first, so the needle is the background showing through rather
than a second colour. That is what keeps the mark to one brush and lets it sit on the near-black
panel and on the README's white without being drawn twice.

Two constraints then fix the sizes rather than taste. It is seen at 16 px in a browser tab, where
one unit is half a pixel, and a mark that size dies by merging: the hub is 6 units across, the
point stands 3 units clear of the arc and the hub 4 clear of the floor. The needle's taper is the
one thing exempt from the four-unit floor the segments were held to, and deliberately — it is meant
to reach nothing, and a taper is read from the body behind it. And it is seen monochrome, so it
reads as a shape and not as a colour: take the fill away and the gauge is still a gauge.

It is drawn twice, because WPF cannot render an SVG and Markdown cannot render a `Canvas`. The two
copies are now **one string**: `MarkShape.PathData` is the SVG's own `d`, character for character,
`Ui.Mark` hands it to `Geometry.Parse` behind an `F0` for the same even-odd rule, and `MarkTests`
compares them. The rev bar was held together by four numbers per bar instead, and it still drifted
— the rectangles matched and the corner radius did not, because a corner is neither a position nor
a size. `MarkTests` also holds the rules above rather than only the numbers, so the shape cannot
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
