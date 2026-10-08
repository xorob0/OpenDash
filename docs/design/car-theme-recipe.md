# How a car theme is made

What the Porsche theme taught, written on 2026-10-08 so that the next theme takes one pass where the
first took seven. A theme is a build-time variant of the face that looks like the car's own display:
same catalogue, same three zones and band, same shedding, drawn in the car's register. The records
are [ADR 0015](../decisions/0015-car-themes.md) and
[ADR 0016](../decisions/0016-themed-package-distribution.md); this page is the working method.

The method has two halves, and the order matters: the design is iterated on a canvas and in the
ticket until it is settled, and only then is it integrated. Every round that the Porsche needed
after the code existed came from a question that the design could have answered first.

## 1. Research the car before drawing anything

**One theme per display family, not per car.** Compare the manual renders of every car of the marque
first. Cars that share a display unit, as the 992 GT3 R, both Cups and the legacy GT3 R do, are one
theme with a per-car table of differences: which setting boxes exist, lock-up colours, gear colour,
what the tyre box holds. Propose closing the sibling tickets and wait for a yes.

**Collect the references on the canvas, never in the repository.** The in-sim render of each page
from the iRacing manual, the pit limiter and the alarms, the value change, and a product photograph
of the licensed replica unit with its size and resolution. These are the car maker's and iRacing's
copyright: they live on the private canvas and in a scratch folder, and no pixel of them is ever
committed, not even as a cropped icon. Give the agent that integrates the theme the paths to those
files from the start, since it cannot open the canvas; the Porsche's agent drew the first five
rounds from the ticket's text alone and measured the photograph only in the sixth.

**Measure, do not eyeball.** Render the manual pages at 150 dpi, crop the screen, enlarge twice and
read off the border width, the radii, the container pattern, the alignment, the weights, and the
size of labels and values. The scale factor is the width of the screen inside the picture, not the
width of the picture: the Porsche render is 1124 px wide but the screen in it is about 675 px, so a
label of 12 px in the picture is 23 px on the 1280 face. Getting this wrong cost one round.

**Reference size = the face whose aspect matches the real display.** The 992 unit is 8:3, so the
reference is 1280 x 480 and every other size is derived from it by rules, never redrawn freehand.

## 2. Settle the register on the canvas

The canvas carries a reference row of the photographs, the real capture of the house face, the
theme at the reference size as one component with its states (pages, limiter under and over, value
change, low fuel, flags), and one board per supported size. Tim judges the canvas before any code is
written, and the questions below are answered there, because each one that was left open became a
round of corrections during integration.

**The container pattern.** The Porsche's boxes are a zone container with a border only and the
black ground inside; a title cell flush against the top and sides of that border, grey, holding the
page name; label cells flush against the left border and against each other with no gap, so the
stacked cells read as one grey column; the value beside its label on black, right-aligned, with no
cell of its own; a lone value bare on black under a title cell; a pair as two half-width title cells
over two bare values. Write the pattern down as these sentences and as numbers (border 3 px, outer
radius 7, inner radius 5, text padding 8 px in a cell, gaps 0) before a line of code. "Container with
inset" was the first reading of the same photograph and it was wrong in three ways.

**One label size and one value size per zone.** The car does not shrink a label to make it fit; it
abbreviates. So the register fixes one pair of sizes (23 px labels, 32 px values on the Porsche at
1280) and a table of short forms for the labels that do not fit ("Sess best", "Delta best"). The
short forms are register data, declared so that the fit tests measure the drawn string.

**The typeface and its weights.** The car's display is set in a plain humanist sans at normal width,
so the whole theme is Barlow at normal width, labels and values alike, one regular weight with bold
on the box names only; the house's condensed display face is used nowhere in it. Any face a package
draws must be shipped and measured into the advances first; this is the one type cost of a theme.

**Which pages keep the house layout.** The label-and-value pattern fits the pages that are lists of
readings (lap times, fuel, car settings, speedo, stint). It does not fit the pages that are rows of
cars (opponents, relative, leaderboard, lap history, track rivals) nor the pages that are one drawing
(inputs, radar, track, tyres, damage): those keep the house's own rows and drawings, restyled in the
theme's type on the theme's ground under the theme's title cell. Decide the split on the canvas.

**Icons are real pictograms.** The headlight is the ISO 2575 low-beam symbol, the warnings are the
car's own. A shape made of rectangles and ellipses is not an icon. The SimHub format has no vector
item, so pictograms ship as small PNGs rendered from SVG sources kept in the dash package, never from
a manual.

**What is not drawn.** No zone letters (removed from every theme in #708). No ghost of the next and
previous gear. No unit beside the strip's speed. The badge is a placeholder shape in the register's
grey; the marque's crest is a trademark and never ships. The wordmark on the bezel is not drawn.

**Settings and lamps.** The car's setting boxes (MAP, THR, TC, ABS, BIAS) in their own colours, in
a column that stacks from the top and shows what the car publishes, hiding the rest and moving the
boxes up. Their colours are user-customisable from the panel later; the register gives the defaults.

**Every size gets a board, by rules.** Taller keeps strip and foot and deepens the modules; wider
widens B and C into two columns; narrower drops the settings column, modelled on the marque's squarer
screens; the short face sheds; portrait stacks. Fonts follow their own box, never a global scale;
gaps are fixed; a box keeps its reference width. One form of each element at every size.

**The ticket is the specification.** It carries the per-car table, the register table, the geometry
tables at the reference size, the size rules, the decisions with their dates, the component source
in a collapsed block and the size generator in a comment, with the canvas linked, so that another
instance can reproduce the result without this conversation.

## 3. Integrate, in this order

The machinery exists now; a new theme is a folder. These are the steps and the gates.

1. **Catalogue row** in `packages/dash/src/contract.ts` (`THEME_CATALOGUE`) and its mirror in
   `plugin/OpenDash/Contract.cs`: id, name, cars, iRacing car paths, sizes. Cross-reading tests on
   both sides hold them equal. A catalogued theme without code is skipped by `--all-themes` and
   refused by `--theme`.
2. **The folder** `packages/dash/src/themes/<id>/`, registered in `THEMES` in `themes/index.ts`
   with type-only imports: `overlay.json` (the car's colours under `palette.<id>.*`, overrides of
   `color` and `purpose`, the data face and its digit cells), the anatomy (a function from the
   house face of a size to the closed roles of `themes/anatomy.ts`), the drawing (the hooks of
   `themes/drawing.ts`: shift lights, bar, chrome, takeovers, change notifications, zone ground,
   gear ghosts, band corners and pages, module frame and header size) and the module register
   (field rows, stack, which pages keep the house layout). Read the Porsche's files first; a second
   theme should be able to copy its structure and change numbers.
3. **The default build stays byte-identical to `main`.** Build both into two folders and
   `diff -r`; this is the review of every change to the machinery and it is what lets the house
   faces be left alone while a theme is drawn.
4. **The conformance harness** (`bun test packages/dash/test/conformance.test.ts`) picks the theme
   up from the registry and holds every claimed size to three properties: nothing clips, nothing
   escapes its frame, nothing disappears, the last one including a page that draws nothing where the
   house layout draws something. No pin for a real theme. Shedding goes through `fitFields` and the
   module's order, never by leaving a value out.
5. **Compare with the photograph by numbers before the VM:** border, radii, cell flushness, the
   label and value sizes, right alignment, row height. A render that was never held against the
   reference went to the VM three times for nothing.
6. **Capture on the VM**, through the claim lock: the main pages on `race`, `pit`, `notc` and
   `yellow`, the value change and the limiter on their scenarios, and then every page of every zone
   as contact sheets, made from capture-only copies of the package with the zone widgets bound to
   fixed page indices (one copy per index, deleted afterwards, never committed). The contact sheets
   are what the review is done on; a theme is not done until each page has been looked at.
7. **Package and plugin:** `bun run package` embeds the theme; the plugin's package list test gets
   the theme's row; the Add sheet offers the theme at the sizes it claims and binds the car's
   playlist entry on install. The site leaves themed packages out of its lists.
8. **The pull request** says what the ticket asks that is not drawn and why, every machinery
   addition, which rows shed differently from the house at each size, and carries the captures.

## 4. What the review loop looks like

Tim reviews captures, not code, and compares them with the car's page side by side. Send the
captures after every round, as files, and send the real thing with them: for each state captured,
one composite image with the car's own page above and the render below at the same width, made
from the reference photographs in the scratch folder, so that the comparison needs no second window.
The composites are review material, kept with the captures in `build/` and never committed, since
the photographs are not ours. Expect the remarks to come in the car's words ("the title container
should have the same height and no padding"). Read each remark against the photograph,
restate it as numbers and as the container sentences above, and only then change the code. The
rounds the Porsche needed, in order, were: the typeface and the ghosts; the module backgrounds and
the icons; the module register at all; the panel literal to the canvas; bordered cells and one size;
sizing from the photograph and the band cells turned the right way; flush blocks and the list
pages. Sections 1 and 2 are the answers to those rounds, asked before the code.

## 5. Open before the next theme

- The foot page of band D (the car's row of boxes, tyres and bias) cannot be reached on a rig: the
  plugin cycles band D through a fixed page count and the zones open on the house defaults rather
  than the car's. Plugin work, one ticket.
- The dim colours for an unavailable reading (label and value both dimmed, as the car does) are not
  drawn, because a module writes its own dash and the layout cannot tell which label it belongs to.
- Band D's Stint cells are blank in every theme: the bindings turn a timespan defaulted to zero into
  seconds, which SimHub does not evaluate before the first stop. Default-build change, one ticket.
- Band D keeps the empty room where its letter stood; using it moves the band's row and changes what
  fits on the 850 and 600 x 686 faces.
- The Porsche itself still owes its other sizes, the per-car table, the lock-up colours and the
  panel-chosen setting boxes; the first slice was 1280 x 480 only.
