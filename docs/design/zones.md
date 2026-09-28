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
| **Band D** | Fuel by default, and seven more pages that suit a wide short band. A flag takes the band over for three seconds when it comes out, and then settles into the block at each end and gives the page back until it clears. |

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
already carries LEDs across its top (#189). Hiding the segments alone leaves the well lit by
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

**Five pit alerts share that one rectangle, ranked among themselves and not against band D.** Engage
the limiter, disengage it, the limiter on in the lane, the ignition off and the engine off are one
ordered list, of which at most one is ever out, each carrying its own test of whether the car is in
the lane rather than the list carrying one. They are deliberately **not** ranked under the flag: the
two draw in different rectangles and never contend, so gating the pit list on "no flag is showing"
would blank the limiter band under a full-course caution, which is precisely when the pit lane is
busiest. `ENGAGE LIMITER` is guarded on the presence of the in-car control itself, since a car
without one should not be told to use it.

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
missing and centres on half of what is left. Every cell therefore takes the column rounded up, 58
on the faces drawn at 57 and 54 on those drawn at 54. A reading wider than its column would widen
that one cell rather than lose a digit, although none is: "Bias 50.5" is the widest of the seven
and fills its column exactly at 34 px, 57 of 57, with four pixels to spare at 28.

**The order it sheds in is not the order it draws in.** A driver on a GT3 car moves the brake bias
every corner and has TC and ABS on wheel dials; the mixture changes once a stint; slip, cut and the
differential are settings some cars do not have at all. So what a narrow strip keeps, in order, is
bias, TC, ABS, slip, cut, map, diff — and what it draws is still the canvas's order.

This is a decision the canvas does not make. It was forced by the first photograph of the 850 × 480
face, where the five cells that had no shedding rule were drawn straight over the right-hand
fields: BIAS on POSITION, ABS on the slash of "3 / 24".

Two of the seven cells do not read what their label promises, and settling them belongs to the
canvas rather than to the build. DIFF is bound to the rear anti-roll bar, which the car settings
page already draws under the label ARB R, so that one reading is published twice under two names.
SLIP, moreover, is bound to the throttle shape, which is a throttle map rather than a slip target.
SimHub normalises neither a differential nor a slip setting, and a car that offers either publishes
it under a name of its own, so the question cannot be settled by looking a property up: what is
required is a statement of which in-car adjustment each of the two cells is meant to show. Until
that statement exists both cells are drawn as they are, and the disagreement is recorded here
rather than resolved quietly in the code.

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
  [#330](https://github.com/xorob0/OpenDash/issues/330) is the ticket.
- **Relative.** `wide`: position, number, name, class, gap. `grid`: the same five. `tall narrow`:
  position, name and gap only, and eight rows rather than six. The number and the class chip go
  from between two columns that stay, which no rule about prefixes produces.

  **The row count is the page's own declaration and not the catalogue's count.** The relative is a
  window on the player and it lists at most five cars either side, eleven rows, and asks for at least
  one either side; zone C of the 1280 × 720 face was listing thirteen and the `tall` reference fifteen,
  which is a wall rather than a list. The row then stretches to fill the body those rows leave, so the
  block spans its box instead of sitting centred in a pool of slack, and it stops short of a stretch
  that would promote the position and the gap and take the width out of the name. See §10 and
  [#339](https://github.com/xorob0/OpenDash/issues/339).

  The catalogue draws that column as a three-letter code — `KLX`, `MOR`, `TSA` — at every one of the
  four shapes, and **the build draws a name**, in whichever of four formats the rig asks for, cut in
  the expression and closed with an ellipsis where the column cannot hold it, the space the cut fell
  on going with it rather than being left in front of the dots. See §10; the code was
  `left(name, 3)`, so Liam Byrne was `LIA` and Hannah Fischer `HAN`, and
  [#385](https://github.com/xorob0/OpenDash/issues/385) is where that was decided. The name is drawn
  upper-cased, as the code was, because at this size the raster welds the dot of a lowercase `i` to
  its stem and the letter reads as an `l`; §10's divergence table has the measurement.

The pattern holds generally: a narrow zone loses columns before it loses rows, and a tall one
buys rows before it buys columns.

### The table

Field and column ids, as the module names them. The `wide` row is the fullest form and keeps
everything the page carries, the companion artboard's fields included, which is why a companion
page is not changed by any of this. The declaration is read before the box is measured, and what
does not fit still sheds afterwards: a declared set is a design decision and a box is a fact.

The order inside a cell is importance, most important first, which is also the order a box too small
takes fields off the end of. Opponents is the one row where that order interleaves, since its rank
is two of the same thing: the car ahead's field and the car behind's twin are named together so that
a short box sheds the same line from both cars rather than emptying one of them.

| № | Page | `wide` | `grid` | `tall narrow` | `tall` |
|---|---|---|---|---|---|
| 1 | Lap times | `last` · `sessionBest` · `yourBest` · `laps` · `estimated` · `delta` · `average5` · `position` · `stintLap` · `s1` · `s2` · `s3` | `last` · `sessionBest` · `yourBest` · `delta` | `last` · `sessionBest` · `yourBest` · `delta` | `last` · `sessionBest` · `yourBest` · `laps` · `estimated` · `delta` |
| 2 | Delta | `delta` · `s1` · `s2` · `s3` | `delta` · `s1` · `s2` · `s3` | `delta` · `s1` · `s2` · `s3` | `delta` · `s1` · `s2` · `s3` |
| 3 | Sectors | `s1` · `s2` · `s3` · `yourBest` · `last` · `sessionBest` · `bestS1` · `bestS2` · `bestS3` | `s1` · `s2` · `s3` · `yourBest` · `last` · `sessionBest` | `s1` · `s2` · `s3` · `yourBest` · `last` | `s1` · `s2` · `s3` · `last` · `sessionBest` |
| 4 | Speedo | `speed` · `rpm` · `redline` | `speed` · `rpm` | `speed` · `rpm` | `speed` · `rpm` |
| 5 | Fuel | `level` · `time` · `toEnd` · `toAdd` · `lastLap` · `thisLap` · `average` · `lapsLeft` | `level` · `toEnd` · `toAdd` · `average` | `level` · `toEnd` · `toAdd` · `average` | `level` · `time` · `toEnd` · `toAdd` · `lastLap` · `thisLap` · `average` · `lapsLeft` |
| 8 | Pit view | `refuel` · `pitTime` | `refuel` · `pitTime` | `refuel` · `pitTime` | `refuel` · `pitTime` |
| 9 | Car settings | `tc` · `abs` · `bb` · `mix` · `arbFront` · `arbRear` | `tc` · `abs` · `bb` · `mix` · `arbFront` · `arbRear` | `tc` · `abs` · `bb` · `mix` | `tc` · `abs` · `bb` · `mix` · `arbFront` · `arbRear` |
| 11 | Session | `type` · `position` · `class` · `lap` · `timeLeft` · `lapsLeft` · `incidents` · `cars` | `position` · `class` · `lap` · `timeLeft` | `position` · `class` · `lap` · `timeLeft` | `type` · `position` · `class` · `lap` · `timeLeft` · `lapsLeft` · `incidents` · `cars` |
| 14 | Leaderboard | `pos` · `num` · `name` · `class` · `gap` · `best` · `last` | `pos` · `num` · `name` · `class` · `gap` | `pos` · `name` · `gap` | `pos` · `num` · `name` · `class` · `gap` |
| 15 | Relative | `pos` · `num` · `name` · `class` · `gap` | `pos` · `num` · `name` · `class` · `gap` | `pos` · `name` · `gap` | `pos` · `num` · `name` · `class` · `gap` |
| 16 | Opponents | `ahead.gap` · `behind.gap` · `ahead.name` · `behind.name` · `ahead.num` · `behind.num` · `ahead.class` · `behind.class` · `ahead.lastLap` · `behind.lastLap` · `ahead.rating` · `behind.rating` | `ahead.gap` · `behind.gap` · `ahead.name` · `behind.name` · `ahead.num` · `behind.num` · `ahead.class` · `behind.class` · `ahead.lastLap` · `behind.lastLap` | `ahead.gap` · `behind.gap` · `ahead.name` · `behind.name` | `ahead.gap` · `behind.gap` · `ahead.name` · `behind.name` · `ahead.lastLap` · `behind.lastLap` |
| 17 | Gear | `speed` · `rpm` | `speed` · `rpm` | `speed` · `rpm` | `speed` · `rpm` |
| 18 | Stint | `lap` · `fuelTime` · `stintTime` · `stintLaps` · `completed` · `stops` · `lastStop` · `avgLap` | `lap` · `fuelTime` · `stintLaps` · `stops` | `lap` · `fuelTime` · `stintLaps` · `stops` | `lap` · `fuelTime` · `stintTime` · `stintLaps` · `completed` · `stops` · `lastStop` · `avgLap` |

Pages with nothing to shed, and why:

- **Energy** (`energy`) — one line of prose: iRacing publishes no virtual energy.
- **Tyres** (`tyres`) — four corners cut from the box; rule 18.
- **Inputs** (`inputs`) — three traces and their bars, cut from the box; rule 18.
- **Radar** (`radar`) — the cars beside you, cut from the box; rule 18.
- **Track** (`track`) — the map, cut from the box; rule 18.
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
- **Fuel's margin, at every shape.** `toEnd` is the signed figure saying whether the fuel in the
  tank reaches the end of the race and by how much, `+1.4` laps or `−3` minutes, and neither fuel
  sheet draws it: `ZoneCatalogue.dc.html` describes the page as "fuel, fuel time, refuel, last lap,
  2 and 5 lap averages, estimated laps, level gauge" and the companion artboard draws the same set.
  It is taken all the same, because it is the only fuel question a race asks and the page already
  carried every term of it — the range and the estimated laps here, the laps left on the session
  page — so a driver was doing the subtraction himself between corners (#387).

  Where it sits is a preference rather than a transcription, and it is declared twice. At `wide` and
  at `tall` it goes third and the estimated laps stay last, since the estimate is the working and this
  is the answer. At `grid` and at `tall narrow` it takes the fuel time's place, which is the same
  trade at the only price those shapes can pay: the lead rank of a 250 px column is two readings
  grown to fill it and not three at the density's own size, so a third field there costs the page its
  growth and a rank besides. The fuel time is how long the tank lasts and the margin is that same
  quantity measured against the race, so the narrow zone carries the one that answers. The tank and
  the bar under it are never the pair that gives way; they are what the page is.

  Displacing the fuel time rather than the per-lap average was reviewed and kept, and the measurement
  is the reason: the module's lead rank is the tank, the fuel time, the margin and the estimate, so
  keeping the time means three fields in that rank instead of two, and three do not grow. Built at
  `grid`, the alternative draws the lead numerals at 34 px where the page draws them at 46 in a
  250 × 290 zone, 43 against 46 at 250 × 328, 51 against 62 at 430 × 300 and 57 against 62 at
  469 × 320 — and sheds the per-lap average at all four anyway, so it trades a quarter of the lead
  rank's height for a field it does not keep. What survives the trade is worth saying plainly: the
  tank's range is still on the page as the tank over the per-lap average, one division of two numbers
  drawn side by side, whereas the per-lap figure is the instrument a negative margin calls for and
  nothing else on a narrow page carries it.

  The canvas owes the redraw, on both sheets, and what it should draw is one design with the fuel
  target of [#326](https://github.com/xorob0/OpenDash/issues/326) rather than two fields added
  separately: the target says whether the lap just done was on plan and this says whether the plan
  reaches the flag. The two shapes above are the first question to put to it, since the choice there
  is between losses and the sheets have not been asked.
- **Leaderboard at `wide`.** The best and the last lap, two columns the zone drawing does not carry
  and the companion's list does. The trade runs the other way as well: the drawing gives the row a
  rating column, and neither list declares one.
- **Stint at `wide` and at `tall`.** The driver, where the drawing closes the page with the pit
  window. The window is not a field the module builds, and a handover is what the recap is read for.
- **Car settings at every shape.** The module draws the seven settings iRacing exposes and the
  drawing draws ten, so what is kept is the proportion rather than the count: `tall narrow` drops
  the three drawn last, which leaves four here against the drawing's seven.
- **Opponents at `tall`.** The last lap, where the drawing has a licence badge instead. The badge
  is one the module has no read for, and the 12 px `B` the catalogue draws there is that badge and
  not the class chip an earlier transcription of this row took it for, so the class does not appear
  at that shape either.

Everywhere else the drawing names a field the module does not build, which is the opposite case and
is not a deviation: delta's three sector deltas, the rating on a list row and the pit window. They
are simply not in the table, because the table is about the module.

### The parts that are not fields

A page is not only a rank. Delta is a number with a bar under it and a scale under that, lap history
is rows under a header, pit view's tyre service is the summary word the drawing writes as the one
line `Tyres · RIGHTS` together with the four corner toggles that say which corner rather than which
pair, tyres sets its four corners under a compound chip and over the captions naming the tread
fill, the change tick and the compound, and inputs puts the steering after its three pedals. The catalogue draws each
of these at some shapes and not at others, so they are declared the same way a field is, in `PARTS`
beside the table above. An empty cell is a part the drawing does not carry at that shape.

| № | Page | `wide` | `grid` | `tall narrow` | `tall` |
|---|---|---|---|---|---|
| 2 | Delta | `bar` · `scale` · `rule` | `bar` · `scale` · `rule` | `bar` · `scale` · `rule` | `bar` · `scale` · `rule` |
| 7 | Tyres | `footer` · `compound` | `footer` · `compound` |  | `footer` · `compound` |
| 8 | Pit view | `tyres` | `tyres` |  |  |
| 10 | Inputs | `steer` | `steer` |  |  |
| 19 | Lap history | `head` | `head` |  |  |

Two tables rather than one because they answer two questions: the first is what a rank sheds, this
is what furniture the page keeps around it, and a page may appear in both. Until they were
declared, a narrow box lost them to `rowsThatFit` instead, which is arithmetic arriving at a design
decision one pixel at a time, and in the tyres caption's case arriving at the wrong one: the module
sized its two rows to the frame exactly, so the caption was dropped at every size the build
produces rather than at the one shape the catalogue drops it.

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

**A pit wall asks it once for the whole screen**, which is the one place the rule differs, and the
cause is a file rather than a preference. A face's four zones are four rectangles of one dashboard,
so each may be asked separately. A pit wall's zones are widgets pointed at one zone dashboard per
rectangle, so zones A and B of the race page are literally the same file, and a per-zone filter
could not reach one without reaching the other. `OpenDash.PitWallClassOnly` is therefore one setting
per pit wall screen, rather than one per zone and rather than one for the rig, since a rig may hold
two pit walls and a board belongs to the screen it is drawn on.

It is **not** `PositionMode`, although since #212 the two meet. That setting is the rig's own
answer and it filters as well as numbering, a column of class positions drawn over the whole field
having been no leaderboard at all; this one is a zone's answer to who is in the list, and one class
counted by overall position remains a legitimate thing to ask for. A list is filtered when either
of the two says so, and `rowsInClass` in `second/values.ts` is where they meet.

Four pages read it: the leaderboard, the relative, the opponents page, whose two cars are a list of
two, and band D's own relative page D7. Zone A lists nobody, which leaves it the one zone with
nothing to filter, and the panel offers the checkbox only where a page would change.

D7 reads it differently from the other three, because it is three gaps and not a list. Filtering a
table means listing fewer cars, whereas filtering three gaps means asking for the car *ahead in the
player's own class*, which is the class-only twin of the same row lookup rather than a shorter
result. On a multi-class grid this is arguably the more useful of the two readings, since the car
ahead on track is frequently in a class the driver is not racing. The rig's `PositionMode` asks the
same of the band as of every list, and the two settings meet in `listNeighbour` as they meet in
`rowsInClass` for a table. The middle field is untouched under either reading, a driver being in
his own class by construction.

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
drawn at 1920 × 480, 1280 × 480, 1280 × 400 and 1280 × 720, and absent at 850 × 480, 800 × 480,
800 × 286 and 600 × 686. The threshold is those drawings, not a round number.

**A page sheds its last field before the rank overflows**, with nothing spread to fill. The rank is
packed and centred in what the side padding, the zone letter and the corners leave, never in the
whole band. The artboards draw the shedding rather than only describing it: the fuel page is seven
fields at 1920, six at 1280, five in the catalogue's 1200-wide reference and three at 600, and the
build sheds the sixth at 1280 because the corner blocks it measures are wider than the ones the
drawing sketches.

The page has seven fields of its own since #387, the margin sitting third, which is the count the
1920 artboard draws and one more than the band used to have. Third is a design decision rather than
an artboard reading, for the reason [§5](#where-the-build-keeps-more-than-the-drawing) gives: the rank
sheds from the tail, so a band that can carry only one of the margin and the estimated laps carries
the answer. What that comes to, face by face, is what the build emits rather than what it ought to:

| Band | Keeps | Sheds | Given up for the margin |
| --- | --- | --- | --- |
| 1920 × 480 | tank, fuel time, margin, estimate, refuel, per lap, last lap | — | nothing |
| 1280 × 480 | tank, fuel time, margin, estimate, refuel | per lap, last lap | per lap |
| 1280 × 400 | tank, fuel time, margin, estimate, refuel, per lap | last lap | last lap |
| 1280 × 720 | tank, fuel time, margin, estimate, refuel | per lap, last lap | per lap |
| 850 × 480 | tank, fuel time, margin, estimate, refuel, per lap | last lap | last lap |
| 800 × 286 | tank, fuel time, margin, estimate, refuel, per lap | last lap | last lap |
| 600 × 686 | tank, fuel time, margin, estimate, refuel, per lap | last lap | last lap |

The last column is the change #387 made and not the shedding: 1280 × 480 and 1280 × 720 were already
five fields and drew the same five less the margin, so the margin costs them one. 1280 × 400 and the
nano-portrait 600 drew all six, so the margin cost those two both consumptions — a whole field more
than the rest, which is the price of a signed figure in a band that was already full. Both have since
had the per lap back: a field is as wide as the wider of its value and its label, and labels written in
sentence case rather than capitals ([#422](https://github.com/xorob0/OpenDash/issues/422)) left each
band room for a sixth. Every face keeps
the estimate; the margin outranks it in the declaration and no width has yet had to spend it.
`bandPages.test.ts` pins this table, because nothing else would notice it going stale.

**The gaps and sizes are each face's own.** Band D is padded 16 px at the sides and 12 in portrait,
its three groups sit 22 apart, a corner block's two fields 18, and a page's fields 34 at the three
1280 faces, 26 at 1920, 850 and the nano and 18 at 600. A label sits 5 px above its value and a
unit 5 px after it. `bandMetrics` in `packages/dash/src/zones/bandPages.ts` is that table, read off
the band of each face's artboard.

**D8 is a rank of lamps rather than of fields.** The page carries the twelve telltales the
1280 × 480 artboard draws, in its order: a tyre beside three straight lines, a tyre beside three
slanted lines, the windscreen wiper, a car above two wavy tracks, ABS, ESP, the engine, a fuel can,
the battery, the speed limiter, the tyre pressure warning and the car door. Each lamp is a 38 × 32
box with a 1 px border and a 20 px pictogram centred in it, the border and the pictogram carrying one
colour between them, and the lamps sit 10 px apart, centred in the same room a page of fields is
centred in. A lamp that is not lit keeps its place and is drawn dark, which is the rule
[§11](#11-a-field-that-is-not-there) states, whereas a band too narrow for twelve sheds from the
tail, so that the 600 × 686 face draws eleven. The state colours are `purpose.telltale`, that is to
say info blue, good green, caution amber, danger red and neutral white over a dark `off`, and they
are the one place on the face where a colour is conventional rather than chosen, ISO 2575 having
fixed them. `packages/dash/src/zones/telltales.ts` is the rank.

Two parts of that drawing are absences rather than refusals, and [§10](#10-where-the-canvas-contradicts-itself)
records each: the pictogram files are not in the repository, and nine of the twelve lamps have
nothing that lights them.

**A flag takes the band over.** When a flag comes out, the flag has the band, because an alert
outranks fuel. This replaces the bottom-edge flag strip the slot model drew, so the same sixty
pixels goes to whichever has the better claim. The band draws as a filled bar with a 3 px border
in the flag's colour and the flag's name in dark text.

**And then it settles into the blocks at the ends, giving the page back.** The takeover lasts
`indicator.alert.durationMs`, which is three seconds and is the same window the lap-time pop-up and
the change notification are out for; after it the band's page is drawn again and the flag continues
in the block at each end, in its colour and with its name where the block has room, until its bits
clear. That is [#380](https://github.com/xorob0/OpenDash/issues/380), and the case it answers is a
safety car: a caution runs several minutes, the flag has said everything it has to say after two
seconds, and what a driver decides during a caution is whether to pit, so SAFETY CAR over an
unreadable fuel page for five minutes is the wrong trade in every minute but the first. A change of
flag takes the band again for its few seconds, including a change no single bit shows — a
full-course caution clearing to the local yellow underneath it is a new thing to tell a driver — so
what the window watches is the rank of the *winning* condition, `raisedRank` in
`packages/dash/src/flags.ts`. A blinking flag keeps blinking in the block.

So band D's own priority over time reads: **the flag alone for three seconds, then the flag at both
ends over the page, then the page alone.** Nothing else about the ranking changes; the fifteen
conditions are ranked by the same expression in both phases, so the phase decides the rectangle and
never which flag wins.

**The blocks a flag settles into are the band's own**, `bandFlagBlocks` in `bandPages.ts`, which is
what keeps a settled flag out of room a page is using. On the four faces that draw corner blocks they
are those blocks, taken whole and to the band's edge: the flag covers the incidents and the track
state at one end and the lamps and both clocks at the other, which is the room the band can most
afford to lose while a flag is out. Whole is the whole width the band reserves for a corner, and that
width is the block's two fields *plus* the side padding *plus* the room the zone letter stands in, so
a settled flag covers band D's own **D** as well on those four faces. That is deliberate and it is
the cheaper of two prices: the letter is twelve pixels, says which zone the band is and never
changes, whereas starting the block 44 px in to clear it would hold the flag inboard of the band's
left edge on four faces and hard against it on the other four, which is two drawings of one thing. On
the four that draw none there is no block to take, so the flag keeps the side padding instead: 16 px
of colour at each end, 12 in portrait, which is the only room in the band no page is ever laid into,
and the letter stands just inboard of it and survives. It writes no name at that width, as the nano's
12 px strip writes none. None of it is drawn on any artboard, and
[§10](#10-where-the-canvas-contradicts-itself) records that.

**The settled form is not a setting**, which #380 asked to have decided rather than assumed, and the
decision is worth stating together with what it costs, because it is not nothing. On the four faces
with corner blocks it costs a driver nothing of the flag: the block holds the name, so he keeps the
colour and the word and gains the page under them. On the four without, the block is sixteen pixels
and writes no name, so after three seconds he keeps the colour alone — and a colour is a family
rather than a member. DISQUALIFIED, BLACK FLAG · FURLED and BLACK FLAG become one outlined sliver,
DEBRIS and YELLOW FLAG one yellow sliver. That is a reading lost, on the very face the ticket is
written about.

It is still not a setting. What a switch would buy that driver is the band held for the whole flag,
at the price of the fuel page for the whole caution — the case #380 opens with, on that same face —
in exchange for a name he has already read during the three seconds the flag had the band. And the
choice between a flag held and a page given back is already a setting: `FlagFormat` set to `full`
gives the flag zones B, A and C for the whole of its duration and names it there, DSQ apart from
FURLED apart from BLACK, at the cost of the gear rather than of the band. A third arm would be a
setting inside a setting for a view one of the two already offers. What those four faces are owed is
a drawing rather than a switch, and [§10](#10-where-the-canvas-contradicts-itself) is where that debt
is written down: a block wide enough for a name there means taking room from the page, so the canvas
has to say which. Reversing the decision costs one more term on each of the two groups' `Visible` and
no new screen, so the author can do it cheaply.

**It draws the whole flag catalogue, which is fifteen conditions and not six.** The band used to
read the six `Flag_*` properties SimHub normalises, and those are a lossy summary of what iRacing
publishes: `Flag_Yellow` folds the standing yellow, the waved yellow and both cautions into one
band, and `Flag_Black` is only the `black` bit. A red flag, a disqualification, a furled black, a
meatball, a full-course caution, a waved yellow, the debris flag and the start gantry were therefore
drawn by the 8x8 box and invisible on the dash, and the face's own ranking disagreed with the box's
about which of two live flags won. The band reads `FLAG_CATALOGUE` in
`packages/dash/src/flags.ts` now, through the same `conditionVisible` the box ranks with, so the
three surfaces that draw flags cannot disagree. Which condition takes which shape, and which rank,
is tabulated in [flag-box.md](flag-box.md), which remains the single place a condition is refused
with its reason.

Three consequences are worth stating. The band is iRacing's, as the box already was, since
`SessionFlagsDetails` is a raw iRacing field: on another sim it stays dark rather than drawing an
approximation of a flag nobody published. The flash belongs to the waved yellow and no longer to the
standing one, the folded property having strobed both. And the green flag alone reads a normalised
property, `Flag_Green`, because iRacing holds the `green` bit for a whole green-flag stint and
SimHub's own limiter on that property is the only clock there is; without it band D would be a solid
green bar over the fuel page for an entire race.

**Three shapes and no fourth**, which is the canvas's rule for the alert catalogue and is
`packages/dash/src/components/alertBand.ts`: a filled bar, a bar outlined in the alert's colour over
an opaque ground, and the chequer. The black family and the start gantry take the outlined form, and
the black flag is the reason it exists. Its token, `purpose.flag.black`, is `#F5F7FA`, which is the
ink and not the ground: a band filled with it would be indistinguishable from the white flag at
`#FFFFFF`. So the black flag fills with `surface.base`, keeps the border, and writes its name in
`purpose.flag.black`. The canvas captions it "outlined", which it now is again in the sense the
canvas means, an edge and a name in the alert's colour, though never with a transparent ground: a
transparent flag left the page underneath fully readable and a flag takes the band over.

**The nano writes no name.** Its twelve pixels are colour alone, so the conditions that share a
colour share a band there: a debris flag reads as a yellow, and the three members of the black
family as one outline. That is the price of the strip's height rather than a decision of the
catalogue's, and it is why the names exist wherever there is width to hold one. Since #380 the nano is
not the only place that pays it: a flag settled into the sixteen pixels at the ends of a band with no
corner block, twelve in portrait, reads the same way, which is the cost
[§6](#6-band-d--eight-pages) weighs above.

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
release: an rc.2 user's properties do not vanish without warning (#170), and a package installed
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
| **Nor has the class filter.** | A checkbox reading "My class only", under the start page in each zone cell where a page would change, and along the strip for band D, whose controls lie in a row rather than stacked. Zone A alone is offered none. Owed on the canvas alongside the mask. |
| **An end of the bar is drawn as one control** reading "Race · lap", and an end carries two fields. | The control stays one box and opens a panel with a picker for each, rather than splitting into two boxes the artboard does not have. |
| **Nothing says what happens to a zone sitting on a page that is then turned off.** | It snaps *forward* to the next enabled page, wrapping once — forward because a cycle runs forward, so the next press of the button carries on rather than repeats. Turning off a zone's last enabled page is refused: a zone with an empty cycle has nothing to draw. |

Two zones showing the same page is reported and allowed, which the canvas is explicit about. The
comparison is by page **id** and not page number, because the four catalogues overlap: zone A's
track page and module 13 are one drawing under two numbers, and a comparison by number would miss
exactly the duplicate a driver would notice.

**A pit wall holds its glance rather than storing a property.** A second-screen property has to be
read by a package, which the suite enforces, and nothing reads a glance value: the glance works by
moving the zone-page settings the dashboard already reads and putting them back on release. It is
consequently plugin state on the screen rather than a declared property, exactly as the companion's
own glance is, and the trigger is a hold bound through `<ns>HoldQuickGlance`, a hold rather than a
click because a hold cannot be left on by accident, which matters most on a screen nobody is
watching continuously. The four data zones are its targets; the wide zone is not one.

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
   — and it needs a show-for-N-seconds primitive the format may not have (#167).

This section is filled in when the canvas answers it. #154 owns the question.

---

## 9. What a round face is

**Zones on a ring.** Of the three answers #145 put — zones on a ring, freeze the round faces on the
card path, or drop them — the first is taken. A round face is the rev arc, zone A in the middle, the
rectangles its cards occupy today turned into small catalogue zones, and the flag on the ring. No
bar and no band: the ring is where a band would have gone, which is rule 10, and there is no
straight edge long enough to settle values along. It is taken because it reuses every part the
rectangular faces already have, and because dropping the round faces would take fourteen packages to
twelve over a question about two rectangles.

**It is built after 1.0, and until then the two round faces ship on the card model deliberately.**
That is the second half of the answer and the half a reader is most likely to need: the round faces
are not undecided, they are decided and not yet converted. So through 1.0 `480round.ts` and
`800round.ts` keep reading `layout.slots`, `OpenDash.Slot01` to `Slot12` keep driving them and
nothing else ([§7](#7-the-settings-the-contract-fixes) and #170), and the card path is not retired at
1.0 — #146 now waits on the conversion rather than on this answer.

Part by part, what a round face becomes:

| | |
|---|---|
| The rev arc | Unchanged. It is already the round faces' answer to the rev bar, at radius 206 on the 480 and 352 on the 800, and `OpenDash.RevBar`'s `off` state falls back to the plain arc rather than selecting a second arrangement, because a round face has no well to give back ([ADR 0004](../decisions/0004-rev-bar-model.md)). |
| Zone A | The middle of the disc, cycling the same four pages as on a rectangular face. The rect is the one the gear already has: 160 × 340 on the 480, 320 × 280 on the 800. |
| The catalogue zones | The card rectangles, as they are drawn: two 140 × 108 on the 480 and six 180 × 110 on the 800. Each cycles its own catalogue and each is one wheel action, the way zones B and C are. |
| The flag | The ring, being the outer 12 px the artboards already reserve for it, rather than a band. Whether a round face also gains the full-screen flag the rectangular faces have under `FlagFormat` is owed with the conversion; that setting is declared per rectangular face today and a round face has none. |
| The bar | None. |
| Band D | None. |

**Which pages a round zone may show is the work's to answer, not this section's.** A 140 × 108 box is
far smaller than any zone a rectangular face gives, and rule 17 in [§2](#2-the-shape-model) says a
page answers to the shape of its box, shedding or shrinking rather than drawing outside it. So the
catalogue of a round zone is whatever survives that box, and not the twenty-one by declaration. Counting it needs the boxes measured against the pages
the way `secondScreens.test.ts` measures the module shapes, which is part of the conversion.

**The canvas is owed two artboards, and it is not this repository's to draw.** `DashRound480.dc.html`
and `DashRound800.dc.html` draw the card model, and `canvas.json` still titles them "2 slots" and "6
slots". They are the two of twenty-five the zone pass never reached, and after this decision they
disagree with the design rather than merely lagging it. The disagreement is recorded here because
`design/` is the author's; the artboards are owed before the conversion, being the thing the rects and
the catalogue would be read off.

**The panel owes a round picker with it.** A face is configured on a picture of itself
([ADR 0020](../decisions/0020-the-panel-draws-what-it-configures.md)), and the rectangular plan in
[plugin.md](plugin.md) does not fit a disc. The picker the conversion needs is the arc, zone A in the
middle and the catalogue zones where the card rects are. Until the conversion the round faces keep
the Layout section they have, which assigns cards to slots.

**The three obligations above have no ticket yet.** The two artboards, the round picker and the
catalogue a 140 × 108 box leaves are the conversion's work, and the conversion is a noun in this
section rather than an issue number: #145 is the decision and closes with it, #146 waits on the
conversion, and nothing tracks it. Filing it is the first thing to do when #145 closes, and its
number replaces this paragraph and the matching one in
[ADR 0006](../decisions/0006-the-zone-face.md#unresolved).

---

## 10. Where the canvas contradicts itself

Recorded rather than resolved. A reader who finds one of these has found a real disagreement, not
a mistake in this document.

| | |
|---|---|
| Band D's page count | The catalogue heading reads "band D · seven pages", its anatomy row reads "fuel by default, and six more pages", and the drawings are D1 through D8. **Eight is taken**, because the drawings are more specific than the captions and the mask is sized for eight either way. D8 now draws the twelve-lamp rank the artboard gives it, in place of the water, oil, oil pressure, fuel pressure and voltage readings it carried in the meantime. Those five are consequently drawn nowhere on the face any longer, no module of zones B and C carrying them either, and whether the face owes them a home of their own is the author's to say. |
| The bar's fields | The catalogue's anatomy says "three fields a driver may swap", the Foundations anatomy on the Main artboard says "a field at each end", and every face artboard draws two at each end. **Two per end is taken**, because that is what is drawn; §1 and §3 above both say so now, the first of them having repeated the one-per-end caption until this row was written. The catalogue those fields are chosen from is ten entries where the artboard draws eleven, strength of field being the one that went, under [ADR 0009](../decisions/0009-does-the-plugin-compute.md), because SimHub publishes it in no form at all. |
| Zone C's capacity | Stated as ten drivers at 1920; seven rows are drawn, which since [#339](https://github.com/xorob0/OpenDash/issues/339) is the relative's own declaration rather than what the height divided out. Ten would be inside its window of eleven; what the 745 × 276 body does not have is the height for ten at a row a driver reads. |
| Page dots | `pageIndicator` is still in the component list, against "there is no row of page dots". |
| The fuel tank | Dropped from the drawn objects in the 0.7.0 changelog — "a quantity is a number" — and still listed among five in `canvas.json`'s detail-pass annotation. **Four objects are taken.** |
| The numeral family | Rule 4 says numerals are Barlow Condensed. The files ship as `openDash Display`, because WPF reads the width word out of a family name and folds the condensed faces into Barlow as a stretch, which a `.djson` cannot ask back. Same outlines, different name; see #159. |
| The telltales' pictograms | Twenty-eight Material Design Icons are named on the canvas and the build "rasterises the chosen twelve", which are not listed, so the twelve are still owed as files. An `ImageItem` carries no tint, which the format research verifies, and a lamp therefore owes one file per colour it can be drawn in: nineteen in all, being a dark file for each of the twelve and a lit file for each of the seven that the drawing or a source gives a colour to. They are named `telltale-<lamp>-<state>` in `packages/dash/src/zones/telltales.ts`, and the rank draws whichever of them `design/assets.ts` holds, so the lamps gain their pictograms in the commit that brings the artwork together with its Apache 2.0 licence and the notice naming Pictogrammers. Until then a lamp is its box. |
| What lights a telltale | Three of the twelve have a source and nine do not. The engine reads the `EngineWarnings` bits for water temperature and oil pressure, the fuel can reads the same low-fuel threshold every other light OpenDash drives reads, and the speed limiter reads `PitLimiterOn`. Nothing lights the two tyre lamps, the wiper, the car above the wavy tracks, ABS, ESP, the battery, the tyre pressure warning or the door: iRacing publishes no wiper, stability, tyre pressure or door state at all, `dcABS` is the level the driver has dialled in rather than an intervention, and a battery lamp reading the raw voltage would need a threshold nobody has chosen. **The nine are built and left dark**, because a dark lamp asserts nothing whereas a lamp bound to a property that means something else asserts the wrong thing. Which property lights each of them is the author's to answer, and two further answers are owed with it: the colour of the engine lamp, which the artboard draws dark and which is taken as danger red here because both bits it reads are failures rather than advisories, and the source of the count the artboard draws in the wiper's corner. That count is recorded in `telltales.ts` and is not drawn, for the reason the relative page's country flag is not drawn. |
| Band D's value size | Every 60 and 58 px band draws its page values at 34 px over a 13 px label, 5 px apart. WPF's line box around a 34 px value runs 60.6 px from the top of that label, so the band clips it by a pixel. **The value shrinks** — 32 at 60, 30 at 58 — because a clipped numeral reads as a rendering fault. The band would have to grow, or the drawing come down; the 54 and 56 px bands draw 24 and are honoured exactly. |
| The face with no rev bar | #189 offered three answers, namely leave the gap, reclaim it, or give the band to something else, and said the artboards would choose. Since the second pass of 15 September the FaceVariants sheets do draw the third state and both arrangements beside each other, so this row no longer reads as it did. What the rev-bar-off drawing still carries, however, is the rev bar itself: an 822 × 28 rectangle at (14, 6) on the 850 sheet, a 576 × 24 one at (12, 6) on the 600, underneath a bar that has already risen into its room. **The caption is taken over the rectangle**, and `faceItems` leaves the well and the segments out entirely rather than hiding them; `Plugin.dc.html`, for its part, still reads "the rev bar stays". Reclaim is taken for the room, because the gap reads as a mis-crop and on the nano it is a ninth of the screen, and the rectangles in §1 remain derived by one rule and remain the thing to delete when drawn ones arrive. |
| The slot counts in the titles | `canvas.json` titles the 1920 × 480 artboard "MVP · 12 slots" and the 1280 × 720 one "wheel screens · 12 slots", while what each draws underneath is the five-part zone face [ADR 0006](../decisions/0006-the-zone-face.md) settled, and `Dash.dc.html` keeps `.slotbox`, `.card` and `.grid4` in its stylesheet with nothing using them. **The drawing is taken**: a `ZoneLayout` declares no slot count at all, and twelve matches nothing on the 1280 × 720 body either, whose bar draws eleven readouts and whose band draws ten and three lamps. The twelve-slot package does still build beside the zone face, since `LAYOUTS` keeps `layout1920x480` and `build.ts` walks both lists until #146 retires the card path. |
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
| Lap times at `tall narrow` | The catalogue draws two times at 34 px in a 274 × 300 zone and leaves 234 px of it empty. **Four are taken**, one per line and grown to 46 px, because the box the drawing answers is a real zone on the base face and a driver reads it at arm's length. The redraw and the same pass over the other twenty pages are [#327](https://github.com/xorob0/OpenDash/issues/327) and the twenty tickets under it. |
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
| The flag once it has settled | No artboard draws a flag anywhere but across the whole band, so the block at each end that [§6](#6-band-d--eight-pages) describes is an addition rather than a reading of a drawing. **It is taken** because the alternative is the case [#380](https://github.com/xorob0/OpenDash/issues/380) opens with, namely SAFETY CAR over an unreadable fuel page for the length of a caution. On the four faces with corner blocks the addition is nearly a rectangle the canvas does draw, though not exactly one: it covers what a corner block holds and, the corner width being those two fields plus the band's side padding plus the room the zone letter stands in, the padding and the letter as well, so band D has no **D** for as long as a flag is out. §6 says why that is the cheaper of the two prices available. On 850 × 480, 800 × 480, 800 × 286 and 600 × 686 there is no such rectangle and the flag keeps the side padding, which is 16 px of colour at each end and 12 in portrait: enough to say a flag is still out and not enough for a word. **The word is what a colour cannot carry**, so on those four faces a settled flag names a family and not a member: DISQUALIFIED, BLACK FLAG · FURLED and BLACK FLAG are one outlined sliver, DEBRIS and YELLOW FLAG one yellow sliver. Widening it there means taking room from the page, and the 600 × 686 fuel page has 6 px of slack, so the canvas owes either a drawn settled form for those four faces or the judgement that colour alone is enough once the name has had its three seconds. |
| The relative's row count | The catalogue draws six rows at `wide` and at `grid`, eight at `tall narrow` and eleven at `tall`, and the build divided the body by the row instead, which reached thirteen in zone C of the 1280 × 720 face and fifteen in its second arrangement. **A declaration is taken**: at most five cars either side of the player, eleven rows, and at least one either side. The question the page answers is *is the car behind me going to be there at the next corner*, which is the two either side; the third, fourth and fifth are the traffic a driver is about to be in, and past the fifth a row is not read. The counts the canvas draws are all inside that window, so what changes is only the two boxes that were outside it. The canvas owes the count at `tall`, and with it a redraw at `tall narrow`, whose eight rows the build reaches at 28 px in a 290 px body and does not at the row the catalogue draws them with. |
| The relative's row height | The canvas states one row height per shape — 34 px, and 28 in the narrow zone — and the build stretches it. A declared count in a box that holds more leaves the difference somewhere, and `table()` leaves it above and below the block, `justify-content: center` being what every list body on the catalogue carries: eleven rows of 28 px in the 560 px body of the 1280 × 720 face's second arrangement is 328 px of list and 232 px of nothing. **The row fills the body instead**, and the type does not follow it past the 38 px row the canvas's tallest table draws, so what is bought above that is space between the rows and never size. It also stops short of any stretch that would cost the name a letter, the name column being the only one that flexes: **ten characters is the line**, `Liam Byrne` being what the default format makes of the entry every artboard and every trace carries, and a box whose own declared row holds fewer is held to what it had rather than to ten. Five boxes therefore keep a row shorter than the one that would fill them — the 469 px bodies of zones B and C on the 1280 × 480, 1280 × 400 and 1280 × 720 faces stay at 37 px rather than reach 38, 42, 45 and 49, where promoting the car number would have taken the column from 138 px to 122 and the budget from ten characters to nine; and the 800 × 286 face's second arrangement keeps 33 rather than 36. Zone C of the 1280 × 720 is where that shows most: 427 px of rows in a 560 px body, with the remaining 133 above and below the block. The canvas owes a row height per box rather than per shape, or a statement that the pitch is the build's to choose. |
| The driver name's size on a list row | The catalogue draws it at 13 px in every row of every list at every shape, under a gap drawn at 34. **15 is taken from the 34 px row up**, which is what the face labels at everywhere else and what the pit wall's own boards already draw a name at; `density.ts` calls 13 the floor rather than the size, and a column that says *who* sitting on the floor of the ramp is what [#339](https://github.com/xorob0/OpenDash/issues/339) is about. **13 stays in the 28 px row, and the reason is the catalogue rather than the count.** This row first said 15 would cost the narrow column three characters, "seven at 13 and five at 15 in the 82 px the 850 × 480 face gives it", and that was wrong everywhere it applied: the three boxes the 28 px row is handed are 82, 77 and 57 px of name column, where 15 costs one character, one character and nothing — seven against six, six against five, four against four. One character does not buy a second divergence from a catalogue that draws 13 at every shape, so the catalogue keeps this one. The six counts are pinned in `tables.test.ts`, a documented number nothing runs being a number that rots. |
| The driver column's three-letter code | Every list on the catalogue and on the opponents page draws the driver as three upper-case letters, `KLX` and `MOR` and `TSA`, at 13 px and at every shape. **A name is taken instead.** The code was built as `left(name, 3)`, which makes Liam Byrne `LIA` and Hannah Fischer `HAN`: it identifies nobody and collides for any two drivers who share a first name, and it is drawn on the one page a driver reads to answer *who is that*. What the build draws is one of four formats the rig chooses between — the full name, `L. Byrne`, `B. Liam` or `Byrne Liam` — cut in the expression to the characters the column holds and closed with an ellipsis where it was cut, since WPF clips rather than truncates. The canvas owes the redraw at all four shapes, and with it an answer to the width, because **on the three narrow faces no format fits whole.** The budget per box, in characters of the widest glyph the name face draws, is 7 in the 82 px zone C of the 850 × 480 face, 6 in the 77 px of the 800 × 286 and 4 in the 57 px of the 800 × 480; 8 on the companion's portrait page; 10 or more everywhere else. `L. Byrne` is eight, so even the shortest of the four ellipsises at `tall narrow` — `L. BYR…` at 850 × 480 and `L. BY…` at 800 × 286, shouted by the row below and cut here — and at 800 × 480 the four formats draw `LIA…`, `L.…`, `B.…` and `BYR…`, which is one glyph more than the `LIA` this row deletes for two of them and the same three for the other two, a cut that lands on a space losing the space with it. `driverNames.test.ts` evaluates those four. **The column stays regardless**, a list of gaps belonging to nobody being [§11](#11-a-field-that-is-not-there)'s own failure and what `NEVER_DROPPED` refuses. The lever the row still has is the position: the gap's 92 px is the canvas's floor over content that needs 79 at 24 px, so dropping a decimal would not narrow it, whereas giving the name the position's 40 px buys 11, 11 and 9 characters at the three faces. It is not taken, the position being what says whether the car behind is racing you or lapping you, and a third column dropped from a list being the canvas's decision rather than the build's. What the canvas therefore owes at `tall narrow` is either 93 px of name column — the eight characters the shortest format needs at 13 px — out of zones 249 to 274 px wide whose bodies are 225 to 250, or a column set with one column fewer in it. [#385](https://github.com/xorob0/OpenDash/issues/385) decided the formats; [#149](https://github.com/xorob0/OpenDash/issues/149) wants the same width again for a licence badge and a rating. |
| The driver name's case on a list row | Every artboard writes the driver column in the sim's own mixed case, `Liam Byrne` and `Hannah Fischer`, the name being the one piece of prose on a row of labels. **Upper case is taken at every size a list draws a name at**, and the reason is a glyph rather than a preference. Barlow's `i` is a stem with its dot floating 0.080 em above it, which is 1.04 device pixels at the 13 px of the 28 px row; under two device pixels there is no pixel row the gap is certain to fall wholly inside at any sub-pixel phase, so the raster may shade the row above and the row below at partial coverage and bridge them. Four of the nine names in zone C of the VM's 850 × 480 face came back welded — `Llam B…` for Liam Byrne, `NIna H…` for Nina Hartmann, `Sofla …` for Sofia Rossi, `Henrlk…` for Henrik Solberg — and since `i` and `l` are the same height to within 0.017 em that is a different letter and not a blurred one, in the one column of the one page whose whole job is to say *who*. The three remedies answer to the same measurement and `advances.test.ts` holds all of it, read back out of the bundled outlines: **a heavier weight closes the gap** rather than opening it, 0.057 em at Bold against 0.080 at Medium, a fatter stem and a fatter dot being drawn into the same vertical; **a bigger size costs letters and still does not clear the bound**, 15 px buying 6, 5 and 4 characters in the three narrow boxes where 13 buys 7, 6 and 4; **upper case costs nothing**, no *unaccented* upper-case letter in any bundled face being drawn in two pieces at all, and the budget being counted in characters of the face's *widest* glyph, so shouting a name changes no budget anywhere and the six counts in the row above are the same numbers after it as before. **What it buys is the substitution rather than the construction**, which is the limit worth writing down: an accented capital is a mark floating over a letter, the same shape the `i` failed at, and four of them are tighter than it — `É`, `Å`, `Í` and `Ö` break at 0.064 to 0.076 em in the name face against the `i`'s 0.080, every one under two device pixels at 13 px and at 15 — so RÄIKKÖNEN may still come back with an umlaut welded to its A. That is a letter drawn badly where the `i` was a name read wrongly, a welded acute leaving `É` an `É` where a welded tittle left `Liam` a legal `Llam`, and there is no tighter bound to reach for, these being the marks the bundled faces draw. The line is therefore a size, `dottedLetterSize(NAME_FACE)`, which is 25 px — where the gap first reaches two device pixels — and every name any list draws is under it: 15 from the 34 px row up, 13 in the narrow zone's, 12 on the opponents page. So every list shouts, the team name a rig may show in a driver's place with them, and the player's own `YOU` stops being the only shouted thing on the row. The canvas owes the redraw, or a ruling that the driver column may stay prose at a size no list is drawn at. [#339](https://github.com/xorob0/OpenDash/issues/339) is where it was found. |
| What a name is corrected to before it is formatted | #385 asked for a bracketed prefix from the sim's entry list to be stripped and the words title-cased before any format is applied. **Neither is built, and neither can be.** SimHub's NCalc has no `indexof`, `substring` or `length`, so nothing in an expression can find the closing bracket of a prefix whose length varies; and its `tcase` is .NET's `TextInfo.ToTitleCase`, which lower-cases the rest of every word it capitalises unless the word is entirely upper case, so it turns McDonald into Mcdonald and leaves a shouted name shouting — which is the case it would have been reached for. A name is therefore drawn with the sim's own spelling and its own prefix, upper-cased by the row above and corrected in nothing. The two are not the same act: `ucase` is total, so there is no name it can get wrong, where `tcase` guesses at which letters a word wanted. Both would be corrections the plugin could make, and it cannot either: a name belongs to a car on a row and the plugin publishes no per-row property. |
| A short box's ranks | `keepsSecondaryRanks` says a short box keeps one rank, while `archetypeOf` hands a wide short box the `grid` answer, which keeps two. The code follows `archetypeOf`, and the helper is unused. Either the short boxes the build produces get a fifth declared answer, agreed with the canvas, or the helper goes so that one rule governs. |
| The tyre temperature's unit | Every drawing of a tyre corner writes the temperature as `84°`, a degree sign and no scale, beside a pressure that does carry `psi` and a tread that does carry `%`. **The scale is drawn**, `°C`, `°F` or `K` by the driver's own `TemperatureUnit`, because the sign alone says that the figure is a temperature and not whether 85 is a cold tyre or a cooked one, which is what [#384](https://github.com/xorob0/OpenDash/issues/384) was filed about. The mark leaves the value's cells and follows it as a unit the way the pressure's does, flush against the figure at the five pixels the drawing leaves between a reading and its unit -- a two-digit temperature would otherwise carry its scale a whole cell further out than the corner beside it reading three -- which costs a zone's row eleven pixels, taken off the drawing beside it under rule 18, and saves a companion's four, the degree having had a 64 px cell there and the mark taking a 13 px one. No reading is shed anywhere the build draws: the narrowest corner the catalogue cuts is 107 px across against the 75 a labelled temperature needs. The canvas owes the sign a scale, or a ruling that a tyre page may assume Celsius. |
| The compound chip's label | The same ticket asked for a label on the compound as well, and **no word is drawn beside the chip**, because nothing on the axle line has room for one: the chip is 80 px wide and centred there, the only band free of the two inboard drawings is the column gap, 18 px in the grid and 28 at the widest, the chip already overlaps each front tyre by about 30 px, and `COMPOUND` at 66 px more would cover a third of each of them. **What names it is a caption under the grid**, beside the one the canvas already writes about the tick, and the caption line is where the disagreement now sits. It holds three sentences at 1007 px and two below that, so a third caption costs one of the canvas's two, and **the tread's is the one given up**: its columns stand beside a figure already carrying its own per cent, where the tick and the compound carry nothing else, which is the test #384 sets. So the pit wall's 1039 × 255 tyre zone draws all three, the companion page and the two 769 px face zones draw the tick's and the compound's where they drew the tread's and the tick's, and the twelve narrower boxes that draw the chip name the tick alone. What the canvas owes is either the room for a label on the axle line, or a shorter set of sentences, or the ruling that a chip drawing a word speaks for itself — the class chip on both leaderboards is an unlabelled word for that reason, though a one-letter compound is a poor word. Where PARTS sheds the footer outright, at `tall narrow` and on the compact faces, the tick is drawn with nothing naming it and the chip is not drawn at all. |
| Band D's four tyre temperatures | The catalogue draws them as one line of equal cells under a single `TYRES °C`, which is what the build lays, and says nothing about how a reading shorter than its cell sits in it. **Each is centred in its cell**, where every other value on the band is set against the left edge of its own. Nothing on that page labels the four, so the reader maps them onto the car by the order they are in and the set has to read as evenly spaced; set left in cells cut for three digits, the VM photographed `52   113 88   95`, which at 34 px is 24, 8 and 24 pixels of ink apart, the middle two crowding into one group. The three alignments were measured: left gives 24, 8, 24 and right gives 8, 24, 24, sixteen pixels between the widest gap and the narrowest, which is a whole digit cell; centred gives 16, 16, 24, and is the only one of the three that puts each figure's middle on the 56 px pitch the cells are laid on. The price is that a corner crossing a hundred moves half a cell, which is accepted here and refused for a field with a unit after it: this is a set read as a set, and nothing follows a reading that the shift would open a gap in front of. The canvas owes either the same centring on the sheet or the ruling that a crowded pair is what it wants. |
| A unit and the budget its figure is cut from | The rule [#384](https://github.com/xorob0/OpenDash/issues/384) gave the tyre corners is now every surface's: a follower sits beside the figure and not at the end of the cells the figure is laid in. What the VM photographed is what it is for -- band D's `MARGIN −4` carried `MIN` four empty cells out, nearer `EST. LAPS` than its own number, and `FUEL 30.35` carried `L` one cell out -- and nothing in the drawings asks for it: every artboard writes its readings flush, `84°`, `30.35 L`, `81 KM/H`, at the one gap its sheet gives. **The figure keeps its cells** and only the mark after it moves, because right-aligning the value would trade one jitter for a worse one, and **the row is still measured at the budget**, so no width, no shedding and no page order moves with this. Every bound value that carries a follower now declares how wide it really draws, in `second/drawn.ts`, and one that declares nothing is refused where it is drawn rather than placed at the end of its budget. `followerPlacement.test.ts` holds both halves, the design-time gap and the runtime binding, for every follower of every package the build emits. |
| Band D's energy budgets | D2's **Energy** and **Refuel** are cut for three bare digit cells, `CHARS.temperature`, and `notAvailable` formats every reading on the page `0.0`, so a car that publishes 68 per cent draws `68.0`: four characters and a point in a box that holds three digits, which WPF clips. The estimate and the per-lap figure beside them are cut for `CHARS.consumption`, four cells and a point, and hold `100.0` exactly. **Widening those two to match is the answer and is not taken here**, because two fields gaining 23 px each changes what the rank sheds on the narrow bands and that is a page redraw rather than a placement fix. What is taken is that the `%` after each **stops at the budget**: a declaration wider than a field's own cells binds the mark outside the region the box was measured for, which `drawnWithin` now refuses at build time, so the mark sits at the end of the ink the driver can see rather than 25 px past it. |
| Zone A's centred row | Zone A is the one place the build still draws a mark at the end of a budget, and the reason is that its rows are centred as a group: a speed is cut for `299` and the rpm for `123,456`, so `81 KM/H` and `5,851 RPM` leave one and two empty cells inside the group, and pulling the mark in moves the ink off the column's middle by half of that. **The cells are kept**, because centring on the ink instead costs the portrait face something real: at 600 × 268 the row is fitted to the column and has two pixels of slack where it would need thirty-one, so A3 would have to drop its rpm or step its speed down a size. The canvas owes the ruling -- a row centred on what it draws, and a hero number that shifts as it gains a digit, or the hole beside a short reading. |
| The bar's right-hand end | A field of the bar's left end is drawn from its own edge and its denominator now follows the figure, so lap 4 and lap 16 both read `/ 32` at the six pixels the artboards give. A field of the right end is laid from the padding inwards instead, the denominator flush against it and the value right aligned one gap in front, which is what the artboards draw and which keeps the gap constant as the figure changes. What stands between the two runs of ink there is twelve or thirteen pixels rather than six, being the narrow cell `/ 32` leaves unused in the four-digit-and-a-special budget `/ 100` needs. **The artboards' arrangement is kept**; closing the gap means either a budget the denominator fills or a denominator drawn from its own left edge, which leaves the padding short on a one-digit field size, and the canvas has decided neither. |

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
| The flag over the band, in six colours | **Built, and wider than the sheet asks**: `flagStrip` draws all fifteen conditions of `FLAG_CATALOGUE` over band D's rectangle, in the three shapes of the alert catalogue, where the sheet draws the six SimHub normalises. The black family keeps a `surface.base` ground rather than its own token, which is the ink. |
| A full-screen flag over zones B, A and C, with `OpenDash.FlagFormat` set to band or full | **Built.** The property carries a face's prefix, as the zone settings do, and it is declared, mirrored, defaulted to `band` and offered on the screen's own pane. It did not need the further pair of arrangements this row once predicted: `components/flagFull.ts` draws one opaque block over the body rectangle, derived from the layout, and `face.ts` gates the band group and the block against each other, so one screen carries both. `flagFormat.test.ts` holds the block against the sheets at all eight sizes and in both rev-bar arrangements. The block reads band D's own fifteen-condition catalogue through the band's own expression, and names each condition in a word short enough for a block measured on the longest of them, which is MEATBALL. |
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
setting iRacing has no property for is worse than a narrower strip. Band D's pages are laid out
under the same mode, although none of them carries an optional field today: the car page removed an
oil pressure the sim does not wire rather than drawing 0.0, which is a reading and a wrong one, and
it gave those readings up when D8 became the telltale rank.

**The lap review is the largest of the boxes drawn over zone A**, at `min(1200, face width)` by 160,
centred on the hero rectangle and clamped to the face, and it is pushed last, so while it is out it
covers the lap-time pop-up and the change notification, whose frames it contains in both directions.
The ranking is by geometry rather than by an exclusion chain, because a chain would have to reach
into the pop-ups, whose conditions know nothing of a face and so cannot ask which face's setting is
on. What it never covers is band D, the rev bar well, the bar of settled values or the limiter
banner, the pit alerts being pushed before the whole transient family. It sheds the fuel pair, then
the driver line and its sector strip, before it shrinks anything, and the two deltas are the floor:
a review shed down to a lap time alone would say less than the pop-up it replaces. It is off by
default, for the reason the band is the default flag format.

A change of setting is announced the same way, and one token of its group is deliberately unread:
`indicator.changeNotification.settleFrames` exists for a rotary swept through its positions, and
SimHub's own `changed()` window already collapses a sweep into one notification, so nothing settles
a value that the window has not settled already.

**A telltale that is unlit keeps its place and is drawn dim.** A lamp coming on is then a change of
colour and not of layout: one that vanished and returned would move every lamp beside it at the
moment the driver most needs to read them. DRS, push to pass and the spotter sit in the band's
right-hand corner and behave this way, and so does the twelve-lamp rank of page D8, which is the
case the rule was written for. A lamp that nothing publishes a state for is likewise drawn dark in
its place rather than left out of the row, since a row of eleven would say something about the car
that is not true.

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
[#327](https://github.com/xorob0/OpenDash/issues/327) is one ticket per page: what each of the twenty-one
would have to change to put the reading a driver needs first.
[second-screens.md](../second-screens.md) is the companion and the pit wall, which share the
twenty-one pages.
[research/simhub-dash-format.md](../research/simhub-dash-format.md) is what SimHub actually does,
and is the place to check before guessing.
