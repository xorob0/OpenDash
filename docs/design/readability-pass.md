# The readability pass: one ticket per page

**Status:** ticket bodies, not a design document. Everything below is written to be pasted into
Linear as an issue in team Xorob, project *The zone face*, and it is here rather than there because
Linear refused every create with `You've exceeded the free issue limit for this workspace` on
2026-09-13. Move them across and delete this file; the numbers they will get are not known yet, so
nothing cites them by number.

Read [zones.md §2](zones.md#2-the-shape-model) first — rules 17, 18 and 20 are the vocabulary every
one of these uses — and the catalogue artboard
[`design/canvas/ZoneCatalogue.dc.html`](../../design/canvas/ZoneCatalogue.dc.html), which is the
source each of them is measured against.

---

## Parent — Every page is laid out for the box it has, and eighteen of them are not

Rule 20 landed: a rank of fields fills the box it is given, growing until it meets the height, the
width, or the next size up its density ramp. `packages/dash/src/second/layout.ts` does it and
[zones.md §2](zones.md#2-the-shape-model) writes it down. It moved three pages — lap times, car
settings and session — because those three are ranks of fields and nothing else.

The other eighteen did not move, and why they did not is the subject of this ticket.

**What rule 20 cannot reach.** A stack grows whole or not at all, so a page with one row the
mechanism cannot resize does not grow: sectors is a drawn strip over a rank, fuel a rank over a
gauge, speedo a rank over a rev bar, delta a number over a centre-zero bar, pit view a rank over
corner toggles. Growing whole is the right answer for hierarchy — growing the rank alone brought the
lap times under the sectors up to the size of the sectors themselves — and the wrong answer for the
box, which stays two-thirds empty. Rule 20 cannot reach a table at all: the four list pages buy rows
as the box grows and never type. And it cannot reach a drawing, which rule 18 already says is cut
from its box, and which four pages are made of.

A second reason, from the other side: a page whose lead value is already at the top of its ramp has
a growth ceiling of exactly 1 and can never grow whatever its box. That is fuel (`d.hero`), stint
(`d.hero`), speedo (`d.hero`), delta (`d.hero`) and pit view (`d.hero`) — five of the eighteen, and
they are the five whose one big number is the whole point of the page.

**What the catalogue says, which is the other half.** Every drawing on `ZoneCatalogue.dc.html` lays
its ranks out with `justify-content: space-between` over `height: 100%`, and `stack()` centres them
as one tight block. That is most of the unused space the README's 1280 × 480 capture shows in
zone B. The catalogue also promotes hard at `tall` — lap times 46 → 88, delta 64 → 132, speedo
64 → 128, and fuel, sectors, stint and session 34 → 76 — where rule 20's ramp ceiling allows one
step of about 1.35. Nobody has decided whether the ceiling is right or the drawings are.

**So: one ticket per page, and two for the mechanism.** Each asks the same question of one page —
*of what this page carries, what does a driver read first, and is that what the drawing makes
largest?* — and answers it against the boxes the build really produces rather than the four the
catalogue draws. Those boxes are, for zone B: 737 × 270, 437 × 276, 437 × 214, 254 × 292, 229 × 292,
437 × 510, 249 × 158, 580 × 124 and the companion's 802 × 336.

### Done when

Every child is closed, `secondScreens.test.ts` and `textFit.test.ts` still measure every text of
every package against its box, and [zones.md §5](zones.md#5-zones-b-and-c--twenty-one-pages) carries
whatever the passes changed. The canvas is the source and it owes redraws; each child says which.

---

## Mechanism 1 — A table buys rows and never type, so a list page never fills its box

`packages/dash/src/second/table.ts` answers a taller box with `rowCapacity`: more rows, at
`d.rowHeight`, with the cells at the density's sizes. Rule 20 reaches a `StackRow` that carries
fields and a table row is a `blockRow`, so nothing about a list page changes between a 437 × 214 box
and a 437 × 510 one except how many drivers are in it.

That is right for the pit wall, where the question is *who is in the race*. It is wrong for zone B
of a face, where the question is *who is near me and by how much*, the answer is three or four rows,
and the rest of the height goes into whitespace between 26 px rows.

**The specific harm is the driver name.** `table.ts:105` draws it at `d.name`, which is 13 px at
`zone` and **12 px at `compact`** — the floor `density.ts` calls "below that a label stops being
readable at arm's length on a DDU". On the 850 × 480 face the relative therefore draws the one
column that says *who* at the smallest size in the design, under a gap drawn at 24 px. The catalogue
agrees with the build here (13 px codes at every shape), so this is a question for the canvas rather
than a bug: **is a three-letter code at 13 px the right answer on a screen 600 mm from a driver's
eye?**

### What it needs

A row height and a cell ramp that answer the box, the way `fitFields` answers it for a rank: rows
first to the count the page declares, then type, then the rest as spacing. `rowCapacity` already
takes a `rowHeight`; the missing half is the caller deciding one.

### Done when

`relative` and `leaderboard` draw larger rows in a box with height to spare, the row count each page
wants is a declaration rather than a division, and `secondScreens.test.ts` measures the wider cells
against their columns.

---

## Mechanism 2 — A module is centred in its zone where the canvas spreads it

`stack()` in `packages/dash/src/second/layout.ts` centres its rows as one block:
`y = frame.top + (frame.height - total) / 2`. Every drawing on the catalogue artboard instead sets
`display: flex; flex-direction: column; height: 100%; justify-content: space-between` on the ranks
inside the page — lap times, fuel, sectors, car settings, stint, session, speedo, pit view, energy,
damage and track all do — so the first rank sits at the top of the zone and the last at the bottom.

The two disagree on every page with more than one rank. At 1280 × 480 zone B the block is 212 px in
a 276 px body, so the build leaves 32 px above and 32 px below where the canvas would put 64 px
between the two ranks. A driver's eye then has to travel between values that sit closer together
than they need to, inside a zone whose edges say nothing.

`tall narrow` is the exception and the canvas says so outright: `justify-content: center;
align-items: center`, the tokens 0.7.0 changelog line "a zone narrow enough for one column centres
what is in it". So the rule is not "spread always" but "spread unless the zone is one column".

### What it needs

`stack()` gains the choice, and it is the shape that makes it: centred at `narrow`, spread
otherwise. `stackFromTop` already exists for tables and is a third case rather than this one.

### Done when

A multi-rank page at `wide`, `grid` and `tall` puts its first rank on the top edge of its body and
its last on the bottom, a `tall narrow` page stays centred, and the zone captures in `media/readme/`
are retaken.

---

## 1 — Lap times: the catalogue's `tall narrow` drawing is superseded, and `grid` is still greedy

Half done. `tallNarrow` now keeps four fields rather than two and rule 20 draws them one per line at
46 px where the build drew two at 34 px side by side; [zones.md §10](zones.md#10-where-the-canvas-contradicts-itself)
records the disagreement. **The canvas owes the redraw**: `ZoneCatalogue.dc.html` still draws two
34 px times in a 274 × 300 zone with 234 px of it empty.

Two things are left.

**`grid` is drawn and not built.** The catalogue draws last lap alone on a full-width line and then
a two-column grid of session best, your best and delta. The build wraps greedily, which at 437 px
gives `[last · session best]`, `[your best]`, `[delta]` — the same four values with the most
important one sharing its line. A rank that wants a line of its own has no way to ask for one.

**`tall` is 88 px on the canvas and 62 px in the build.** Rule 20's ceiling is the next size up the
ramp and the ramp's own smallest step, which for a page mixing 46 px and 34 px values is ×1.353. The
canvas asks for ×1.91. One of the two is wrong and this page is the clearest place to decide it.

### Done when

The canvas redraws `tall narrow` as four values in a column, the `grid` arrangement is either built
or withdrawn from the drawing, and the ×1.35 ceiling is either defended in `zones.md` or raised.

---

## 2 — Delta: the page is one number and the number never grows

`modules/delta.ts` is the delta at `d.hero`, a centre-zero bar, and a five-mark scale. The bar and
the scale are `blockRow`s, so rule 20 will not grow the stack; and `d.hero` is the top of the ramp,
so the ceiling would be 1 even if it would. The page therefore draws 64 px at 737 × 270 and 64 px at
437 × 510, in boxes that differ by 240 px of height.

The catalogue draws it at **72 px at `tall narrow`, 64 at `wide` and `grid`, and 132 at `tall`** —
the only page it sizes larger in a narrow zone than in a wide one, because a zone with one column
and one number in it should give the number the column.

This is the page a driver glances at mid-corner and the one where size is the whole design. It is
also the page whose lead is a signed two-decimal number that flips colour, so it is read by shape
and colour before it is read as digits, and shape is size.

### What it needs

The bar and the scale cut from the box rather than fixed (rule 18) — `barHeight` is a literal 10 or
14 today — and the number then free to take what is left. A `d.hero` that is a floor rather than a
ceiling for this one page.

`XOR-171` is the other half of this page and stays separate: the catalogue draws three sector deltas
under the scale at all four shapes and the module has never built them.

### Done when

The delta grows with its box up to the catalogue's own sizes, the bar and scale are proportions of
the box rather than literals, and `shape.test.ts` still fits the page at all four archetypes.

---

## 3 — Sectors: a drawn strip stops the page growing, and the times under it are the same size as the sectors

`modules/sectors.ts` is three sector fields, a strip, and a rank of three lap times.
`sectorRowHeight` is computed from the ramp and drawn by `sectorFields` as a `blockRow`, so rule 20
refuses to grow the stack at all — correctly, because growing the lap-times rank alone brought it up
to `d.big` and the page briefly had the last lap and sector 1 at the same size, which
`shape.test.ts` now guards against.

The catalogue draws the sectors at **46 px at `wide` and `grid`, 34 at `tall narrow` and 76 at
`tall`**, with the lap times under them at 34, 24 and 34. So the canvas has the hierarchy the build
has and sizes both halves to the box, which is exactly what the build cannot do.

There is a second question under it. At `tall narrow` the catalogue keeps *your best* and *last*, at
`tall` it keeps *last* and *session best* — a deliberate swap the shedding table already carries.
Nobody has checked it against a real zone since it was transcribed.

### What it needs

`sectorFields` to take a size rather than compute one, so the sector rank joins rule 20's stack as a
field row rather than a block. The strip is 6 px and can stay a literal.

### Done when

Sectors grows with its box, sector 1 stays larger than the last lap at every shape, and the
`tall narrow` / `tall` swap is either confirmed on the VM or corrected in the table.

---

## 4 — Speedo: speed is at the top of its ramp, and zone A is already drawing it

Two problems, and the second is the interesting one.

`modules/speedo.ts` draws speed at `d.hero` with RPM and the redline beside it and a rev bar under.
`d.hero` is the ramp's top so the growth ceiling is 1, and the rev bar is a `blockRow`, so the page
draws 64 px in every zone it is ever given. The catalogue draws **46 at `tall narrow`, 64 at `wide`
and `grid`, and 128 at `tall`** — a three-to-one range the build has none of.

**And zone A already draws the speed under the gear** on every face with a body — it is in the
README's own captures, `142 KMH` under the 3. A driver who puts speedo in zone B is therefore
reading the same number twice, 300 px apart, in two different sizes. Either the page is for the
driver who turned zone A's speed off, in which case it should say so and be laid out for a zone that
is the only place speed appears; or it is the RPM page with speed on it, in which case RPM should
lead. It is currently neither.

### Done when

The page has an argued lead value, grows with its box, and `zones.md` §5 says what it is for on a
face whose zone A already draws speed.

---

## 5 — Fuel: the one page where the number has to be right, and it never changes size

`modules/fuel.ts` draws the level at `d.hero` with a level gauge under it, so — the same as delta,
speedo and stint — the ceiling is 1 and the gauge is a block: 64 px at every zone size from
249 × 158 to 737 × 270. The catalogue draws 34 px at three shapes and **76 at `tall`**.

The ranks also disagree with the drawing in a way worth settling. The catalogue's first rank is
*fuel · fuel time · est. laps* and its second is *refuel · per lap · avg 5*; the build's first rank
is *level · time · toAdd* and `lapsLeft` is in the second, behind three consumptions. Estimated laps
is the number a driver uses to decide whether to stop, and it is the one the build puts last.

At `grid` and `tall narrow` the catalogue keeps four values in two ranks of two, and the shedding
table keeps `level · time · toAdd · average` — so the build keeps the average and drops est. laps
where the drawing keeps refuel and per lap. Three of the four disagree.

### Done when

The rank order matches an argued reading of what a driver needs before a stop, the page grows with
its box, and the shedding table's `grid` and `tall narrow` rows are reconciled with the drawings.

---

## 6 — Pit view: the catalogue sizes this page continuously and the build sizes it from the ramp

The catalogue draws refuel at **58, 61, 64 and 96 px** across the four shapes and pit time at **40,
42, 44 and 67** — sizes that are on no ramp, in a fixed 1.45 : 1 ratio, cut from the height of the
box. That is rule 18 applied to a number, and this page is the only one on the artboard that does
it.

The build draws `d.hero` and `d.mid`: 64 and 34 at `zone`, 46 and 24 at `compact`, a ratio of 1.88
and no response to the box at all. The corner toggles under them already wrap themselves to the
frame, so the page is half cut-from-box already.

This is a page a driver reads stationary in a pit box with a crew shouting, so it is the page where
a large number costs least and buys most.

### Done when

Refuel and pit time are cut from what the toggles leave, at the catalogue's ratio, and
`textFit.test.ts` measures them at every zone the build produces.

---

## 7 — Car settings: seven cells wrapped greedily where the catalogue draws a grid

The catalogue draws the car on its own line and then **a three-column grid at `wide` and `grid` and
a two-column grid at `tall narrow` and `tall`**, always aligned, always the same cell width. The
build wraps greedily to whatever fits, so the same seven cells make three columns at 737 px, two at
437 px and a ragged last line at both. Rule 20 grew them (24 → 32 px at `zone`), which made the
ragged line more visible rather than less.

Two more things the drawing says and the build does not.

**The vocabulary differs.** The catalogue names ten cells — TC slip, TC cut, TC, Diff, BB, Migr.,
Map, ABS, KERS — and the module has seven with different ids (`tc`, `abs`, `bb`, `mix`, `arbFront`,
`arbRear`). Some of the difference is real (iRacing does not publish all ten) and some is
transcription. Nobody has reconciled them.

**`tall narrow` keeps four cells and drops the rest,** which the shedding table has; the catalogue
keeps six there at 16 px. 16 px is below the 12 px label floor argument's cousin and probably wrong,
but four cells in a column at 24 px is a different page from six at 16, and the choice has not been
made.

**The wide car-telemetry page pins its cells at 16 px, which is an exception to rule 20 rather than
an application of it.** That page draws the same four readings in the 380 px column the sheet gives
it, beside the pedal traces, and the room the column has left over belongs to the traces rather than
to the grid: letting the cells fill it drew the values at 29 px and the car number at 41, which is
the size of a subject on a page whose subject is the trace. The cells are therefore built at `d.tiny`
and placed by `drawFieldBlock`, which does not fill, instead of by a stack that does. Car settings
itself is not affected, since it still fills the zone it is given at every one of its own shapes, and
the exception is written down here because pinning a value against rule 20 is a change of principle
rather than a tweak.

### Done when

The cells are a grid of equal columns whose count comes from `columnsAt`, the cell vocabulary is
reconciled against what SimHub actually publishes, and the `tall narrow` count is decided.

---

## 8 — Inputs: the three numbers are the smallest thing on the page and the trace is the reading

`modules/inputs.ts` draws a three-series trace, three bar gauges and three percentages at `d.tiny` —
16 px at `zone` and **14 px at `compact`**, which is below the label floor `density.ts` defends for
labels. The page is entirely blocks, so rule 20 never touches it and the numbers are 14 px in a
249 × 158 zone and 14 px in a 254 × 292 one.

The catalogue draws them at 24 px at three shapes and 16 at `tall narrow`, with the trace taking the
rest — so the canvas has the same instinct and one size more generous.

The real question is whether the numbers belong at all. A driver reads pedal application as a shape,
which is what the trace and the bars are for; a percentage to the unit is a thing to read back
afterwards, which is the companion's job rather than a zone's. Either they are worth 24 px or they
are worth nothing.

### Done when

The numbers are either dropped from the zone forms or drawn at a size that can be read, the trace is
cut from what is left, and `tracePoints` is checked against the width it actually gets.

---

## 9 — Session: it grows now, and the denominators do not

Rule 20 moved this one: position and class go from 46 px to 62 at `zone` and from 34 to 46 at
`compact`. What did not move is the `/ 24` after the position and the `/ 30` after the lap, which
are drawn by `unit()` at `d.labelSm` — 13 px at `zone` and **12 px at `compact`** — beside a value
that is now five times their size.

The catalogue draws the denominator at 23 px beside a 34 px value and at 53 px beside a 76 px one,
so it scales the follower with the value at roughly 0.7. `field()` in `second/field.ts` uses
`d.labelSm` flat. That is a one-line change with a consequence on every page that has a follower:
the lap counter, the fuel unit, the speed unit, the position.

Also: the catalogue's `tall` form promotes the whole first rank to 76 px and draws the session type
as a value, which the build has at `d.big` and sheds at `grid`.

### Done when

A follower is a proportion of the value it follows rather than a density constant, every page
carrying one is re-measured by `textFit.test.ts`, and the `tall` promotion is decided with lap
times' (they are the same question).

---

## 10 — Relative: the page a driver reads most, drawn at the smallest size in the design

See *Mechanism 1* for the table half. What is specific to this page:

The relative is the one page that answers *is the car behind me going to be there at the next
corner*, and it answers it with three columns at `tall narrow`: position, code, gap. The code is
drawn at `d.name`, **12 px at `compact`**, which is the 850 × 480 face — smaller than the position
beside it at 24 px and smaller than any label on the screen.

The build already lists thirteen cars at 850 × 480 and nine at 1280 × 480 (the README says so). It
is worth asking whether thirteen is a number anybody reads. A driver reads the two rows above their
own and the two below; the rest is a wall. Fewer rows at a larger size is a design decision nobody
has taken because `rowCapacity` takes it arithmetically.

### Done when

The row count each face gives the relative is a declaration with an argument behind it, the driver
code is drawn at a size that can be read at arm's length, and the class chip and car number are
checked against the room that leaves.

---

## 11 — Leaderboard: the same table, and a column that vanishes when a name is long

See *Mechanism 1*. Specific to this page: `fittingColumns` drops a whole column when the columns do
not fit, and `XOR-121` records that a driver name longer than its column has nowhere to go. Those
two interact badly — a long name pushes the total over and the *gap* column is what pays, which is
the column the page exists for.

The catalogue keeps `pos · num · name · class · gap` at `grid` and `tall`, seven columns at `wide`,
and three at `tall narrow`. The order in `LEADERBOARD_COLUMNS` puts `best` and `last` after `gap`,
so the tail drops first, which is right. What is not decided is what happens when the *kept* set
still does not fit.

### Done when

A long name cannot cost the page its gap column, the row height answers the box (Mechanism 1), and
`secondScreens.test.ts` measures the columns at every zone the build produces.

---

## 12 — Opponents: the gap is large and the name beside it is 13 px

**Settled.** `modules/opponents.ts` used to draw two blocks, ahead and behind, each of them a gap at
`d.big` with a name and a car number at `d.small` and a line of detail under them, and both of them
were `blockRow`s with a rule between.

The name is `d.name` now, which is 15 on the companion and 13 in a zone, and is therefore the size
the catalogue draws it at on both ramps. That is the same token the list pages set a driver code in,
so the question is answered once for the two of them; moreover the name is a `label()` in Barlow
Medium inside the 64 px box the canvas fixes, rather than the monospaced numeral it was.

Rule 20 was declined here rather than wired in, which is the one part of this ticket that did not
end where it began. One could think that a page with slack over it should spend it, since that is
what the rule is for. In reality the catalogue names this page's gap at every shape it draws, 64 on
the companion, 46 in a zone and 34 on the compact faces, and those three are exactly the three
densities' `big`: a stack that spent its slack on the next rung up would draw 64 where the sheet
writes 46 at every one of them. Thus the room a tall zone leaves over two blocks stays slack. The
growth chips on the face sheets measure the face against the catalogue and not the drawing inside
it, which `FaceVariants1280x480` records in its own note.

What a short box does instead is shed, and it sheds the two cars' lines in pairs, the table naming
`ahead.gap` beside `behind.gap` and so on down the row, so that a box with no room for an identity
row loses both identity rows rather than losing the car behind entirely. Only a box too short for
two headings and two gaps moves the size at all, and then it moves it for both cars at once.
Besides, a wide box too short to stack in, zone C of the 600 by 686 face being 576 by 112, takes the
side-by-side arrangement the canvas gives the wide zone and draws both cars whole.

### Still open

The licence badge and its safety rating, which the catalogue draws on the identity row and at
`tall` in place of a class chip. No reader for the iRacing licence has been verified, so the
shedding table keeps the last lap in the badge's place and `docs/second-screens.md` carries the
reason; the direction triangle and the nationality flag are open for the same kind of reason, being
a shape SimHub does not draw and an image asset respectively.

---

## 13 — Stint: the lead is at the top of its ramp, so the page never grows

`modules/stint.ts` draws stint laps at `d.hero` with stint time and laps completed at `d.big` and
three more at `d.mid`. `d.hero` caps the growth ceiling at 1, so the page draws 64 px in a 437 × 510
zone and 64 px in a 249 × 158 one. The catalogue draws 34 px at three shapes and **76 at `tall`**.

The shedding order is also worth a second look. At `grid` and `tall narrow` the table keeps
`stintLaps · stops · lastStop`, and the catalogue's drawing keeps the same three. But a stint page
in a zone is read when a driver is deciding whether to pit now or next lap, and the number that
answers that is the stint time against the stint window — which the catalogue draws at `wide` and
`tall` as *Window 14–18* and the module has not built.

### Done when

The page grows with its box, and the stint window is either built or recorded as something SimHub
does not publish.

---

## 14 — Lap history: a table with a header the build has not got

See *Mechanism 1* for rows and type. Specific to this page: the catalogue draws a **fuel target** in
the header — `Fuel · target 2.85` at `wide`, `Fuel` at `grid` — and the module draws three columns
with no header value. `XOR-275` already records the missing target; this ticket is the layout half.

Three columns of lap, time and fuel at 24–34 px, as many rows as fit, is the whole page. The
readability question is which of the three a driver's eye lands on: the build gives all three the
same size, so the page reads as a block of digits. The lap time is what a driver is comparing and
the fuel is what they are checking; the lap number is an index.

### Done when

The lap number is drawn as an index rather than a peer of the two values, the row height answers the
box, and the fuel target header is built or refused.

---

## 15 — Tyres: four corners, three quantities each, and no hierarchy between them

`modules/tyres.ts` cuts a two-by-two grid from the frame and hands each cell to `wheel()`. That is
rule 18 done properly and the page needs nothing from rule 20.

What it does need is a decision about the three quantities. The catalogue draws temperature at
34 px and pressure and wear at 24 (16 at `tall narrow`), so temperature leads. Whether that is right
depends on the driver: temperature is what a driver acts on mid-stint, pressure is what they act on
in the pits, and wear is what they act on over a run. `XOR-274` is the user-choice half of this —
*the tyre widget draws three quantities and offers no choice among them*.

The layout half is this: at `tall narrow` the catalogue shrinks all three to 16 px rather than
dropping two of them, which is exactly what rule 17 says not to do. A 254 × 292 zone should show one
quantity per corner at a size a driver can read, not three at a size nobody can.

### Done when

A narrow zone drops quantities rather than shrinking them, the lead quantity is argued rather than
assumed, and the page is checked on the VM at 850 × 480.

---

## 16 — Radar: a fixed scale where everything else is cut from its box

`modules/radar.ts` sets `scale` to a literal 0.8 at `zone` and 1.25 at `companion`, and the two
spotter bands to a literal 18 px wide. So the radar in a 249 × 158 nano zone is drawn at the same
scale as the radar in a 437 × 510 one — the box changes and what it shows does not.

Rule 18 says a drawing is cut from its box. A radar's scale is how much track it covers, which is
the drawing's equivalent of a font size, and it should come from the box the way the gear's cell
size does in `gearSizeIn`.

The readability question under it: the radar exists to answer *is there a car beside me*, and the
two spotter bands answer that better than the plot does. On a narrow zone the plot may be worth
dropping entirely in favour of two wide bands.

### Done when

The scale is a function of the box, the spotter bands are a proportion of the width, and the narrow
form is decided.

---

## 17 — Track: a map at a fixed line width, and a title above it

`modules/track.ts` draws a `staticMap` with `trackWidth` at a literal 6 or 10 and a title line above
it. The map fills whatever is left, so the page half-obeys rule 18 already: the box decides the map
and a literal decides the line.

At 249 × 158 a 6 px line on a track drawn into 249 px is a fat worm; at 437 × 510 it is a hairline.
Both are the same number.

The readability question is what the page is for. A map in a zone answers *where am I on the lap*,
which the driver already knows, and *where is everybody else*, which the relative answers better. It
may be a companion page rather than a zone page, and saying so is cheaper than making it work in a
249 px box.

### Done when

`trackWidth` is a proportion of the map's smaller dimension, and the page's place in the zone
catalogue is confirmed or the page is made companion-only.

---

## 18 — Gear: zone A is the one page with no header and no way to say what it is showing

Zone A draws the gear, and `gearSizeIn` in `packages/dash/src/zones/zoneAPages.ts` already cuts it
from its box — the one place in the repo that got rule 18 right first time. Nothing here needs rule
20.

Two readability questions remain and both are already tickets: `XOR-103` (zone A has no header, so
nothing says which of its four pages is showing) and `XOR-89` (zone A's four pages). What this
ticket adds is the measurement: at 850 × 480 zone A is 300 × 328 and the gear is drawn at 260 px
with the speed under it at `d.hero`, and the README's capture shows the two neighbouring gears drawn
at a third of the size on either side. Whether those neighbours earn their space, or whether the
gear should take the column, is a question nobody has asked of the rendered face.

### Done when

The neighbour gears are confirmed against the capture or dropped, and the speed under the gear is
reconciled with the speedo page (ticket 4).

---

## 19 — The three prose pages: energy, damage and track rivals

One ticket for three, because they have the same shape and the same answer.

`shedding.ts` records them as `nothing`, with reasons: *one line of prose: iRacing publishes no
virtual energy*, *one line of prose: iRacing publishes no damage*, *one line of prose: SimHub times
sectors, not segments*. The catalogue draws all three as full pages — energy with four values and a
`tall` form at 76 px, damage with four corner states, track rivals with a segment, a rank and two
times.

So there is nothing to reorganise and nothing to make readable: a page that says *iRacing publishes
no virtual energy* is one line of prose and should stay one. What this ticket is for is deciding
whether they should be **in the catalogue at all** while they say that. A driver cycling zone B
through twenty-one pages hits three that are apologies. They are off by default
([second-screens.md](../second-screens.md) says which), which is most of the answer, but "off by
default" and "in the cycle" are different things.

### Done when

The three are either removed from the zone catalogue until their data exists, or the prose form is
designed rather than defaulted, and `contract.ts`'s page count follows either way.

---

## 20 — Bar and band D: the two rows that are not pages, and never asked the question

Not modules, so they have no row in the shedding table and no ticket above. They are on the face all
the time, which makes them the two rows a driver's eye passes most.

**The bar** (`zones/bar.ts`) is two fields at each end and a settings strip between, at 50–54 px
tall. The README's capture shows `RACE 0:21:08`, `LAP 15 / 30`, then TC, BIAS, ABS at label size,
then `POSITION 3 / 24` and `CLASS GT3 P12`. The settings strip is the smallest type on the face and
it is the part that changes least.

**Band D** (`zones/bandPages.ts`) is one rank across the full width with a corner block at each end,
60 px tall, and a flag takes it over. Eight pages. At 1280 px wide and 60 px tall it is the shape
rule 20 helps least (a `short` box) and the shape a spread layout helps most.

### Done when

Both have been looked at on a rendered face with the same question the pages got, and `zones.md` §4
and §6 carry the answer.
