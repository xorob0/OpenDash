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
| **Rev bar** | Shift lights in a recessed well, full width, never moves. It can be turned off entirely, in which case the face is drawn in its second arrangement and the well's room goes to the zones. |
| **The bar** | What the car is set to and where the session is: two fields at each end that a driver may swap, one per end at 600 × 686, and a settings strip between them that hides what the game does not expose. |
| **Zone B** | A page from the catalogue of twenty-one. |
| **Zone A** | The one read by reflex: gear, gear and speed, speed, or the track. |
| **Zone C** | A page from the same catalogue of twenty-one. |
| **Band D** | Fuel by default, and seven more pages that suit a wide short band. A flag takes the band over while one is out. |

Zone A is **a narrow column holding the gear**, not a third of the screen holding one digit.

There is **no row of page dots**. The zone letter and the page name say what is showing.

### The rectangles

Read from the artboards. Every number is a rect of `left, top, width x height` on the face's own
canvas.

One part is separated from the next by a single pixel rather than by a gap. Every artboard leaves
an empty row above each row of the body and above band D, and, where the body is a row of zones, an
empty column between them, and it draws a 1 px rule in `surface.raised` in each: at 1280 × 480 the
two rows are y 98 and y 419 and the two columns are x 469 and x 810, while the portrait face, which
stacks its zones, has four rows, at y 82, 317, 478 and 629, and no column at all. The build reads
those boundaries off the rects rather than tabulating them, so that the arrangement below, which no
artboard gives, is ruled the same way.

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

### The face with the rev bar off

**Derived, not drawn.** `OpenDash.RevBar` has a third state, `off`, for a driver whose wheel or DDU
already carries LEDs across its top (XOR-138). Hiding the segments alone leaves the well lit by
nothing, so the face has a second arrangement, and its rectangles are the only ones in this document
that no artboard gives. §10 records what the canvas owes.

The rule, in `zonesWithoutRevBar`:

- What is given back is **the well and the gap under it**. The one to four pixels above the well are
  not: that is the top margin of the face, and giving it back would put the bar's labels' line box a
  pixel above the canvas, where WPF clips the row.
- The bar rises to where the well began. The zones that **start the body** rise with it and grow by
  the same amount, so they keep their bottom edge. In portrait only zone A starts the body, so B and
  C keep both their rectangles and their zone dashboards.
- Band D does not move: it is measured from the bottom edge and the bottom edge has not changed. The
  pit limiter moves with zone A, because that is what it is drawn over.
- The rules rise with the parts they separate, since they are read off the rects. The nano is the
  one face where the body then starts on row 1, leaving no row above it for a rule to sit in, so
  that arrangement draws the rule above band D and none above its body.

| Face | Given back | Of the height | Bar | Body |
|---|---|---|---|---|
| 1920 × 480 | 44 | 9% | 0, 4 | 61, 358 |
| 1280 × 480 | 41 | 9% | 0, 3 | 58, 361 |
| 1280 × 400 | 34 | 9% | 0, 2 | 53, 292 |
| 1280 × 720 | 44 | 6% | 0, 4 | 61, 598 |
| 850 × 480 | 38 | 8% | 0, 2 | 53, 366 |
| 800 × 480 | 38 | 8% | 0, 2 | 53, 366 |
| 600 × 686 | 34 | 5% | 0, 2 | zone A 49, 268 |
| 800 × 286 | 32 | 11% | none | 1, 226 |

It is a second **screen** and not a second package: the two arrangements sit in one `.djson` with
complementary `ScreenEnabledExpression`s, so the setting changes the face in front of the driver
rather than asking them to reinstall. SimHub re-evaluates those expressions every frame and moves
off a screen that has stopped being enabled — `EditorModel.CheckGameModeScreen` in 9.12.6. The cost
is the zone dashboards the grown rectangles need: two more per landscape face, one per portrait one,
about 17% on a package.

### The zone header

Every zone but A carries a **22 px header line**: the zone letter in the label style, then the
page name. Zone A has none, which is the one thing the model leaves open — see §8.

The frame around it is the artboards' and not the pit wall's. A face zone is padded `6px 12px`, its
header row is 22 px of 15 px labels in `color.text.label`, and 4 px separate that row from the page,
which leaves a 769 × 314 zone a body of 745 × 276 and the nano's 269 × 194 one of 245 × 156. The pit
wall's zones keep the 28 px row over 16 px of padding `PitWallZones.dc.html` draws them with, so
`zoneFrameMetrics` takes a chrome beside its density and the two frames no longer share one table.

Band D carries the letter alone, drawn by the face at the band's own side padding and centred on its
height, since a band has no header row to put it in and counts no cycle.

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

**Where "nothing is ever scaled down" gives.** The rule is about a rank with something left to shed
and it holds there. It cannot hold for a rank with nothing left, because a value that neither sheds
nor shrinks is a value WPF clips. Five places therefore shrink as a floor, each argued where it
stands: `second/field.ts`, when one field is left and shedding it would leave the page empty;
`second/wheel.ts`, where a tyre corner is two numbers and a bar and none of them is secondary;
`second/sectors.ts`, where a sector time steps down a ladder until it fits its third of the box;
`zones/bandPages.ts`, where a band rank is one row; and `second/placeholder.ts`, for the line of
prose saying a reading is missing, which clipped would be the worst of both. A reader who finds one
of them has found the floor of the rule rather than a breach of it, and the canvas owes the same
qualification.

**Rule 20.** *A rank fills the box it is given. It grows until it meets an edge, and there are
three: the height of the box, the width of the box, and the next size up its density ramp.*

Rule 17 is one half of a thought and this is the other. Rule 17 says what a page does when its box
is too small; rule 20 says what it does when the box is too large, which on this product is the
commoner case — zone B of the 850 × 480 face is 274 × 328 and lap times was using 58 px of it.

The ramp is the part that makes this filling rather than scaling. A rank grows by one factor, the
whole stack at once, and no value may pass the next named size on `density.ts`'s ramp — so what
comes out is the same drawing one size larger, with its hierarchy intact, and never a drawing
stretched to a rectangle. Three consequences worth knowing:

- **All of the stack grows or none of it does.** A page whose sectors are a drawing and whose lap
  times are fields would otherwise grow the times alone until they matched the sectors above them.
- **Growing is what stacks a narrow zone.** Two lap times fit side by side in a 254 px zone at
  34 px and do not at 46 px, so the rank wraps to one column on its way up. `columnsAt` is the
  declaration of what a shape may hold rather than a cap on the wrap: it names the columns of a page
  that asks for a grid, and it is what says a zone is down to one column and should centre what is
  in it.
- **A stack already too tall for its box does not grow.** It has nothing to spend, and rule 17 is
  about to take a row off it.
- **A rank may not grow into a worse shape than it started in.** Almost every rank on the catalogue
  artboard is an even grid — `repeat(3, minmax(0, 1fr))` over three or nine cells — so a grown wrap
  is refused when it is more ragged than the wrap it grew from. Turning two lines of two and one
  into three lines of one is the narrow zone stacking itself and is allowed; turning one line of
  three into two and one is the companion's lap times and is not. That is the difference between
  72 px and 75 px there, and 72 keeps the row.

The room a grown stack may take is its box less its own tail at each end, not the flat two pixels
`ROW_TAIL` reserved: a WPF line box runs about a tenth of the font size below the row it sits on,
which is two pixels at 24 px and seven at 75 px. A constant was enough while every value was a ramp
size in a box with slack. A value grown into its box is exactly where it stops being enough.

**How a rank is set out** is three questions rather than one. Its lines are the greedy wrap by
default, one field to a line where the page asks for that, or an equal-column grid of `columnsAt`
cells where the catalogue draws one. Within a line the fields share the baseline of the largest,
because a row mixes sizes, unless the page asks them to share their top edge instead. The line then
sits where the shape puts it: a zone narrow enough for one column centres what is in it, and a wider
one draws from its left edge. A page may also spread its ranks over the whole height rather than
centring them as one block, which is what the catalogue's own wrappers do for eight of the
twenty-one pages; section 10 records that this choice belongs to the page and not to the shape.

**What a box too short takes off is the page's declaration, not its last row.** The stack sheds the
least important id the shedding table names, one at a time, and builds the rank again without it;
only once nothing declared is left to shed does it drop a trailing row, which is how a gauge, a
strip or a table goes, since none of those declares an id. Before this, a 249 × 158 zone took fuel's
level gauge off with the average it sat under, although the table keeps the average and every
drawing of the page has a gauge.

The four shapes the catalogue draws, which are the test fixtures:

| Shape | Size | What it does |
|---|---|---|
| `wide` | 600 × 280 | One rank. The fullest form of a page. |
| `grid` | 430 × 300 | Two ranks; the least important column goes. |
| `tall narrow` | 274 × 300 | One column, stacked, centred; only the reading the page exists for. |
| `tall` | 360 × 470 | Stacked and expanded: the sectors come back, a list grows rows. |

A fifth band, `strip`, survives these because the pit wall already hands a module 607 × 158 and
1007 × 211, and it is not on the canvas. Say so in the code rather than pretending it is.

A `short` box is given no fifth drawing of its own. It takes the drawing of the next shape down —
a wide one takes `grid`, a medium or narrow one takes `tall narrow` — because what a short box has
is room for less, and §5 is where "less" is written page by page. That is the whole of the rule,
and it lives in `archetypeOf`; a predicate beside the bands saying that a short box keeps one rank
stated it a second time in other words, so it is gone.

**Rule 18.** *A drawing is cut from its box. Never placed in it at a fixed size.* A tyre is as
tall as the readings beside it; a car is capped at a third of the zone width however tall the box
is; the traces take what is left. The catalogue gives the car at 142 wide in `wide`, 129 in
`grid`, 79 in `tall narrow` and 106 in `tall` — about a third of each frame.

**Rule 19.** *A value is laid in cells, and only what fits a cell may be drawn.* Barlow
Condensed's digits are proportional and SimHub cannot ask the font for its tabular ones, so every
value is drawn with SimHub's monospace cells, cut to hold the widest ink a value can draw. WPF
clips a glyph that overruns one, so `#`, `%`, `&`, `@`, `M`, `W`, `m` and `w` are never part of a
value. A car number is drawn bare and its hash belongs to the label. A class beside a number is the one
value that is text rather than a number, and the bar draws it as the artboard does, as a single
proportional run of "GT3 · P4" measured from the widest class and place it promises to hold; a
cell would hold the dot and the letters, but only by spacing them as digits.

---

## 3. The bar

Settled values, which a driver reads between corners rather than at speed.

**Each end carries two fields**, dropping to one per end at 600 × 686. A field is chosen from a
catalogue of ten:

race time · lap and total · time left · clock · simulated time · position · class position ·
incidents · air temperature · track temperature

Strength of field was the eleventh and is not built, because SimHub publishes it in no form at all
and OpenDash does not compute ([ADR 0009](../decisions/0009-does-the-plugin-compute.md)). It is
named here only so that a reader of an older draft knows where it went.

The default is Race and Lap on the left, Position and Class on the right. The left end is drawn
from the left edge and the right end from the right one, each field flush to the padding on its
own side, and the class reads "GT3 · P4".

The bar is drawn at a scale of its own rather than at the zone density ramp's. Every artboard
gives it a 15 px label in a 13 px row, five pixels under it, six between a value and the dimmer
"/ 32" after it, and twenty of side padding; what changes with the face is the value, the
denominator, the strip column and the gap between two readouts:

| face | value | denominator | column | gap |
|---|---|---|---|---|
| 1920 × 480, 1280 × 480, 1280 × 720 | 34 | 24 | 57 | 22 |
| 1280 × 400, 850 × 480, 800 × 480 | 28 | 20 | 54 | 22 |
| 600 × 686 | 28 | 20 | 54 | 12 |

Between them is the **car settings strip**: slip, TC, cut, bias, ABS, map, diff. It draws what the
game exposes and **hides what it does not**, because a strip drawing an empty box for a setting
iRacing has no property for is worse than a narrower strip.

It also gives up cells where the width is not there. The two ends are laid out from their own edges
for the widest entry the catalogue holds, so what the strip gets is whatever is left, and on a
narrow face that is not seven cells:

| face | cells |
|---|---|
| 1920 × 480, 1280 × 480, 1280 × 400, 1280 × 720 | all seven |
| 600 × 686 | slip, TC, cut, bias, ABS — one field per end leaves more room than two |
| 850 × 480 | slip, TC, bias, ABS |
| 800 × 480 | TC, bias, ABS |
| 800 × 286 | there is no bar |

A cell is the artboard's column rather than a measurement of its own reading, so the seven read as
a rank of equal cells; it is rounded up to an even width, because the strip closes over what is
missing and centres on half of what is left. A reading wider than the column widens that cell
rather than losing a digit, and "Bias 50.5" at 34 px is the only one that does: it takes 58 px
where the other six sit in their 57.

**The order it sheds in is not the order it draws in.** A driver on a GT3 car moves the brake bias
every corner and has TC and ABS on wheel dials; the mixture changes once a stint; slip, cut and the
differential are settings some cars do not have at all. So what a narrow strip keeps, in order, is
bias, TC, ABS, slip, cut, map, diff — and what it draws is still the canvas's order.

This is a decision the canvas does not make. It was forced by the first photograph of the 850 × 480
face, where the five cells that had no shedding rule were drawn straight over the right-hand
fields: BIAS on POSITION, ABS on the slash of "3 / 24".

The bar is the one region of the face that is not a zone and does not cycle. It is settled by
definition, and that is what earns it the space.

---

## 4. Zone A — four pages

The one a driver reads by reflex, and the reason the zone is a narrow column: the gear wants
height, not width.

| | Page | What it draws |
|---|---|---|
| A1 | Gear, speed, revs | The gear with the gear below and above ghosted either side, the speed under it with its unit beside it, the revs under that. The three take 55, 19 and 11 per cent of the column's height. **The default.** |
| A2 | Gear alone | The gear as large as the column allows, nothing else. |
| A3 | Speed | The speed as the largest value at 44 per cent of the column, the gear under it at 24 in the secondary ink, each with its label beside it. |
| A4 | Track | The track map with every car on it, titleless and inset by the column's padding. |

Every page is cut from the column under rule 18 rather than drawn at a size of its own: no run here
is on the density ramp and none is capped at `ds.size.gear`, which stays the card and round faces'
size. What bounds a run is the box, its line box down and its cells across, and what a column is
too narrow for is shrunk rather than drawn outside it. Whatever is left over is split above the page
and below it, so a page is centred in its column as well as cut from it.

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
shedding order**, and it is data rather than mechanism. The table below is those drawings read off
page by page; `packages/dash/src/modules/shedding.ts` is the same table in code, and
`shedding.test.ts` fails when the two disagree.

Two worked examples first, because they are the two that show why it cannot be derived:

- **Lap times.** `wide`: last lap, session best, your best, laps, estimated, delta to your best —
  six. `grid`: drops laps and estimated — four. So what goes is neither the tail of the row nor
  the narrowest field; the delta outlives both of the values drawn before it. `tall narrow`: the
  same four as `grid`, one per line. `tall`: all six again, stacked.

  The `tall narrow` row used to be two, which is what the catalogue draws at 274 × 300, and it was
  wrong about the box it really answers: a zone that stacks one column has room for four of them,
  and what the build actually drew there was two 34 px times side by side with 234 px of the zone
  empty under them. See §10 — **the catalogue owes a redraw of this one**, and
  [readability-pass.md](readability-pass.md) §1 is the ticket.
- **Relative.** `wide`: position, number, code, class, gap. `grid`: the same five. `tall narrow`:
  position, code and gap only, and eight rows rather than six. The number and the class chip go
  from between two columns that stay, which no rule about prefixes produces.

The pattern holds generally: a narrow zone loses columns before it loses rows, and a tall one
buys rows before it buys columns.

### The table

Field and column ids, as the module names them. The `wide` row is the fullest form and keeps
everything the page carries, the companion artboard's fields included, which is why a companion
page is not changed by any of this. The declaration is read before the box is measured, and what
does not fit still sheds afterwards: a declared set is a design decision and a box is a fact.

| № | Page | `wide` | `grid` | `tall narrow` | `tall` |
|---|---|---|---|---|---|
| 1 | Lap times | `last` · `sessionBest` · `yourBest` · `laps` · `estimated` · `delta` · `average5` · `position` · `stintLap` · `s1` · `s2` · `s3` | `last` · `sessionBest` · `yourBest` · `delta` | `last` · `sessionBest` · `yourBest` · `delta` | `last` · `sessionBest` · `yourBest` · `laps` · `estimated` · `delta` |
| 2 | Delta | `delta` · `s1` · `s2` · `s3` | `delta` · `s1` · `s2` · `s3` | `delta` · `s1` · `s2` · `s3` | `delta` · `s1` · `s2` · `s3` |
| 3 | Sectors | `s1` · `s2` · `s3` · `yourBest` · `last` · `sessionBest` · `bestS1` · `bestS2` · `bestS3` | `s1` · `s2` · `s3` · `yourBest` · `last` · `sessionBest` | `s1` · `s2` · `s3` · `yourBest` · `last` | `s1` · `s2` · `s3` · `last` · `sessionBest` |
| 4 | Speedo | `speed` · `rpm` · `redline` | `speed` · `rpm` | `speed` · `rpm` | `speed` · `rpm` |
| 5 | Fuel | `level` · `time` · `toAdd` · `lastLap` · `thisLap` · `average` · `lapsLeft` | `level` · `time` · `toAdd` · `average` | `level` · `time` · `toAdd` · `average` | `level` · `time` · `toAdd` · `lastLap` · `thisLap` · `average` · `lapsLeft` |
| 8 | Pit view | `refuel` · `pitTime` | `refuel` · `pitTime` | `refuel` · `pitTime` | `refuel` · `pitTime` |
| 9 | Car settings | `car` · `tc` · `abs` · `bb` · `mix` · `arbFront` · `arbRear` | `car` · `tc` · `abs` · `bb` · `mix` · `arbFront` · `arbRear` | `car` · `tc` · `abs` · `bb` | `car` · `tc` · `abs` · `bb` · `mix` · `arbFront` · `arbRear` |
| 11 | Session | `type` · `position` · `class` · `lap` · `timeLeft` · `lapsLeft` · `incidents` · `cars` | `position` · `class` · `lap` · `timeLeft` | `position` · `class` · `lap` · `timeLeft` | `type` · `position` · `class` · `lap` · `timeLeft` · `lapsLeft` · `incidents` · `cars` |
| 14 | Leaderboard | `pos` · `num` · `name` · `class` · `gap` · `best` · `last` | `pos` · `num` · `name` · `class` · `gap` | `pos` · `name` · `gap` | `pos` · `num` · `name` · `class` · `gap` |
| 15 | Relative | `pos` · `num` · `name` · `class` · `gap` | `pos` · `num` · `name` · `class` · `gap` | `pos` · `name` · `gap` | `pos` · `num` · `name` · `class` · `gap` |
| 16 | Opponents | `ahead.gap` · `ahead.name` · `ahead.num` · `ahead.class` · `ahead.detail` · `behind.gap` · `behind.name` · `behind.num` · `behind.class` · `behind.detail` | `ahead.gap` · `ahead.name` · `ahead.num` · `ahead.class` · `ahead.detail` · `behind.gap` · `behind.name` · `behind.num` · `behind.class` · `behind.detail` | `ahead.gap` · `ahead.name` · `behind.gap` · `behind.name` | `ahead.gap` · `ahead.name` · `ahead.class` · `ahead.detail` · `behind.gap` · `behind.name` · `behind.class` · `behind.detail` |
| 18 | Stint | `stintLaps` · `stintTime` · `completed` · `stops` · `lastStop` · `avgLap` · `driver` | `stintLaps` · `stops` · `lastStop` | `stintLaps` · `stops` · `lastStop` | `stintLaps` · `stintTime` · `completed` · `stops` · `lastStop` · `avgLap` · `driver` |

Pages with nothing to shed, and why:

- **Energy** (`energy`) — one line of prose: iRacing publishes no virtual energy.
- **Tyres** (`tyres`) — four corners cut from the box; rule 18.
- **Inputs** (`inputs`) — three traces and their bars, cut from the box; rule 18.
- **Radar** (`radar`) — the cars beside you, cut from the box; rule 18.
- **Track** (`track`) — the map, cut from the box; rule 18.
- **Gear** (`gear`) — the gear, cut from the box; rule 18.
- **Lap history** (`lapHistory`) — lap and time at every shape with a declared row count, plus a delta the wide page adds; there is no field the table drops.
- **Damage** (`damage`) — one line of prose: iRacing publishes no damage.
- **Track rivals** (`trackRivals`) — one line of prose: SimHub times sectors, not segments.

A drawing is cut from its box rather than shed (rule 18), and a page that says it has no data is
one line of prose with nothing in it to drop.

### Where the build keeps more than the drawing

The table is the catalogue read off page by page, and in five places it is deliberately not what the
catalogue draws. Each of them is a decision rather than a drift, so each is recorded here: a reader
holding a drawing against a zone should find the argument rather than suspect a bug.

- **Lap times at `tall narrow`.** Four values where the drawing has two, which §10 argues from the
  234 px of a real zone the drawing leaves empty. The catalogue owes the redraw.
- **Fuel at `wide` and at `tall`.** The last lap, this lap and the five-lap average, where the zone
  drawing carries one per-lap cell. The three come from the companion artboard, which is what the
  `wide` row is for; the narrower shapes keep the average alone, since one number three ways is
  still one number.
- **Leaderboard at `wide`.** The best and the last lap, two columns the zone drawing does not carry
  and the companion's list does. The trade runs the other way as well: the drawing gives the row a
  rating column, and neither list declares one.
- **Stint at `wide` and at `tall`.** The driver, where the drawing closes the page with the pit
  window. The window is not a field the module builds, and a handover is what the recap is read for.
- **Car settings at every shape.** The module draws the seven settings iRacing exposes and the
  drawing draws ten, so what is kept is the proportion rather than the count: `tall narrow` drops
  the three drawn last, which leaves four here against the drawing's seven.

Everywhere else the drawing names a field the module does not build, which is the opposite case and
is not a deviation: delta's three sector deltas, the rating on a list row, the pit window, the
steering readout on the inputs page. They are simply not in the table, because the table is about
the module.

### The parts that are not fields

A page is not only a rank. Delta is a number with a bar under it and a scale under that, lap history
is rows under a header, pit view's four corner toggles are what the drawing writes as the one line
`Tyres · RIGHTS`, and tyres sets its four corners under a compound chip and over the two captions
that say how the tread fills the drawing and what a tick on one means. The catalogue draws each of
these at some shapes and not at others, so they are declared the
same way a field is, in `PARTS` beside the table above. An empty cell is a part the drawing does not
carry at that shape.

| № | Page | `wide` | `grid` | `tall narrow` | `tall` |
|---|---|---|---|---|---|
| 2 | Delta | `bar` · `scale` · `rule` | `bar` · `scale` · `rule` |  | `bar` · `scale` · `rule` |
| 7 | Tyres | `footer` · `compound` | `footer` · `compound` |  | `footer` · `compound` |
| 8 | Pit view | `tyres` | `tyres` |  |  |
| 19 | Lap history | `head` | `head` |  |  |

Two tables rather than one because they answer two questions: the first is what a rank sheds, this
is what furniture the page keeps around it, and a page may appear in both. Until they were
declared, a narrow box lost them to `rowsThatFit` instead, which is arithmetic arriving at a design
decision one pixel at a time, and in the tyres caption's case arriving at the wrong one: the module
sized its two rows to the frame exactly, so the caption was dropped at every size the build
produces rather than at the one shape the catalogue drops it.

The catalogue also draws a steering readout on the inputs page at `wide` and at `grid`. It is not
in the table because the module does not build one, and a part a page cannot draw would be a line
nobody will notice is dead.

### A page that takes another page's drawing

The medium height band runs from 200 to 400 px and holds both the catalogue's `grid 430 × 300` and
the 1280 × 400 face's zone body, which is 437 × 214. Two pages cannot be drawn the same way in
both, and `FaceVariants1280x400` marks them: car settings draws seven cells where 214 px has room
for four, and lap history draws a header row where 214 px would rather have one more lap. **Both
take the `tall narrow` drawing in a `grid` box shorter than 260 px**, which is the floor between
that face's two arrangements, 214 and 248 px, and the 1280 × 480 face's 276 px, drawn from the
`grid` sheet.

A floor for two pages rather than a fifth shape for all of them, because a fifth column on the
table would repeat the fourth on nineteen rows.

What a rank does with a field that is **not there at all** is a different question from this one,
and the answer is in [§11](#11-a-field-that-is-not-there).

### The class filter

**A zone may list the player's own class rather than the whole field.** It is the one option the
leaderboard and the relative need that the companion never gave them, and it is a setting of the
zone rather than one switch for the whole face, because the point of it is zone B listing the race
while zone C lists the class a driver is actually racing in. It sits in the face's own group with
every other zone setting, so a rig with a face on the wheel and one beside it filters them apart.

It is **not** `PositionMode`. That setting is which number a position column shows; this one is who
is in the list at all, and one class counted by overall position is a legitimate thing to ask for.
What `PositionMode: class` currently does to a list it did not reorder is XOR-161.

Two pages read it: the leaderboard and the relative. Zone A lists nobody. Band D's own relative
page is three gaps rather than a list, so filtering it means asking for the car *ahead in class*
rather than listing fewer of them — the same idea, a different change, and XOR-159. The panel
offers the checkbox only where a page would change.

### The counter

**A zone's header counts its cycle, not its catalogue.** A zone with three pages enabled reads
"2 / 3" and not "15 / 21": the mask is what decides how long the cycle is, so it is what the
counter counts.

The counter is drawn by the face rather than by the zone, for the same reason the letter is —
zones B and C share one dashboard file where they are the same rectangle, and a screen in it cannot
know whose mask is deciding its length. The arithmetic is in the expression, which is what
[ADR 0009](../decisions/0009-does-the-plugin-compute.md) settled: a popcount is
`truncate(mask / 2^i) % 2` summed over the catalogue, and the mask is a property that already
exists. Without the plugin, the mask reads as its default and the counter says "n / 21".

---

## 6. Band D — eight pages

A band the full width of the face showing one page at a time, recessed into the same `block.well`
the rev bar and the bar sit in. The well is the band's own ground rather than a rectangle behind its
widget, because a widget paints its dashboard's background over whatever the face drew underneath;
the face draws one there as well, so that a face whose zone D widget has not resolved is still right.

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

**A page sheds its last field before the rank overflows**, with nothing spread to fill. The rank is
packed and centred in what the side padding, the zone letter and the corners leave, never in the
whole band. The artboards draw the shedding rather than only describing it: the fuel page is seven
fields at 1920, six at 1280, five in the catalogue's 1200-wide reference and three at 600, and the
build sheds the sixth at 1280 because the corner blocks it measures are wider than the ones the
drawing sketches.

**The gaps and sizes are each face's own.** Band D is padded 16 px at the sides and 12 in portrait,
its three groups sit 22 apart, a corner block's two fields 18, and a page's fields 34 at the three
1280 faces, 26 at 1920, 850 and the nano and 18 at 600. A label sits 5 px above its value and a
unit 5 px after it. `bandMetrics` in `packages/dash/src/zones/bandPages.ts` is that table, read off
the band of each face's artboard.

**A flag takes the band over.** While a flag is out, the flag has the band, because an alert
outranks fuel. This replaces the bottom-edge flag strip the slot model drew, so the same sixty
pixels goes to whichever has the better claim. The band draws as a filled bar with a 3 px border
in the flag's colour and the flag's name in dark text.

The black flag is the one exception, and it is drawn light on dark rather than dark on light. Its
token, `purpose.flag.black`, is `#F5F7FA`, which is the ink and not the ground: a band filled with
it would be indistinguishable from the white flag at `#FFFFFF`. So the black flag fills with
`surface.base`, keeps the border, and writes its name in `purpose.flag.black`. The canvas captions
it "outlined", which it no longer is, because a transparent flag left the page underneath fully
readable and a flag takes the band over.

---

## 7. The settings the contract fixes

One property per decision, all under the `OpenDash` prefix.

| Property | What it carries |
|---|---|
| `ZoneA` … `ZoneD` | The page each zone is showing. A button advances it. |
| `ZoneAPages` … `ZoneDPages` | A mask of which pages are enabled, which is what sets the cycle's length. |
| `ZoneAStart` … `ZoneDStart` | The page the zone opens on. |
| `ZoneAClassOnly` … `ZoneDClassOnly` | Whether the zone's list pages show the player's own class rather than the whole field. Off. |
| `QuickGlance` | The zone and page held while a button is down, as one property rather than a pair per zone. |
| `BarLeft1`, `BarLeft2`, `BarRight1`, `BarRight2` | The bar's four end fields. |

Each name is written here without its prefix, for the shape. A screen owns its settings, so what is
attached is `Face1920x480ZoneA` and `Face1280x400ZoneBClassOnly`: the same decision, once per face.

The four modes the slot model already had — `ShiftLights`, `PositionMode`, `DeltaReference`,
`SessionProgress` — are unchanged, and `RevBar` joins them: what the top of the face carries,
`shift`, `rpm` or `off`, with `off` drawing the second arrangement. It carries no face's prefix
because it is not one face's, whatever the second arrangement is: the round faces' rev arc and the
companion's speedo draw the same segments from the same setting, and a screen may not read a
property another screen owns. `ShiftLights` is now its deprecated alias and stays attached for a
release: an rc.2 user's properties do not vanish without warning (XOR-119), and a package installed
beside an older plugin falls back through it.

Every expression that reads one of these wraps it in `isnull()` with the default, so a package
installed without the plugin shows each zone's start page and simply cannot cycle. That is still a
complete product by [ADR 0003](../decisions/0003-plugin-settings-through-properties.md)'s letter,
and it is the first feature for which the plugin buys something material.

### What the panel draws

`Plugin.dc.html` draws the Zones section as a plan of the face at 844 px wide: the rev bar strip,
the bar with an end control at each side, zones B, A and C across the body at 246, 316 and 280, and
band D along the foot. Every part is at the size the artboard gives it, because "zone C" means
nothing until you see where zone C is.

Four things the artboard does not settle, and what the panel does about each:

| | |
|---|---|
| **The mask has no control drawn.** | It is the setting that decides how long a driver's cycle is, so it cannot simply be missing. The panel puts a second drop in each zone cell, reading "21 of 21 pages", opening a checkbox per page. Owed on the canvas. |
| **Nor has the class filter.** | A checkbox under the start page in each zone cell, reading "My class only", and only in the cells where a page would change — zones B and C. Owed on the canvas alongside the mask. |
| **An end of the bar is drawn as one control** reading "Race · lap", and an end carries two fields. | The control stays one box and opens a panel with a picker for each, rather than splitting into two boxes the artboard does not have. |
| **Nothing says what happens to a zone sitting on a page that is then turned off.** | It snaps *forward* to the next enabled page, wrapping once — forward because a cycle runs forward, so the next press of the button carries on rather than repeats. Turning off a zone's last enabled page is refused: a zone with an empty cycle has nothing to draw. |

Two zones showing the same page is reported and allowed, which the canvas is explicit about. The
comparison is by page **id** and not page number, because the four catalogues overlap: zone A's
track page and module 13 are one drawing under two numbers, and a comparison by number would miss
exactly the duplicate a driver would notice.

The quick glance is a fifth participant in that comparison, and it reads "Zone C and the quick
glance both show the track." It is compared against each zone's *start* page rather than against
the whole cycle, exactly as the zones are compared with each other, because a glance set to a page
a zone can cycle to is something somebody may well want and warning about it would be a false alarm
on every second rig. The page is named with an article and a lower-case noun, "the relative", save
where a name lists what a page draws rather than naming one thing: "Gear, speed, revs" does not read
after an article, so it keeps the spelling the drop-down uses.

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
| Band D's page count | The catalogue heading reads "band D · seven pages", its anatomy row reads "fuel by default, and six more pages", and the drawings are D1 through D8. **Eight is taken**, because the drawings are more specific than the captions and the mask is sized for eight either way. D8 is nonetheless the one page built short of what it is for: it draws the readings the telltale row would sit beside, and the lamps themselves wait on the generator's image item (XOR-115) before XOR-97 can draw them. |
| The bar's fields | The catalogue's anatomy says "three fields a driver may swap", the bar section of that same artboard says each end is one field, and every face artboard draws two at each end. **Two per end is taken**, because that is what is drawn; §1 and §3 above both say so now, the first of them having repeated the one-per-end caption until this row was written. The catalogue those fields are chosen from is ten entries where the artboard draws eleven, strength of field being the one that went, under [ADR 0009](../decisions/0009-does-the-plugin-compute.md), because SimHub publishes it in no form at all. |
| Zone C's capacity | Stated as ten drivers at 1920; seven rows are drawn. |
| Page dots | `pageIndicator` is still in the component list, against "there is no row of page dots". |
| The fuel tank | Dropped from the drawn objects in the 0.7.0 changelog — "a quantity is a number" — and still listed among five in `canvas.json`'s detail-pass annotation. **Four objects are taken.** |
| The numeral family | Rule 4 says numerals are Barlow Condensed. The files ship as `openDash Display`, because WPF reads the width word out of a family name and folds the condensed faces into Barlow as a stretch, which a `.djson` cannot ask back. Same outlines, different name; see XOR-108. |
| The telltales | Twenty-eight Material Design Icons are named and the build "rasterises the chosen twelve", which are not listed. Owed before XOR-97 starts. |
| Band D's value size | Every 60 and 58 px band draws its page values at 34 px over a 13 px label, 5 px apart. WPF's line box around a 34 px value runs 60.6 px from the top of that label, so the band clips it by a pixel. **The value shrinks** — 32 at 60, 30 at 58 — because a clipped numeral reads as a rendering fault. The band would have to grow, or the drawing come down; the 54 and 56 px bands draw 24 and are honoured exactly. |
| The face with no rev bar | XOR-138 offered three answers, namely leave the gap, reclaim it, or give the band to something else, and said the artboards would choose. Since the second pass of 15 September the FaceVariants sheets do draw the third state and both arrangements beside each other, so this row no longer reads as it did. What the rev-bar-off drawing still carries, however, is the rev bar itself: an 822 × 28 rectangle at (14, 6) on the 850 sheet, a 576 × 24 one at (12, 6) on the 600, underneath a bar that has already risen into its room. **The caption is taken over the rectangle**, and `faceItems` leaves the well and the segments out entirely rather than hiding them; `Plugin.dc.html`, for its part, still reads "the rev bar stays". Reclaim is taken for the room, because the gap reads as a mis-crop and on the nano it is a ninth of the screen, and the rectangles in §1 remain derived by one rule and remain the thing to delete when drawn ones arrive. |
| The slot counts in the titles | `canvas.json` titles the 1920 × 480 artboard "MVP · 12 slots" and the 1280 × 720 one "wheel screens · 12 slots", while what each draws underneath is the five-part zone face [ADR 0006](../decisions/0006-the-zone-face.md) settled, and `Dash.dc.html` keeps `.slotbox`, `.card` and `.grid4` in its stylesheet with nothing using them. **The drawing is taken**: a `ZoneLayout` declares no slot count at all, and twelve matches nothing on the 1280 × 720 body either, whose bar draws eleven readouts and whose band draws ten and three lamps. The twelve-slot package does still build beside the zone face, since `LAYOUTS` keeps `layout1920x480` and `build.ts` walks both lists until XOR-95 retires the card path. |
| The six slots of the 850 | The same convention gives 850 × 480 "5in · 6 slots", and nothing six-fold is drawn there. The only reading that yields six is the parts themselves, that is to say the bar's left end, its settings strip and its right end, then zones B and C and band D. **The parts are taken**, because that is what the artboard draws and what `faceItems` composes; the count is vocabulary left over from the model the face replaced. |
| The "D grid" chip | Every FaceVariants sheet chips band D as `grid`, whereas the band it draws is 1280 × 60, or 800 × 58 on the nano, which `second/shape.ts` bands as wide and short rather than as the 430 × 300 the `grid` archetype is. **Neither is taken, because the band does not consult the shape model at all**: `bandPages.ts` draws one centred rank for a wide short box, and only zones B and C ask `shapeOf` for their page. The 600 × 686 sheet chips its own zones B and C the same way, and they measure 600 × 160 and 600 × 150, which is wide and short again. |
| The strip at 850 × 480 and 800 × 480 | Both artboards caption five cells, namely slip, TC, cut, bias and ABS, and the build keeps four at 850 and three at 800, which §3 tabulates and `barStrip.test.ts` pins. **The artboards' own scale is taken**: each face now draws the bar at the size its artboard gives it, so the narrower faces gain cells the earlier measured layout had shed. What the two still drop is cut at 850 and cut and slip at 800, and the cause is the ends rather than the strip, each end being laid out from its own edge for the widest entry the catalogue holds rather than for the entry actually selected. Raising the count further therefore means narrowing the reserved end or measuring the strip's values below the size the end fields use, and the canvas has made neither decision. The 600 × 686 sheet is no longer a disagreement: it draws its five cells in fixed 54 px columns at a 12 px gap, which is what the build now does, with four pixels to spare that the widest class name governs. |
| The 600 × 686 well | The size's own chip names a 36 px well above the bar. The artboard draws the well at 6, 2, 588 × 32 with the segments at 12, 6, 576 × 24, and the bar begins at y 36, so that 36 is the room above the bar, being a 2 px face margin, the 32 px well and a 2 px gap, rather than the height of anything. **The artboard is taken** and §1 tabulates the 32. Were the well itself meant to be 36, the rect in `faces/600x686.ts` would move and `revBarReclaim` would become 38, which moves the second arrangement's table as well. |
| Zone A, centred or filled | The face artboards centre zone A's block in its column, `justify-content: center` with a 198 px gear in a 320 px column at 1280 × 480, while the FaceVariants sheets caption the same zone "Zone A fills its column. Padding stays; empty height does not". **Both are taken, and they turn out not to disagree**: every page is cut from the column, each run being a share of its height rather than a size of its own, and what is then left over goes half above the page and half below it, so all four fill and all four centre. What the sheets ask for and the format refuses is the last three per cent of the gear, which is the subject of the row below. |
| The catalogue's zone A against the per-size sheets | The two draw different pages. The catalogue's A1 is a 62 per cent gear over a speed and no revs, its A2 carries the ghosted neighbours and its A3 puts an rpm value between the speed and the gear; the FaceVariants sheets give A1 the three runs at 55, 19 and 11 per cent, A2 the gear alone and A3 two rows. **The per-size sheets are taken**, being the more specific drawing and there being eight of them, with two exceptions that cost nothing: the ghosts stay on A1, where they have always been, and A3 draws the rpm beside the speed wherever the column is wide enough to hold the group, which today is the 600 × 686 face and nowhere else. The gaps disagree too, the Dash artboards drawing 3 px at the nano and 6 at 850 × 480 where the variants sheets draw 4 and 7; **the sheets are taken**, as a share of the column rather than a literal per face. |
| Zone A's gear at 86 per cent | Every FaceVariants sheet draws A2's gear at about 86 per cent of the column, 282 px in 328 and 167 in 194. **The line box is taken instead**, which is about 82: SimHub hands the box to WPF as `MaxTextHeight` and WPF clips what does not fit, so a box has to hold the whole 1.2 em line, and `zoneFace.test.ts` keeps every box inside its dashboard. The twelve pixels between the two are the leading under the baseline, which a digit does not use but the "N" and the "R" the gear also draws do. Reaching 86 means letting the box be cut below its line box, which is the author's to decide. |
| The hero that never moves | The Main artboard reads "Gear, speed, rev bar, flag and pit limiter are fixed per layout. Every other value is a card". **It predates the model**: zone A cycles four pages under ADR 0006, so the gear gives way to the speed or to the track map on a button press, and the speed was card 12 rather than part of the hero even under the model the sentence describes. Only the rev bar, the flag and the pit limiter are fixed on the zone face. The sentence wants marking superseded, as the DashComponents slot numbers already are. |
| DashComponents' zone A | The component sheet calls zone A "fixed on every layout" and describes the rev bar 40 tall in its well over a 1 px rule, the gear alone, a flag band 40 tall at the bottom edge and the limiter above the gear. That is the card face, which still builds and still draws precisely that. **The zone face follows the Zones artboards instead**: a 56 px bar of settled values takes the place of the rule under the rev bar, the segments are 32 tall inside a 40 px well, and the flag takes band D's sixty pixels rather than a strip of its own. The section wants the same superseded marking as its slot numbers. |
| The same five parts on every face | The catalogue's anatomy says the five parts differ only in size from one rectangular face to the next. Two of the per-size artboards draw otherwise: 800 × 286 has no bar at all, which leaves four parts, and 600 × 686 stacks A over B over C rather than setting B beside A beside C. **The per-size artboards are taken**, being the more specific drawing, and §1 tabulates both departures. |
| The gap chips on the face sheets | Each `FaceVariants` sheet counts the pages that do not fit its rectangle as the catalogue draws them, and the 1280 × 720 and 1280 × 480 sheets give every one of the twenty-one a shed count of nought. The catalogue's own `tall` drawings do shed: sectors keeps two of its three lap times, a leaderboard row loses its best and its last, and the opponents blocks lose the car number. **The drawings are taken**, since §5 was read off them; the counts are annotation over the top of them. |
| Lap times at `tall narrow` | The catalogue draws two times at 34 px in a 274 × 300 zone and leaves 234 px of it empty. **Four are taken**, one per line and grown to 46 px, because the box the drawing answers is a real zone on the base face and a driver reads it at arm's length. The redraw and the same pass over the other twenty pages are [readability-pass.md](readability-pass.md). |
| Session's sixth field | The catalogue labels it *Est. laps* at `wide` and at `tall`, where the build labels it *Laps left*. **The build's label is kept**, on two grounds. Firstly, the value behind it is `RemainingLaps`, which is the session's own count of laps still to run, and no research note here describes that property as an estimate, so *Est.* would be a claim the datum does not make. Secondly, *Est. laps* is already the label of the fuel page's sixth field, where it carries `Computed.Fuel_RemainingLaps`, that is to say the range left in the tank; two pages drawing the same two words over two different quantities is precisely the confusion the rename would introduce. Either the catalogue renames this one, or the session field is rebound to something that is genuinely estimated. |
| Session's third rank | The catalogue draws Strength, Incidents and Cars at `wide` and at `tall`, and **two of the three are built**. Strength of field is left out under [ADR 0009](../decisions/0009-does-the-plugin-compute.md), which found it published by SimHub in no form at all and struck it from the bar's catalogue of end fields for the same reason. The row is therefore two fields wide rather than three, and it closes over the hole the way [§11](#11-a-field-that-is-not-there) describes. |
| Lap history's third column | The catalogue draws the fuel each lap cost, at every shape, where the pit wall's wide page draws the delta to the session best. **The delta is taken, and only at `wide`**, because no previous-lap property carries a consumption beside the time and keeping one per lap would be the plugin remembering between frames, which [ADR 0009](../decisions/0009-does-the-plugin-compute.md) refuses. The three narrower shapes therefore list two columns where the drawing lists three, and [second-screens.md](../second-screens.md) records the datum that is not there. |
| A slower lap's colour | The catalogue paints every lap slower than the session best in red and draws no middle band, whereas the module steps through caution at half a second behind and danger at a full second. **The ladder is kept**, since a lap half a second off and a lap a second off are two readings and a driver acts differently on them. The two were nonetheless the same red for as long as the caution branch read `purpose.fuel.low`, which resolves to the danger colour, so the ladder said nothing until that was put right. |
| Lap history's row count | The catalogue lists six laps at `wide` and at `grid` and seven at the two tall shapes, while the companion artboard lists seven in a box the shape model reads as `wide`. **The catalogue is taken**, being the drawing of record at the four shapes, so the companion page lists six. A box too short for its declared count lists fewer regardless, which is why zone C of the 600 × 686 face lists four where its own sheet draws six. |
| The ramp ceiling at `tall` | Rule 20 stops a rank at the next size up its ramp, which is one step of about 1.35, while the catalogue promotes far harder at `tall`: lap times 46 to 88, the delta 64 to 132, the speedo 64 to 128, and fuel, sectors, stint and session 34 to 76. Either the ceiling is too low or the drawings are, and nobody has decided which; the ceiling stands until somebody does, since it is what keeps filling a box distinct from scaling into one. |
| The mini-sector strip | The catalogue and both companion artboards draw twelve mini-sectors under the sector times. **Three cells are taken**, one per real sector, because SimHub times sectors and not segments and [ADR 0009](../decisions/0009-does-the-plugin-compute.md) forbids inventing the data a twelve-cell strip would need. The canvas owes either a redraw at three or a caption saying the twelve are notional. The same strip is a second disagreement of its own: the FaceVariants sheets draw the sectors page as two ranks with nothing between them, so the 6 px strip the build puts there is an addition the drawings do not carry, and it is deliberate rather than accidental. |
| Lap times at the companion | The companion artboard draws twelve fields in four ranks of 64, 46, 46 and 34, and the build draws nine: at the module box the build really hands the page, 802 by 336, the four ranks come to 337 px against 332 of room and the sector rank is shed. The 20 px are the flag band, which the artboard draws 12 high and `ds.indicator.flagBand.heightSm` gives 32. **The shedding is taken** rather than a page drawn past its box, and the twelfth field returns if the band ever comes down to the artboard's height. |
| The sector deltas' size | The companion draws the delta page's S1/S2/S3 rank at 34 px and the catalogue draws it at 34 as well, which is `small` on one ramp and `mid` on the other; the page therefore names the ramp rung by density rather than by one token. The same question decides the recap under the sectors: 34 on the companion and 24 in a zone are both `small`, and a compact zone's `small` is 18 where the 800 × 480 sheet chips 24. |
| Three sector columns in a narrow zone | The catalogue draws the sectors page as three columns at every shape, including `tall narrow`, where three 34 px sector times and their gaps need 286 px of a 274 px zone. The build used to reach three columns by stepping the rank down to 18 px, which is the page shrinking the reading it exists for. **The rank now keeps 34 and wraps to two lines and one**, per rule 17; the canvas owes the redraw, as it does for lap times at the same shape. |
| Spreading or centring | Whether a page spreads its ranks over the full height or centres them as one block is decided page by page on the catalogue and not by shape: sectors, fuel, session, stint, the speedo and car settings spread at all four shapes, lap times spreads at three and centres at `tall narrow`, the delta centres at three and spreads at `tall`, and the lists, the drawings and the pit view centre everywhere. The engine therefore takes it from the page (`justify: 'spaceBetween'` on `stack`) and centres by default. |
| A short box's ranks | `keepsSecondaryRanks` says a short box keeps one rank, while `archetypeOf` hands a wide short box the `grid` answer, which keeps two. The code follows `archetypeOf`, and the helper is unused. Either the short boxes the build produces get a fifth declared answer, agreed with the canvas, or the helper goes so that one rule governs. |

### Every variant the 1280 × 480 sheet lists

`FaceVariants1280x480.dc.html` is captioned "Every variant this size can be in, and every page each
of its zones can show", which is a stronger claim than the two screens `faceScreen` builds, so the
list is worth reading item by item. The same sheet exists at each of the other seven sizes and
lists the same things.

| What the sheet draws | Where the build stands |
|---|---|
| The two arrangements, rev bar on and rev bar off | **Built**, as the two screens of one `.djson` with complementary `ScreenEnabledExpression`s. |
| Zone A's four pages, A1 to A4 | **Built**, as `zoneface-zoneA-340x320` and again at 340 × 361 for the second arrangement. Three of the four carry a `proposed` chip on the sheet and are built regardless, the fourth being the catalogue's own track page. |
| Twenty-one pages for zone B and twenty-one for zone C | **Built**, as the one `zoneface-module-469x320` both zones point at, and again at 469 × 361. |
| Band D's eight pages, D1 to D8 | **Built**, as `zoneface-band-1280x60`. What D8 is still short of is in the table above. |
| The flag over the band, in six colours | **Built**: `flagStrip` draws black, chequered, yellow, blue, white and green over band D's rectangle, and the black flag fills with `surface.base` rather than with its own token. |
| A full-screen flag over zones B, A and C, with `OpenDash.FlagFormat` set to band or full | **Deferred.** It is chipped proposed, and it is a further pair of arrangements rather than an option on the two that exist: a screen cannot resize its neighbours at run time, so the format would be built the way the rev bar is, as more screens with complementary expressions, and the property would carry a face's prefix like the zone settings. The contract declares no such property today. |
| The chips "bar: 2 fields per end" and "band corners: yes" | **Built**: `barFieldsPerEnd` is 2 and `bandCorners` is true at this size. |
| The chips "A grid", "B grid", "C grid" and "D grid" | Three of the four are what `shapeOf` returns for those rectangles. The fourth is the disagreement recorded above. |
| A growth factor per page of zones B and C, from ×1.08 to ×2.07 | **Recorded, not checked.** A rank grows by rule 20 until it meets the width, the height or the next size on its ramp, and nothing compares the factor it reaches against the factor the sheet chips. |

---

## 11. A field that is not there

Shedding is what a page does when a field **will not fit**. A field can also be missing because
there is nothing to draw, and the design asks for the opposite behaviour in the two cases that
arise.

**A field the sim does not publish is removed, and the rank closes over the hole.** The band says
it plainly: nothing is spread to fill, the rank is packed and centred in what the corners leave.
The bar's strip hides what the game does not expose, because a strip drawing an empty box for a
setting iRacing has no property for is worse than a narrower strip. Band D's car page removes an
oil pressure the sim does not wire rather than drawing 0.0, which is a reading and a wrong one.

**A telltale that is unlit keeps its place and is drawn dim.** A lamp coming on is then a change of
colour and not of layout: one that vanished and returned would move every lamp beside it at the
moment the driver most needs to read them. DRS, push to pass and the spotter sit in the band's
right-hand corner and behave this way.

Both rules are deliberate and they contradict each other, which is why the choice is a mode of one
component — `packages/dash/src/second/rank.ts`, `when: 'close'` or `when: 'dim'` — rather than a
judgement taken once per page. A rank whose members can never go missing carries no binding at all.

Closing over a hole happens **while the dashboard is running**, not while it is built: the item's
`Left` is bound to the arithmetic that repacks and recentres whatever is left. `Left` is a bindable
target, verified in [research/simhub-dash-format.md](../research/simhub-dash-format.md).

## Related

[scope.md](../scope.md) is what OpenDash is and what it refuses to be.
[ADR 0006](../decisions/0006-the-zone-face.md) is why the model changed.
[brand.md](brand.md) is the reasoning behind the colours and the type.
[readability-pass.md](readability-pass.md) is one ticket per page: what each of the twenty-one
would have to change to put the reading a driver needs first.
[second-screens.md](../second-screens.md) is the companion and the pit wall, which share the
twenty-one pages.
[research/simhub-dash-format.md](../research/simhub-dash-format.md) is what SimHub actually does,
and is the place to check before guessing.
