# Brand and visual direction

**Last updated:** 2026-09-10
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
name `OpenDash Display`. WPF reads the width word out of a family name and files "Barlow
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

The mark is **the rev bar**, which is the one shape OpenDash owns: four rising segments and a
separated fifth, which is the shift point a driver acts on. It is `media/logo.svg`, drawn in a 32
unit square in `purpose.ui.accent` and nothing else.

Two constraints decided its shape rather than taste. It is seen at 16 px in a browser tab, so
nothing in it is thinner than four units of thirty-two, which is two pixels there. And it is seen
monochrome on a dark panel, so it reads as a shape and not as a colour: take the fill away and the
sweep is still a sweep.

It is drawn twice, because WPF cannot render an SVG and Markdown cannot render a `Canvas`. The SVG
is the source; `MarkShape.Bars` in the plugin mirrors it and `MarkTests` reads the file and checks
the numbers against each other, so the copies cannot drift. Change the SVG first.

Where it goes: the settings panel's header beside the wordmark, and the top of `README.md`. Not on
the face, which is a driver's instrument and not a billboard.

## Open questions

Shift-light thresholds are settled: they are SimHub's per-car values
(`CarSettings_RPMShiftLight1`, `CarSettings_RPMShiftLight2` and the current gear redline),
which users already tune in SimHub, and the on-dash shift lights can be switched off from the
plugin for DDUs with physical LEDs.

Whether tyre temperature uses a continuous colour ramp or discrete bands remains open. Bands
are more glanceable, ramps carry more information, and the current leaning is bands. The
question matters less for the MVP than it first seemed, since iRacing only refreshes tyre data
in the pit stall.
