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
  34 px and do not at 46 px, so the rank wraps to one column on its way up. `columnsAt` is still the
  declaration of what a shape may hold; it is not a second mechanism.
- **A stack already too tall for its box does not grow.** It has nothing to spend, and rule 17 is
  about to take a row off it.

The room a grown stack may take is its box less its own tail at each end, not the flat two pixels
`ROW_TAIL` reserved: a WPF line box runs about a tenth of the font size below the row it sits on,
which is two pixels at 24 px and seven at 75 px. A constant was enough while every value was a ramp
size in a box with slack. A value grown into its box is exactly where it stops being enough.

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
catalogue of ten:

race time · lap and total · time left · clock · simulated time · position · class position ·
incidents · air temperature · track temperature

Strength of field was the eleventh and is not built, because SimHub publishes it in no form at all
and OpenDash does not compute ([ADR 0009](../decisions/0009-does-the-plugin-compute.md)). It is
named here only so that a reader of an older draft knows where it went.

The default is Race and Lap on the left, Position and Class on the right.

Between them is the **car settings strip**: slip, TC, cut, bias, ABS, map, diff. It draws what the
game exposes and **hides what it does not**, because a strip drawing an empty box for a setting
iRacing has no property for is worse than a narrower strip.

It also gives up cells where the width is not there. The two ends are laid out from their own edges
for the widest entry the catalogue holds, so what the strip gets is whatever is left, and on a
narrow face that is not seven cells:

| face | cells |
|---|---|
| 1920 × 480, 1280 × 480, 1280 × 400, 1280 × 720 | all seven |
| 600 × 686 | slip, TC, bias, ABS — one field per end leaves more room than two |
| 850 × 480 | TC, bias, ABS |
| 800 × 480 | TC, bias |
| 800 × 286 | there is no bar |

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
| 1 | Lap times | `last` · `sessionBest` · `yourBest` · `laps` · `estimated` · `delta` | `last` · `sessionBest` · `yourBest` · `delta` | `last` · `sessionBest` · `yourBest` · `delta` | `last` · `sessionBest` · `yourBest` · `laps` · `estimated` · `delta` |
| 2 | Delta | `delta` | `delta` | `delta` | `delta` |
| 3 | Sectors | `yourBest` · `last` · `sessionBest` | `yourBest` · `last` · `sessionBest` | `yourBest` · `last` | `last` · `sessionBest` |
| 4 | Speedo | `speed` · `rpm` · `redline` | `speed` · `rpm` | `speed` · `rpm` | `speed` · `rpm` |
| 5 | Fuel | `level` · `time` · `toAdd` · `lastLap` · `thisLap` · `average` · `lapsLeft` | `level` · `time` · `toAdd` · `average` | `level` · `time` · `toAdd` · `average` | `level` · `time` · `toAdd` · `lastLap` · `thisLap` · `average` · `lapsLeft` |
| 8 | Pit view | `refuel` · `pitTime` | `refuel` · `pitTime` | `refuel` · `pitTime` | `refuel` · `pitTime` |
| 9 | Car settings | `car` · `tc` · `abs` · `bb` · `mix` · `arbFront` · `arbRear` | `car` · `tc` · `abs` · `bb` · `mix` · `arbFront` · `arbRear` | `car` · `tc` · `abs` · `bb` | `car` · `tc` · `abs` · `bb` · `mix` · `arbFront` · `arbRear` |
| 11 | Session | `type` · `position` · `class` · `lap` · `timeLeft` · `lapsLeft` | `position` · `class` · `lap` · `timeLeft` | `position` · `class` · `lap` · `timeLeft` | `type` · `position` · `class` · `lap` · `timeLeft` · `lapsLeft` |
| 14 | Leaderboard | `pos` · `num` · `name` · `class` · `gap` · `best` · `last` | `pos` · `num` · `name` · `class` · `gap` | `pos` · `name` · `gap` | `pos` · `num` · `name` · `class` · `gap` |
| 15 | Relative | `pos` · `num` · `name` · `class` · `gap` | `pos` · `num` · `name` · `class` · `gap` | `pos` · `name` · `gap` | `pos` · `num` · `name` · `class` · `gap` |
| 16 | Opponents | `ahead.gap` · `ahead.name` · `ahead.num` · `ahead.class` · `ahead.detail` · `behind.gap` · `behind.name` · `behind.num` · `behind.class` · `behind.detail` | `ahead.gap` · `ahead.name` · `ahead.num` · `ahead.class` · `ahead.detail` · `behind.gap` · `behind.name` · `behind.num` · `behind.class` · `behind.detail` | `ahead.gap` · `ahead.name` · `behind.gap` · `behind.name` | `ahead.gap` · `ahead.name` · `ahead.class` · `ahead.detail` · `behind.gap` · `behind.name` · `behind.class` · `behind.detail` |
| 18 | Stint | `stintLaps` · `stintTime` · `completed` · `driver` · `stops` · `lastStop` | `stintLaps` · `stops` · `lastStop` | `stintLaps` · `stops` · `lastStop` | `stintLaps` · `stintTime` · `completed` · `driver` · `stops` · `lastStop` |

Pages with nothing to shed, and why:

- **Energy** (`energy`) — one line of prose: iRacing publishes no virtual energy.
- **Tyres** (`tyres`) — four corners cut from the box; rule 18.
- **Inputs** (`inputs`) — three traces and their bars, cut from the box; rule 18.
- **Radar** (`radar`) — the cars beside you, cut from the box; rule 18.
- **Track** (`track`) — the map, cut from the box; rule 18.
- **Gear** (`gear`) — the gear, cut from the box; rule 18.
- **Lap history** (`lapHistory`) — three columns and as many rows as fit; there is no fourth to drop.
- **Damage** (`damage`) — one line of prose: iRacing publishes no damage.
- **Track rivals** (`trackRivals`) — one line of prose: SimHub times sectors, not segments.

A drawing is cut from its box rather than shed (rule 18), and a page that says it has no data is
one line of prose with nothing in it to drop.

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

**A page sheds its last field before the rank overflows**, with nothing spread to fill. The rank is
packed and centred in what the corners leave, never in the whole band. No shipped page reaches that
limit: the widest catalogue entry holds five fields, and five fit at 600 × 56, which is the
narrowest band. The shedding rule is therefore a guarantee about a page that grows, not a
description of one that exists.

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
| The face with no rev bar | XOR-138 offered three answers — leave the gap, reclaim it, or give the band to something else — and said the artboards would choose. The canvas still draws neither the third state nor the face without a rev bar, and `Plugin.dc.html` still reads "the rev bar stays". **Reclaim is taken**, because the gap reads as a mis-crop and on the nano it is a ninth of the screen; the rectangles above are derived by one rule and are the thing to delete when the artboards arrive. |
| Lap times at `tall narrow` | The catalogue draws two times at 34 px in a 274 × 300 zone and leaves 234 px of it empty. **Four are taken**, one per line and grown to 46 px, because the box the drawing answers is a real zone on the base face and a driver reads it at arm's length. The redraw and the same pass over the other twenty pages are [readability-pass.md](readability-pass.md). |

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
