# A core and its themes: where the line falls today, and what moving it would take

**Status:** research, written on 2026-10-14 for the author's question of that day, and the basis of
[ADR 0022](../decisions/0022-core-and-theme.md), which decides it; the ticket is
[#1139](https://github.com/xorob0/OpenDash/issues/1139).

## 1. The question

The author's picture of the project is a **core** that does everything the product has to do
(reach every page of the catalogue, read and synchronise the data, publish the settings) and
**themes** that decide how to display it, each free to skip what it does not want to draw. A theme may
compute: it may take a flag state and write "full course yellow" on a yellow ground, or take a fuel
caution and draw a red banner over the whole dash with the litres left in it. What the core hands
over should be as plain as the value allows: a boolean for a state, a number for laps or litres, an
enumeration for a flag, and text only where the value is text, a driver's name or a session's.

The impression behind the question is that a lot of logic currently lives in the theme that could
live in the core. This document checks that impression against the code, says what the core and the
themes are today, measures how far the present line is from the one described, and lists what would
have to move, where, in what order, and what it would cost. It also records one constraint the
author added while it was being written: the house look, the one `main` has always built, has to be a
theme like the others, not the code path the others are exceptions to.

## 2. What the project is made of today

### 2.1 Two runtimes, and where "the data" is read

OpenDash has no runtime of its own ([ADR 0001](../decisions/0001-simhub-native-rendering.md)). A
package is a scene graph of items whose properties carry NCalc or JavaScript bindings, and SimHub
evaluates every binding on every frame against the properties it publishes. Everything a dashboard
"reads" is therefore an expression string resolved at build time into the `.djson`
([ADR 0002](../decisions/0002-djson-generated-from-source.md)), and the plugin, which is the only
other code that runs on the rig, publishes properties and nothing else
([ADR 0003](../decisions/0003-plugin-settings-through-properties.md)).

So there are two places where something that could be called a core can live, and the project
already uses both:

| Where | What lives there today | Evaluated |
|---|---|---|
| `packages/dash/src/second/values.ts`, `flags.ts`, `shift.ts`, `second/tracked.ts`, `components/pitAlerts.ts`, `zones/telltales.ts` | the semantic layer: one NCalc helper per reading, the alert catalogue and its ranking, the shift ladders, the watched settings, the pit family, the engine warnings | at build time into strings, then by SimHub per item per frame |
| `plugin/OpenDash` (`OpenDash.cs` `DataUpdate`, `LivePosition.cs`, `ClassBestLap.cs`, `SimCapabilities.cs`, `CarLightService.cs`, `SlipEstimate.cs`, `FaceSettings.cs`) | the settings as properties, and six computed families that pass [ADR 0009](../decisions/0009-does-the-plugin-compute.md)'s gate | in C#, once per frame, published as `OpenDash.*` |
| `packages/dash/src/contract.ts` and `plugin/OpenDash/Contract.cs` | the settings contract, mirrored in both languages and held equal by tests on both sides | n/a |

The semantic layer is large and, as a layer, already well separated from the drawing: `values.ts`
alone is 3 056 lines and about 250 exports, every property name in it was checked against the
decompiled readers, and the themes reach it through imports (nine of the twenty-five theme files
import it) rather than by reading telemetry themselves. In that respect the impression is partly
wrong: the data is read in one place, and a theme that wants the litres left calls `fuelLitres()`
exactly as the house face does.

### 2.2 What a theme is, as the code defines it

A theme ([ADR 0015](../decisions/0015-car-themes.md)) is a folder under
`packages/dash/src/themes/<id>/` holding a token overlay, an anatomy (a function from a face size to
named regions with roles, `themes/anatomy.ts`) and, since the Porsche, a *drawing*: the
`ThemeDrawing` interface in `themes/drawing.ts`, whose sixteen hooks are all optional and are described
by that file in one sentence that decides this whole analysis:

> A theme with no drawing is drawn exactly as the house face is, which is what keeps the default
> theme's packages byte-identical: every hook is optional and every one left out is the house's code
> path unchanged.

The hooks are `revBar`, `bar`, `chrome`, `takeovers`, `changeNotifications`, `zoneGround`,
`zoneHeaderSize`, `moduleFrame`, `zoneAPage`, `zoneCounters`, `restyle`, `gearGhosts`, `bandCorners`,
`flagSettled`, `bandPages`, `modules` and `bandPage`, and `settings`. The modules of zones B and C are
reached through `ModuleRegister` (`fieldsRow`, `stack`, `houseLayout`), which `themes/moduleRegister.ts`
hands to `modules/module.ts` through a process-global slot, with `withHouseLayout` as a depth counter
that switches the slot off while a page is drawn "the house's way".

Three facts follow from reading those files together with `zones/face.ts`, `zones/pages.ts` and
`themes/index.ts`:

**The default theme is the absence of a theme.** `themes/default/` holds a twelve-line anatomy that
hands back `zones/layout.ts`'s rectangles and an overlay of `{}`; it registers no drawing, and
`moduleRegister()` returns `undefined` for it. The house face is not a theme that implements every
hook; it is the body of `faceItems`, `houseChrome`, `zoneAPage`, `bandPageItems`, `fieldsRow` and
`stack`, and each theme hook is an `if (drawing.x)` branch in front of that body. The author's new
requirement, that the main theme be a theme, is therefore not a refinement of today's design but
its inversion.

**The seam is one of code, not of data.** A hook receives a `FaceContext` (the house layout, the
regions, the size) and returns items; the Porsche's `takeovers` and the AiM's `takeovers` each rebuild
their state's condition from the semantic layer and redraw it. What a hook does *not* receive is a
description of what the house would have drawn there: which readings, in which order, with which
budgets. The `ModuleRegister` is the one exception, since `fieldsRow` is handed the page's `FieldSpec`s,
and it is the exception that shows what the rest is missing.

**The theme is chosen per process.** `tokens.ts` resolves the overlay before `ds` is built, forty-odd
modules read `ds` at import time, so a build of several themes is one process per theme
(`build.ts` `composeTheme`, `THEME_PROCESS`), and `moduleRegister` and `THEME_ID` are process globals.
Any design in which a theme is a value passed to functions has to undo this first or live with it.

### 2.3 What a page is, as the code defines it

A module (`modules/module.ts`) is "a function of a rect, a density and a shape", and that is exactly
what it is: `modules/fuel.ts` calls `stack`, `fieldsRow`, `blockRow` and `levelGauge` itself, and chooses
its own colours inline (`iff(lowFuel(), str(ds.purpose.fuel.low), str(ds.color.text.primary))` three
times in one file). The `FieldSpec` it builds carries, in one object, what the author would call core
(`id`, `label`, `bind`, `visibleBind`, `chars`, `widest`, the shedding order through `modules/shedding.ts`)
and what is presentation (`fs`, `color`, `colorBind`, `weight`, `follower.gap`, `labelBelow`). A theme's
`fieldsRow` therefore receives readings with the house's font sizes and colours already on them, and the
Porsche's and the AiM's registers spend a visible part of their 830 lines undoing that: `valueOf` scales
the size away, the AiM drops every colour binding through `withoutBindings` because its LCD has one ink,
and both keep a `SHORT` table of the house's labels because the label is a string rather than an id.

Band D's eight pages are a second kind of page (`zones/bandPages.ts`, one rank of fields), zone A's four
are a third (`zones/zoneAPages.ts`), and the bar is a fourth (`zones/bar.ts`). Each is drawn by its own
code and each is themed through its own hook (`bandPage`, `zoneAPage`, `bar`), so a theme that wants
one register across the face has to implement it four times.

### 2.4 Where presentation sits inside the semantic layer

The impression the author has is not only about the themes. The layer that was meant to be the data
also decides some of the display:

- `values.ts` returns **colour** from three helpers, `fuelToEndColour`, `referenceDeltaColour` and
  `deltaColour`, which read `purpose.delta.*` and `color.text.primary` from the tokens.
- It returns **formatted text** wherever a reading is drawn: `lapTime` writes `−:−−.−−−` when there is
  no lap, `positionLabelled` prepends `P`, `fuelToEndText` and `lapsLeftText` write `--`, `ratingK`
  appends `k`, `NO_VALUE`, `NO_TIME` and `UNTIMED_MARK` live there, and `CHARS` is a table of character
  budgets, which is a measurement of the house's cells rather than a fact about the value.
- `flags.ts` carries, on every entry of `ALERT_CATALOGUE`, a `band` field with the **shape, colour, label
  and short label** the house draws it in, and `AlertBandSpec` is typed so that "a condition that reaches
  the catalogue without a shape, a colour and a name is a condition the face cannot draw". The theme hook
  that restyles the face, `restyle`, says in its own doc comment "never the flag". A theme cannot write
  "full course yellow" on a yellow ground today, which is the author's first example.
- `components/pitAlerts.ts` declares each state of the pit family with its `shape` and `colour` beside its
  `when`; `zones/telltales.ts` does the same for the lamps.

None of this is wrong as a house design. It is wrong as a core, because a theme that disagrees with
one of those choices has to reach past the helper to the reading under it, and there is then a second
place where the rule can drift.

### 2.5 Where data rules sit inside the themes

The converse also happens, and the second survey of this document (section 4) counts it. The cases
read directly are:

- `themes/porsche/register.ts` defines `lowFuelAlarm`, the tank under ten litres, as the theme's own
  threshold, while the house's `tankIsLow` in `values.ts` is a count of laps against the rig's
  `LowFuelLaps` setting. The face then draws both: the car's alarm in zone C's panel and the house's
  low-fuel pop-up over the gear (`overlays.ts`, #1047). Two rules for one tank.
- `themes/aim/takeovers.ts` keeps its own `WARNINGS` list of three engine-warning bits with its own
  texts, beside `zones/telltales.ts`'s `ENGINE_WARNING_BITS` and the house's lamps.
- `themes/aim/modules.ts` keeps `SHORT_SESSION`, a table rewriting iRacing's and Assetto Corsa's session
  names, beside `SESSION_NAMES` in `values.ts`; the session is the one reading the LCD writes in its own
  words, and the AiM's Session cell was the subject of the most recent commit on `main` (#1127).
- `themes/porsche/overlays.ts` decides that the limiter body is red "over the limit" through
  `overPitLimit()`, which is a house helper, so this one is a theme computing from core signals, which
  is the thing the author wants to allow.

Both themes also draw the pit limiter state and the change notifications again from scratch, gated on
`pitAlertVisible('limiter')` and `changeNotificationVisible(id)`; that is the right direction (the
condition is the core's, the drawing is the theme's) and it shows that the seam can work when the
condition is exposed under a name.

### 2.6 The other renderers

A face is not the only thing that reads the semantic layer. The companion, the pit wall, the idle
screen, the round faces (still on the card model), the 8x8 flag box (`leds/states.ts`,
`leds/glyphs.ts`), the LED strips (`leds/effects.ts`, `leds/rpmStrip.ts`) and the panel's own preview all
read the same conditions, and three of them rank the same `ALERT_CATALOGUE`. In the author's
vocabulary these are themes of other devices: the flag box draws `LIMITER_IN_LANE` as a two-pixel frame
for the same state the face draws as a filled banner. Any core that is defined for the face should be
defined so that the matrix and the strip read it too, because they already do.

## 3. The constraints that bound any answer

These are facts about SimHub, established by decompilation or on the VM and recorded in the research
and the decisions, and no architecture changes them.

1. **A binding is a string evaluated per item per frame.** There is no shared evaluation except the
   one SimHub does by expression text for expressions that read no variable
   (`docs/research/simhub-dash-format.md`); every item carrying the ranking of twenty conditions
   evaluates it. This is why `raisedRank` exists: one number instead of twenty questions.
2. **Bindable types are `string`, `int`, `double`, `bool`, `Color` and `Brush`**
   ([ADR 0011](../decisions/0011-personalisation.md)). An enumeration reaches an expression as a string
   compared with `=` or as a number; a theme's "enum" is therefore a convention on the generator side,
   not a type SimHub knows.
3. **No memory between frames in the scene graph**, except `changed(ms, value)`, which is SimHub's own
   window; `setvalue`/`getvalue` are refused by ADR 0009. #167 asked whether a JavaScript binding can
   hold state and was closed on 2026-09-22 as cancelled, without an answer, every ticket it gated
   having been built by comparing two published numbers instead; ADR 0009's "Unresolved" still names
   it as open.
4. **The plugin computes only through ADR 0009's gate**, and every value it publishes is read with
   SimHub's own answer as the `isnull` fallback, so each computed value exists twice by design: once in
   C# and once as the NCalc derivation that stands in for it. There are six such families today
   (section 2.1), and 256 of the 666 pinned property names are the live order alone.
5. **Property names are literals and global**; the contract is pinned in
   `packages/dash/test/declared-properties.txt` and the plugin's set must equal it
   ([ADR 0017](../decisions/0017-a-screen-is-an-instance.md)). Every new property is a both-sides change.
6. **Geometry is a build input and text is measured at build time** (`textFit`, `widest`,
   `secondScreens`, `conformance`). Whatever a theme draws, the test harness has to be able to measure it
   without a renderer, which is why a reading carries `chars` and `widest` and why those two are core data
   even though they look like presentation.
7. **The default build is required to stay byte-identical** across machinery changes (ADR 0015), which is
   the one property that makes a refactor of this size reviewable.

## 4. Where the seam leaks, measured

The counts are of lines matching a direct property read, `game(`, `raw(`, `computed(`, `prop(`,
`driver(` or `changed(`, with or without the `ncalc.` prefix, and include a few comment lines; they
are an order of magnitude rather than an audit.

| Directory | Matching lines | Reading |
|---|---|---|
| `second/values.ts` | 107 | the semantic layer, where they belong |
| `contract.ts` | 55 | every one a settings read |
| `shift.ts` | 16 | the shift model, which is core |
| `flags.ts` | 10 | the alert catalogue, which is core |
| `zones/` | 23 | `bandPages.ts` 21, `zoneAPages.ts` 1, `telltales.ts` 1 |
| `cards/` | 15 | the retired card path, still built for the round faces |
| `modules/` | 13 | `stint` 5, `energy` 4, `engineReadings` 2, `carSettings` 1, `lapTimes` 1 |
| `leds/` | 12 | `effects` 7, `states` 2, `shiftPoints` 2, `gear` 1 |
| `components/` | 11 | `flagStrip` 3, `popUp` 3, `gear` 2, `changeNotification`, `lapReview`, `revSegments` |
| `screens/` | 3 | `pitwall.ts` |
| `themes/porsche` | 4 | `strip.ts` 2, `overlays.ts` 1, `register.ts` 1 |
| `themes/aim` | 2 | `zoneA.ts` |

About ninety direct reads therefore sit in drawing code, and the themes account for six of them. The
impression that the themes hold the logic is, by this measure, wrong: the drawing code of the *house*
holds it, and the themes inherit the habit. The cases that matter are the ones where the same rule is
written more than once:

- **The gear** has no helper in `values.ts` and `game('Gear')` is read in nine places: `components/gear.ts`
  (twice), `zones/zoneAPages.ts`, `screens/pitwall.ts`, `leds/gear.ts`, `leds/shiftPoints.ts`, `shift.ts`,
  `themes/porsche/overlays.ts` and `themes/aim/zoneA.ts`.
- **The speed unit** is `speedUnit()` in `values.ts` and is re-derived in `cards/speed.ts` without the
  `isnull`, and in `themes/aim/zoneA.ts`, which writes `kph` where the house writes `km/h`.
- **Low fuel** has three definitions: `tankIsLow` (laps against the rig's setting, read by the telltales,
  the matrix, the strips, the pop-up and the fuel page), the Porsche's ten litres
  (`themes/porsche/register.ts`, driving the takeover and the hazard lamp in `body.ts`), and
  `cards/fuelLaps.ts`, which writes the laps comparison again inline with the settle gate applied apart.
- **Push to pass** is read three ways: `game('PushToPassActive')` in `flags.ts`, the same by full name in
  `leds/effects.ts`, and `raw('PushToPass')`, a different property, in `zones/bandPages.ts`'s corner lamp.
- **The headlight flash** is `HEADLIGHT_FLASH` in `flags.ts`, with its `driving()` gate, and is read again
  with its own `changed()` window and no gate in `themes/porsche/strip.ts`.
- **The engine warnings** are decoded in `zones/telltales.ts`, not in `values.ts`; both themes import it,
  and the AiM then ranks three of the bits in an order of its own in `takeovers.ts`. The Porsche's `fuel`
  lamp means the fuel-pressure bit where the house's `fuel` lamp means `tankIsLow`, so one lamp id carries
  two meanings across themes.
- **The stint**, **virtual energy** and **track grip** reads are each repeated between a module and a band
  page (`modules/stint.ts` and `bandPages.ts`; `modules/energy.ts` and `bandPages.ts`; `trackGrip()` and two
  inline copies in `bandPages.ts`), and `Fuel_RemainingLaps` is re-read in `cards/fuelLaps.ts` and
  `components/popUp.ts`.
- **Session classification** is `isRace()` and `SESSION_NAMES` in `values.ts`, upper-cased, and a
  case-sensitive `eq(sessionType(), 'Race')` with its own `QUALIFYING` list in `themes/porsche/strip.ts`.
- **Thresholds in drawing code:** the same-lap epsilon `0.0005` appears four times (`cards/lastLap.ts`,
  `modules/lapHistory.ts`, `second/sectors.ts`, `values.ts`); the lap history's delta bands (0.5 s caution,
  1 s danger) are its own and not `deltaColour`'s; the tyre temperature and wear bands are written in
  `cards/tyreTemps.ts` and again in `second/wheel.ts`; the "at the line" windows are 3 s in `popUp.ts` and
  4 s in `lapReview.ts`, deliberately.
- **`values.ts` depends on the fonts.** It imports `design/advances`, `design/metrics`, `elements/mark` and
  `second/drawn`, measures `CLASS_AND_PLACE_WIDEST` with `measureText`, and `bandFuelQuantity` carries a
  layout rule ("whole from ten"). About 45 of its 195 helpers return formatted text, five return a
  `DrawnFigure`, and three return a colour.

The six theme reads above were moved behind core signals in the pull request for #1139, with the
three themes' packages byte-identical before and after; the house's reads stay as counted until the
steps of section 6 reach them.

Two things the themes do are worth naming on their own, because the second step of section 6 would
make them impossible. The AiM's register finds the session field by comparing its binding string with
`sessionType()` (`themes/aim/modules.ts`), and both registers rewrite the house's label *bindings* by
substring replacement of the long label with the short one; an id on the field would let both say what
they mean.

## 5. Three shapes the core could take

### 5.1 A runtime core: the plugin publishes the typed signals

The most literal reading of the author's description is a plugin that reads the sim each frame and
publishes `OpenDash.Flag` as an enumeration, `OpenDash.FuelCaution` as a boolean and
`OpenDash.FuelLitres` as a number, so that every theme and every device reads one answer. It is the
shape a dashboard author new to the project would expect, and it is what DahlDesign and the other
large packages do.

It would seem the cleanest, and it is rejected here for reasons the project has already written down.
ADR 0009 refuses it unless nothing else can provide the value, and almost everything here can be had
in the expression; the plugin would then enter the 60 Hz path for every reading rather than for six
families; each published signal needs its NCalc twin for the frames on which the plugin publishes
nothing (constraint 4), so the semantic layer would not go away but be duplicated in C#; and the
property pin would grow by one name per signal on both sides. The second point is the decisive one:
the whole point of the exercise is to have one definition of each rule, and a plugin core gives two.

### 5.2 A build-time core: typed signals in TypeScript, compiled to expressions

The semantic layer already is this, minus the typing and minus the presentation it carries. The core
would be a set of **signals**, each a function returning a branded expression type that says what the
expression evaluates to:

```ts
type Bool = Expr & { readonly __t: 'bool' };
type Num  = Expr & { readonly __t: 'num' };
type Enum<V extends string> = Expr & { readonly __t: 'enum'; readonly __v: V };
type Text = Expr & { readonly __t: 'text'; readonly widest: string };
```

`Expr` is `string` today (`bind.ts`), so this is a phantom type over the same strings, costs nothing in
the output, and lets the compiler refuse `iff(fuelLitres(), ...)` where a boolean is expected. A
signal is either a **reading** (`fuelLitres(): Num`, `inTheCar(): Bool`, `sessionKind(): Enum<...>`), a
**state** (`tankIsLow(): Bool`, `pitFamily(): Enum<'engineOff'|'ignitionOff'|'limiterOut'|...>`,
`alert(): Enum<AlertId>`), or a **setting** (`setting.positionMode(): Enum<'overall'|'class'>`). A
`Text` carries its `widest` because the fit tests need it (constraint 6), and that is the one piece of
measurement the core keeps.

The themes receive the signals and the catalogue, and draw. A theme may combine signals
(`and(inTheCar(), lt(fuelLitres(), num(10)))` is a valid theme rule, and is exactly the Porsche's
alarm), and the one thing a theme may not do is read a property SimHub publishes directly: no
`game(`, `raw(`, `prop(`, `computed(` or `driver(` under `themes/`. That rule is a one-line test.

This is the shape recommended below. It keeps every decision from 0001 to 0021 standing, it is what the
code is already most of the way to, and it is the only one of the three in which a rule is written
once.

### 5.3 A middle shape: JavaScript bindings as named derivations

ADR 0009 names the JavaScript binding (`Interpreter: 1`) as the first move when a derivation is
shared by many items, "which the second screens already use". The code does not bear the second half
out: the generator supports it (`model.ts` `interpreter: 'js'`, `serialize.ts`), and the dash package
emits none, `Expr` being a string and `formula()` building NCalc only, while `leds/rpmStrip.ts` records
that a JavaScript body was rejected because the validator cannot read it. So the middle shape is
available and unused. It is not a core on its own, since the derivation still has to be written in
TypeScript to be emitted, but it is the tool the build-time core would reach for when an expression
grows past what NCalc reads well, and it should be allowed to the typed layer as an emission strategy
rather than treated as a separate design. The sentence in ADR 0009 is one to amend whichever way this
goes.

## 6. What would have to change, and where

The list is ordered so that each step leaves the default build byte-identical (constraint 7) and
`bun run check` green, which is the project's own rule for a commit. Nothing here is implemented.

### 6.1 The core's contents

| Concern | Today | Proposed home | What moves |
|---|---|---|---|
| Readings (telemetry as expressions) | `second/values.ts`, 3 056 lines | `core/signals/*.ts`, one file per family (session, car, fuel, tyres, opponents, pit, lights) | the reading half of `values.ts`, retyped as `Bool`/`Num`/`Enum`/`Text` |
| States and rules | spread over `values.ts`, `flags.ts`, `pitAlerts.ts`, `telltales.ts`, `tracked.ts`, `shift.ts` | `core/states/*.ts` | `tankIsLow`, `fuelIsSettled`, the pit family's five `when`s, the engine warnings, the twenty alert conditions and `raisedRank`, the shift ladders, the watched settings |
| Formatting | `values.ts` (`lapTime`, `positionLabelled`, `NO_VALUE`, `ratingK`, `clock`, `minutesClock`) | `themes/house/format.ts`, exported for reuse by any theme that wants the house formats | every helper that returns a string a driver reads, with its `widest`; a theme that prefers `P 4` to `P4` writes its own |
| Colour rules | `fuelToEndColour`, `deltaColour`, `referenceDeltaColour` in `values.ts`; `band.colour` in `flags.ts`; `colour` in `pitAlerts.ts` | `themes/house/colours.ts` | the three helpers become `Bool`/`Enum` states in the core (`fuelReachesFlag(): Enum<'unknown'|'yes'|'no'>`, `deltaSign(): Enum<...>`) and the house maps them to tokens |
| The alert catalogue | `ALERT_CATALOGUE` with `band: AlertBandSpec` on each entry | `core/alerts.ts` keeps `id`, `name`, `bits`/`when`, `critical`, `motion`, `neutral` and the ranking; `themes/house/alertBands.ts` keeps a `Record<AlertId, AlertBandSpec>` | the `band` field, out of the core and into a table every theme owns (the flag box's glyph table in `leds/glyphs.ts` is already that table for the matrix) |
| The settings contract | `contract.ts` / `Contract.cs` | unchanged | nothing; the contract is already the core's public interface, and the `setting.*` readers become `Enum`/`Bool`/`Num` signals in place |
| The plugin's computed families | `OpenDash.cs` and the six classes | unchanged | nothing; ADR 0009's gate stays, and the fallback derivation lives in the signal that reads the property |

### 6.2 The page catalogue as data

Today a page is a function that draws. For a theme to lay a page out its own way without re-deriving
the readings, the catalogue has to be data first and a drawing second:

```ts
interface Field {
  id: string;                       // the key in the shedding order and in a theme's label table
  label: LabelKey;                  // not a string: 'session.best', which each theme spells
  value: Num | Text | Enum<...>;    // the signal
  kind: 'number' | 'lapTime' | 'delta' | 'clock' | 'word' | 'position' | ...;  // what formatter applies
  unit?: Enum<'L' | 'gal'> | ...;   // a signal, since the unit follows the sim
  visible?: Bool;
  rank: number;                     // place in the shedding order, from shedding.ts
  budget: Chars;                    // the cells the widest reading needs, which the fit tests measure
}
interface Page { id: string; name: LabelKey; ranks: Field[][]; gauges?: ...; drawings?: ... }
```

`FieldSpec` would then be a house construction *from* a `Field` (the house adds `fs`, `color`, `weight`
and `follower.gap` from its density), and `ModuleRegister.fieldsRow` would receive `Field`s rather than
`FieldSpec`s. The `SHORT` tables of both themes become each theme's own label table, keyed by `LabelKey`,
which is also where the AiM's `SHORT_SESSION` belongs. The twenty-two modules under `modules/` keep their
drawings for the pages that are a drawing rather than a list (inputs, radar, track, tyres, damage) and
hand those over as `drawings` the register may take or replace; that is already how `houseLayout` works,
read the other way round.

Band D's pages, zone A's pages and the bar are expressed in the same `Page` shape, so that one register
can draw all four, which neither theme can today.

### 6.3 The theme as a complete renderer, the house included

`ThemeDrawing` loses its optionality: every hook is required, `faceItems` in `zones/face.ts` calls
`theme.revBar(ctx)`, `theme.chrome(ctx)` and so on with no `else` branch, and the house's bodies move
under `packages/dash/src/themes/house/` (the name is the author's to choose; "default" is what the
catalogue calls it today). The process-global `moduleRegister` slot and `withHouseLayout` go away with
the branch they served, and a theme that wants a house page draws it by calling the house theme's
function for it, which is an import rather than a depth counter.

That inversion is the one the author asked for, and it is also what makes the `ThemeDrawing` interface
honest: a hook list that grew "the parts the Porsche needed and no more" becomes the list of what a face
is, and a new theme can be written from the interface rather than by reading the house's `if`s.

A theme that does not want a part at all declares so: today `bar` is the one optional role
(`optionalRegionRect`), and ADR 0015 forbids removing a page or dropping a field. That rule should
stay, since it is what keeps a wheel button meaning the same thing on every face; "skipping" in the
author's sense is a theme choosing not to draw a *house decoration* (the zone counters, the gear ghosts,
the band corners, the well), which the hooks already permit, and not a theme hiding a reading.

### 6.4 The theme as a value rather than a process

`ds` and `THEME_ID` being module-level constants resolved from an environment variable is the reason a
multi-theme build is one process per theme and the reason `moduleRegister` is a global. The clean form is
a `ThemeContext` (tokens, drawing, label table) passed down, which is a wide mechanical change (every
`ds.` read at import time becomes a read inside a function). It is listed last because the per-process
build works, is tested, and does not stand in the way of the other steps; it is the step to take when a
theme is first drawn twice in one process (a theme whose panel preview is built in the same run, for
instance), and not before.

### 6.5 The plugin

Nothing in the split asks the plugin for a new property. What it asks is that the plugin's catalogue
knowledge of a theme (`Contract.ThemeEntry`: id, name, cars, car paths, sizes, band pages) and the
`Theme<Theme><Setting>` properties stay what they are, since they are the contract and not the
rendering. The one plugin consequence worth writing down is negative: the runtime core of section 5.1
is not built, so `DataUpdate` does not grow.

### 6.6 The tests

The fit tests (`textFit`, `widest`, `secondScreens`, `conformance`, `flagSettled`, `faceFit`) measure
items, and items do not change shape, so they are the regression net rather than the cost. The
snapshots under `packages/dash/test/__snapshots__` change only if an item's name or order moves, and the
byte-identical diff of `build/` is the stronger check anyway. Two tests are new: the one that forbids
`game(`/`raw(`/`prop(`/`driver(`/`computed(` under `themes/`, and the one that holds every theme to
drawing every `Field` of every `Page` of the catalogue by id, which is `conformance.test.ts`'s "nothing
disappears" made exact.

What is lost is the ability to write a page in one file. A module today is thirty lines that read
naturally; split into a `Page` of fields and a house drawing it is two files, and a contributor adding a
reading touches the catalogue, the shedding order and the house drawing. That is the price of a theme
being able to draw the reading without knowing the house, and it is the same price the plugin already
pays for the contract.

## 7. Order of work and cost

A rough sizing, in the units the project uses (one pull request, one green checkpoint each, default
build byte-identical at the end of each). It is a judgement from reading rather than a measurement.

| Step | What | Touches | Size |
|---|---|---|---|
| 0 | The record (the draft beside this document), the `themes/` purity test, a `LabelKey` type with the house's strings as its values | `docs/`, one test | small |
| 1 | Branded signal types on `Expr`; retype `values.ts` exports in place, family by family, without moving them | `bind.ts`, `values.ts`, callers that pass the wrong kind | medium, mechanical, many small PRs |
| 2 | Move formatting and colour out of `values.ts` into `themes/house/`; replace the three colour helpers by states | `values.ts`, the modules that import them, both car themes | medium |
| 3 | Split `ALERT_CATALOGUE` into conditions and the house's band table; the same for the pit family and the telltales | `flags.ts`, `components/*`, `leds/*` | medium, with the matrix and the strips as consumers to keep green |
| 4 | `Field`/`Page` as data; `FieldSpec` built from `Field`; the two registers rewritten to take `Field`s; `SHORT` tables become label tables | `second/field.ts`, `modules/*`, `zones/bandPages.ts`, `zones/zoneAPages.ts`, `zones/bar.ts`, both registers | large, the heart of the work |
| 5 | Every `ThemeDrawing` hook required; house bodies under `themes/house/`; `faceItems` without `else`; `moduleRegister` and `withHouseLayout` deleted | `zones/face.ts`, `zones/pages.ts`, `themes/*` | medium, mechanical |
| 6 | `ThemeContext` instead of process globals | `tokens.ts` and every importer of `ds` | large, optional, last |

Steps 1 to 3 can start now and each stands on its own. Step 4 is where the design is decided, and the
draft record proposes that the `Field` shape be settled on a ticket before it is written. Step 5 is what
answers the author's "the main theme should still be a theme", and it depends on step 4 only for the
module register; the other hooks could be made required earlier, at the cost of a `themes/house/` that
re-exports today's bodies for a while.

## 8. What this does not change

The author should not read this document as a plan to move computation into the plugin, to add a
runtime theme switch, to let a theme drop a reading, or to regenerate anything on the rig. All four
have been decided against (ADR 0009, ADR 0015, ADR 0011) and nothing in the code read for this document
argues for reopening them. The split described here is a build-time split inside `packages/dash`, and
its one external effect is a cleaner `ThemeDrawing` interface for the next car.
