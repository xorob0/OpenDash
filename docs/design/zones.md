# The zone face

**Status:** current. This is the written record of the design that settled on the canvas on
2026-09-11, and it replaces the twelve-slot model and, before that, a cell-and-span model.
[ADR 0006](../decisions/0006-the-zone-face.md) records why. The canvas itself is the source:
[`design/canvas/ZoneCatalogue.dc.html`](../../design/canvas/ZoneCatalogue.dc.html) draws every
page at every shape, and the per-size artboards give every rectangle below.

A face is not a grid of cards. It is a rev bar, a bar of settled values, three zones across the
body and a band, and **each zone cycles through its own catalogue from a wheel button**. The
plugin decides which pages are enabled per zone, which one opens, and which one a held button
shows.

That is the whole idea, and it is worth saying why. A slot is arranged once, with a mouse, before
a session. A zone is changed with a thumb in the middle of a lap. So the thing the settings carry
is a page number a button can advance, not an assignment a panel writes, and a face shows more by
being deeper rather than by being more crowded.

---

## 1. The anatomy

The same five parts on every rectangular face. Only their sizes change.

| | |
|---|---|
| **Rev bar** | Shift lights in a recessed well, full width, never moves. |
| **The bar** | What the car is set to and where the session is: a field at each end that a driver may swap, and a settings strip between them that hides what the game does not expose. |
| **Zone B** | A page from the catalogue of twenty-one. |
| **Zone A** | The one read by reflex: gear, gear and speed, speed, or the track. |
| **Zone C** | A page from the same catalogue of twenty-one. |
| **Band D** | Fuel by default, and seven more pages that suit a wide short band. A flag takes the band over while one is out. |

Zone A is **a narrow column holding the gear**, not a third of the screen holding one digit.

There is **no row of page dots**. The zone letter and the page name say what is showing.

### The rectangles

Read from the artboards. Every number is a rect of `left, top, width x height` on the face's own
canvas.

**1920 × 480** — `Dash.dc.html`, the reference face

| Part | Rect |
|---|---|
| Rev bar well | 18, 4, 1884 × 40 |
| Bar | 0, 48, 1920 × 56 |
| Zone B | 0, 105, 769 × 314 |
| Zone A | 770, 105, 380 × 314 |
| Zone C | 1151, 105, 769 × 314 |
| Band D | 0, 420, 1920 × 60 |

**1280 × 480** — `Dash1280x480.dc.html`

| Part | Rect |
|---|---|
| Rev bar well | 10, 3, 1260 × 38 |
| Bar | 0, 44, 1280 × 54 |
| Zone B | 0, 99, 469 × 320 |
| Zone A | 470, 99, 340 × 320 |
| Zone C | 811, 99, 469 × 320 |
| Band D | 0, 420, 1280 × 60 |

**1280 × 400** — `Dash1280x400.dc.html`, the same zones in a shorter body

| Part | Rect |
|---|---|
| Rev bar well | 10, 2, 1260 × 32 |
| Bar | 0, 36, 1280 × 50 |
| Zone B | 0, 87, 469 × 258 |
| Zone A | 470, 87, 340 × 258 |
| Zone C | 811, 87, 469 × 258 |
| Band D | 0, 346, 1280 × 54 |

**1280 × 720** — `Dash1280x720.dc.html`. The tall body is what lets zone C list the field.

| Part | Rect |
|---|---|
| Rev bar well | 14, 4, 1252 × 40 |
| Bar | 0, 48, 1280 × 56 |
| Zone B | 0, 105, 469 × 554 |
| Zone A | 470, 105, 340 × 554 |
| Zone C | 811, 105, 469 × 554 |
| Band D | 0, 660, 1280 × 60 |

**850 × 480** — `Dash850x480.dc.html`. The first genuinely narrow zones, and five settings in the
bar rather than seven.

| Part | Rect |
|---|---|
| Rev bar well | 8, 2, 834 × 36 |
| Bar | 0, 40, 850 × 50 |
| Zone B | 0, 91, 274 × 328 |
| Zone A | 275, 91, 300 × 328 |
| Zone C | 576, 91, 274 × 328 |
| Band D | 0, 420, 850 × 60 |

**800 × 286** — `DashNano800x286.dc.html`. **No bar at all**: the height is not there, so the band
carries the one changeable zone and the body starts straight under the rev bar.

| Part | Rect |
|---|---|
| Rev bar well | 6, 1, 788 × 30 |
| Zone B | 0, 33, 269 × 194 |
| Zone A | 270, 33, 260 × 194 |
| Zone C | 531, 33, 269 × 194 |
| Band D | 0, 228, 800 × 58 |

**600 × 686** — `DashDisplayDash600x686.dc.html`. Portrait, so the body stacks **A over B over C**
rather than B beside A beside C, and each end of the bar carries one field rather than two.

| Part | Rect |
|---|---|
| Rev bar well | 6, 2, 588 × 32 |
| Bar | 0, 36, 600 × 46 |
| Zone A | 0, 83, 600 × 234 |
| Zone B | 0, 318, 600 × 160 |
| Zone C | 0, 479, 600 × 150 |
| Band D | 0, 630, 600 × 56 |

**800 × 480** has no artboard and never did. It is derived from 850 × 480, and the layout file
says so, exactly as `layouts/800x480.ts` says it today.

### The zone header

Every zone but A carries a **22 px header line**: the zone letter in the label style, then the
page name. Zone A has none, which is the one thing the model leaves open — see §8.

### The pit limiter

Drawn over zone A as a full-width white banner with dark text while the limiter is on: at 1920 it
is 824, 111, 272 × 30. It is not a page and it is not part of the catalogue; it covers.

---

## 2. The shape model

**Rule 17.** *A page answers to the shape of its zone. Not to its width alone. A long shallow zone
takes one rank, a tall narrow one stacks its values and sizes them to the height, and a page sheds
its secondary rows before it shrinks its numerals. Nothing is ever scaled down.*

Shape is **a pair of bands, width and height**, not one ratio. A single ratio threshold cannot
express the drawings: `tall narrow 274 × 300` is 0.91 and `tall 360 × 470` is 0.77, so any one
threshold collapses them into the same shape — and lap times shows two fields at one and six at
the other.

- **Width** picks the column set and the rank width.
- **Height** picks the row count, and whether the lead values are promoted.

The four shapes the catalogue draws, which are the test fixtures:

| Shape | Size | What it does |
|---|---|---|
| `wide` | 600 × 280 | One rank. The fullest form of a page. |
| `grid` | 430 × 300 | Two ranks; the least important column goes. |
| `tall narrow` | 274 × 300 | One column, stacked, centred; only the reading the page exists for. |
| `tall` | 360 × 470 | Stacked and expanded: the sectors come back, a list grows rows. |

A fifth band, `strip`, survives these because the pit wall already hands a module 607 × 158 and
1007 × 211, and it is not on the canvas. Say so in the code rather than pretending it is.

**Rule 18.** *A drawing is cut from its box. Never placed in it at a fixed size.* A tyre is as
tall as the readings beside it; a car is capped at a third of the zone width however tall the box
is; the traces take what is left. The catalogue gives the car at 142 wide in `wide`, 129 in
`grid`, 79 in `tall narrow` and 106 in `tall` — about a third of each frame.

**Rule 19.** *A value is laid in cells, and only what fits a cell may be drawn.* Barlow
Condensed's digits are proportional and SimHub cannot ask the font for its tabular ones, so every
value is drawn with SimHub's monospace cells, cut to hold the widest ink a value can draw. WPF
clips a glyph that overruns one, so `#`, `%`, `&`, `@`, `M`, `W`, `m` and `w` are never part of a
value. A car number is drawn bare and its hash belongs to the label; a class beside a number is a
proportional label and a monospaced value, not one string.

---

## 3. The bar

Settled values, which a driver reads between corners rather than at speed.

**Each end carries two fields**, dropping to one per end at 600 × 686. A field is chosen from a
catalogue of eleven:

race time · lap and total · time left · clock · simulated time · position · class position ·
incidents · strength of field · air temperature · track temperature

The default is Race and Lap on the left, Position and Class on the right.

Between them is the **car settings strip**: slip, TC, cut, bias, ABS, map, diff. It draws what the
game exposes and **hides what it does not**, because a strip drawing an empty box for a setting
iRacing has no property for is worse than a narrower strip. At 850 × 480 and 600 × 686 it drops
from seven cells to five, losing map and diff.

The bar is the one region of the face that is not a zone and does not cycle. It is settled by
definition, and that is what earns it the space.

---

## 4. Zone A — four pages

The one a driver reads by reflex, and the reason the zone is a narrow column: the gear wants
height, not width.

| | Page | What it draws |
|---|---|---|
| A1 | Gear, speed, revs | The gear with the gear below and above ghosted either side, the speed under it, the revs under that. **The default.** |
| A2 | Gear alone | The gear as large as the column allows, nothing else. |
| A3 | Speed | The speed as the largest value, the gear demoted to a small readout. |
| A4 | Track | The track map with every car on it. |

---

## 5. Zones B and C — twenty-one pages

The catalogue is `MODULE_CATALOGUE` in `packages/dash/src/contract.ts`. It is no longer
companion-only: the same twenty-one pages serve the companion, the pit wall zones and now the
face.

| № | Page | № | Page | № | Page |
|---|---|---|---|---|---|
| 1 | Lap times | 8 | Pit view | 15 | Relative |
| 2 | Delta | 9 | Car settings | 16 | Opponents |
| 3 | Sectors | 10 | Inputs | 17 | Gear |
| 4 | Speedo | 11 | Session | 18 | Stint |
| 5 | Fuel | 12 | Radar | 19 | Lap history |
| 6 | Energy | 13 | Track | 20 | Damage |
| 7 | Tyres | 14 | Leaderboard | 21 | Track rivals |

Energy, Damage and Track rivals are off by default because iRacing publishes none of their data.
[second-screens.md](../second-screens.md) says which, and why.

Each is drawn at all four shapes on the catalogue artboard — eighty-four drawings. **That is the
shedding order**, and it is data rather than mechanism: XOR-104 reads it, and until it is
transcribed here page by page, the artboard is the reference.

Two worked examples, so the shape of the table is clear:

- **Lap times.** `wide`: last lap, session best, your best, laps, estimated, delta to your best —
  six. `grid`: drops laps and estimated — four. `tall narrow`: last lap and session best — two.
  `tall`: all six again, stacked.
- **Relative.** `wide`: position, number, code, licence, rating, gap — six columns, six rows.
  `grid`: drops the rating. `tall narrow`: position, code and gap only, but eight rows. `tall`:
  every column and nine rows.

The pattern holds generally: a narrow zone loses columns before it loses rows, and a tall one
buys rows before it buys columns.

---

## 6. Band D — eight pages

A band the full width of the face showing one page at a time.

| | Page | | Page |
|---|---|---|---|
| D1 | Fuel — **the default** | D5 | Weather |
| D2 | Energy | D6 | Sectors |
| D3 | Stint | D7 | Relative |
| D4 | Tyres | D8 | Car (the telltale row) |

Fuel is the default because it is what a driver checks on a straight.

**Corner blocks.** A block at each end on the wider faces: incidents against their limit and the
track state on the left; DRS, push to pass, spotter lamps and both clocks on the right. They are
drawn at 1920 × 480, 1280 × 480, 1280 × 400 and 1280 × 720, and absent at 850 × 480, 800 × 286 and
600 × 686. The threshold is those drawings, not a round number.

**The field count follows the width** — seven at 1920, five at 850 — with nothing spread to fill.
The rank is packed and centred while the corners take the ends.

**A flag takes the band over.** While a flag is out, the flag has the band, because an alert
outranks fuel. This replaces the bottom-edge flag strip the slot model drew, so the same sixty
pixels goes to whichever has the better claim. The band draws as a filled bar with a 3 px border
in the flag's colour and the flag's name in dark text.

---

## 7. The settings the contract fixes

One property per decision, all under the `OpenDash` prefix.

| Property | What it carries |
|---|---|
| `ZoneA` … `ZoneD` | The page each zone is showing. A button advances it. |
| `ZoneAPages` … `ZoneDPages` | A mask of which pages are enabled, which is what sets the cycle's length. |
| `ZoneAStart` … `ZoneDStart` | The page the zone opens on. |
| `QuickGlance` | The zone and page held while a button is down, as one property rather than a pair per zone. |
| `BarLeft1`, `BarLeft2`, `BarRight1`, `BarRight2` | The bar's four end fields. |

The four modes the slot model already had — `ShiftLights`, `PositionMode`, `DeltaReference`,
`SessionProgress` — are unchanged.

Every expression that reads one of these wraps it in `isnull()` with the default, so a package
installed without the plugin shows each zone's start page and simply cannot cycle. That is still a
complete product by [ADR 0003](../decisions/0003-plugin-settings-through-properties.md)'s letter,
and it is the first feature for which the plugin buys something material.

---

## 8. What a zone does when its page changes

Zones B, C and D carry a permanent header, so the answer is already on the screen: the letter and
the page name change with the page.

**Zone A has no header and cycles four pages, and the model has no answer for it.** Three
candidates, none yet chosen:

1. Nothing. Gear, gear-and-speed, speed and the map are arguably self-evident.
2. A header like the others, which costs 22 px of the column the gear is sized to.
3. A name that appears for a second after the page changes and fades. This costs nothing at rest
   and is the same behaviour as the change notification, which suggests the two are one component
   — and it needs a show-for-N-seconds primitive the format may not have (XOR-116).

This section is filled in when the canvas answers it. XOR-103 owns the question.

---

## 9. What a round face is

**Not yet decided.** The two round artboards are the only two of twenty-five still drawing the
slot model, and `canvas.json` still titles them "2 slots" and "6 slots". The only zone-era word
touching them is rule 10: *a round face uses its ring instead of a band.*

`480round.ts` and `800round.ts` read `layout.slots`, which the zone work makes optional rather
than removing, so they keep building throughout. The decision is owed before the card path is
retired, not before the first zone face. XOR-94 owns it.

---

## 10. Where the canvas contradicts itself

Recorded rather than resolved. A reader who finds one of these has found a real disagreement, not
a mistake in this document.

| | |
|---|---|
| Band D's page count | The catalogue heading reads "band D · seven pages" and the drawings are D1 through D8. **Eight is taken**, because the drawings are more specific than the caption, and the mask is sized for eight either way. |
| The bar's fields | Described as "three fields a driver may swap", specced as one per end, and drawn as two per end on every face. **Two per end is taken**, because that is what is drawn. |
| Zone C's capacity | Stated as ten drivers at 1920; seven rows are drawn. |
| Page dots | `pageIndicator` is still in the component list, against "there is no row of page dots". |
| The fuel tank | Dropped from the drawn objects in the 0.7.0 changelog — "a quantity is a number" — and still listed among five in `canvas.json`'s detail-pass annotation. **Four objects are taken.** |
| The numeral family | Rule 4 says numerals are Barlow Condensed. The files ship as `openDash Display`, because WPF reads the width word out of a family name and folds the condensed faces into Barlow as a stretch, which a `.djson` cannot ask back. Same outlines, different name; see XOR-108. |
| The telltales | Twenty-eight Material Design Icons are named and the build "rasterises the chosen twelve", which are not listed. Owed before XOR-97 starts. |

---

## Related

[scope.md](../scope.md) is what openDash is and what it refuses to be.
[ADR 0006](../decisions/0006-the-zone-face.md) is why the model changed.
[brand.md](brand.md) is the reasoning behind the colours and the type.
[second-screens.md](../second-screens.md) is the companion and the pit wall, which share the
twenty-one pages.
[research/simhub-dash-format.md](../research/simhub-dash-format.md) is what SimHub actually does,
and is the place to check before guessing.
