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

The anatomy below is the default theme's output rather than the shape every face must have. What a
theme declares is a function from a face size to a set of named regions, each of which carries a
role and a rectangle (`packages/dash/src/themes/anatomy.ts`), and the roles are the parts the face
knows how to fill: the rev bar and its well, the bar, zones B, A and C, band D, the rectangle the
pit alerts are drawn in, the hero the pop-ups, the change notifications and the lap review are
centred on, and the body a full-screen flag takes. The default theme returns the rectangles of
`packages/dash/src/zones/faces/*.ts` as they are, so the tables of this section remain its data,
whereas another theme may move a region or go without the bar while every zone keeps its whole
catalogue (ADR 0015).

Under the default theme, the same five parts are on every rectangular face, and only their sizes
change.

| | |
|---|---|
| **Rev bar** | Shift lights in a recessed well, full width, never moves. It can be turned off entirely, in which case the face is drawn in its second arrangement and the well's room goes to the zones. |
| **The bar** | What the car is set to and where the session is: two fields at each end that a driver may swap, one per end at 600 × 686, and a settings strip between them that hides what the game does not expose. |
| **Zone B** | A page from the catalogue of twenty-two. |
| **Zone A** | The one read by reflex: gear, gear and speed, speed, or the track. |
| **Zone C** | A page from the same catalogue of twenty-two. |
| **Band D** | Fuel by default, and seven more pages that suit a wide short band. A flag takes the band over for three seconds when it comes out, and then settles into the block at each end and gives the page back until it clears. |

Zone A is **a narrow column holding the gear**, not a third of the screen holding one digit.

There is **no row of page dots**, and no zone letter either since [#708](https://github.com/xorob0/OpenDash/issues/708): the page name says what is showing.

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

The rule, in `regionsWithoutRevBar`, which is a function of the regions and therefore applies to a theme's as it does to the default's:

- What is given back is **the well and the gap under it**. The one to four pixels above the well are
  not: that is the top margin of the face, and giving it back would put the bar's labels' line box a
  pixel above the canvas, where WPF clips the row.
- The bar rises to where the well began. The zones that **start the body** rise with it and grow by
  the same amount, so they keep their bottom edge. In portrait only zone A starts the body, so B and
  C keep both their rectangles and their zone dashboards.
- Band D does not move: it is measured from the bottom edge and the bottom edge has not changed. The
  pit limiter moves with zone A, because that is what it is drawn over; under a theme, it moves with
  whichever zone holds its top left corner.
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

Zones B and C carry a **22 px header line**: the page name in the label style at the header's left
padding, and the counter at its right. Zone A has none, which is the one thing the model leaves open
— see §8. The artboards open the line with the zone letter and 8 px before the name, and the build
drew it until [#708](https://github.com/xorob0/OpenDash/issues/708), where it was judged a label
nobody reads and removed from every theme, the default included; the name has taken its place.

The frame around it is the artboards' and not the pit wall's. A face zone is padded `6px 12px`, its
header row is 22 px of 15 px labels in `color.text.label`, and 4 px separate that row from the page,
which leaves a 769 × 314 zone a body of 745 × 276 and the nano's 269 × 194 one of 245 × 156. The pit
wall's zones keep the 28 px row over 16 px of padding `PitWallZones.dc.html` draws them with, so
`zoneFrameMetrics` takes a chrome beside its density and the two frames no longer share one table.

Band D has no header and counts no cycle, and since #708 it draws no letter either, so the band
opens on its page. The room the `D` stood in at the band's left is still reserved, because the
rank, the corner blocks and the shedding below are read off artboards that lay the band out around
it; giving that room to the page would let the 850 × 480 fuel page keep the last lap and the
600 × 686 band all twelve telltales, which is a redraw for the canvas to make rather than a
consequence of dropping a label.

### The pit limiter

Drawn over zone A as a full-width white banner with dark text while the limiter is on: at 1920 it
is 824, 111, 272 × 30. It is not a page and it is not part of the catalogue; it covers.

**Five pit alerts share that one rectangle, ranked among themselves and not against band D.** Engage
the limiter, disengage it, the limiter on in the lane, the ignition off and the engine off are one
ordered list, of which at most one is ever out, each carrying its own test of whether the car is in
the lane rather than the list carrying one. The last two are the lane's half of a pair: out of the
lane the same two readings are the alert catalogue's first two entries and draw on band D, so a car
stalled on the circuit is told too, and the one condition is never drawn in both places. They are deliberately **not** ranked under the flag: the
two draw in different rectangles and never contend, so gating the pit list on "no flag is showing"
would blank the limiter band under a full course yellow, which is precisely when the pit lane is
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
three: the height of the box, the width of the box, and a ceiling, which on a face is ×2.2 and on
the companion and the pit wall is the next size up its density ramp.*

Rule 17 is one half of a thought and this is the other. Rule 17 says what a page does when its box
is too small; rule 20 says what it does when the box is too large, which on this product is the
commoner case — zone B of the 850 × 480 face is 274 × 328 and lap times was using 58 px of it.

The one factor is the part that makes this filling rather than stretching. A rank grows by one
factor, the whole stack at once, so every size on the page moves in proportion and what comes out
is the same drawing larger, with its hierarchy intact, and never a drawing pulled to the shape of a
rectangle. Four consequences worth knowing:

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

**Why the ceiling is ×2.2 on a face** ([#330](https://github.com/xorob0/OpenDash/issues/330)). It
was the next size up the ramp everywhere until then, defended as what kept filling distinct from
scaling. It did not do that job, since the one factor does, and all it decided was how far. What it
decided was about ×1.35 in every box, because it is the smallest step any size on the page takes
and every page mixing 46 and 34 takes the 34-to-46 one; and exactly ×1 on a page whose lead was
already at the top of its ramp, which froze the delta, the speedo, the fuel, the stint and pit view,
the five pages whose one big number is the point of them. Two of those got past it by working round
the rule rather than through it — pit view cuts its numbers from the box and the stint draws its
three leads at one size — and a ceiling that a page has to be redrawn to get past is the wrong
ceiling. At the 1280 × 720 face's 445 × 516 zone it stopped lap times at 62 px with 170 px of the
box unspent.

The canvas answers the question the other way. Each `FaceVariants` sheet embeds the catalogue's
drawing of every page, reflows it to that face's real zone and grows it "as one, hierarchy intact,
until it meets the width, the height or a ceiling of ×2.2 (rule 20)", chipping the factor each page
reached, from ×1.02 to ×2.2. That is this rule with another third edge, the canvas is the source,
and so a face takes it. What stops a page on a face is now almost always its box, which is the edge
the readability pass is about.

The companion and the pit wall keep the ramp step, because their artboards are drawn at the size of
the screen they are on. A page there is already the drawing of its box, and what growing has to
spend is the difference between the artboard's module box and the build's, a matter of pixels. The
×2.2 answers a zone the catalogue never drew, which is every zone of every face, since the catalogue
draws a page at four archetype sizes and a face hands it a fifth. `grownAtMost` in `density.ts` is
the one place both answers live, and the opponents page, which grows its gap by the same rule in a
loop of its own, reads it too. Pit view reads neither, being cut from its box by rule 18 at the
sizes the catalogue draws it, which is also how the sheets treat it: a page with a picture in it "is
cut from its box (rule 18) and is not grown", and none of them chips it a factor.

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

The same answer covers a **mark**, which is what a reading draws in place of its value in a state
that has no figure: `∞` where a session has no clock (#439). Measured it is 0.651 em in SemiBold
against a 0.47 em digit cell — a third over, which is the same thing that bans the eight glyphs
above, four of which (`%`, `@`, `W` and `m`) are wider still — so the mark is a
proportional run in the value's own box, on the value's line and at the value's size, with the value
hidden while it is drawn. A clock and its mark are therefore two items in one place and never both
drawn — monospace is a property of the item, and no binding makes a cell wide for one reading and
narrow for the next.

And it covers a **meridiem**, the `AM` or `PM` a twelve-hour clock writes after its digits when the
rig asks for one ([#324](https://github.com/xorob0/OpenDash/issues/324)). Both halves carry an `M`, so
neither goes in a cell; the digits stay a value — `12:59` is the same four digits and a colon as
`23:59`, so one budget, `CHARS.timeOfDay`, holds either format — and the word follows them as a
proportional run, the way a unit follows its figure, drawn only while `OpenDash.ClockFormat` reads
`12h`. What a setting cannot do is resize a box at runtime, so **every surface measures its clock
with the word** and then gives the room back when the word is not there: the digits of a clock laid
from the right move up to the edge, the pit wall header's groups move up behind them, and the idle
screen's digits stay centred where the twenty-four-hour clock is and let the word hang after them. A
twelve-hour hour is one digit or two, so the word is placed after the figure's drawn width, which is
the rule #387 set for every follower. `clockFormat.test.ts` evaluates each surface under both formats.

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

**Position counts what the rig counts; class position is always the class.** The position field is
the place `PositionMode` asks for, with the count it is out of following it to the class, the same
reading the session module and the pit wall draw. It used to bind the overall place and field
whatever the mode said, so a rig counting in class read `16 / 40` in the bar beside `GT3 · P2`.
The class field is the class under either mode, because what it adds is the class name, and with
the rig counting overall it is where the class place still reads. Under `class`, which is the
default since #432, the two fields at the default right end therefore draw the same place, once
out of the class count and once after the class name; which fields that end carries by default is
the canvas's question and is left to it. `positionMode.test.ts` evaluates both fields under both
modes. [#432](https://github.com/xorob0/OpenDash/issues/432).

Race and Time left are the two clocks, and a session with no clock is a state they have to read
rather than a reading they lack: they draw `∞` there, the mark of rule 19, and `-:--:--` only where
there is no session at all. Hiding the field instead would leave an empty slot under the label a
driver put there, which an end field is — a fixed slot, not a member of a closing rank.

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

## 5. Zones B and C — twenty-two pages

The catalogue is `MODULE_CATALOGUE` in `packages/dash/src/contract.ts`. It is no longer
companion-only: the same twenty-two pages serve the companion, the pit wall zones and now the
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
|  |  |  |  | 22 | Engine readings |

Energy, Damage and Track rivals are off by default because iRacing publishes none of their data.
[second-screens.md](../second-screens.md) says which, and why. Engine readings is the last page because
it was the last to be added (#752), and an added page goes at the end of the catalogue so that no page
leaves the index a zone setting already holds ([ADR 0015](../decisions/0015-car-themes.md)).

Each is drawn at all four shapes on the catalogue artboard — eighty-four drawings. **That is the
shedding order**, and it is data rather than mechanism. The table below is those drawings read off
page by page; `packages/dash/src/modules/shedding.ts` is the same table in code, and
`shedding.test.ts` fails when the two disagree.

Two worked examples first, because they are the two that show why it cannot be derived:

- **Lap times.** `wide`: last lap, session best, your best, laps, estimated, delta to your best —
  six. `grid`: drops laps and estimated — four. So what goes is neither the tail of the row nor
  the narrowest field; the delta outlives both of the values drawn before it. The four are set out
  as the drawing sets them rather than wrapped: the last lap alone on the first line, the two bests
  on two equal columns under it, and the delta alone at the foot. A greedy wrap at 437 px puts two
  lap times on a line, so it gave `[last · session best]`, `[your best]`, `[delta]`, the value a
  driver reads first sharing its line; `shape.test.ts` holds the drawing at every `grid` body a
  face produces. `tall narrow`: the same four as `grid`, one per line. `tall`: all six again,
  stacked, and the three times promoted a size over the three under them, which is the drawing's
  88 over 34: at the 1280 × 720 face's zone they draw at 100 px over 53, where the sheet draws 111
  over 43.

  The `tall narrow` row used to be two, which is what the catalogue draws at 274 × 300, and it was
  wrong about the box it really answers: a zone that stacks one column has room for four of them,
  and what the build actually drew there was two 34 px times side by side with 234 px of the zone
  empty under them. See §10 — **the catalogue owes a redraw of this one**, and
  [#330](https://github.com/xorob0/OpenDash/issues/330) is the ticket.
- **Relative.** `wide`: position, number, name, class, gap. `grid`: the same five. `tall narrow`:
  position, name and gap only, and eight rows rather than six. The number and the class chip go
  from between two columns that stay, which no rule about prefixes produces.

  **The row count is the page's own declaration and not what the box divides out.** The relative is
  a window on the player and it lists three cars either side at every shape but `tall`, seven rows, and
  five either side there, eleven; it asks for at least one either side however short the box. Zone C of
  the 1280 × 720 face was listing thirteen before [#339](https://github.com/xorob0/OpenDash/issues/339),
  and zone B of the 850 × 480 face nine and eleven before
  [#328](https://github.com/xorob0/OpenDash/issues/328), which is a wall rather than a list. The rows
  are then drawn as *How a list answers its box* below says, at the largest type their height and their
  width allow and with the rest of the body as space between them. See §10.

  The catalogue draws that column as a three-letter code — `KLX`, `MOR`, `TSA` — at every one of the
  four shapes, and **the build draws a name**, in whichever of four formats the rig asks for, cut in
  the expression and closed with an ellipsis where the column cannot hold it, the space the cut fell
  on going with it rather than being left in front of the dots. See §10; the code was
  `left(name, 3)`, so Liam Byrne was `LIA` and Hannah Fischer `HAN`, and
  [#385](https://github.com/xorob0/OpenDash/issues/385) is where that was decided. The name is drawn
  upper-cased, as the code was, because at this size the raster welds the dot of a lowercase `i` to
  its stem and the letter reads as an `l`; §10's divergence table has the measurement.

The pattern holds generally: a narrow zone loses columns before it loses rows, and a tall one
buys rows before it buys columns — up to the count its page declares, and then type and space rather
than more rows.

### How a list answers its box

The leaderboard and the relative are tables, and rule 20 reaches a `StackRow` that carries fields
where a table row is a block, so for a long time nothing answered a list's box but its row count: the
table drew the density's row, 34 px on a zone and 28 on a narrow one, and stamped as many as the
body held. A 437 × 214 box and a 437 × 510 one differed only in how many drivers they listed, and zone
B of the 850 × 480 face spent its height on nine rows of 28 with the driver's name at 13 px — the one
column that says *who*, at the smallest size in the design, on a screen 600 mm from the eye. That is
[#328](https://github.com/xorob0/OpenDash/issues/328), and `listPlan` in `second/table.ts` is the
answer, in the order `fitFields` answers a rank:

1. **Rows, to the count the page declares.** Each list page states how many rows it wants at each of
   the four shapes, and a box too short for them at the canvas's row lists fewer. The row is never
   counted shorter than the canvas draws it, so a declaration can only buy height, never squeeze.

   | Page | `wide` | `grid` | `tall narrow` | `tall` | Why |
   |---|---|---|---|---|---|
   | Relative | 7 | 7 | 7 | 11 | Three either side, five at `tall`; odd, so the player is a row. Seven is the catalogue's six and eight made odd, eleven its `tall`. |
   | Leaderboard | 7 | 6 | 8 | 11 | The catalogue's counts, but `wide` takes the companion artboard's seven, as the `wide` column of the table above does, and `tall` the relative's eleven, since `tall` is the shape where a list grows rows. |

2. **Then type.** The row takes the largest step of the list ramp its height allows and its width
   allows too. The ramp has four steps: the 28 px row's 24 px numerals and 13 px name; **the same
   numerals under a 15 px name**, from 34 px; the 34 px row's 34 and 15; and the companion's 38 px row,
   which promotes the car number to 34 as well. The second step is the one #328 added. The canvas
   draws the name at 15 in its 34 px row and the build had tied it to the 34 px numerals, which in a
   narrow zone cost the name the width they took: on the 850 × 480 face the 34 px position and gap
   leave the name 61 px where the 24 px ones leave it 82. So a narrow zone with height to spare grows
   the column that flexes and holds the ones that do not.

   The width's edge is the name's character budget, which is what the cut is counted in. **Where the
   canvas's row holds ten characters it keeps ten**, `Liam Byrne` being the default format's own
   sample and a larger type never the reason it lost a letter — that is #339's rule, and it is what
   holds zone B of the 1280 × 480 face at the 34 px type rather than the 38, whose car number would
   take the name to nine. **Where the canvas's row already cut it, a step may cost one character and
   never two**: below ten the default sample is ellipsised whatever the row does, so the column's
   question is whether it can be read rather than whether it is whole, and one character of a cut name
   buys two pixels of every character left. The 850 × 480 face is that case, seven characters at
   13 px against six at 15, and the 34 px numerals, which would cost it two more, are refused.

3. **Then the rest as space.** The row is stretched to fill the body whatever type it carries, so a
   declared list spans its box instead of sitting centred in a pool of slack; a row taller than its
   type is the same row with air above and below its cells. The type stops at the 38 px row, the
   tallest the canvas draws a list at, so rule 20's three edges all hold.

What that draws, at the zone bodies the faces hand the two pages:

| Box | Relative | Leaderboard |
|---|---|---|
| 850 × 480, zones B and C (250 × 290) | 7 rows of 39, name 15 over 24 | 8 rows of 34, name 15 over 24 |
| 850 × 480 without the rev bar (250 × 328) | 7 rows of 45, name 15 over 24 | 8 rows of 39, name 15 over 24 |
| 800 × 480, zones B and C (225 × 290) | 7 rows of 39, name 15 over 24 | 8 rows of 34, name 15 over 24 |
| 800 × 286, zones B and C (245 × 156) | 5 rows of 29, name 13 over 24 | 5 rows of 29, name 13 over 24 |
| 800 × 286 without the rev bar (245 × 188) | 5 rows of 36, name 15 over 24 | 6 rows of 29, name 13 over 24 |
| 1280 × 480, zone B (445 × 282) | 7 rows of 38, name 15 over 34 | 6 rows of 45, name 15 over 34 |
| 1280 × 720, zone C (445 × 516) | 11 rows of 45, name 15 over 34 | 11 rows of 45, name 15 over 34 |

**The pit wall is the ticket's own exception.** Its boards are not lists and are untouched: a board's
question is who is in the race, and it lists as much of the field as its page has room for at the row
its artboard states. A pit wall *zone* showing the leaderboard asks the same question, so it keeps
listing every car its box holds at the canvas's row rather than its shape's count, the row stretched
to fill the body and carrying the largest type the plan allows: eight in the `519 × 359` and
`639 × 338` zones, four in the `639 × 206`, eleven in the portrait pit wall's, which is what each listed
before #328. `isPitWall` in `second/density.ts` is how a page tells, the pit wall's zones and panels
being the only things drawn at `panel` and `wide`. The relative keeps its window there too, since *who
is near me* is its question on any screen, and its pit wall counts did not move.

**The companion takes the declaration.** Its landscape page lists the artboard's seven leaderboard
rows, where it listed eight, and its portrait page, a `tall` box of 432 × 726 with no artboard of its
own, lists eleven rows of 64 where it listed seventeen of 40, the relative's eleven beside it being
the count it has drawn since #339. That is a decision rather than a side effect, and §10 records it.

`secondScreens.test.ts` lays each row out from the plan its page made and holds every cell against
its own column at every box the build produces, and `tables.test.ts` pins the counts, the fill and the
name's budget.

**What a row gives up when it is short of width** ([#340](https://github.com/xorob0/OpenDash/issues/340)).
A long driver name used to push the leaderboard's row over, and the gap — drawn last, and the one
column the page exists for — was what paid. A name cannot push anything now: it is cut in the
expression to the characters its column holds ([#385](https://github.com/xorob0/OpenDash/issues/385)),
so a twenty-five character name draws in the same column as `Liam Byrne`. What a box too narrow for the
columns it keeps gives up is written at `LEADERBOARD_COLUMNS`, and the relative, which draws the same
row, sheds it the same way. Each step is taken only when the one before it is spent:

1. **What the shape does not declare**, which is §5's table: the lap times go at every shape but
   `wide`, and the chip and the number at `tall narrow`.
2. **The droppable columns, from the right**, until the name clears the floor the shortest name format
   needs: the best lap, the last lap, the class chip, the car number. A row sheds a column before it
   shortens a name. The one box the build produces that takes this step is the `639 × 338` pit wall
   zone, whose `wide` declaration draws the last lap and not the best.
3. **The name's length.** With nothing droppable left, the name is ellipsised to what the position and
   the gap leave it: six, six and four characters on the 850 × 480, 800 × 286 and 800 × 480 faces.
4. **The name, then the position**, once the name cannot hold one letter and its ellipsis and once the
   position and the gap alone overrun the row. In the 28 px row that is below 191 px and 156 px of body;
   the narrowest the build produces is 225, so no box takes this step. It is written down so that the
   first one to does not lay its gap out past its edge, which is what the table did before: it floored
   the name at nothing and drew the fixed columns wherever they ended.

**The gap is on no list.** It is never dropped and never narrowed, so every step above is taken before
it moves a pixel; a box narrower than the gap alone, 104 px, is the one width the page cannot answer.
A larger type is held to the same rule: `listPlan` never takes a step that would cost a column the
canvas's row kept, which the name's budget alone could not refuse once a row had given its name up.
`tables.test.ts` walks every width from the gap alone to 1300 px at every density and pins where each
step is taken, and `secondScreens.test.ts` names the boxes that give way beyond their shape.

### How lap history answers its box

Lap history is the third list, and it is not a `table()`: its rows are laps rather than cars, so none of
a table's per-car machinery applies, and it lays out its own three columns. It answers its box in the
same order all the same ([#343](https://github.com/xorob0/OpenDash/issues/343)), which it did not
before. Every box drew the density's own row at the density's own value and stacked the rows from the
top, so zone C of the 1280 × 720 face listed seven laps of 26 px over 334 px of nothing, and the narrow
zones of the 850 × 480 face listed seven of 20 at 18 px.

1. **Rows, to the count the page declares**: six at `wide` and at `grid`, and seven at the two tall
   shapes, which is the catalogue's own count (§10 has the row). A box too short for them lists fewer,
   counted at the density's own row, and ten is the other ceiling, since SimHub keeps ten previous laps.
2. **Then type.** There are three steps, each a row the canvas draws at its own numerals: the compact
   zone's 20 px row at 18, the zone's 26 px row at 24, which is the pit wall's sheet, and the catalogue's
   34 px row at 34, which is the face's and the companion's. The row takes the largest step its height
   allows whose columns the width holds, and never a step below the density's own. The width's edge is
   the time: a 34 px `1:42.905` needs 147 px of column beside a 50 px lap number, which fits the 225 px
   narrow zones of the 800 × 480 face and does not fit a 200 px one.
3. **Then the rest as space.** The row is stretched to fill the body, with its rule at its foot. The
   type stops at 34, the largest the canvas draws a list at anywhere, so a taller row buys air between
   two laps and never a larger lap.

**The lap number is an index**, and it is drawn a step down the ramp from the values it stands beside:
24 under 34, 16 under 24, and 14 under the compact zone's 18. Every drawing of the page draws the lap
number, the time and the third column at one size and tells them apart by colour alone. The build did
the same, so the page read as a block of digits, when a driver compares the times down the page and
reads the lap number only to find a row. The number keeps the label grey the drawings give it and is
centred on the row, as a list centres its cells. It is drawn the way a list draws the car number
beside the position, and §10 records the redraw this asks of the canvas.

What that draws, at the zone bodies the faces hand the page:

| Box | Drawing | Laps | Time over lap |
|---|---|---|---|
| 1920 × 480, zones B and C (745 × 276) | `wide` | 6 rows of 42 | 34 over 24 |
| 1920 × 480 without the rev bar (745 × 320) | `wide` | 6 rows of 50 | 34 over 24 |
| 1280 × 480, zones B and C (445 × 282) | `grid` | 6 rows of 43 | 34 over 24 |
| 1280 × 480 without the rev bar (445 × 323) | `grid` | 6 rows of 50 | 34 over 24 |
| 1280 × 400, zones B and C (445 × 220) | `tall narrow` | 7 rows of 31 | 24 over 16 |
| 1280 × 400 without the rev bar (445 × 254) | `tall narrow` | 7 rows of 36 | 34 over 24 |
| 850 × 480, zones B and C (250 × 290) | `tall narrow` | 7 rows of 41 | 34 over 24 |
| 850 × 480 without the rev bar (250 × 328) | `tall narrow` | 7 rows of 46 | 34 over 24 |
| 800 × 480, zones B and C (225 × 290) | `tall narrow` | 7 rows of 41 | 34 over 24 |
| 800 × 480 without the rev bar (225 × 328) | `tall narrow` | 7 rows of 46 | 34 over 24 |
| 1280 × 720, zones B and C (445 × 516) | `tall` | 7 rows of 73 | 34 over 24 |
| 1280 × 720 without the rev bar (445 × 560) | `tall` | 7 rows of 80 | 34 over 24 |
| 800 × 286, zones B and C (245 × 156) | `tall narrow` | 7 rows of 22 | 18 over 14 |
| 800 × 286 without the rev bar (245 × 188) | `tall narrow` | 7 rows of 26 | 24 over 16 |
| 600 × 686, zone B (576 × 122) | `grid` | 5 rows of 21 | 18 over 14 |
| 600 × 686, zone C (576 × 112) | `grid` | 4 rows of 24 | 18 over 14 |

**The pit wall takes the same plan.** Its reference zone keeps the 24 its sheet draws, six rows of 29 in
607 × 196, and its taller zones take 34: six rows of 49 in the `519 × 359`, and seven of 61 in the
portrait pit wall's. **The companion takes it too**, with six rows of 55 on its landscape page. Its
portrait page, a `tall` box of 432 × 726 that no artboard draws, is where the space shows most: seven
laps of 103 px. Whether that box should list the ten laps SimHub keeps instead is a question about the
count and not the row, and it is the author's to answer; the catalogue's seven stand until then.

**The fuel target is refused, together with its column.** The catalogue writes the target into the
fuel column's heading, `Fuel · target 2.85` at `wide` and the bare `Fuel` at `grid`, so the target is a
value in a heading rather than a heading of its own. The build draws no fuel column, as §10 and
[second-screens.md](../second-screens.md) record, so the target has nothing to head, and beside
`Δ best` it would read as a target for the delta. It is also a number the driver sets, which no setting
holds: that is [#326](https://github.com/xorob0/OpenDash/issues/326), and #326 needs the column before
it needs the heading.

`lapHistory.test.ts` pins the index step and the fill at every box the build produces, the width edge,
the 34 px ceiling, and the absence of a fuel heading.

### How the opponents page answers its box

The opponents page is two cars rather than a list, but its identity row is a list row — a name beside
a number, 13 over 16 in the catalogue's zones and 15 over 34 on the companion artboard, two of the
steps above — and it had been answering the name's size a second way, from the density: 13 in a zone
and 12 on the compact faces, under a gap drawn at 46 and 34.
[#341](https://github.com/xorob0/OpenDash/issues/341) makes it one answer, in `modules/opponents.ts`:

1. **The name is the relative's.** 15 wherever the row can carry it, and the width is the edge, by the
   same rule: where 13 px would keep the ten characters of `Liam Byrne` the larger name keeps ten, and
   where 13 px already cut it the larger one may cost one character and never two. It may not cost a
   piece either, the number or the class chip a 13 px name left room for. The name's box is the eight
   characters of the shortest format at least and takes what the row leaves over up to the ten of the
   default one, so a 600 px zone no longer cuts the default name to `LIAM BY…`. Every box the build
   produces has the width for 15 and ten characters, the narrow faces included, since at `tall narrow`
   the name is alone on its line.
2. **Then the gap grows.** Rule 20, for a page that has shed nothing: the gap, which is what the page is
   read for, grows from the density's `big` until it meets the height of the box, the width of its row
   or rule 20's ceiling, which is ×2.2 on a face and the next size up the ramp elsewhere, both cars
   together. The heading, the name and the
   recaps keep their sizes, being labels and a list row.
3. **Then the rest is space between the cars**, either side of the rule — the canvas's twelve at least,
   measured from the gap's line box as the canvas measures it, so the rule sits in the middle. The
   ahead block reads from the top of the zone and the behind block down to its bottom, which is the
   order they are in on the track.

What that draws: the gap at 64 in every zone-density box that keeps its whole set but the 445 × 254
body (61, the height), the pit wall's 607 px zones side by side (61, the width) and the 1280 × 400 face's
445 × 220 (46, having shed the chip and the last lap); at 46 on the compact faces' narrow zones and in
the 600 × 686 face's short ones, 36 in the nano's 245 × 188 and 34 in its 245 × 156, which sheds the
names; 96 on the landscape companion page, where the height stops it, and the companion ramp's 116
on the portrait one, whose body is the tallest the page is ever given. `secondScreens.test.ts` holds
the name to the list ramp and the relative beside it, and the growth to its three edges and the space
to the middle of the rule.

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
| 5 | Fuel | `level` · `time` · `toEnd` · `lapsLeft` · `toAdd` · `average` · `lastLap` · `thisLap` | `level` · `toEnd` · `toAdd` · `average` | `level` · `toEnd` · `toAdd` · `average` | `level` · `time` · `toEnd` · `lapsLeft` · `toAdd` · `average` · `lastLap` · `thisLap` |
| 8 | Pit view | `refuel` · `pitTime` | `refuel` · `pitTime` | `refuel` · `pitTime` | `refuel` · `pitTime` |
| 9 | Car settings | `tc` · `abs` · `bb` · `mix` · `arbFront` · `arbRear` | `tc` · `abs` · `bb` · `mix` · `arbFront` · `arbRear` | `tc` · `abs` · `bb` · `mix` | `tc` · `abs` · `bb` · `mix` · `arbFront` · `arbRear` |
| 11 | Session | `type` · `position` · `class` · `lap` · `timeLeft` · `lapsLeft` · `incidents` · `cars` | `position` · `class` · `lap` · `timeLeft` | `position` · `class` · `lap` · `timeLeft` | `type` · `position` · `class` · `lap` · `timeLeft` · `lapsLeft` · `incidents` · `cars` |
| 14 | Leaderboard | `pos` · `num` · `name` · `class` · `gap` · `best` · `last` | `pos` · `num` · `name` · `class` · `gap` | `pos` · `name` · `gap` | `pos` · `num` · `name` · `class` · `gap` |
| 15 | Relative | `pos` · `num` · `name` · `class` · `gap` | `pos` · `num` · `name` · `class` · `gap` | `pos` · `name` · `gap` | `pos` · `num` · `name` · `class` · `gap` |
| 16 | Opponents | `ahead.gap` · `behind.gap` · `ahead.name` · `behind.name` · `ahead.num` · `behind.num` · `ahead.class` · `behind.class` · `ahead.lastLap` · `behind.lastLap` · `ahead.rating` · `behind.rating` | `ahead.gap` · `behind.gap` · `ahead.name` · `behind.name` · `ahead.num` · `behind.num` · `ahead.class` · `behind.class` · `ahead.lastLap` · `behind.lastLap` | `ahead.gap` · `behind.gap` · `ahead.name` · `behind.name` | `ahead.gap` · `behind.gap` · `ahead.name` · `behind.name` · `ahead.lastLap` · `behind.lastLap` |
| 17 | Gear | `speed` · `rpm` | `speed` · `rpm` | `speed` · `rpm` | `speed` · `rpm` |
| 18 | Stint | `lap` · `fuelTime` · `stintTime` · `stintLaps` · `completed` · `stops` · `lastStop` · `avgLap` | `lap` · `fuelTime` · `stintLaps` · `stops` | `lap` · `fuelTime` · `stintLaps` · `stops` | `lap` · `fuelTime` · `stintTime` · `stintLaps` · `completed` · `stops` · `lastStop` · `avgLap` |
| 22 | Engine readings | `water` · `oilTemp` · `oilPressure` · `fuelPressure` · `voltage` · `manifold` | `water` · `oilTemp` · `oilPressure` · `fuelPressure` · `voltage` · `manifold` | `water` · `oilTemp` · `oilPressure` · `fuelPressure` · `voltage` · `manifold` | `water` · `oilTemp` · `oilPressure` · `fuelPressure` · `voltage` · `manifold` |

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
- **Fuel at `wide` and at `tall`.** Three consumptions under the refuel where both fuel sheets draw
  two, the per-lap average and a five-lap one. The average is the same figure in both. The five-lap
  one is not built, because no property says what an earlier lap cost
  ([second-screens.md](../second-screens.md) records the datum), and the build draws the last lap
  and this lap instead, the last lap being what band D's artboard draws beside the average. The
  narrower shapes keep the average alone, since one number three ways is still one number.

  **The order is the drawing's**, rank by rank and left to right, and it is argued rather than
  copied, as the reading a driver makes before a stop: what is in the tank and how long it lasts,
  whether that reaches the flag and how many laps it is worth, and then what to put in at the stop
  and what a lap costs. Past the tank, a figure outranks the figures it is worked out from. The
  margin is the estimate less the session's laps left, the estimate is the tank over the per-lap
  average, and the refuel is the laps left at that average less the tank, so the estimate goes after
  the margin and before every consumption, and the average goes first of the three because the
  figures ahead of it are taken from it. Band D's fuel page sheds in the same order. The table used to list the estimate last, behind
  the three consumptions, on a page that draws it in the lead rank at the lead size, so the one
  number a driver counts down to the stop by was the first a short box gave up
  ([#334](https://github.com/xorob0/OpenDash/issues/334)).
- **Fuel's margin, at every shape.** `toEnd` is the signed figure saying whether the fuel in the
  tank reaches the end of the race and by how much, `+1.4` laps or `−3` minutes, and neither fuel
  sheet draws it: `CompanionModules.dc.html` describes the page as "fuel, fuel time, refuel, last
  lap, 2 and 5 lap averages, estimated laps, level gauge", and `ZoneCatalogue.dc.html` draws those
  less the last lap.
  It is taken all the same, because it is the only fuel question a race asks and the page already
  carried every term of it — the range and the estimated laps here, the laps left on the session
  page — so a driver was doing the subtraction himself between corners (#387).

  Where it sits is a preference rather than a transcription, and it is declared twice. At `wide` and
  at `tall` it goes third and the estimated laps come after it, since the estimate is the working and
  this is the answer. At `grid` and at `tall narrow` it takes the fuel time's place, which is the same
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
  at that shape either. [#341](https://github.com/xorob0/OpenDash/issues/341) read the two drawings
  again and confirms both rows: `tall narrow · 274 by 300` draws the direction triangle, the flag, the
  code and the gap, and `tall · 360 by 470` those four with the badge and the bare last lap; the badge
  is 18 px high with a 12 px `B` in the primary text colour, where the class chip is 20 high with a
  13 px `GT3` in the label colour. The real boxes draw the declared sets: the faces' 225 to 250 px
  narrow zones keep the name and the gap, and the 1280 × 720 face's 445 × 516 and 445 × 560, the pit
  wall's 507 × 427 portrait zones and the portrait companion page add the last lap, which
  `shedding.test.ts` holds at every zone body the build produces. The nano's 245 × 156 is the one box
  that sheds the names, and is pinned there. The ticket's own reading of `tall`, that it keeps the
  class, is the transcription this row corrects.

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

The counter is drawn by the face rather than by the zone, because
zones B and C share one dashboard file where they are the same rectangle, and a screen in it cannot
know whose mask is deciding its length. The length is in the expression, which is what
[ADR 0009](../decisions/0009-does-the-plugin-compute.md) settled: a popcount is
`truncate(mask / 2^i) % 2` summed over the catalogue, and the mask is a property that already
exists. Without the plugin, the mask reads as its default and the counter says "n / 21".

**The position is the plugin's since #791.** The panel lets a driver arrange a zone's pages as well
as tick them, so "this is the third of five" depends on an order, and an expression cannot read an
ordered list. The plugin publishes it as `Face<size>Zone<X>Position`, counting from one, and the
counter reads that. The catalogue-order count -- the popcount of the mask below the page showing,
plus one -- stays behind it as the `isnull` fallback, which is the right answer for a zone nobody has
arranged and for a face with no plugin at all. ADR 0009's exception for it is written there.

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
| D4 | Tyres | D8 | Car (the telltale row), **held back from 1.0** |

Fuel is the default because it is what a driver checks on a straight.

**D8 is held back from 1.0** ([#969](https://github.com/xorob0/OpenDash/issues/969)). It draws twelve
outlined boxes with no pictogram in any of them, because the artwork is not in the repository and
nine of the twelve lamps have nothing in iRacing to light them, and a page of empty boxes reads as
broken. So no driver can reach it: it is in no cycle and no default mask, no start or glance may name
it, the plugin's panel and the site do not list it, and the built band draws fuel in its place.
It keeps its place in the catalogue all the same, and its number with it, because a zone's setting is
its page's index: the seven pages before it and a theme's pages after it keep theirs, so a rig that
never chose it reads exactly as it did. A rig that had chosen it is moved on when the plugin
normalises its settings, to the next page of the band's own order, which is fuel on a band nobody
reordered and the theme's page on a Porsche face. The one switch is `HELD_BACK_BAND_PAGES` in
`contract.ts`, mirrored by `FacePages.HeldBack` in the plugin; the rank, its fit tests and its
snapshot stay as they are, so [#148](https://github.com/xorob0/OpenDash/issues/148) re-enables the page
by taking `car` out of that list once the pictograms land.

**A theme's band pages come after the house's eight** ([#718](https://github.com/xorob0/OpenDash/issues/718)).
The eight above are the contract's, and a theme may add pages after them, never between or before,
since a zone's setting is its page's index ([ADR 0015](../decisions/0015-car-themes.md)). The pages are
part of the theme's catalogue entry, `bandPages` in `THEME_CATALOGUE` and `BandPages` in
`Contract.Themes`, so both halves of the contract count them: band D's catalogue on a face of a theme
is the house's eight and then the theme's, numbered on from 8 (`bandDPages`, `pagesForZone(zone,
theme)`), its whole-catalogue mask is that many bits wide, and the band opens on the theme's first page
rather than on fuel (`defaultZonePage`), with the plugin as without it. The drawing of each page is the
theme's `bandPages` hook, and the build refuses a theme whose drawing and catalogue entry do not name
the same pages in the same order. The default theme adds none, so a default face is the eight above
exactly as before.

| Theme | Pages added | Index | Band D opens on |
|---|---|---|---|
| OpenDash (default) | none | | Fuel, 0 |
| Porsche | Porsche: the badge, the TC and ABS boxes, the tyre box and Brake Bias, the car's foot (`porscheFoot`) | 8 | Porsche, 8 |

The plugin reads the count, the mask width, the order, the default page and the names per screen, from
the screen's theme: a Porsche screen added from the Add sheet opens band D on the Porsche row with all
nine pages ticked, and its button steps through the eight house pages and back to it. A Porsche screen
whose settings were written before #718 keeps the eight-page mask and the start it had, and is offered
the Porsche row unticked rather than given it behind its driver's back.

**Corner blocks.** A block at each end on the wider faces: incidents against their limit and the
track state on the left; DRS, push to pass, spotter lamps and both clocks on the right. They are
drawn at 1920 × 480, 1280 × 480, 1280 × 400 and 1280 × 720, and absent at 850 × 480, 800 × 480,
800 × 286 and 600 × 686. The threshold is those drawings, not a round number.

**A page sheds its last field before the rank overflows**, with nothing spread to fill. The rank is
packed and centred in what the side padding, the letter's room and the corners leave, never in the
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

**D8 is a rank of lamps rather than of fields**, held back from 1.0 as above. The page carries the twelve telltales the
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
full course yellow: a caution runs several minutes, the flag has said everything it has to say after
two seconds, and what a driver decides during a caution is whether to pit, so FULL COURSE YELLOW over
an unreadable fuel page for five minutes is the wrong trade in every minute but the first. A change of
flag takes the band again for its few seconds, including a change no single bit shows — a
full course yellow clearing to the local yellow underneath it is a new thing to tell a driver — so
what the window watches is the rank of the *winning* condition, `raisedRank` in
`packages/dash/src/flags.ts`. A blinking flag keeps blinking in the block.

So band D's own priority over time reads: **the flag alone for three seconds, then the flag at both
ends over the page, then the page alone.** Nothing else about the ranking changes; the twenty
conditions are ranked by the same expression in both phases, so the phase decides the rectangle and
never which flag wins.

**The blocks a flag settles into are the band's own**, `bandFlagBlocks` in `bandPages.ts`, which is
what keeps a settled flag out of room a page is using. On the four faces that draw corner blocks they
are those blocks, taken whole and to the band's edge: the flag covers the incidents and the track
state at one end and the lamps and both clocks at the other, which is the room the band can most
afford to lose while a flag is out. Whole is the whole width the band reserves for a corner, and that
width is the block's two fields *plus* the side padding *plus* the room the zone letter stood in, so
the block runs to the band's own edge. Before #708 that meant a settled flag covered band D's own **D**
on those four faces, which was judged the cheaper of two prices, since starting the block 44 px in to
clear it would have held the flag inboard of the band's left edge on four faces and hard against it
on the other four; no face draws the letter any more, so the question has gone with it. On the four
that draw none there is no block to take, so the flag keeps the side padding instead: 16 px of colour
at each end, 12 in portrait, which is the only room in the band no page is ever laid into. It writes
no name at that width, as the nano's 12 px strip writes none. None of it is drawn on any artboard, and
[§10](#10-where-the-canvas-contradicts-itself) records that.

**The settled form is not a setting**, which #380 asked to have decided rather than assumed, and the
decision is worth stating together with what it costs, because it is not nothing. On the four faces
with corner blocks it costs a driver nothing of the flag: the block holds the name, so he keeps the
colour and the word and gains the page under them. On the four without, the block is sixteen pixels
and writes no name, so after three seconds he keeps the colour alone — and a colour is a family
rather than a member. DISQUALIFIED and BLACK become one outlined sliver. That is a reading lost, on
the very face the ticket is written about. DEBRIS used to be lost with it, one yellow sliver with
YELLOW, and is not since #498: the stripes are drawn, and there are never fewer than three of them,
so sixteen pixels hold one red between two yellow.

It is still not a setting. What a switch would buy that driver is the band held for the whole flag,
at the price of the fuel page for the whole caution — the case #380 opens with, on that same face —
in exchange for a name he has already read during the three seconds the flag had the band. And the
choice between a flag held and a page given back is already a setting: `FlagFormat` set to `full`
gives the flag zones B, A and C for the whole of its duration and names it there, DSQ apart from
BLACK, at the cost of the gear rather than of the band; the furled black reads BLACK there as the
black flag does since #497, as it does on the band, and neither tells those two apart. A third arm would be a
setting inside a setting for a view one of the two already offers. What those four faces are owed is
a drawing rather than a switch, and [§10](#10-where-the-canvas-contradicts-itself) is where that debt
is written down: a block wide enough for a name there means taking room from the page, so the canvas
has to say which. Reversing the decision costs one more term on each of the two groups' `Visible` and
no new screen, so the author can do it cheaply.

**It draws the whole flag catalogue, which is fifteen conditions and not six.** The band used to
read the six `Flag_*` properties SimHub normalises, and those are a lossy summary of what iRacing
publishes: `Flag_Yellow` folds the standing yellow, the yellow being waved and both cautions into
one band, and `Flag_Black` is only the `black` bit. A red flag, a disqualification, a furled black,
a meatball, a full course yellow, a yellow being waved, the debris flag and the start gantry were
therefore drawn by the 8x8 box and invisible on the dash, and the face's own ranking disagreed with the box's
about which of two live flags won. The band reads `ALERT_CATALOGUE` in
`packages/dash/src/flags.ts` now, through the same `conditionVisible` the box ranks with, so the
three surfaces that draw flags cannot disagree. Which condition takes which shape, and which rank,
is tabulated in [flag-box.md](flag-box.md), which remains the single place a condition is refused
with its reason.

**And five car alerts ranked in the same list, since #762.** The canvas's alert catalogue is flags
and car alerts in one order, and so is the band: the ignition off and the engine stalled out on the
circuit rank above the red flag, an incident with its count against the limit below the flags that
mean slow down, and push to pass and the headlight flash below the chequer. They take the band and
settle into its blocks as a flag does, and every one of them reads whether anybody is in the car,
which is `inTheCar` in `second/values.ts`, and reads it as "no" in a sim that does not say. In the lane the ignition and
the stall are the pit family's instead, below. The incident and the flash are events, held for the
same three seconds after the value they watch moves. Push to pass and the flash are white, which is
two flags' colour without their name, so they are drawn only where the name is: not on the nano, not
in a sixteen-pixel block, and not on the full-screen block. [flag-box.md](flag-box.md) has the table
and the reasons, and §10 the departures from the canvas.

Three consequences are worth stating. The band is iRacing's, as the box already was, since
`SessionFlagsDetails` is a raw iRacing field: on another sim it stays dark rather than drawing an
approximation of a flag nobody published. The flash belongs to the yellow being waved and no longer
to the standing one, the folded property having strobed both, and since #497 it is all that tells the two
apart, both being named YELLOW. And the green flag alone reads a normalised
property, `Flag_Green`, because iRacing holds the `green` bit for a whole green-flag stint and
SimHub's own limiter on that property is the only clock there is; without it band D would be a solid
green bar over the fuel page for an entire race.

**Five shapes and no sixth**, which are the canvas's four for the alert catalogue, bands, outlined
bands and two patterns, and the meatball's disc, and are
`packages/dash/src/components/alertBand.ts`: a filled bar, a bar outlined in the alert's colour over
an opaque ground, the chequer, the debris flag's red stripes over its yellow, and an orange disc in
the middle of the near-black. The stripes were left out until #498, on the grounds that the name
carried the difference, and the name is exactly what the nano and a sixteen-pixel block do not
write. They are vertical where the canvas draws them at 135 degrees, as §10 records, and the name is
written on a plate of the yellow so that no glyph straddles a stripe. The disc is the author's
ruling on #498 rather than the canvas's: the meatball is a black box with an orange disc in the
middle and no text, the disc two thirds of the rectangle's shorter side, and like the chequer it
writes no name, being its own flag. The black family and the start gantry take the outlined form,
and the black flag is the reason it exists. Its token, `purpose.flag.black`, is `#F5F7FA`, which is
the ink and not the ground: a band filled with it would be indistinguishable from the white flag at
`#FFFFFF`. So the black flag fills with `surface.base`, keeps the border, and writes its name in
`purpose.flag.black`. The canvas captions it "outlined", which it now is again in the sense the
canvas means, an edge and a name in the alert's colour, though never with a transparent ground: a
transparent flag left the page underneath fully readable and a flag takes the band over.

**The nano writes no name.** Its twelve pixels are colour and shape alone, so the conditions that
share both share a band there: the three members of the black family are one outline. That is the
price of the strip's height rather than a decision of the catalogue's, and it is why the names exist
wherever there is width to hold one. Since #380 the nano is not the only place that pays it: a flag
settled into the sixteen pixels at the ends of a band with no corner block, twelve in portrait, reads
the same way, which is the cost [§6](#6-band-d--eight-pages) weighs above. The debris flag used to
pay it as well, reading as a yellow in both places, and no longer does: its stripes are drawn at
every size, six pixels wide on the nano and three, the middle one red, in a sixteen-pixel block.

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
`SessionProgress` — keep their names and their meaning, `DeltaReference` having since gained a
third value, `lastlap`, and a sibling, `DeltaPrecision`, which draws the live delta to hundredths or
to thousandths ([#322](https://github.com/xorob0/OpenDash/issues/322)); and `RevBar` joins
them: what the top of the face carries, `shift`, `rpm` or `off`, with `off` drawing the second
arrangement. It carries no face's prefix because it is not one face's, whatever the second
arrangement is: the round faces' rev arc and the companion's speedo draw the same segments from the
same setting, and a screen may not read a property another screen owns. `ShiftLights` is now its
deprecated alias and stays attached for a release: an rc.2 user's properties do not vanish without
warning (#170), and a package installed beside an older plugin falls back through it.

`ClockFormat` is shared for the same reason: `24h` draws `14:32` and `12h` draws `2:32 PM`, the wall
clock and the sim's time of day alike, and which of the two a driver reads without thinking does not
change between the rim, the pit wall and the idle screen every package ends with. Rule 19 above says
how a word that fits no cell is drawn after the digits.

Every expression that reads one of these wraps it in `isnull()` with the default, so a package
installed without the plugin shows each zone's start page and simply cannot cycle. That is still a
complete product by [ADR 0003](../decisions/0003-plugin-settings-through-properties.md)'s letter,
and it is the first feature for which the plugin buys something material.

### What the panel draws

The Screens page draws a face as a picture of itself, to the `Screens` artboard of #791: the rev
segments, the info bar with a field at each end and the car settings between them, zones B, A and C
across the body, and band D along the foot ([plugin.md](plugin.md#screens)). `PanelFacePlan` scales
the face's own proportions to the column, because "zone C" means nothing until you see where zone C
is. Each part is a press that opens its aside: a zone's pages, or the info bar's fields. This replaces
the plan of the face the four-tab panel drew to `Plugin.dc.html`.

Four things the earlier artboard did not settle, and what the panel does about each:

| | |
|---|---|
| **The mask had no control drawn.** | It is the setting that decides how long a driver's cycle is. Each zone cell reads its count, "4 of 21", and the zone's aside lists every page to tick and drag into order, with All and None, and Show all or Only ticked. The `Screens` artboard draws this now. |
| **Nor had the class filter.** | "My class only", in the aside of each zone where a page would change. Zone A alone is offered none. |
| **An end of the bar carries two fields.** | The info bar's aside has a picker for each field, rather than two boxes on the picture. |
| **Nothing said what happens to a zone sitting on a page that is then turned off.** | It snaps *forward* to the next enabled page, wrapping once, because a cycle runs forward and the next press carries on rather than repeats. Unticking a zone's last page is refused: a zone with an empty cycle has nothing to draw. |

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

Zones B and C carry a permanent header, so the answer is already on the screen: the page name
changes with the page.

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

**It is built before 1.0 as #487, and until then the two round faces ship on the card model
deliberately.** That is the second half of the answer and the half a reader is most likely to need:
the round faces are not undecided, they are decided and not yet converted. So until #487 lands
`480round.ts` and `800round.ts` keep reading `layout.slots`, `OpenDash.Slot01` to `Slot12` keep
driving them and nothing else ([§7](#7-the-settings-the-contract-fixes) and #170), and the card path
is retired at 1.0 behind it — #146 waits on #487 rather than on this answer. The rule that kept the
card path through 1.0 was removed on 2026-09-29; [ADR 0006](../decisions/0006-the-zone-face.md)
records the amendment.

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
([ADR 0020](../decisions/0020-the-panel-draws-what-it-configures.md)), and the rectangular picture on
the Screens page ([plugin.md](plugin.md#screens)) does not fit a disc. The picker the conversion needs
is the arc, zone A in the middle and the catalogue zones where the card rects are. Until the
conversion a round screen keeps its Cards on the disc, which assigns cards to slots.

**The three obligations above are #487.** The two artboards, the round picker and the catalogue a
140 × 108 box leaves are that ticket's work; #145 was the decision and closed with it, and #146
waits on #487.

---

## 10. Where the canvas contradicts itself

Recorded rather than resolved. A reader who finds one of these has found a real disagreement, not
a mistake in this document.

| | |
|---|---|
| Band D's page count | The catalogue heading reads "band D · seven pages", its anatomy row reads "fuel by default, and six more pages", and the drawings are D1 through D8. **Eight is taken**, because the drawings are more specific than the captions and the mask is sized for eight either way. D8 now draws the twelve-lamp rank the artboard gives it, in place of the water, oil, oil pressure, fuel pressure and voltage readings it carried in the meantime. Those five are consequently drawn nowhere on the face any longer, no module of zones B and C carrying them either, and whether the face owes them a home of their own is the author's to say. |
| The bar's fields | The catalogue's anatomy says "three fields a driver may swap", the Foundations anatomy on the Main artboard says "a field at each end", and every face artboard draws two at each end. **Two per end is taken**, because that is what is drawn; §1 and §3 above both say so now, the first of them having repeated the one-per-end caption until this row was written. The catalogue those fields are chosen from is ten entries where the artboard draws eleven, strength of field being the one that went, under [ADR 0009](../decisions/0009-does-the-plugin-compute.md), because SimHub publishes it in no form at all. |
| Zone C's capacity | Stated as ten drivers at 1920; seven rows are drawn, which since [#339](https://github.com/xorob0/OpenDash/issues/339) is the relative's own declaration rather than what the height divided out. Since [#328](https://github.com/xorob0/OpenDash/issues/328) the zone's `wide` shape declares seven, three cars either side, so ten is past the page's own count before it is past the 745 × 276 body, which does not have the height for ten at a row a driver reads either. |
| Page dots | `pageIndicator` is still in the component list, against "there is no row of page dots". |
| The zone letters | Every face artboard opens zones B and C with their letter, 8 px before the page name, and band D with a **D** at its side padding. **None is drawn**, by Tim's decision of 7 October 2026 ([#708](https://github.com/xorob0/OpenDash/issues/708)): the page name says what is showing, the letter was a label nobody reads, and it goes from every theme with the default. The page name takes the letter's place at the header's left padding, whereas band D keeps the room its letter stood in, since its rank, its corners and its shedding are read off the drawing. The canvas owes the redraw, and with it the answer to whether band D's page may have that room. |
| The fuel tank | Dropped from the drawn objects in the 0.7.0 changelog — "a quantity is a number" — and still listed among five in `canvas.json`'s detail-pass annotation. **Four objects are taken.** |
| The numeral family | Rule 4 says numerals are Barlow Condensed. The files ship as `openDash Display`, because WPF reads the width word out of a family name and folds the condensed faces into Barlow as a stretch, which a `.djson` cannot ask back. Same outlines, different name; see #159. |
| The telltales' pictograms | Twenty-eight Material Design Icons are named on the canvas and the build "rasterises the chosen twelve", which are not listed, so the twelve are still owed as files. An `ImageItem` carries no tint, which the format research verifies, and a lamp therefore owes one file per colour it can be drawn in: nineteen in all, being a dark file for each of the twelve and a lit file for each of the seven that the drawing or a source gives a colour to. They are named `telltale-<lamp>-<state>` in `packages/dash/src/zones/telltales.ts`, and the rank draws whichever of them `design/assets.ts` holds, so the lamps gain their pictograms in the commit that brings the artwork together with its Apache 2.0 licence and the notice naming Pictogrammers. Until then a lamp is its box. |
| What lights a telltale | Three of the twelve have a source and nine do not. The engine reads the `EngineWarnings` bits for water temperature and oil pressure, the fuel can reads the same low-fuel threshold every other light OpenDash drives reads, and the speed limiter reads `PitLimiterOn`. Nothing lights the two tyre lamps, the wiper, the car above the wavy tracks, ABS, ESP, the battery, the tyre pressure warning or the door: iRacing publishes no wiper, stability, tyre pressure or door state at all, `dcABS` is the level the driver has dialled in rather than an intervention, and a battery lamp reading the raw voltage would need a threshold nobody has chosen. **The nine are built and left dark**, because a dark lamp asserts nothing whereas a lamp bound to a property that means something else asserts the wrong thing. Which property lights each of them is the author's to answer, and two further answers are owed with it: the colour of the engine lamp, which the artboard draws dark and which is taken as danger red here because both bits it reads are failures rather than advisories, and the source of the count the artboard draws in the wiper's corner. That count is recorded in `telltales.ts` and is not drawn, for the reason the relative page's country flag is not drawn. |
| Band D's value size | Every 60 and 58 px band draws its page values at 34 px over a 13 px label, 5 px apart. WPF's line box around a 34 px value runs 60.6 px from the top of that label, so the band clips it by a pixel. **The value shrinks** — 32 at 60, 30 at 58 — because a clipped numeral reads as a rendering fault. The band would have to grow, or the drawing come down; the 54 and 56 px bands draw 24 and are honoured exactly. |
| The face with no rev bar | #189 offered three answers, namely leave the gap, reclaim it, or give the band to something else, and said the artboards would choose. Since the second pass of 15 September the FaceVariants sheets do draw the third state and both arrangements beside each other, so this row no longer reads as it did. What the rev-bar-off drawing still carries, however, is the rev bar itself: an 822 × 28 rectangle at (14, 6) on the 850 sheet, a 576 × 24 one at (12, 6) on the 600, underneath a bar that has already risen into its room. **The caption is taken over the rectangle**, and `faceItems` leaves the well and the segments out entirely rather than hiding them; `Plugin.dc.html`, for its part, still reads "the rev bar stays". Reclaim is taken for the room, because the gap reads as a mis-crop and on the nano it is a ninth of the screen, and the rectangles in §1 remain derived by one rule and remain the thing to delete when drawn ones arrive. |
| The slot counts in the titles | `canvas.json` titles the 1920 × 480 artboard "MVP · 12 slots" and the 1280 × 720 one "wheel screens · 12 slots", while what each draws underneath is the five-part zone face [ADR 0006](../decisions/0006-the-zone-face.md) settled, and `Dash.dc.html` keeps `.slotbox`, `.card` and `.grid4` in its stylesheet with nothing using them. **The drawing is taken**: a `ZoneLayout` declares no slot count at all, and twelve matches nothing on the 1280 × 720 body either, whose bar draws eleven readouts and whose band draws ten and three lamps. The twelve-slot package does still build beside the zone face, since `LAYOUTS` keeps `layout1920x480` and `build.ts` walks both lists until #146 retires the card path. |
| The six slots of the 850 | The same convention gives 850 × 480 "5in · 6 slots", and nothing six-fold is drawn there. The only reading that yields six is the parts themselves, that is to say the bar's left end, its settings strip and its right end, then zones B and C and band D. **The parts are taken**, because that is what the artboard draws and what `faceItems` composes; the count is vocabulary left over from the model the face replaced. |
| The "D grid" chip | Every FaceVariants sheet chips band D as `grid`, whereas the band it draws is 1280 × 60, or 800 × 58 on the nano, which `second/shape.ts` bands as wide and short rather than as the 430 × 300 the `grid` archetype is. **Neither is taken, because the band does not consult the shape model at all**: `bandPages.ts` draws one centred rank for a wide short box, and only zones B and C ask `shapeOf` for their page. The 600 × 686 sheet chips its own zones B and C the same way, and they measure 600 × 160 and 600 × 150, which is wide and short again. |
| The twelve-hour clock | No artboard draws one: every clock on the canvas reads `14:32`. The build's answer is recorded here rather than presented as the drawing's ([#324](https://github.com/xorob0/OpenDash/issues/324)). The `AM` or `PM` is set as each surface already sets what follows a figure — at the denominator's size and in its ink in the bar, as a band D unit in the corner, as a small label in the pit wall header and on the idle screen — and one gap after the digits. Every box a clock is drawn in is measured with it, so band D's corner, which was cut for the eight cells of `0:42:15` and drew `13:11`, is twelve pixels narrower measured for `12:59 AM` than it was, and its pages centre six pixels further right. How the word should look beside the figure is the author's to draw. |
| The strip at 850 × 480 and 800 × 480 | Both artboards caption five cells, namely slip, TC, cut, bias and ABS, and the build keeps four at 850 and three at 800, which §3 tabulates and `barStrip.test.ts` pins. **The artboards' own scale is taken**: each face now draws the bar at the size its artboard gives it, so the narrower faces gain cells the earlier measured layout had shed. What the two still drop is cut at 850 and cut and slip at 800, and the cause is the ends rather than the strip, each end being laid out from its own edge for the widest entry the catalogue holds rather than for the entry actually selected. Raising the count further therefore means narrowing the reserved end or measuring the strip's values below the size the end fields use, and the canvas has made neither decision. The 600 × 686 sheet is no longer a disagreement: it draws its five cells in fixed 54 px columns at a 12 px gap, which is what the build now does, with four pixels to spare that the widest class name governs. |
| The 600 × 686 well | The size's own chip names a 36 px well above the bar. The artboard draws the well at 6, 2, 588 × 32 with the segments at 12, 6, 576 × 24, and the bar begins at y 36, so that 36 is the room above the bar, being a 2 px face margin, the 32 px well and a 2 px gap, rather than the height of anything. **The artboard is taken** and §1 tabulates the 32. Were the well itself meant to be 36, the rect in `faces/600x686.ts` would move and `revBarReclaim` would become 38, which moves the second arrangement's table as well. |
| Zone A, centred or filled | The face artboards centre zone A's block in its column, `justify-content: center` with a 198 px gear in a 320 px column at 1280 × 480, while the FaceVariants sheets caption the same zone "Zone A fills its column. Padding stays; empty height does not". **Both are taken, and they turn out not to disagree**: every page is cut from the column, each run being a share of its height rather than a size of its own, and what is then left over goes half above the page and half below it, so all four fill and all four centre. What the sheets ask for and the format refuses is the last three per cent of the gear, which is the subject of the row below. |
| The catalogue's zone A against the per-size sheets | The two draw different pages. The catalogue's A1 is a 62 per cent gear over a speed and no revs, its A2 carries the ghosted neighbours and its A3 puts an rpm value between the speed and the gear; the FaceVariants sheets give A1 the three runs at 55, 19 and 11 per cent, A2 the gear alone and A3 two rows. **The per-size sheets are taken**, being the more specific drawing and there being eight of them, with two exceptions that cost nothing: the ghosts stay on A1, where they have always been, and A3 draws the rpm beside the speed wherever the column is wide enough to hold the group, which today is the 600 × 686 face and nowhere else. The gaps disagree too, the Dash artboards drawing 3 px at the nano and 6 at 850 × 480 where the variants sheets draw 4 and 7; **the sheets are taken**, as a share of the column rather than a literal per face. |
| Zone A's gear at 86 per cent | Every FaceVariants sheet draws A2's gear at about 86 per cent of the column, 282 px in 328 and 167 in 194. **The line box is taken instead**, which is about 82: SimHub hands the box to WPF as `MaxTextHeight` and WPF clips what does not fit, so a box has to hold the whole 1.2 em line, and `zoneFace.test.ts` keeps every box inside its dashboard. The twelve pixels between the two are the leading under the baseline, which a digit does not use but the "N" and the "R" the gear also draws do. Reaching 86 means letting the box be cut below its line box, which is the author's to decide. |
| The hero that never moves | The Main artboard reads "Gear, speed, rev bar, flag and pit limiter are fixed per layout. Every other value is a card". **It predates the model**: zone A cycles four pages under ADR 0006, so the gear gives way to the speed or to the track map on a button press, and the speed was card 12 rather than part of the hero even under the model the sentence describes. Only the rev bar, the flag and the pit limiter are fixed on the zone face. The sentence wants marking superseded, as the DashComponents slot numbers already are. |
| DashComponents' zone A | The component sheet calls zone A "fixed on every layout" and describes the rev bar 40 tall in its well over a 1 px rule, the gear alone, a flag band 40 tall at the bottom edge and the limiter above the gear. That is the card face, which still builds and still draws precisely that. **The zone face follows the Zones artboards instead**: a 56 px bar of settled values takes the place of the rule under the rev bar, the segments are 32 tall inside a 40 px well, and the flag takes band D's sixty pixels rather than a strip of its own. The section wants the same superseded marking as its slot numbers. |
| The same five parts on every face | The catalogue's anatomy says the five parts differ only in size from one rectangular face to the next. Two of the per-size artboards draw otherwise: 800 × 286 has no bar at all, which leaves four parts, and 600 × 686 stacks A over B over C rather than setting B beside A beside C. **The per-size artboards are taken**, being the more specific drawing, and §1 tabulates both departures. |
| The gap chips on the face sheets | Each `FaceVariants` sheet counts the pages that do not fit its rectangle as the catalogue draws them, and the 1280 × 720 and 1280 × 480 sheets give every one of the twenty-one a shed count of nought. The catalogue's own `tall` drawings do shed: sectors keeps two of its three lap times, a leaderboard row loses its best and its last, and the opponents blocks lose the car number. **The drawings are taken**, since §5 was read off them; the counts are annotation over the top of them. |
| Lap times at `tall narrow` | The catalogue draws two times at 34 px in a 274 × 300 zone and leaves 234 px of it empty. **Four are taken**, one per line and grown to 45 px, 55 with the rev bar off, because the box the drawing answers is a real zone on the base face and a driver reads it at arm's length. The redraw is the canvas's to make and [#330](https://github.com/xorob0/OpenDash/issues/330) holds it open: the live canvas still drew two times at 34 on 29 September. |
| Session's sixth field | The catalogue labels it *Est. laps* at `wide` and at `tall`, where the build labels it *Laps left*. **The build's label is kept**, on two grounds. Firstly, the value behind it is `RemainingLaps`, which is the session's own count of laps still to run, and no research note here describes that property as an estimate, so *Est.* would be a claim the datum does not make. Secondly, *Est. laps* is already the label of the fuel page's sixth field, where it carries `Computed.Fuel_RemainingLaps`, that is to say the range left in the tank; two pages drawing the same two words over two different quantities is precisely the confusion the rename would introduce. Either the catalogue renames this one, or the session field is rebound to something that is genuinely estimated. |
| Session's third rank | The catalogue draws Strength, Incidents and Cars at `wide` and at `tall`, and **two of the three are built**. Strength of field is left out under [ADR 0009](../decisions/0009-does-the-plugin-compute.md), which found it published by SimHub in no form at all and struck it from the bar's catalogue of end fields for the same reason. The row is therefore two fields wide rather than three, and it closes over the hole the way [§11](#11-a-field-that-is-not-there) describes. |
| Lap history's third column | The catalogue draws the fuel each lap cost, at every shape, where the pit wall's wide page draws the delta to the session best. **The delta is taken, and only at `wide`**, because no previous-lap property carries a consumption beside the time and keeping one per lap would be the plugin remembering between frames, which [ADR 0009](../decisions/0009-does-the-plugin-compute.md) refuses. The three narrower shapes therefore list two columns where the drawing lists three, and [second-screens.md](../second-screens.md) records the datum that is not there. |
| A slower lap's colour | The catalogue paints every lap slower than the session best in red and draws no middle band, whereas the module steps through caution at half a second behind and danger at a full second. **The ladder is kept**, since a lap half a second off and a lap a second off are two readings and a driver acts differently on them. The two were nonetheless the same red for as long as the caution branch read `purpose.fuel.low`, which resolves to the danger colour, so the ladder said nothing until that was put right. |
| Lap history's row count | The catalogue lists six laps at `wide` and at `grid` and seven at the two tall shapes, while the companion artboard lists seven in a box the shape model reads as `wide`. **The catalogue is taken**, being the drawing of record at the four shapes, so the companion page lists six. A box too short for its declared count lists fewer regardless, which is why zone C of the 600 × 686 face lists four where its own sheet draws six. |
| Lap history's lap number | Every drawing of the page draws the lap number at the size of the time beside it, in the label grey: the catalogue's four, the companion's module 19 and the pit wall's two zones. **A step down the ramp is taken** ([#343](https://github.com/xorob0/OpenDash/issues/343)): 24 under a 34 px time, 16 under 24 and 14 under 18. Three columns at one size read as a block of digits, and the columns are not equal. The time is what a driver compares down the page; the lap number is an index, read to find a row and never across one, which is how a list already draws its car number beside the position. The grey is kept. The canvas owes the redraw on all seven drawings. |
| Lap history's row | The catalogue draws 34 px rows at 34 at `wide`, `grid` and `tall`, and 28 px rows at 24 at `tall narrow`; the pit wall's sheet draws 26 at 24. The build drew the density's row in every box and stacked the rows from the top, however tall the box. **The row now fills the body and carries the largest type its height and width allow**, never past 34 ([#343](https://github.com/xorob0/OpenDash/issues/343); §5's *How lap history answers its box* has the steps). At `tall narrow` that is 34 wherever the zone has the height, one step past the drawing: the drawing's narrow row carries a fuel column the build does not have, and the two columns the build does have fit at 34 in a 225 px zone. The canvas owes a narrow drawing of two columns, or a statement that the narrow page stays at 24 whatever it carries. |
| Lap history's fuel target | The catalogue writes `Fuel · target 2.85` over the third column at `wide`, and the companion and the pit wall's sheets do the same; `grid` writes the bare `Fuel`. **It is refused, together with its column** ([#343](https://github.com/xorob0/OpenDash/issues/343)). The target is a value in the fuel column's heading, and the build has no fuel column (see *Lap history's third column*), so there is nothing for it to head, and beside `Δ best` it would read as a target for the delta. It is also a number the driver sets, which no setting holds. [#326](https://github.com/xorob0/OpenDash/issues/326) is that setting and the colouring it drives, and it needs a per-lap consumption before it needs a heading. |
| The ramp ceiling at `tall` | Rule 20 stopped a rank at the next size up its ramp, which is one step of about 1.35, while the catalogue promotes far harder at `tall`: lap times 46 to 88, the delta 64 to 132, the speedo 64 to 128, and fuel, sectors, stint and session 34 to 76. **The ceiling was too low, and on a face it is now the ×2.2 the `FaceVariants` sheets grow every page by** ([#330](https://github.com/xorob0/OpenDash/issues/330)); §2 has the argument, which is that the one factor rather than the ceiling is what keeps filling distinct from scaling. The companion and the pit wall keep the ramp step, their artboards being drawn at their own size. What the ceiling does not settle is the other half of these numbers: the catalogue's `tall` drawings do not only grow, they promote, the lead rank going up while the rank under it stays at 34, and that is rule 17's lever rather than rule 20's. **The promotion is taken too**, as `leadRankSize` in `modules/module.ts`: on a face whose zone is tall a page's lead rank is drawn at the next name up the ramp, `hero` over `mid`, 64 over 34 in a zone, which is the catalogue's lap times to within a few per cent, and rule 20 then grows the page into its box. Lap times takes it, and at the 1280 × 720 face draws 100 over 53 where it drew 62 over 46; session and stint take it too, their `tall` drawings promoting a first rank the others draw at 34 to 76 over the same 34, and draw 84 over 45 there. At the catalogue's own 360 × 470, which no face produces, the stint's promoted rank meets the height at 64 and the average lap its table sheds first goes. The delta, the speedo and the fuel promote at `tall` on the catalogue as well, and each has more to settle than a size, so the lever is theirs to take in [#331](https://github.com/xorob0/OpenDash/issues/331), [#333](https://github.com/xorob0/OpenDash/issues/333) and [#334](https://github.com/xorob0/OpenDash/issues/334); the sectors page is a drawn strip over a rank, which the strip sizes. |
| The mini-sector strip | The catalogue and both companion artboards draw twelve mini-sectors under the sector times. **Three cells are taken**, one per real sector, because SimHub times sectors and not segments and [ADR 0009](../decisions/0009-does-the-plugin-compute.md) forbids inventing the data a twelve-cell strip would need. The canvas owes either a redraw at three or a caption saying the twelve are notional. The same strip is a second disagreement of its own: the FaceVariants sheets draw the sectors page as two ranks with nothing between them, so the 6 px strip the build puts there is an addition the drawings do not carry, and it is deliberate rather than accidental. |
| Lap times at the companion | The companion artboard draws twelve fields in four ranks of 64, 46, 46 and 34, and the build draws nine: at the module box the build really hands the page, 802 by 336, the four ranks come to 337 px against 332 of room and the sector rank is shed. The 20 px are the flag band, which the artboard draws 12 high and `ds.indicator.flagBand.heightSm` gives 32. **The shedding is taken** rather than a page drawn past its box, and the twelfth field returns if the band ever comes down to the artboard's height. |
| The sector deltas' size | The companion draws the delta page's S1/S2/S3 rank at 34 px and the catalogue draws it at 34 as well, which is `small` on one ramp and `mid` on the other; the page therefore names the ramp rung by density rather than by one token. The same question decides the recap under the sectors: 34 on the companion and 24 in a zone are both `small`, and a compact zone's `small` is 18 where the 800 × 480 sheet chips 24. |
| Three sector columns in a narrow zone | The catalogue draws the sectors page as three columns at every shape, including `tall narrow`, where three 34 px sector times and their gaps need 286 px of a 274 px zone. The build used to reach three columns by stepping the rank down to 18 px, which is the page shrinking the reading it exists for. **The rank now keeps 34 and wraps to two lines and one**, per rule 17; the canvas owes the redraw, as it does for lap times at the same shape. |
| Spreading or centring | Whether a page spreads its ranks over the full height or centres them as one block is decided page by page on the catalogue and not by shape: sectors, fuel, session, stint, the speedo and car settings spread at all four shapes, lap times spreads at three and centres at `tall narrow`, the delta centres at three and spreads at `tall`, and the lists, the drawings and the pit view centre everywhere. The engine therefore takes it from the page (`justify: 'spaceBetween'` on `stack`) and centres by default. |
| The flag once it has settled | No artboard draws a flag anywhere but across the whole band, so the block at each end that [§6](#6-band-d--eight-pages) describes is an addition rather than a reading of a drawing. **It is taken** because the alternative is the case [#380](https://github.com/xorob0/OpenDash/issues/380) opens with, namely a full course yellow written over an unreadable fuel page for the length of the caution. On the four faces with corner blocks the addition is nearly a rectangle the canvas does draw, though not exactly one: it covers what a corner block holds and, the corner width being those two fields plus the band's side padding plus the room the zone letter stood in, the padding and that room as well, which before [#708](https://github.com/xorob0/OpenDash/issues/708) meant that band D had no **D** for as long as a flag was out; §6 says why that was the cheaper of the two prices available. The block writes the flag's name there, and the blue flag's detail as well, BLUE · P4 GT3 as `BlueFlagDetail` asks ([#497](https://github.com/xorob0/OpenDash/issues/497)), since the widest it can draw, BLUE · P99 LMP2, is about 110 px against blocks of 228 and 350; the incident's count against its limit is the one run left to the three seconds of the takeover. On 850 × 480, 800 × 480, 800 × 286 and 600 × 686 there is no such rectangle and the flag keeps the side padding, which is 16 px of colour at each end and 12 in portrait: enough to say a flag is still out and not enough for a word. **The word is what a colour cannot carry**, so on those four faces a settled flag names a family and not a member: DISQUALIFIED and BLACK are one outlined sliver. DEBRIS is not YELLOW there, its stripes being drawn at any width since [#498](https://github.com/xorob0/OpenDash/issues/498). Widening it there means taking room from the page, and the 600 × 686 fuel page has 6 px of slack, so the canvas owes either a drawn settled form for those four faces or the judgement that colour alone is enough once the name has had its three seconds. |
| The whole-track caution's name | PagesAndAlerts calls iRacing's `caution` Safety car (7 · SafetyCar), and the build used to call it three things: SAFETY CAR on the band, SAFETY on the full-screen block, and "Full-course caution" on the LED row, the site and the flag table. **Full course yellow is taken** ([#497](https://github.com/xorob0/OpenDash/issues/497)) on every surface that writes a name, the 8x8 box aside, which writes SC for want of columns ([#499](https://github.com/xorob0/OpenDash/issues/499)), and its name is as long as the room: FULL COURSE YELLOW on band D and its corner blocks, and on every full-screen block where it stays legible at a size of its own, which the author ruled is half the size the one-word names set or more, FCY on the others, which are the portrait screens'. The colour stays `purpose.alert.safetyCar`, which resolves to the flag yellow, and the token keeps its name. The canvas owes the rename, and [flag-box.md](flag-box.md#where-the-names-depart-from-the-canvas) says which block writes which form. |
| Every flag's name | The face artboards and DashComponents write "Yellow flag", "Blue flag", "Green flag" and "Black flag" on band D, and PagesAndAlerts adds "Red flag", "Black flag · furled" and "Blue flag · GT3 behind". **No flag's name says FLAG** ([#497](https://github.com/xorob0/OpenDash/issues/497)): band D, its corner blocks and the pit wall's band write RED, BLACK, YELLOW, BLUE and GREEN, the blue flag's detail reads BLUE · P4 GT3, and the furled black reads BLACK as the black flag does, so that on the band and on the full-screen block the two are one drawing. The canvas owes the rename, and [flag-box.md](flag-box.md#where-the-names-depart-from-the-canvas) lists the drawings concerned. |
| The debris flag's stripes | PagesAndAlerts draws 19 · Debris as `repeating-linear-gradient(135deg, #FFD400 0 20px, #FF2D46 20px 32px)`, with the name written straight over the stripes. **Vertical stripes are taken** ([#498](https://github.com/xorob0/OpenDash/issues/498)): a diagonal stripe is a rotated rectangle clipped to the band, and [simhub-dash-format.md](../research/simhub-dash-format.md) establishes neither, whereas the real flag's stripes are vertical and need nothing SimHub is not known to draw. They are equal and an odd count, so the flag opens and closes on its yellow, never fewer than three, so that a sixteen-pixel settled block still holds a red one, and as wide as the chequer's check on the same rectangle: half the band high on the band and the nano, and the full-screen chequer's column on the block, which is three columns and a single red stripe on the portrait face, the portrait companion and the portrait pit wall. The name is written on a plate of the yellow, which the sheet does not draw, since a stripe half the band high is narrower than DEBRIS and the word would straddle an edge. The canvas owes the vertical drawing and the plate, or the ruling that a rotated rectangle is worth establishing for a diagonal. The 8x8 box keeps its diagonal stripes, which are pixels rather than rectangles. |
| The meatball's band | PagesAndAlerts draws 18 · Meatball as a plain band of `purpose.flag.orange`, which is the caution amber, named MEATBALL. **A black box with an orange disc in the middle and no text is taken**, which is the author's ruling on [#498](https://github.com/xorob0/OpenDash/issues/498): the flag is black with an orange disc, and a band of amber is neither of its colours. Band D, its corner blocks, the nano's strip, the companion's and the pit wall's bands and the full-screen block therefore draw the opaque `surface.base` ground with no border and no name, and a disc of the orange in its middle, two thirds of the rectangle's shorter side, which is the real flag's proportion and keeps the disc inside a sixteen-pixel settled block. The disc is a fifth shape beside the catalogue's bands, outlined bands and two patterns, drawn with the ellipse the round faces' rings already use. Having no name, the meatball no longer sets the full-screen block's type, whose widest name is now INCIDENT. The 8x8 box keeps its disc and the LED strip keeps the meatball on the black flag's lamp. The canvas owes the redraw of PagesAndAlerts 18 as the disc on black, or the ruling that the flag is an orange band. |
| The relative's row count | The catalogue draws six rows at `wide` and at `grid`, eight at `tall narrow` and eleven at `tall`, and the build divided the body by the row instead, which reached thirteen in zone C of the 1280 × 720 face and fifteen in its second arrangement. **A declaration is taken**, per shape since [#328](https://github.com/xorob0/OpenDash/issues/328): three cars either side of the player at `wide`, `grid` and `tall narrow`, seven rows, and five either side at `tall`, eleven, with at least one either side however short the box. The question the page answers is *is the car behind me going to be there at the next corner*, which is the two either side; the third is the traffic a driver is about to be in, and the fourth and fifth are worth their rows only where the zone is tall. Seven is the one odd count between the catalogue's six and eight. The canvas owes the count at every shape: one fewer than it draws at `tall narrow`, and one more at `wide` and at `grid`, where the companion artboard already draws seven. |
| The leaderboard's row count | `ZoneCatalogue.dc.html` draws six rows at `wide`, `grid` and `tall` and eight at `tall narrow`; the companion artboard draws seven; and the build divided the body by the row, which reached fifteen in zone C of the 1280 × 720 face. **A declaration is taken** ([#328](https://github.com/xorob0/OpenDash/issues/328)): the catalogue's six at `grid` and eight at `tall narrow`, the companion's seven at `wide`, which is what the `wide` column of §5's table stands for, and eleven at `tall`, where the catalogue's six leave 250 px of its own 360 × 470 drawing empty and its relative lists eleven in the same box. **The declaration is a face's and the companion's, and not the pit wall's**: a pit wall zone lists every car its box holds, which is the ticket's own exception — buying rows is right where the question is *who is in the race* — so its `519 × 359` and `639 × 338` zones list the eight they listed before rather than the six and seven their shapes would declare. The companion's portrait page, which no artboard draws, takes the `tall` count and lists eleven rows of 64 where it listed seventeen of 40: a companion is one page read from the seat, the same eleven as its relative, with the leftover height spent as air between the rows rather than on six more cars. The canvas owes the `tall` drawing, a ruling between six and seven at `wide`, and a portrait companion artboard that says whether its leaderboard is eleven rows or the field. Which cars a zone's leaderboard should list, the head of the field or the cars around the player, is not decided: #328 left it to [#340](https://github.com/xorob0/OpenDash/issues/340), which took the columns and not this, and the build lists the head of the field. |
| The list row's height | The canvas states one row height per shape — 34 px, and 28 in the narrow zone — and the build stretches it, on the relative since #339, on the leaderboard since [#328](https://github.com/xorob0/OpenDash/issues/328), and on lap history since [#343](https://github.com/xorob0/OpenDash/issues/343), whose own row is described in *Lap history's row* above. A declared count in a box that holds more leaves the difference somewhere, and `table()` leaves it above and below the block, `justify-content: center` being what every list body on the catalogue carries: eleven rows of 28 px in the 560 px body of the 1280 × 720 face's second arrangement is 328 px of list and 232 px of nothing. **The row fills the body instead**, in every box, and the type is chosen apart from it: the largest step of the list ramp the row's height and the name's budget both allow, and never past the 38 px row the canvas's tallest table draws, so what is bought above that is space between the rows and never size. §5's *How a list answers its box* has the steps and the budget rule. Until #328 the type was read off the height, so a box whose fill would have crossed a step that cost the name a letter kept a shorter row and left the rest as slack — five of them, zone C of the 1280 × 720 among them with 133 px above and below its block; they fill now, at the type they had. The canvas owes a row height per box rather than per shape, or a statement that the pitch is the build's to choose. |
| The driver name's size on a list row | The catalogue draws it at 13 px in every row of every list at every shape, under a gap drawn at 34. **15 is taken from the 34 px row up**, which is what the face labels at everywhere else and what the pit wall's own boards already draw a name at; `density.ts` calls 13 the floor rather than the size, and a column that says *who* sitting on the floor of the ramp is what [#339](https://github.com/xorob0/OpenDash/issues/339) is about. **13 stays in the 28 px row, and the reason is the catalogue rather than the count.** This row first said 15 would cost the narrow column three characters, "seven at 13 and five at 15 in the 82 px the 850 × 480 face gives it", and that was wrong everywhere it applied: the three boxes the 28 px row is handed are 82, 77 and 57 px of name column, where 15 costs one character, one character and nothing — seven against six, six against five, four against four. The six counts are pinned in `tables.test.ts`, a documented number nothing runs being a number that rots. **What [#328](https://github.com/xorob0/OpenDash/issues/328) changed is that a narrow zone is no longer held to the 28 px row.** A list that declares fewer rows than its box holds at 28 has the height the canvas draws a 15 px name in, and takes 15 there with its numerals held at 24, paying the one character: zones B and C of the 850 × 480 and 800 × 480 faces, both arrangements, and the 800 × 286 face without its rev bar. That is the ticket's question answered for the build — a 13 px name is not the right answer 600 mm from the eye where the zone has the height for 15 — and it is a second divergence the catalogue owes a redraw for: its `tall narrow` lists draw eight rows of 28 with the name at 13. Where the row is still 28, on the 800 × 286 face with its rev bar, the name stays at 13 with it. |
| The opponents page's name and gap | The catalogue draws the page at the same size in every box it gives it: the name at 13 px beside the car number, the gap at 46, and the two blocks as one centred group with twelve pixels either side of the rule, so its `tall · 360 by 470` drawing leaves most of its own height empty and the portrait companion page drew 236 px of nothing above the page and below it. **The build takes three things the drawing does not** ([#341](https://github.com/xorob0/OpenDash/issues/341)): the name at 15, which is the relative's answer and the list row's, wherever the row has the width, which is every box the build produces; the name's box widened to the ten characters of the default format where the row has them; and the gap grown from the density's `big` under rule 20 — past 46 as far as 101 in a zone and past 34 as far as 74 on the compact faces, which is the face's ×2.2, and past 64 as far as 116 on the companion — with the height it does not take spent between the two cars so that the page spans its zone. Only the gap grows: the heading and the recaps keep a label's size and the identity row a list row's, which applies rule 20's *all of the stack grows or none of it does* to the gap alone, and whether the car number and the recaps should grow with it is the author's to rule. The canvas owes the redraw at `tall` and at `grid`, and a ruling on whether the space goes between the cars, which is what the build does, or around the group, which is what `justify-content: center` does. Its `tall narrow` drawing also centres the group across the zone, which the build does not do, and its zone catalogue draws the car number at 24 where the pit wall sheets and the build draw it at 16. |
| The driver column's three-letter code | Every list on the catalogue and on the opponents page draws the driver as three upper-case letters, `KLX` and `MOR` and `TSA`, at 13 px and at every shape. **A name is taken instead.** The code was built as `left(name, 3)`, which makes Liam Byrne `LIA` and Hannah Fischer `HAN`: it identifies nobody and collides for any two drivers who share a first name, and it is drawn on the one page a driver reads to answer *who is that*. What the build draws is one of four formats the rig chooses between — the full name, `L. Byrne`, `B. Liam` or `Byrne Liam` — cut in the expression to the characters the column holds and closed with an ellipsis where it was cut, since WPF clips rather than truncates. The canvas owes the redraw at all four shapes, and with it an answer to the width, because **on the three narrow faces no format fits whole.** The budget per box, in characters of the widest glyph the name face draws, is 7 in the 82 px zone C of the 850 × 480 face, 6 in the 77 px of the 800 × 286 and 4 in the 57 px of the 800 × 480; 8 on the companion's portrait page; 10 or more everywhere else. `L. Byrne` is eight, so even the shortest of the four ellipsises at `tall narrow` — `L. BYR…` at 850 × 480 and `L. BY…` at 800 × 286, shouted by the row below and cut here — and at 800 × 480 the four formats draw `LIA…`, `L.…`, `B.…` and `BYR…`, which is one glyph more than the `LIA` this row deletes for two of them and the same three for the other two, a cut that lands on a space losing the space with it. `driverNames.test.ts` evaluates those four. **The column stays regardless**, a list of gaps belonging to nobody being [§11](#11-a-field-that-is-not-there)'s own failure and what `THE_ROW` refuses for as long as the name holds a letter and its ellipsis, which in the 28 px row is down to 191 px of body — 34 px under anything the build produces (see *What a row gives up* in §5). The lever the row still has is the position: the gap's 92 px is the canvas's floor over content that needs 79 at 24 px, so dropping a decimal would not narrow it, whereas giving the name the position's 40 px buys 11, 11 and 9 characters at the three faces. It is not taken, the position being what says whether the car behind is racing you or lapping you, and a third column dropped from a list being the canvas's decision rather than the build's. What the canvas therefore owes at `tall narrow` is either 93 px of name column — the eight characters the shortest format needs at 13 px — out of zones 249 to 274 px wide whose bodies are 225 to 250, or a column set with one column fewer in it. [#385](https://github.com/xorob0/OpenDash/issues/385) decided the formats; [#149](https://github.com/xorob0/OpenDash/issues/149) wants the same width again for a licence badge and a rating. |
| The driver name's case on a list row | Every artboard writes the driver column in the sim's own mixed case, `Liam Byrne` and `Hannah Fischer`, the name being the one piece of prose on a row of labels. **Upper case is taken at every size a list draws a name at**, and the reason is a glyph rather than a preference. Barlow's `i` is a stem with its dot floating 0.080 em above it, which is 1.04 device pixels at the 13 px of the 28 px row; under two device pixels there is no pixel row the gap is certain to fall wholly inside at any sub-pixel phase, so the raster may shade the row above and the row below at partial coverage and bridge them. Four of the nine names in zone C of the VM's 850 × 480 face came back welded — `Llam B…` for Liam Byrne, `NIna H…` for Nina Hartmann, `Sofla …` for Sofia Rossi, `Henrlk…` for Henrik Solberg — and since `i` and `l` are the same height to within 0.017 em that is a different letter and not a blurred one, in the one column of the one page whose whole job is to say *who*. The three remedies answer to the same measurement and `advances.test.ts` holds all of it, read back out of the bundled outlines: **a heavier weight closes the gap** rather than opening it, 0.057 em at Bold against 0.080 at Medium, a fatter stem and a fatter dot being drawn into the same vertical; **a bigger size costs letters and still does not clear the bound**, 15 px buying 6, 5 and 4 characters in the three narrow boxes where 13 buys 7, 6 and 4; **upper case costs nothing**, no *unaccented* upper-case letter in any bundled face being drawn in two pieces at all, and the budget being counted in characters of the face's *widest* glyph, so shouting a name changes no budget anywhere and the six counts in the row above are the same numbers after it as before. **What it buys is the substitution rather than the construction**, which is the limit worth writing down: an accented capital is a mark floating over a letter, the same shape the `i` failed at, and four of them are tighter than it — `É`, `Å`, `Í` and `Ö` break at 0.064 to 0.076 em in the name face against the `i`'s 0.080, every one under two device pixels at 13 px and at 15 — so RÄIKKÖNEN may still come back with an umlaut welded to its A. That is a letter drawn badly where the `i` was a name read wrongly, a welded acute leaving `É` an `É` where a welded tittle left `Liam` a legal `Llam`, and there is no tighter bound to reach for, these being the marks the bundled faces draw. The line is therefore a size, `dottedLetterSize(NAME_FACE)`, which is 25 px — where the gap first reaches two device pixels — and every name any list draws is under it: 15 from the 34 px row up and 13 in the narrow zone's, the opponents page taking the same answer since [#341](https://github.com/xorob0/OpenDash/issues/341) where it drew 13 and 12 of its own. So every list shouts, the team name a rig may show in a driver's place with them, and the player's own `YOU` stops being the only shouted thing on the row. The canvas owes the redraw, or a ruling that the driver column may stay prose at a size no list is drawn at. [#339](https://github.com/xorob0/OpenDash/issues/339) is where it was found. |
| What a name is corrected to before it is formatted | #385 asked for a bracketed prefix from the sim's entry list to be stripped and the words title-cased before any format is applied. **Neither is built, and neither can be.** SimHub's NCalc has no `indexof`, `substring` or `length`, so nothing in an expression can find the closing bracket of a prefix whose length varies; and its `tcase` is .NET's `TextInfo.ToTitleCase`, which lower-cases the rest of every word it capitalises unless the word is entirely upper case, so it turns McDonald into Mcdonald and leaves a shouted name shouting — which is the case it would have been reached for. A name is therefore drawn with the sim's own spelling and its own prefix, upper-cased by the row above and corrected in nothing. The two are not the same act: `ucase` is total, so there is no name it can get wrong, where `tcase` guesses at which letters a word wanted. Both would be corrections the plugin could make, and it cannot either: a name belongs to a car on a row and the plugin publishes no per-row property. |
| A short box's ranks | `keepsSecondaryRanks` says a short box keeps one rank, while `archetypeOf` hands a wide short box the `grid` answer, which keeps two. The code follows `archetypeOf`, and the helper is unused. Either the short boxes the build produces get a fifth declared answer, agreed with the canvas, or the helper goes so that one rule governs. |
| The tyre temperature's unit | Every drawing of a tyre corner writes the temperature as `84°`, a degree sign and no scale, beside a pressure that does carry `psi` and a tread that does carry `%`. **The scale is drawn**, `°C`, `°F` or `K` by the driver's own `TemperatureUnit`, because the sign alone says that the figure is a temperature and not whether 85 is a cold tyre or a cooked one, which is what [#384](https://github.com/xorob0/OpenDash/issues/384) was filed about. The mark leaves the value's cells and follows it as a unit the way the pressure's does, flush against the figure at the five pixels the drawing leaves between a reading and its unit -- a two-digit temperature would otherwise carry its scale a whole cell further out than the corner beside it reading three -- which costs a zone's row eleven pixels, taken off the drawing beside it under rule 18, and saves a companion's four, the degree having had a 64 px cell there and the mark taking a 13 px one. No reading is shed anywhere the build draws: the narrowest corner the catalogue cuts is 107 px across against the 75 a labelled temperature needs. The canvas owes the sign a scale, or a ruling that a tyre page may assume Celsius. |
| The compound chip's label | The same ticket asked for a label on the compound as well, and **no word is drawn beside the chip**, because nothing on the axle line has room for one: the chip is 78 px wide and centred there, the only band free of the two inboard drawings is the column gap, 18 px in the grid and 28 at the widest, the chip already overlaps each front tyre by about 30 px, and `Compound` at 62 px more would cover a third of each of them. **What names it is a caption under the grid**, beside the one the canvas already writes about the tick, and the caption line is where the disagreement now sits. It holds three sentences at 1007 px and two below that, so a third caption costs one of the canvas's two, and **the tread's is the one given up**: its columns stand beside a figure already carrying its own per cent, where the tick and the compound carry nothing else, which is the test #384 sets. So the pit wall's 1039 × 255 tyre zone draws all three, the companion page and the two 769 px face zones draw the tick's and the compound's where they drew the tread's and the tick's, and the twelve narrower boxes that draw the chip name the tick alone. What the canvas owes is either the room for a label on the axle line, or a shorter set of sentences, or the ruling that a chip drawing a word speaks for itself — the class chip on both leaderboards is an unlabelled word for that reason, though a one-letter compound is a poor word. Where PARTS sheds the footer outright, at `tall narrow` and on the compact faces, the tick is drawn with nothing naming it and the chip is not drawn at all. |
| Band D's four tyre temperatures | The catalogue draws them as one line of equal cells under a single `TYRES °C`, which is what the build lays, and says nothing about how a reading shorter than its cell sits in it. **Each is centred in its cell**, where every other value on the band is set against the left edge of its own. Nothing on that page labels the four, so the reader maps them onto the car by the order they are in and the set has to read as evenly spaced; set left in cells cut for three digits, the VM photographed `52   113 88   95`, which at 34 px is 24, 8 and 24 pixels of ink apart, the middle two crowding into one group. The three alignments were measured: left gives 24, 8, 24 and right gives 8, 24, 24, sixteen pixels between the widest gap and the narrowest, which is a whole digit cell; centred gives 16, 16, 24, and is the only one of the three that puts each figure's middle on the 56 px pitch the cells are laid on. The price is that a corner crossing a hundred moves half a cell, which is accepted here and refused for a field with a unit after it: this is a set read as a set, and nothing follows a reading that the shift would open a gap in front of. The canvas owes either the same centring on the sheet or the ruling that a crowded pair is what it wants. |
| A unit and the budget its figure is cut from | The rule [#384](https://github.com/xorob0/OpenDash/issues/384) gave the tyre corners is now every surface's: a follower sits beside the figure and not at the end of the cells the figure is laid in. What the VM photographed is what it is for -- band D's `MARGIN −4` carried `MIN` four empty cells out, nearer `EST. LAPS` than its own number, and `FUEL 30.35` carried `L` one cell out -- and nothing in the drawings asks for it: every artboard writes its readings flush, `84°`, `30.35 L`, `81 km/h`, at the one gap its sheet gives. **The figure keeps its cells** and only the mark after it moves, because right-aligning the value would trade one jitter for a worse one, and **the row is still measured at the budget**, so no width, no shedding and no page order moves with this. Every bound value that carries a follower now declares how wide it really draws, in `second/drawn.ts`, and one that declares nothing is refused where it is drawn rather than placed at the end of its budget. `followerPlacement.test.ts` holds both halves, the design-time gap and the runtime binding, for every follower of every package the build emits. |
| Band D's energy budgets | D2's **Energy** and **Refuel** are cut for three bare digit cells, `CHARS.temperature`, and `notAvailable` formats every reading on the page `0.0`, so a car that publishes 68 per cent draws `68.0`: four characters and a point in a box that holds three digits, which WPF clips. The estimate and the per-lap figure beside them are cut for `CHARS.consumption`, four cells and a point, and hold `100.0` exactly. **Widening those two to match is the answer and is not taken here**, because two fields gaining 23 px each changes what the rank sheds on the narrow bands and that is a page redraw rather than a placement fix. What is taken is that the `%` after each **stops at the budget**: a declaration wider than a field's own cells binds the mark outside the region the box was measured for, which `drawnWithin` now refuses at build time, so the mark sits at the end of the ink the driver can see rather than 25 px past it. |
| Zone A's centred row | Zone A is the one place the build still draws a mark at the end of a budget, and the reason is that its rows are centred as a group: a speed is cut for `299` and the rpm for `123,456`, so `81 km/h` and `5,851 RPM` leave one and two empty cells inside the group, and pulling the mark in moves the ink off the column's middle by half of that. **The cells are kept**, because centring on the ink instead costs the portrait face something real: at 600 × 268 the row is fitted to the column and has two pixels of slack where it would need thirty-one, so A3 would have to drop its rpm or step its speed down a size. The canvas owes the ruling -- a row centred on what it draws, and a hero number that shifts as it gains a digit, or the hole beside a short reading. |
| The bar's right-hand end | A field of the bar's left end is drawn from its own edge and its denominator now follows the figure, so lap 4 and lap 16 both read `/ 32` at the six pixels the artboards give. A field of the right end is laid from the padding inwards instead, the denominator flush against it and the value right aligned one gap in front, which is what the artboards draw and which keeps the gap constant as the figure changes. What stands between the two runs of ink there is twelve or thirteen pixels rather than six, being the narrow cell `/ 32` leaves unused in the four-digit-and-a-special budget `/ 100` needs. **The artboards' arrangement is kept**; closing the gap means either a budget the denominator fills or a denominator drawn from its own left edge, which leaves the padding short on a one-digit field size, and the canvas has decided neither. |
| The last lap as the delta's reference | Every drawing of the delta on the canvas is against the session best, the build already offered the all-time best as a caption the canvas does not draw, and [#322](https://github.com/xorob0/OpenDash/issues/322) adds a third, the last lap, captioned **vs last lap**. It is iRacing's own live delta to the lap before this one, since SimHub's lap tracker publishes none. The word is not the lap review's *vs previous*, which the canvas does draw: that is a finished lap against the one before it, read once at the line, and a driver who sees both should not take them for one comparison. The caption is shorter than *vs all-time best*, which is still the widest the box is measured by, so nothing moves. |
| Lap times' "Delta to your best" | The companion artboards and `CompanionModules` label the Lap times delta *Delta to your best*, the catalogue and the 1280 × 720 face *Delta to best*, and the build follows the first. Under the last-lap reference the words are false, since the value is then a delta to the last lap, so **the label is bound**: *Delta to last lap* under that reference and the canvas's words under the other two, the session best and the all-time best both being the driver's own. The bound label is narrower than the canvas's, which is what the field is still measured by. |
| The delta to thousandths | Every drawing of the live delta, on the card, the delta page, Lap times, the lap pop-up and the pit wall, writes it to two places, `−0.21`, and nothing on the canvas offers a third. [#322](https://github.com/xorob0/OpenDash/issues/322) lets a driver ask for thousandths, which is what a hotlap is decided by, and **every box that draws the live delta is cut for three places whichever is chosen**: the sign, two whole digits and three decimals, `−12.345`, which is what each of the five declares as its `widest` and what the fit tests measure it by. A delta of 100 s or more, which a long stop in the pits can make, is drawn to hundredths at either precision, `+100.00`, since a third place would need a seventh cell, so the six cells hold every reading under 1000 s. A box cannot change its cells at runtime, so the alternative was a second drawing of each surface per precision, which buys back one empty cell. The samples stay the canvas's `−0.21`, and the sector deltas, the lap review and the lap history keep the two-place budget they had, being other comparisons. What grows is the box: card 3's value is 207 px at the L rung where it was 176, the delta page's number is 367 on the companion where it was 312, and Lap times' delta and the pit wall's gain a cell each; nothing is shed and nothing leaves its frame. The band inside which a delta is level narrows with it, to half a thousandth, since a figure that shows four thousandths and colours them level says something and then takes it back. |
| The delta page's caption on the portrait companion | The canvas draws the reference caption beside the number wherever there is room across, and the portrait companion had it at two places: 306 px of number, ten of gap and 102 of caption in 432. **At three places the caption goes under the number, and it does so at both precisions**, because beside it the pair is 473 px and the box is cut for three places whichever is drawn. The number moves up 20 px with it. No artboard draws the portrait companion's delta page, so this is not a drawing departed from, but it is the one page whose arrangement the setting changed for a driver who never touches it. |
| A centred delta in a narrow zone | The compact zones of the 800 and 850 faces centre the delta page across the zone, and a centred field centres its box rather than its ink. The two-place box already had one cell more than `−0.21` fills, the second whole digit, so the figure sat half a cell, 11 px at 46, left of the zone's centre. The box now holds three places, so **a two-place figure sits a further half cell left**, 22 px in all, and a three-place one sits where the two-place one used to. Lap times, which centres each of its fields across the same zones, moves too, though less and at both precisions: its delta field was as wide as its label, 100 px, and is now as wide as its three-place box, 105 px, so the label and the figure both sit 2 px further left on the 850 and 3 px on the 800. With the rev bar off, where the zone is tall and the page promotes its delta to 39 px, the field goes from 106 to 125 px and both sit 9 px further left on the 850 and 10 px on the 800. Centring on the ink would need the field's `Left` bound to its reading, which the page's other rows do not do and the canvas has not asked for; the offset is recorded rather than bound away. |
| Lap times at the 1280 × 720 face with the rev bar off | Rule 20 grows lap times until a rank meets its box, and at this face's 445 × 560 body the rank that meets the width first is the one along the foot: laps, the estimate and the delta. [#330](https://github.com/xorob0/OpenDash/issues/330) grew the page there to 108 over 57. The delta's box now holds three places whichever precision is chosen, so at 57 the foot no longer fits across, and **the page stops a step sooner, at 100 over 53**, which is what it draws with the rev bar on: the delta's box is 164 px at 53 where the two-place one was 150 at 57, and the three times are drawn at 100 px where they were drawn at 108, so that the cell a third place needs is there. Keeping 108 would need the foot's delta cut for two places at this one size, which thousandths would then overrun, or the foot to wrap, which no drawing of the page does. The loss is recorded rather than designed away, and is the canvas owner's to rule on. |

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
| Band D's eight pages, D1 to D8 | **Built**, as `zoneface-band-1280x60`. D8 is built and held back from 1.0, its screen drawing fuel ([#969](https://github.com/xorob0/OpenDash/issues/969)); what it is still short of is in the table above. |
| The flag over the band, in six colours | **Built, and wider than the sheet asks**: `flagStrip` draws all twenty conditions of `ALERT_CATALOGUE` over band D's rectangle, the fifteen flags and five car alerts, in the four shapes of the alert catalogue and the meatball's disc, where the sheet draws the six SimHub normalises. The black family keeps a `surface.base` ground rather than its own token, which is the ink. |
| The alert catalogue's car alerts | **Built from the five iRacing publishes, with three departures the author owes a ruling on** (#762). The ignition sits above the stalled engine, where PagesAndAlerts numbers them 2 and 1, because the pit family already ranked them that way and a face should not answer the same pair in two orders either side of the pit entry. The incident was a fourth, outlined where the sheet fills it because `purpose.alert.incident` and `purpose.flag.orange` are both `#FFB300` and the meatball was the filled band in it; since [#498](https://github.com/xorob0/OpenDash/issues/498) the meatball is a disc of that amber on the near-black, and the incident is filled as the sheet draws it. Push to pass and the headlight flash are drawn only where their name is written, because `purpose.alert.p2p` is white: filled it is the white flag and outlined the black family, and the nano, a sixteen-pixel block and the full-screen block would draw it without the word. And "Push to pass · 3 left" is written without the count, which iRacing publishes with two meanings by session type. The tokens are left as they are; the canvas either gives the two neutral alerts colours of their own or accepts these shapes. |
| A full-screen flag over zones B, A and C, with `OpenDash.FlagFormat` set to band or full | **Built.** The property carries a face's prefix, as the zone settings do, and it is declared, mirrored, defaulted to `band` and offered on the screen's own pane. It did not need the further pair of arrangements this row once predicted: `components/flagFull.ts` draws one opaque block over the body rectangle, derived from the layout, and `face.ts` gates the band group and the block against each other, so one screen carries both. `flagFormat.test.ts` holds the block against the sheets at all eight sizes and in both rev-bar arrangements. The block reads band D's own catalogue through the band's own expression, eighteen of its twenty conditions, the two neutral alerts apart, and names each condition but the chequer and the meatball, which are their own flags, in a word short enough for a block measured on the longest of them, which is INCIDENT. The full course yellow is the one condition with two names, and its word in that measure is FCY: the block writes FULL COURSE YELLOW in its place, at the largest size at which the whole name fits, up to the one size the other names are set at, wherever that size is at least half the one size, which is every block but the portrait screens', and FCY on those ([#497](https://github.com/xorob0/OpenDash/issues/497)). |
| The chips "bar: 2 fields per end" and "band corners: yes" | **Built**: `barFieldsPerEnd` is 2 and `bandCorners` is true at this size. |
| The chips "A grid", "B grid", "C grid" and "D grid" | Three of the four are what `shapeOf` returns for those rectangles. The fourth is the disagreement recorded above. |
| A growth factor per page of zones B and C, from ×1.08 to ×2.07 | **Recorded, not checked**, although the ceiling the chips are measured against is now the build's own: a rank on a face grows by rule 20 until it meets the width, the height or ×2.2, as the sheet's pages do. The factors still differ wherever the build's drawing of a page differs from the catalogue's, and nothing compares the factor a page reaches against the factor the sheet chips. |

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
