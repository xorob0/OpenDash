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

This is why openDash's accent is `#33D9F2` ice cyan and never appears on the dash face. An
amber accent would collide with shift stage 2, and green or red would collide with flags and
alarms. Cyan sits outside the functional palette entirely, so it can be the identity colour in
the logo, the plugin panel and the documentation without ever being misread at speed. It is a
defensible position rather than a preference: legibility first, identity second.

## Palette

### Dash face

| Role | Token | Value |
|---|---|---|
| Background | `color.bg.base` | `#0A0B0D` |
| Zone fill | `color.bg.surface` | `#14161A` |
| Separator | `color.bg.raised` | `#1C1F24` |
| Primary numerals | `color.text.primary` | `#F5F7FA` |
| Supporting values | `color.text.secondary` | `#8A9099` |
| Field labels | `color.text.label` | `#5A6069` |
| Inactive segments | `color.text.dim` | `#33383F` |

Background is near-black rather than pure black, because pure black blooms on the IPS and OLED
panels used in DDUs and reduces perceived contrast rather than increasing it.

### Functional colour, state only

| Meaning | Token | Value |
|---|---|---|
| Shift 1, delta faster, within limits | `state.good` | `#00D96A` |
| Shift 2, caution | `state.caution` | `#FFB300` |
| Shift 3, delta slower, alarm | `state.danger` | `#FF2D46` |
| Blue flag | `state.info` | `#2E7BFF` |
| Session best | `state.best` | `#B14BFF` |
| White flag, pit limiter | `state.neutral` | `#FFFFFF` |

Purple for session best follows established sim-racing convention, and breaking it in order to
be distinctive would cost drivers real recognition time.

### Brand, identity surfaces only

| | Token | Value |
|---|---|---|
| openDash cyan | `brand.cyan` | `#33D9F2` |

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
and size alone. Moreover, SimHub exposes no OpenType features, so whether lap times tick without
jitter depends on Barlow Condensed having tabular digits by default; the spike checks it, and
if they turn out to be proportional, values are right-aligned and a tabular alternative is
considered.

## Form

- Hard edges. Radius 0 by default, since race dashes are not rounded.
- No gradients, no gloss, no skeuomorphism.
- Zones separated by 1 px rules, not by boxes or cards.
- Hierarchy through size, aggressively: gear is 260 px and labels are 15 px, and a 17:1 ratio
  is correct here because timid hierarchy is unreadable at a glance.
- Negative space is functional. It is what makes a value findable by position, which is how
  drivers actually read a dash, by muscle memory rather than by scanning.

## Logo

Wordmark: `open` in a light weight, `Dash` in bold. Lowercase, condensed, technical. An
alternate monogram is a single rev-bar segment sweep. Not yet designed.

## Open questions

Shift-light thresholds are settled: they are SimHub's per-car values
(`CarSettings_RPMShiftLight1`, `CarSettings_RPMShiftLight2` and the current gear redline),
which users already tune in SimHub, and the on-dash shift lights can be switched off from the
plugin for DDUs with physical LEDs.

Whether tyre temperature uses a continuous colour ramp or discrete bands remains open. Bands
are more glanceable, ramps carry more information, and the current leaning is bands. The
question matters less for the MVP than it first seemed, since iRacing only refreshes tyre data
in the pit stall.
