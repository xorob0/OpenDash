# Brand and visual direction

**Last updated:** 2026-09-10
**Status:** direction agreed; layouts in progress.

Token values live in [`design/tokens.json`](../../design/tokens.json). That file is the source
of truth — this document explains the *reasoning*, so that future changes are made for reasons
rather than taste.

## Reference

Porsche 911 GT3 R / 963 race dash. Flat, functional, high-contrast, blocky numerals, no
ornament. Not the 992 road-car cluster (too decorative), not the Taycan (too soft).

**Not Lovely.** Their licence covers their UI design — see
[../research/competitors.md](../research/competitors.md#-licence-restriction--read-this).

## The governing principle

> **The brand is achromatic. Colour is reserved for meaning.**

Real race dashes are white numerals on black, where the *only* chroma is functional — shift
lights, warnings, flags. A driver at 200 km/h reads colour as state, instantly, without
parsing. Every decorative use of colour erodes that reflex.

This is why openDash's accent is `#33D9F2` ice cyan and **never appears on the dash face**. An
amber accent would collide with shift stage 2. Green or red would collide with flags and
alarms. Cyan sits outside the functional palette entirely, so it can be the identity colour in
the logo, plugin panel, and docs without ever being misread at speed.

It is a defensible position rather than a preference: legibility first, identity second.

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

Background is near-black rather than pure black — pure black blooms on the IPS and OLED panels
used in DDUs, and reduces perceived contrast rather than increasing it.

### Functional colour — state only

| Meaning | Token | Value |
|---|---|---|
| Shift 1 / delta faster / within limits | `state.good` | `#00D96A` |
| Shift 2 / caution | `state.caution` | `#FFB300` |
| Shift 3 / delta slower / alarm | `state.danger` | `#FF2D46` |
| Blue flag | `state.info` | `#2E7BFF` |
| Session best | `state.best` | `#B14BFF` |
| White flag / pit limiter | `state.neutral` | `#FFFFFF` |

Purple for session best follows established sim-racing convention. Breaking it to be
distinctive would cost drivers real recognition time.

### Brand — identity surfaces only

| | Token | Value |
|---|---|---|
| openDash cyan | `brand.cyan` | `#33D9F2` |

Logo, plugin panel, documentation, website. **Never on the dash face.**

## Typography

| Role | Family | Licence |
|---|---|---|
| Numerals and data | Barlow Condensed | SIL OFL 1.1 |
| Labels and plugin UI | Barlow | SIL OFL 1.1 |

One family, two widths. DIN-adjacent, which is the right register for a race dash, and
condensed buys horizontal room for large numerals on a 4:1 screen.

**The licence is not incidental.** SimHub bundles fonts into `_SHFonts/` inside the dash
package, so shipping a dash means *redistributing the font file*. Anything not OFL/MIT is
disqualified regardless of how good it looks. This ruled out several obvious DIN choices.

Labels are uppercase, `0.14em` tracking, small, and dim — they should be findable but never
compete with values. Numerals are `-0.01em` tracking, weight 600–700.

## Form

- **Hard edges.** Radius 0 by default. Race dashes are not rounded.
- **No gradients, no gloss, no skeuomorphism.**
- **Zones separated by 1px rules**, not by boxes or cards.
- **Hierarchy through size**, aggressively — gear is 260px, labels are 15px. A 17:1 ratio is
  correct here. Timid hierarchy is unreadable at a glance.
- **Negative space is functional.** It is what makes a value findable by position, which is
  how drivers actually read a dash — by muscle memory, not by scanning.

## Logo

Wordmark: `open` light weight, `Dash` bold. Lowercase, condensed, technical.
Alternate monogram: a single rev-bar segment sweep.

Not yet designed.

## Open questions

- Exact shift-light stage thresholds — currently 33% / 66% / 100% of the usable RPM band, per
  `shiftLights.stages`. Needs validation against real iRacing cars, which vary widely.
- Whether tyre temperature uses a continuous colour ramp or discrete bands. Bands are more
  glanceable; ramps carry more information. Leaning bands.
