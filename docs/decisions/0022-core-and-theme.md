# ADR 0022: A core of typed signals, and a theme that draws all of a face

**Date:** 2026-10-14
**Status:** Accepted, on the author's ruling of 2026-10-14, as the record
[#1139](https://github.com/xorob0/OpenDash/issues/1139) asked for. The research behind it is
[research/core-and-theme.md](../research/core-and-theme.md), and the first step landed with the
record: the purity test over the themes and the six reads it found. It narrows
[ADR 0015](0015-car-themes.md), whose hooks become required, amends one sentence of
[ADR 0009](0009-does-the-plugin-compute.md), and leaves [ADR 0003](0003-plugin-settings-through-properties.md),
[ADR 0011](0011-personalisation.md), [ADR 0016](0016-themed-package-distribution.md),
[ADR 0017](0017-a-screen-is-an-instance.md) and [ADR 0021](0021-a-dashboard-never-names-a-game.md) standing.

## Context

The author's picture of the project is a core that reads and synchronises the data and reaches every
page of the catalogue, and themes that decide how to display it, computing where they wish from what
the core hands them: a boolean for a state, a number for a quantity, an enumeration for a flag, text
only where the value is text. The house look is to be one of those themes rather than the code the
others are exceptions to.

The code is a good distance from that picture, and not in the direction the author expected. The
semantic layer exists (`second/values.ts`, `flags.ts`, `shift.ts`, the pit family, the telltales), the
themes read it rather than the sim, and the plugin publishes only what [ADR 0009](0009-does-the-plugin-compute.md)
lets through. What is missing is on three other sides. First, the layer is untyped: every helper
returns `Expr`, which is `string`, so nothing says which helpers answer a question and which write a
reading, and about a quarter of them write formatted text, three return a colour, and the alert
catalogue carries the shape, colour and label of every condition. Second, a page is a function that
draws itself, choosing its sizes and colours inline, and a theme sees it only through two hooks that
receive readings with the house's presentation already on them, which the two car themes then spend
their effort undoing. Third, and decisive for the author's added requirement, the default theme is the
absence of a theme: every hook of `ThemeDrawing` is optional and every one left out "is the house's
code path unchanged", `moduleRegister()` returns `undefined` for the default, and the house face is the
body of `zones/face.ts` and its neighbours rather than an implementation of the interface. The
consequences are measured in the research: about ninety direct telemetry reads in drawing code, of
which six are in the themes; one tank with three definitions of "low"; one gear read in nine places;
twenty-eight theme commits since 2026-10-06 of which twenty-six also touched the house's code.

## Decision

**The core is a set of typed signals, written in TypeScript and compiled to expressions, and a
catalogue of pages declared as data. A theme is a complete renderer of that catalogue, the house
theme included, and a theme may derive from signals and may never read a property.**

In the order of what each sentence binds:

1. **A signal is a branded expression.** `Expr` stays a string in the output and gains a phantom type
   in the source: `Bool`, `Num`, `Enum<V>` and `Text`, the last carrying the `widest` string the fit tests
   measure by. A reading (`fuelLitres(): Num`), a state (`tankIsLow(): Bool`, `alert(): Enum<AlertId>`)
   and a setting (`setting.positionMode(): Enum<'overall' | 'class'>`) are the three kinds, and
   `values.ts` is retyped in place before anything moves. A colour, a formatted string and a `DrawnFigure`
   are not signals and leave the core.

2. **The catalogue is data.** A page is a list of ranks of fields, each field an id, a label key, a
   signal, a kind that names its formatter, an optional unit signal and visibility, its place in the
   shedding order and its character budget. The twenty-two modules, band D's eight pages, zone A's four
   and the bar are all declared in that one shape; a page that is a drawing (inputs, radar, track, tyres,
   damage) declares its fields and hands its drawing over as one more thing a theme may take or replace.
   `FieldSpec` becomes something the house builds from a field, not something a module hands to a theme.

3. **The alert catalogue holds conditions and ranks them; what a condition looks like is a theme's
   table.** `id`, `name`, `bits` or `when`, `critical`, `motion`, `neutral` and `raisedRank` stay in the core
   and are read by every renderer, the matrix and the strips included; `band: AlertBandSpec` moves to a
   `Record<AlertId, …>` the house theme owns and every theme must fill. The pit family and the telltales
   are split the same way. A theme may then write "full course yellow" on a yellow ground, which it
   cannot today, since `restyle` is told "never the flag".

4. **Every hook of `ThemeDrawing` is required, and the house implements all of them** under
   `packages/dash/src/themes/house/` (or whatever name the author prefers to "default"). `faceItems`
   calls the theme with no `else`; the process-global module register and `withHouseLayout` are deleted;
   a theme that wants a house page imports the house's function for it. The default build stays
   byte-identical through every step, which is ADR 0015's rule and the review of each pull request.

5. **A theme computes from signals and never from properties.** `and(inTheCar(), lt(fuelLitres(), num(10)))`
   is a legitimate theme rule, and is the Porsche's alarm. `game(`, `raw(`, `prop(`, `computed(`,
   `driver(` and `changed(` do not appear under `themes/`, and a test says so. A theme that needs a
   reading the core lacks adds the signal to the core, in the pull request that needs it, exactly as a
   module does today.

6. **The plugin is untouched.** No property is added for the split, `DataUpdate` does not grow, and
   ADR 0009's gate stands. The settings contract is already the core's public interface and stays
   mirrored in both languages as it is.

7. **A theme still draws every field of every page.** ADR 0015's line between a theme and a skin holds:
   declining a size, a decoration (counters, ghosts, corners, the well) or the bar is a theme's right, and
   hiding a reading is not. `conformance.test.ts`'s "nothing disappears" becomes a check by field id
   against the catalogue rather than against the house's items.

## What was decided against

**A runtime core in the plugin**, publishing `OpenDash.Flag`, `OpenDash.FuelCaution` and the rest as
properties every renderer reads. It is the most literal reading of the author's description and the
shape most SimHub packages take. One could think that it gives the one answer every item would share.
In reality it gives two: ADR 0003's fallback means every published signal keeps an NCalc twin for the
frames on which the plugin publishes nothing, so the semantic layer would be duplicated in C# rather
than replaced; the plugin would enter the 60 Hz path for every reading rather than for six families;
and the pinned contract would grow by a name per signal on both sides. ADR 0009 refuses it, and the
case for the refusal is stronger here than where it was written.

**Keeping the hooks optional and adding the missing ones.** It is the cheapest path to a better
Porsche, and it keeps the house as the thing a theme is a deviation from. It does not meet the
requirement that the main theme be a theme, and it leaves the `ThemeDrawing` interface as what it
admits to being, "the parts the Porsche needed and no more". The next car would add its hooks.

**A runtime theme switch.** Rejected by ADR 0015 for three reasons that have not moved, and not
needed by anything here.

**Typing the signals by a runtime wrapper rather than a phantom type.** A `Signal` class carrying its
kind would let the generator check kinds at build time and would cost a change to every call site; the
branded string costs nothing at the call sites that are already right and lets the compiler find the
ones that are not. The class can come later if the brand proves too weak.

## What it costs

**A page is two files rather than one.** A module today is thirty readable lines that declare and draw;
as data plus a house drawing it is a catalogue entry, a shedding rank and a drawing, and a contributor
adding a reading touches all three. This is the same price the plugin already pays for the contract and
it is paid so that a theme can draw a reading without knowing the house.

**The work is large and must be staged.** The research sizes it at six steps, of which the catalogue as
data is the heart and the only one that needs a design decision before it is coded; the first three
(types, formatting out, catalogue split) can start now and each stands alone. The theme-as-a-value step,
which undoes the one-process-per-theme build and the `ds` globals, is optional and last.

**Snapshots and the byte-identical diff do the reviewing**, and both are only as good as the item names
and order being kept, which the stable GUIDs depend on anyway. A step that reorders items is a step that
has to say why in its commit message.

**Some helpers are harder to write without presentation.** `bandFuelQuantity`'s "whole from ten" rule and
`CLASS_AND_PLACE_WIDEST`'s measured widest are layout facts that live in `values.ts` because the fit
tests need them; they move to the house theme and the core keeps only the `widest` string of a `Text`.
Whether a `widest` is core data at all is answered yes here, since no theme can be measured without it.

## Unresolved

**The `Field` shape.** The sketch in the research names the fields a theme has asked for so far (the two
registers, the label tables, the shedding order). What a drawing page hands over, and whether a gauge is
a field with a `kind` or a drawing, is to be settled on the ticket before step four is written.

**The LED and matrix profiles as themes.** They already read the core's conditions and draw them their
own way, and the split treats them as renderers; whether they should implement a `ThemeDrawing` of their
own device, with the house glyph tables as its default, is a separate question and is not decided here.

**Whether a JavaScript binding can hold state between frames**, which #167 asked and which was closed
without an answer. Nothing here depends on it, but a typed core that could emit a JavaScript body for a
long derivation would want the validator to read one, which `leds/rpmStrip.ts` records it cannot.

**The theme as a value.** `ds`, `THEME_ID` and the module register are process globals, and the clean
form passes a theme context down. It is left out of the decision because the per-process build works
and nothing above needs it; it should be taken up when a theme first has to be drawn twice in one
process.
