# The design audit: every statement the canvas makes, against the code

**What this is.** The design canvas is the design of record. This document is the result of reading
all thirty-six of its artboards, turning every concrete statement they make into a checkable
requirement, and auditing the code against each one. It was produced on 16 September 2026 from canvas
version `1789504323-ed9f`; a requirement id names the artboard it came from, so `ledvariants-42` is the
forty-second requirement of the LEDs sheet.

It is a survey rather than a plan of what will be built. Section 4 groups the gaps into work packages,
and those are the unit of work; section 3 lists what the author has to decide before the rest can be.

## 1. What the numbers say

Of **1938 requirements**, the code implements 357 exactly.

| verdict | count | what it means |
|---|---|---|
| implemented | 357 | the code does exactly this, at every size the statement covers |
| partial | 1180 | some of it is there, with a named difference in a value, a name, an order or a size |
| missing | 208 | nothing of it is there |
| contradicts | 113 | the code deliberately does something else, usually with an ADR or a comment saying why |
| blocked | 80 | SimHub's format cannot express it, or the author has to decide |

Every verdict of *implemented* was then attacked by a second reader whose brief was to refute it; 112
claims did not survive that pass and were downgraded, which is why the figure above is lower than the
auditors first reported.

### By area

| area | requirements | implemented | partial | missing | contradicts | blocked |
|---|---|---|---|---|---|---|
| tokens | 97 | 33 | 61 | 0 | 0 | 3 |
| elements-components | 224 | 31 | 111 | 39 | 11 | 32 |
| zones-faces | 281 | 65 | 194 | 4 | 15 | 3 |
| modules-pages | 583 | 39 | 434 | 32 | 58 | 20 |
| alerts-notifications | 142 | 51 | 44 | 37 | 1 | 9 |
| second-screens | 33 | 3 | 27 | 0 | 1 | 2 |
| companion | 26 | 12 | 14 | 0 | 0 | 0 |
| pitwall | 130 | 34 | 79 | 7 | 5 | 5 |
| round | 45 | 27 | 15 | 1 | 2 | 0 |
| lights-strip | 101 | 17 | 50 | 23 | 10 | 1 |
| flag-box | 55 | 16 | 23 | 13 | 2 | 1 |
| plugin-panel | 175 | 25 | 96 | 44 | 7 | 3 |
| plugin-install | 42 | 4 | 30 | 7 | 1 | 0 |
| docs | 4 | 0 | 2 | 1 | 0 | 1 |

The shape of the table is the finding. The face's geometry and the settings contract are largely
built; what the pages *draw inside* their rectangles is where the design and the code have diverged,
and the lights, the flag box and the settings panel carry the design's newest decisions, most of which
postdate the code.

## 2. The seven differences that account for most of the count

A great many of the partial verdicts are instances of a handful of systematic differences. Fixing each
of these once closes dozens of requirements.

1. **The zone frame.** The canvas draws a 22 px header inside 6 by 12 px padding, with the letter and
   the page name at 15 px in `text.label`; the code draws a 28 px header inside 16 px padding at 13 px
   in `text.secondary`, and adds a page counter no artboard has. Every module therefore receives a body
   eight pixels narrower and six shorter than the design gives it.
2. **The horizontal hairlines.** Every face artboard separates the bar from the body and the body from
   band D with a one-pixel rule in `surface.raised`. The code draws the two vertical rules and neither
   horizontal one.
3. **Band D.** The canvas's catalogue draws each of the eight band pages as a rank of label-over-value
   fields with corner blocks; the per-size sheets used to describe them as a label, a lead and a muted
   secondary. The catalogue is the older and more specific drawing and it is what the code should
   follow; the per-size sheets have been corrected to embed the catalogue's own drawings.
4. **Zone A's gear.** Rule 18 says a drawing is cut from its box. The gear is capped at the 260 px
   token on every face, so on the taller boxes it is drawn at about half the height the rule allows and
   the page keeps the empty height instead of removing it.
5. **Rule 20 and the catalogue's own drawings.** The catalogue lays every rank out with
   `space-between` over the full height and promotes its type hard at the `tall` shape; the code centres
   a tight block and grows it by a ramp with a much lower ceiling. Most of the modules' differences are
   this one difference, page by page.
6. **The lights.** The strip's blinks alternate a colour with itself, so they have never blinked; the
   Fanatec 3/9/3 is wired in an order the profile does not remap; the flag rates follow the 2019 edition
   of FIA 3504 where the canvas follows the 2020 one. These are in
   [lights-review.md](lights-review.md) section 6 with their fixes.
7. **The settings panel.** The canvas draws four tabs, a rig of screen cards, a face picker and a
   Lights tab with one pane per light; the plugin draws an earlier three-tab arrangement.

## 3. What the author has to decide

249 requirements cannot be implemented as they stand. They fall into two kinds: what SimHub's
format will not express, and what the canvas has not yet settled. The full list is in the packages
below; these are the ones that block other work.

- **companion850x480-05** — Author decision, and the same decision as -06, companion480x850-06 and companionmodules-03. The header pair reserves its box from the widest binding: packages/dash/src/second/header.ts:109 measures three monospace cells from the sample 'P24' and :50 measures …
- **companion850x480-06** — Same decision as companion850x480-05, in its second form. COMPANION_HEADER.groupGap = 20 is applied at packages/dash/src/second/header.ts:112 as positionX = lapX - groupGap - position.width, where position.width includes the denominator measured from '/ …
- **companion480x850-06** — Same decision again, inherited: the portrait header is the same code path, so its rendered pair gap is 20 px against the stated 8 px and its group separation about 26 px against the stated 20 px. It carries no separate work beyond a regression assertion on …
- **companionmodules-03** — Same decision, stated over all twenty-one screens. The typography is already exact, namely Barlow Condensed SemiBold 24 px in #F5F7FA with uppercase Barlow Medium 13 px denominators in #8A9099 on one baseline, and the header is genuinely identical on every …
- **companionmodules-04** — The 794 px gauge width only; the body-height half is delivered by the flag band and body geometry package. Every full-width row is drawn at ctx.frame.width, that is to say 802 px, which is 850 less twice the 24 px padding, whereas the canvas draws 794 for …
- **companionmodules-08** — SimHub cannot, and the author must decide what to give up. docs/research/simhub-dash-format.md:334 establishes that only a WidgetItem's InitialScreenIndex is bindable and that a top-level dashboard's selected screen is not, so the plugin cannot page the …
- **dashround480-08** — The arc span is a decision the author owes, because the canvas contradicts itself. A 12 degree step over 168 degrees puts the end segments inside the slots the same artboard draws: at 84 degrees a segment centre sits only 0.1 r above the horizontal diameter …
- **dashround800-06** — The same decision as dashround480-08 and it must be taken for both faces at once, since REV_ARC_STEP is shared by the two round layouts. Taking the canvas literally means moving the six slots of packages/dash/src/layouts/800round.ts:26-33 inward and …
- **dashround480-11** — The canvas's 480 cluster does not fit the face the canvas itself draws. In the code's cell model the row measures 140 + 14 + 59 + 14 + 59 = 286 px (gear cell ceil(0.52 * 260) = 136 plus 4 px of slack, neighbours ceil(0.52 * 104) = 55 plus 4), whereas the two …
- **dashround480-12** — The neighbour typography, Barlow Condensed SemiBold 600 at 104 px in #33383F, has nowhere to be drawn until the 480 cluster of dashround480-11 is settled. The current gear already matches the row: 260 px, Bold, #F5F7FA, tabular cells …
- **dashround480-02** — Three of the five layers already match: the 12 px flag ring, the two rung-S slots and the pit limiter band between the arc and the gear. The two that do not are the gear cluster, blocked as dashround480-11, and the arc span, blocked as dashround480-08. The …
- **dashround480-09** — Two author decisions rather than a patch. First, the arc geometry the row states is the 12 degree step of dashround480-08. Second, the row's 'when lit' does not hold on every instance of the face: packages/dash/src/shift.ts:93-94 and :129-130 light segments …
- **dashround800-07** — The same decision as dashround480-09, stated from the 800 artboard. The canvas lights segment i whenever i is below the lit count, so every count from 0 to 15 is reachable, whereas the code lights the top third as a block. Moreover the row's sample of nine …
- **dashround480-10** — No code change is recommended and the correction belongs to the canvas, which design/ rules keep out of code. The three properties the row names (RPMShiftLight1, RPMShiftLight2, redline) are the fallback ladder only: packages/dash/src/shift.ts:70-76 prefers …
- **dashround800-08** — The same canvas note as dashround480-10, on the 800 artboard, with the same reason: the row describes SimHub's bands as the source whereas ADR 0014 made them the fallback. Both artboards should be corrected together by the author.
- **dashround800-24** — A canvas and token question that code must not settle. The numerals are drawn in ds.font.data, which resolves to 'openDash Display' because packages/dash/src/design/fontFiles.ts:1-40 renames the bundled Barlow Condensed files on the way into a package: WPF …
- **dashround480-17 (default card half)** — The minus sign half is in the delta package; the default card half is not expressible under the present contract. The twelve Slot properties are shared by every card face, as ScreenInstance.cs:82 states and as OpenDashSettings.cs:97 stores them, one global …
- **band page anatomy: lead-and-secondary row against the label-over-value rank (facevariants1280x480-41..-49, facevariants800x480-39..-47, facevariants1280x720-41..-48, facevariants800x286-42..-49, facevariants600x686-66..-70, facevariants1920x480-43..-46, facevariants850x480-46..-53, dash1280x400-31 field set, dash1280x720-25 field set)** — The canvas contradicts itself. The FaceVariants sheets draw every band page as one left-aligned row of a label, a lead value and a secondary value in #8A9099, at 330x28 and declared not to scale, whereas ZoneCatalogue.dc.html and every per-size Dash artboard …

### What the merge left for you

Two disagreements surfaced while the packages were being integrated, and both are the author's to
settle rather than an implementer's.

- **The section's padding.** The Foundations sheet says `space.6 · 32 · between plugin sections`,
  while the Plugin artboard's own `.sec` says `padding-top: 28px; gap: 20px`. The code takes the
  Plugin artboard, which is what `PanelMetricsTests` holds, and applies the padding above and below
  the rule, so two sections sit 56 apart rather than 32. Either the artboard's 28 is the top padding
  alone, or the Foundations figure is the one to draw.
- **The bar's value overruns its bar.** A 34 px value over a 13 px label 5 px apart needs 60.6 px of
  WPF line box, and the band is 60. Band D shrinks its value to 32 rather than clip; the settings bar
  keeps the overrun it has always had, because the box is transparent and digits have no descenders.
  The two should agree.
- **The companion's flag band costs it a rank.** `ds.indicator.flagBand.heightSm` is 32 and the
  Companion 850 by 480 artboard draws that band 12 px tall. The twenty pixels come out of the module
  above it, which leaves the page 336 px of content where its fourth rank, the three sectors of the
  last lap, needs 348. The lap times page therefore draws three ranks where its own artboard draws
  four, and it is the only page that loses one to this. Either the band is 12 and the token is
  wrong, or the band is 32 and the artboard owes the page a shorter rank.
- **The pit wall board is drawn grouped by class and is not built that way.** Every use of the
  recessed ground `#060708` on the three pit wall sheets, and on the Panels sheet beside them, is a
  28 px row heading a group of cars with its class and the position of that class's leader, for
  example `GT3 · P1` and then `GT4 · P9` further down. The table builds one flat list, so the colour
  appears nowhere in either pit wall package. The audit filed this as a fill to be corrected, which
  it is not: grouping a board by class is a feature of the table, and it should be built once, in
  the table, rather than painted here. Until it is, a multi-class board reads as one race.
- **The push to pass lamp has no lit colour.** Band D's three chips are drawn on eight catalogue
  bands and on four face sheets, and the spotter is amber and the DRS green on every one of them.
  The push to pass is drawn dim on all twelve, so nothing says what colour it takes when it is
  available. The code lights it in the flag blue, while the 1280 by 720 sheet asks for green and the
  lights review proposes blue and then green as the boost is spent. One of the three is the answer.
- **The nano's band is two pixels short of its own drawing.** Band D now draws the artboards' block
  everywhere it fits: a 15 px label centred in a 13 px row, five pixels, and a 34 px value. Under
  WPF that block wants 62 px, since the label's box opens two and a half pixels above its row and
  the value's closes four and a half below its own, and a 60 px band holds it only once the block
  stops being centred and rides up to the label's headroom, which is what every 60 px band draws.
  The nano at 800 by 286 has 58 px, so its value comes down to 32 while its own artboard asks for
  34. Either that band grows by two pixels, or the nano is the one face whose band is written a
  size smaller than the rest, which a driver would notice only beside another face.

## 4. The work, in packages

The 1581 gaps group into 150 packages that can be built independently. A package owns a set of
files; where two would touch the same file, one depends on the other. The order below is the
dependency order within each area.

### tokens

| package | size | gaps | files |
|---|---|---|---|
| ui size tokens and the density ramp's provenance | M | 3 | `packages/dash/src/tokens.ts`, `packages/dash/src/second/density.ts`, `packages/dash/test/tokens.test.ts` |
| the zone label ramp at 15 px | XL | 6 | `packages/dash/src/second/density.ts`, `packages/dash/src/second/header.ts` |
| pit wall type, names at 15 px and the capped panel hero | L | 5 | `packages/dash/src/second/density.ts`, `packages/dash/src/second/table.ts`, `packages/dash/src/screens/pitwall.ts` |
| pit wall recessed surfaces | S | 3 | `packages/dash/src/screens/pitwall.ts`, `packages/dash/src/second/wheel.ts` |
| bound labels and units are upper-cased | M | 2 | `packages/dash/src/elements/label.ts`, `packages/dash/src/elements/unit.ts`, `packages/dash/src/second/field.ts` and 2 more |
| the band D ground | S | 2 | `packages/dash/src/zones/face.ts`, `packages/dash/test/zoneFace.test.ts` |
| the gear and speed, weight and derived size | L | 8 | `packages/dash/src/zones/zoneAPages.ts`, `packages/dash/src/design/metrics.ts`, `packages/dash/src/zones/bandPages.ts` and 2 more |
| the font faces a package ships | M | 5 | `packages/dash/src/dashboard.ts`, `packages/dash/src/screens/index.ts`, `packages/dash/src/design/fontFiles.ts` and 3 more |
| the settings panel's theme, focus and wordmark | L | 8 | `plugin/OpenDash/Theme.cs`, `plugin/OpenDash/Widgets.cs`, `plugin/OpenDash/SettingsControl.cs` and 3 more |
| the grid's row gap | S | 1 | `packages/dash/src/components/grid2x2.ts` |
| built-package invariants, no literal hex and no stray radius | M | 2 | `packages/dash/src/modules/radar.ts`, `packages/generator/src/serialize.ts`, `packages/dash/test/packageInvariants.test.ts` |
| the licence badge and the inline rating | M | 2 | `packages/dash/src/elements/badge.ts`, `packages/dash/src/elements/index.ts`, `packages/dash/src/second/values.ts` and 2 more |

### elements-components

| package | size | gaps | files |
|---|---|---|---|
| typographic minus, delta scale and lap deltas | S | 4 | `packages/generator/src/ncalc.ts`, `packages/dash/src/modules/delta.ts`, `packages/dash/src/modules/lapTimes.ts` and 2 more |
| image assets, licence notices and the telltale icon set | M | 3 | `packages/dash/src/design/notices.ts`, `packages/dash/src/design/telltales.ts`, `packages/dash/scripts/rasterise.ts` and 5 more |
| rev bar segment gap and sample state | S | 9 | `packages/dash/src/zones/layout.ts`, `packages/dash/src/zones/face.ts`, `packages/dash/src/zones/faces/1920x480.ts` and 10 more |
| field, follower and chip metrics | L | 13 | `packages/dash/src/second/density.ts`, `packages/dash/src/second/field.ts`, `packages/dash/src/elements/unit.ts` and 6 more |
| zone frame chrome and page dots | S | 2 | `packages/dash/src/second/header.ts`, `packages/dash/src/zones/pages.ts`, `packages/dash/src/zones/face.ts` |
| flag bands, chequer and the label weight option | M | 3 | `packages/dash/src/components/flagStrip.ts`, `packages/dash/src/elements/label.ts`, `packages/dash/src/design/advances.ts` and 1 more |
| zone A gear cluster, speed and rpm | L | 10 | `packages/dash/src/zones/zoneAPages.ts` |
| band D fields and status chips | M | 9 | `packages/dash/src/zones/bandPages.ts` |
| settings bar fields, denominators and strip cells | M | 6 | `packages/dash/src/zones/bar.ts`, `docs/design/zones.md` |
| the tyre drawing | XL | 22 | `packages/dash/src/second/tyreGlyph.ts`, `packages/dash/src/second/wheel.ts`, `packages/dash/src/second/values.ts` and 2 more |
| second-screen table rows and rank marks | L | 6 | `packages/dash/src/second/table.ts`, `packages/dash/src/modules/relative.ts`, `packages/dash/src/modules/leaderboard.ts` |
| telltale lamps | L | 5 | `packages/dash/src/second/telltale.ts`, `packages/dash/src/zones/bandPages.ts` |
| the top-down car drawing | L | 5 | `packages/dash/src/second/carTopView.ts`, `packages/dash/src/modules/pitView.ts`, `packages/dash/src/modules/damage.ts` |
| input traces, pedal bars and the steering dial | L | 7 | `packages/dash/src/modules/inputs.ts`, `packages/dash/src/second/trace.ts`, `packages/dash/src/second/steering.ts` and 4 more |
| opponents identity row | M | 1 | `packages/dash/src/modules/opponents.ts`, `docs/design/zones.md` |
| pit wall panel chrome | S | 3 | `packages/dash/src/screens/pitwall.ts` |
| track map and radar marks | M | 3 | `packages/dash/src/modules/track.ts`, `packages/dash/src/modules/radar.ts` |
| missing vocabulary — dot, bar and listRow | M | 5 | `packages/dash/src/elements/dot.ts`, `packages/dash/src/elements/bar.ts`, `packages/dash/src/elements/index.ts` and 3 more |
| the rev bar well on the card faces and the speedo | M | 2 | `packages/dash/src/hero/hero.ts`, `packages/dash/src/layouts/flanked.ts`, `packages/dash/src/modules/speedo.ts` |
| sector colour states | S | 1 | `packages/dash/src/second/sectors.ts`, `packages/dash/src/second/values.ts`, `packages/dash/src/modules/sectors.ts` |
| plugin panel icons | S | 1 | `plugin/OpenDash/Widgets.cs`, `plugin/OpenDash/Theme.cs` |
| element signatures and the monospace glyph guard | L | 2 | `packages/dash/src/elements/label.ts`, `packages/dash/src/elements/numeral.ts`, `packages/dash/src/elements/unit.ts` and 6 more |

### zones-faces

| package | size | gaps | files |
|---|---|---|---|
| face chrome and zone frame | XL | 36 | `packages/dash/src/zones/layout.ts`, `packages/dash/src/zones/faces/1920x480.ts`, `packages/dash/src/zones/faces/1280x480.ts` and 12 more |
| per-face defaults in the contract and its plugin mirror | M | 3 | `packages/dash/src/contract.ts`, `plugin/OpenDash/Contract.cs`, `plugin/OpenDash/FaceSettings.cs` and 5 more |
| the track map drawing | S | 7 | `packages/dash/src/modules/track.ts` |
| the bar | L | 20 | `packages/dash/src/zones/bar.ts`, `packages/dash/src/elements/numeral.ts`, `packages/dash/test/barStrip.test.ts` |
| band D fields and rank metrics | L | 26 | `packages/dash/src/zones/bandPages.ts`, `packages/dash/src/second/values.ts`, `packages/dash/test/bandPages.test.ts` |
| zone A pages | L | 42 | `packages/dash/src/zones/zoneAPages.ts`, `packages/dash/src/components/gear.ts`, `packages/dash/test/zoneAPages.test.ts` |
| the written record of the canvas conflicts | S | 13 | `docs/design/zones.md` |

### modules-pages

| package | size | gaps | files |
|---|---|---|---|
| zone frame chrome and headers | M | 9 | `packages/dash/src/second/header.ts`, `packages/dash/src/zones/face.ts`, `packages/dash/src/zones/pages.ts` |
| value expressions and the true minus | M | 14 | `packages/dash/src/second/values.ts`, `packages/generator/src/ncalc.ts`, `packages/dash/src/design/metrics.ts` and 1 more |
| element primitives, unit and label | S | 5 | `packages/dash/src/elements/unit.ts`, `packages/dash/src/elements/label.ts` |
| image assets in a package | M | 5 | `packages/dash/src/design/assets.ts`, `packages/dash/src/build.ts`, `packages/dash/src/design/notices.ts` |
| contract, catalogue and zone defaults | M | 13 | `packages/dash/src/contract.ts`, `packages/dash/src/screens/companion.ts`, `plugin/OpenDash/Contract.cs` and 3 more |
| layout engine, followers, spread, grids and alignment | XL | 18 | `packages/dash/src/second/field.ts`, `packages/dash/src/second/layout.ts`, `packages/dash/src/second/density.ts` and 1 more |
| shedding declarations and the shape answer | L | 12 | `packages/dash/src/modules/shedding.ts`, `packages/dash/src/second/shape.ts`, `docs/design/zones.md` and 1 more |
| the relative and leaderboard tables | XL | 54 | `packages/dash/src/second/table.ts`, `packages/dash/src/modules/relative.ts`, `packages/dash/src/modules/leaderboard.ts` and 1 more |
| lap times, delta and sectors | XL | 56 | `packages/dash/src/modules/lapTimes.ts`, `packages/dash/src/modules/delta.ts`, `packages/dash/src/modules/sectors.ts` and 1 more |
| fuel and energy | M | 22 | `packages/dash/src/modules/fuel.ts`, `packages/dash/src/modules/energy.ts` |
| tyres and the wheel cell | L | 33 | `packages/dash/src/second/wheel.ts`, `packages/dash/src/modules/tyres.ts` |
| opponents | M | 23 | `packages/dash/src/modules/opponents.ts` |
| session, stint and lap history | M | 30 | `packages/dash/src/modules/session.ts`, `packages/dash/src/modules/stint.ts`, `packages/dash/src/modules/lapHistory.ts` |
| car settings vocabulary and grid | M | 12 | `packages/dash/src/modules/carSettings.ts` |
| picture pages cut from the box, radar, track and inputs | M | 34 | `packages/dash/src/modules/radar.ts`, `packages/dash/src/modules/track.ts`, `packages/dash/src/modules/inputs.ts` and 2 more |
| pit view | M | 18 | `packages/dash/src/modules/pitView.ts` |
| the settings bar | M | 7 | `packages/dash/src/zones/bar.ts`, `docs/design/zones.md` |
| band D pages and corners | L | 16 | `packages/dash/src/zones/bandPages.ts` |
| the telltale rank | L | 13 | `packages/dash/src/zones/telltales.ts`, `packages/dash/src/zones/bandPages.ts`, `docs/design/zones.md` |
| zone A gear and speed page | M | 2 | `packages/dash/src/zones/zoneAPages.ts`, `docs/design/zones.md` |
| web view, car telemetry and the prose pages | S | 4 | `packages/dash/src/modules/pages.ts`, `packages/dash/src/second/placeholder.ts` |
| MVP card copy, sign and colour fixes | S | 8 | `packages/dash/src/cards/speed.ts`, `packages/dash/src/cards/delta.ts`, `packages/dash/src/cards/lastLap.ts` and 4 more |
| per-face fit and shed report | S | 4 | `packages/dash/test/faceFit.test.ts` |

### alerts-notifications

| package | size | gaps | files |
|---|---|---|---|
| the chequered band's phase | S | 7 | `packages/dash/src/components/flagStrip.ts`, `packages/dash/src/components/flagRing.ts`, `packages/dash/test/flagBand.test.ts` |
| the band's anatomy, bold names, borders and a flash that does not uncover the page | M | 4 | `packages/dash/src/components/flagStrip.ts`, `packages/dash/src/elements/label.ts`, `packages/dash/src/design/advances.ts` and 4 more |
| expose the indicator and alert duration tokens | S | 1 | `packages/dash/src/tokens.ts`, `packages/dash/test/tokens.test.ts` |
| the flag format setting | M | 6 | `packages/dash/src/contract.ts`, `packages/dash/test/contract.test.ts`, `packages/dash/test/declared-properties.txt` and 6 more |
| the full-screen flag format | L | 11 | `packages/dash/src/components/flagFull.ts`, `packages/dash/src/zones/face.ts`, `packages/dash/src/zones/layout.ts` and 1 more |
| the face draws the flag catalogue, not six normalised properties | XL | 14 | `packages/dash/src/flags.ts`, `packages/dash/src/components/alertBand.ts`, `packages/dash/src/components/flagStrip.ts` and 6 more |
| the alerts whose source is not a SessionFlags bit | M | 4 | `packages/dash/src/alerts.ts`, `packages/dash/src/second/values.ts`, `packages/dash/src/leds/profile.ts` and 2 more |
| the pit alerts | M | 5 | `packages/dash/src/components/pitAlerts.ts`, `packages/dash/src/components/pitLimiter.ts`, `packages/dash/src/zones/face.ts` and 1 more |
| pop-ups | L | 5 | `packages/dash/src/components/popUp.ts`, `packages/dash/src/components/index.ts`, `packages/dash/src/zones/face.ts` and 1 more |
| change notifications | L | 5 | `packages/generator/src/model.ts`, `packages/generator/src/serialize.ts`, `packages/generator/src/images.ts` and 7 more |
| the lap review | L | 3 | `packages/dash/src/components/lapReview.ts`, `packages/dash/src/contract.ts`, `plugin/OpenDash/Contract.cs` and 4 more |

### second-screens

| package | size | gaps | files |
|---|---|---|---|
| the density ramp and its tokens | S | 1 | `packages/dash/src/second/density.ts`, `packages/dash/src/tokens.ts`, `packages/dash/test/tokens.test.ts` |
| the shared value expressions | S | 0 | `packages/dash/src/second/values.ts`, `packages/dash/src/zones/bandPages.ts` |
| field, rank and header primitives | L | 2 | `packages/dash/src/second/field.ts`, `packages/dash/src/second/rank.ts`, `packages/dash/src/second/layout.ts` and 2 more |
| the wide zone title | S | 1 | `packages/dash/src/screens/zones.ts` |
| the six sector fields | S | 1 | `packages/dash/src/second/sectors.ts`, `packages/dash/src/modules/sectors.ts`, `packages/dash/src/modules/shedding.ts` |
| the companion lap-times grid | M | 1 | `packages/dash/src/modules/lapTimes.ts`, `packages/dash/src/screens/companion.ts`, `packages/dash/src/modules/shedding.ts` |
| the table chrome and the relative list | L | 4 | `packages/dash/src/second/table.ts`, `packages/dash/src/modules/relative.ts`, `packages/dash/src/modules/shedding.ts` |
| the wide car-telemetry zone | M | 3 | `packages/dash/src/modules/pages.ts`, `packages/dash/src/modules/module.ts`, `packages/dash/src/modules/carSettings.ts` and 1 more |
| the opponents module | M | 3 | `packages/dash/src/modules/opponents.ts`, `packages/dash/src/second/chip.ts`, `packages/dash/src/modules/shedding.ts` |
| the pit wall header groups | M | 4 | `packages/dash/src/screens/pitwallHeader.ts` |
| the pit wall panels, the track panel and the map | XL | 6 | `packages/dash/src/screens/pitwall.ts`, `packages/dash/src/modules/track.ts`, `packages/dash/src/second/gauge.ts` |

### companion

| package | size | gaps | files |
|---|---|---|---|
| flag band and body geometry | S | 9 | `packages/dash/src/screens/companion.ts`, `packages/dash/src/modules/module.ts`, `packages/dash/test/secondScreens.test.ts` and 2 more |

### pitwall

| package | size | gaps | files |
|---|---|---|---|
| header strip | M | 12 | `packages/dash/src/screens/pitwallHeader.ts`, `packages/dash/test/pitwallHeader.test.ts` |
| panel and trace chrome | M | 3 | `packages/dash/src/second/header.ts`, `packages/dash/src/second/trace.ts`, `packages/dash/test/panelChrome.test.ts` |
| value formats and character budgets | M | 4 | `packages/dash/src/second/values.ts`, `packages/dash/test/pitwallValues.test.ts` |
| wide zone module variants | M | 1 | `packages/dash/src/modules/opponents.ts`, `packages/dash/src/second/wheel.ts`, `packages/dash/src/modules/module.ts` and 1 more |
| table geometry and cell styling | XL | 14 | `packages/dash/src/second/table.ts`, `packages/dash/src/second/density.ts`, `packages/dash/src/second/chip.ts` and 1 more |
| board columns per page | L | 8 | `packages/dash/src/second/table.ts`, `packages/dash/src/screens/pitwall.ts`, `packages/dash/test/pitwallColumns.test.ts` |
| right-column panels and their fields | L | 4 | `packages/dash/src/screens/pitwall.ts`, `packages/dash/src/second/field.ts`, `packages/dash/test/pitwallPanels.test.ts` |
| telemetry traces | L | 8 | `packages/dash/src/screens/pitwall.ts`, `packages/dash/test/telemetryTraces.test.ts` |
| page and zone rectangles | M | 7 | `packages/dash/src/screens/pitwall.ts`, `packages/dash/src/second/layout.ts`, `packages/dash/test/pitwallGeometry.test.ts` |
| split list with a limit line | L | 1 | `packages/dash/src/second/table.ts`, `packages/dash/src/second/values.ts`, `packages/dash/test/splitList.test.ts` |
| wide zone page titles | S | 1 | `packages/dash/src/contract.ts`, `plugin/OpenDash/Modules.cs`, `plugin/OpenDash/SettingsControl.Panes.cs` |
| class-only leaderboard setting | M | 1 | `packages/dash/src/contract.ts`, `packages/dash/src/screens/pitwall.ts`, `packages/dash/src/screens/zones.ts` and 5 more |
| zone quick glance | M | 1 | `packages/dash/src/contract.ts`, `plugin/OpenDash/Contract.cs`, `plugin/OpenDash/OpenDash.cs` and 2 more |
| zone reference frames in the fit suite | S | 2 | `packages/dash/src/screens/zones.ts`, `packages/dash/test/secondScreens.test.ts` |

### round

| package | size | gaps | files |
|---|---|---|---|
| the three-digit gear cluster | L | 4 | `packages/dash/src/components/gear.ts`, `packages/dash/src/hero/hero.ts`, `packages/dash/src/layouts/800round.ts` and 3 more |
| chequered ring density | S | 2 | `packages/dash/src/components/flagRing.ts` |
| the true minus sign on the delta card | S | 2 | `packages/dash/src/cards/delta.ts`, `packages/dash/test/expressions.test.ts`, `packages/dash/test/__snapshots__/snapshots.test.ts.snap` |

### lights-strip

| package | size | gaps | files |
|---|---|---|---|
| one low-fuel definition and the settings a strip reads | M | 1 | `packages/dash/src/contract.ts`, `packages/dash/src/leds/states.ts`, `plugin/OpenDash/Contract.cs` and 7 more |
| the shape table's device attributions | S | 10 | `packages/dash/src/leds/strip.ts`, `packages/dash/test/leds.test.ts` |
| the gates above the tree | M | 5 | `packages/dash/src/leds/gates.ts`, `packages/dash/src/leds/rpmStrip.ts`, `packages/dash/src/leds/profile.ts` and 3 more |
| the ends stop carrying brake, and rpmOnly retires | M | 4 | `packages/dash/src/leds/rpmStrip.ts`, `packages/dash/src/contract.ts`, `plugin/OpenDash/Contract.cs` and 5 more |
| the centre functions | S | 2 | `packages/dash/src/leds/rpmStrip.ts`, `packages/dash/test/leds.test.ts`, `packages/dash/test/__snapshots__` |
| one over-rev layer and the ladder bands | M | 7 | `packages/dash/src/leds/ladder.ts`, `packages/dash/src/leds/rpmStrip.ts`, `packages/dash/src/leds/shiftPoints.ts` and 5 more |
| lamp allocation | L | 11 | `packages/dash/src/leds/lamps.ts`, `packages/dash/src/leds/effects.ts`, `packages/dash/src/leds/rpmStrip.ts` and 2 more |
| what each lamp carries | L | 15 | `packages/dash/src/leds/effects.ts`, `packages/dash/test/leds.test.ts`, `packages/dash/test/leds.lamps.test.ts` |
| two blink rates and a visible off phase | M | 5 | `packages/dash/src/leds/effects.ts`, `packages/dash/src/leds/rpmStrip.ts`, `packages/dash/src/leds/ladder.ts` and 2 more |
| the flag catalogue on the race lamp | L | 15 | `packages/dash/src/leds/effects.ts`, `packages/dash/src/flags.ts`, `packages/dash/src/leds/rpmStrip.ts` and 2 more |
| wiring order and multi-run shapes | M | 3 | `packages/dash/src/leds/strip.ts`, `packages/dash/src/leds/rpmStrip.ts`, `packages/dash/test/leds.test.ts` |

### flag-box

| package | size | gaps | files |
|---|---|---|---|
| the settings a box owns | L | 1 | `packages/dash/src/contract.ts`, `packages/dash/src/leds/profile.ts`, `packages/dash/src/leds/states.ts` and 6 more |
| play-once frame sequences | S | 1 | `packages/dash/src/leds/glyph.ts` |
| the flag catalogue pictures | L | 10 | `packages/dash/src/flags.ts`, `packages/dash/src/leds/glyphs.ts` |
| the pit and warning pictures | M | 5 | `packages/dash/src/leds/states.ts` |
| the spotter becomes an overlay | XL | 10 | `packages/dash/src/leds/profile.ts`, `packages/dash/src/leds/states.ts`, `packages/dash/src/leds/sheet.ts` and 5 more |
| the redline flash stops in the last gear on either ladder | M | 1 | `packages/dash/src/shift.ts`, `packages/dash/src/components/revSegments.ts`, `packages/dash/src/leds/ladder.ts` |
| the written spec catches up | S | 0 | `docs/design/flag-box.md` |

### plugin-panel

| package | size | gaps | files |
|---|---|---|---|
| panel tokens in Theme | S | 3 | `plugin/OpenDash/Theme.cs`, `plugin/OpenDash.Tests/ThemeTests.cs` |
| test-visible panel constants and icon paths | M | 3 | `plugin/OpenDash.Tests/OpenDash.Tests.csproj`, `plugin/OpenDash/PanelIcons.cs`, `plugin/OpenDash/PanelMetrics.cs` and 2 more |
| contract and settings spine | L | 16 | `packages/dash/src/contract.ts`, `plugin/OpenDash/Contract.cs`, `plugin/OpenDash/OpenDashSettings.cs` and 6 more |
| control kit in Widgets and Segmented | XL | 19 | `plugin/OpenDash/Widgets.cs`, `plugin/OpenDash/Segmented.cs` |
| panel shell and control factories | L | 12 | `plugin/OpenDash/SettingsControl.cs` |
| Rig tab cards, screen header and section titles | M | 10 | `plugin/OpenDash/SettingsControl.Rig.cs` |
| face picker miniature | L | 16 | `plugin/OpenDash/SettingsControl.Panes.cs`, `plugin/OpenDash/PanelFacePlan.cs` |
| pit wall and companion panes | L | 13 | `plugin/OpenDash/SettingsControl.Panes.cs`, `plugin/OpenDash/PanelPitWallPlan.cs` |
| Lights tab layout and rows | M | 9 | `plugin/OpenDash/SettingsControl.Lights.cs` |
| Install tab button styles | S | 2 | `plugin/OpenDash/SettingsControl.Install.cs` |
| Data tab rows | S | 2 | `plugin/OpenDash/SettingsControl.Data.cs` |
| full-face flag format | L | 7 | `packages/dash/src/components/flagStrip.ts`, `packages/dash/src/zones/face.ts`, `packages/dash/src/screens/companion.ts` and 3 more |
| blue flag detail on the band | S | 1 | `packages/dash/src/components/flagStrip.ts`, `packages/dash/src/second/values.ts`, `packages/dash/test/textFit.test.ts` |
| companion as one paged screen | M | 1 | `packages/dash/src/screens/companion.ts`, `packages/dash/src/pagedDashboard.ts`, `packages/dash/test/snapshots.test.ts` |
| strips honour the rig's lights settings | L | 6 | `packages/dash/src/leds/rpmStrip.ts`, `packages/dash/src/leds/strip.ts`, `packages/dash/src/leds/effects.ts` and 4 more |
| session progress reaches the zone module | S | 1 | `packages/dash/src/modules/session.ts` |
| the written record in docs/design | S | 5 | `docs/design/plugin.md`, `docs/design/zones.md`, `docs/design/brand.md` and 1 more |

### plugin-install

| package | size | gaps | files |
|---|---|---|---|
| panel primitives and a pure metrics surface | L | 7 | `plugin/OpenDash/Widgets.cs`, `plugin/OpenDash/SettingsControl.cs`, `plugin/OpenDash/PanelMetrics.cs` and 5 more |
| package names, captions and rig attribution in the catalogue | M | 2 | `plugin/OpenDash/PackageCatalogue.cs`, `plugin/OpenDash/ScreenInstance.cs`, `plugin/OpenDash.Tests/PackageCatalogueTests.cs` and 1 more |
| strip and brow profiles become installable | XL | 2 | `plugin/OpenDash/FlagBoxInstaller.cs`, `plugin/OpenDash/FlagBoxProfile.cs`, `plugin/OpenDash/FlagBoxInstallPlan.cs` and 5 more |
| screen cards on the Rig tab | M | 3 | `plugin/OpenDash/SettingsControl.Rig.cs` |
| the Install tab in three sections | S | 1 | `plugin/OpenDash/SettingsControl.Install.cs`, `plugin/OpenDash/SettingsControl.Install.Packages.cs`, `plugin/OpenDash/SettingsControl.Install.Lights.cs` and 2 more |
| package rows on the Install tab | M | 6 | `plugin/OpenDash/SettingsControl.Install.Packages.cs` |
| the This plugin section, its status and install progress | L | 3 | `plugin/OpenDash/SettingsControl.Install.Plugin.cs`, `plugin/OpenDash/DashboardInstaller.Core.cs`, `plugin/OpenDash/UpdateService.cs` and 5 more |
| the Lights section on the Install tab | L | 8 | `plugin/OpenDash/SettingsControl.Install.Lights.cs`, `plugin/OpenDash/SettingsControl.Lights.cs` |

### docs

| package | size | gaps | files |
|---|---|---|---|
| full-screen flag format on every face | XL | 1 | `packages/dash/src/contract.ts`, `packages/dash/src/zones/face.ts`, `packages/dash/src/zones/layout.ts` and 9 more |
| second-screen assertion guards | M | 2 | `packages/dash/test/secondScreens.test.ts`, `packages/dash/src/second/field.ts`, `packages/dash/src/modules/lapTimes.ts` and 20 more |
| brand-colour guard over the zone faces and the second screens | S | 1 | `packages/dash/test/e2e.test.ts` |

## Related

[lights-review.md](lights-review.md) is the review that produced the lights and flag-box sheets and the
defect list behind area `lights-strip`. [zones.md](../design/zones.md) is the written specification of
the face, [readability-pass.md](../design/readability-pass.md) the per-page tickets that anticipated
much of area `modules-pages`, and [plugin.md](../design/plugin.md) the settings panel the canvas draws.
