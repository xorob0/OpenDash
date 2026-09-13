# Architecture

> **Two faces are built, and this describes the older one.** Since 0.2.0 the eight rectangular
> sizes are zone faces and carry the shipped names; what is described below is the twelve-slot
> path, which still builds and now produces the two round faces and the `openDash slots <size>`
> packages beside them. The zone model is the settled design
> ([ADR 0006](decisions/0006-the-zone-face.md), [design/zones.md](design/zones.md)): five parts,
> a catalogue and a wheel button per zone, and a page laid out for the shape of its box.
>
> This document is rewritten when the card path is retired (XOR-95), not before, so that it keeps
> describing the code rather than the intention. Read every mention of a slot below as belonging to
> that path: `packages/dash/src/zones/` is the other one, and `design/zones.md` describes it.
>
> The second screens are already on the zone model and their half of this document is current.

## The pipeline

```
design/tokens.json ───────────────┐
packages/dash/src/cards/*.ts ─────┤  one module per card: readouts, labels, colour rules
packages/dash/src/hero/*.ts ──────┤  gear, RPM bar, flags, pit limiter
packages/dash/src/layouts/*.ts ───┤  slot geometry and slot count for one BaseWidth x BaseHeight
packages/dash/src/zones/*.ts ─────┤  the five parts of a zone face, and the catalogue behind each zone
packages/dash/src/modules/*.ts ───┤  one module per companion page, which is also a pit wall zone page
packages/dash/src/screens/*.ts ───┤  the companion and pit wall packages built from those modules
packages/dash/fonts/*.ttf ────────┤
                                  v
                        packages/generator
              typed node model, binding helpers, validator, serialiser
                                  v
                  build/openDash/openDash.djson        main dashboard
                  build/openDash/cards.djson           one screen per card
                  build/openDash/*.djson.metadata
                  build/openDash/_SHFonts/
                  build/openDash Companion/            21 screens, one per module
                  build/openDash Pit wall/             3 pages and their zone dashboards
                                  v
                  zip  -->  build/openDash.simhubdash
                                  |
                 +----------------+----------------+
                 v                                 v
          GitHub release                 embedded into plugin/  -->  OpenDash.dll
                                                   v
              SimHub renders the dashboard; the plugin's properties select what it shows
```

Contributors write and review TypeScript and C#. The `.djson`, the `.simhubdash` and the plugin
binary are build output and are never committed.

## Why this shape

Three properties fall out of it, and none of them is available to a dashboard drawn in
DashStudio.

The output is **diffable**. A pull request shows a TypeScript change rather than a JSON blob of
several hundred kilobytes, and a reviewer can read what changed before installing the artifact
that CI attached to the run in order to see it.

Sizes reuse everything but the layout. A card is a function that returns items, a layout is a
function of the target dimensions that places cards in slots, and a new screen size is one new
layout file. The design decision per aspect ratio remains, but the cards, the bindings, the
plugin contract and the tests do not move. Competitors maintain each size as a separate
dashboard and repeat every feature change in each of them.

Design has one source of truth. `tokens.json` feeds the generator and the plugin panel, and the
design canvas is derived from it, so a colour exists in exactly one place.

## Components

### Generator (`packages/generator`)

A TypeScript library with no knowledge of openDash, which could serve any SimHub dashboard
project. It provides a typed model of the node types the MVP needs (`TextItem`,
`RectangleItem`, `Layer`, `WidgetItem`, and the screen and dashboard envelopes), helpers that
build NCalc and JavaScript binding objects from typed inputs, a validator, and a serialiser
that writes the JSON shape SimHub 9.x exports: `$type` as the first key of every item, no `$id`
references, colours as `#AARRGGBB`, and only the properties that differ from SimHub's defaults.
It also writes the `.metadata` sidecar and copies fonts into `_SHFonts/`.

The generator emits absolute positions only. SimHub does have a stacking container,
`GroupItem` with `ChildsPositioning`, but resolving layout in the generator keeps the output
deterministic and snapshot-testable, and it keeps the MVP to the smallest set of node types.

Every item `Id` is a GUID derived from the item's path (layout, slot, card, element) with a
hash, so that a rebuild does not churn identifiers.

### Dashboard (`packages/dash`)

openDash itself. `src/cards/` holds one module per card, each exporting a function that takes
a slot rectangle and returns items; `src/hero/` holds the fixed elements;
`src/layouts/1920x480.ts` declares the slot geometry and the slot count; `src/build.ts`
composes them into two documents, the main dashboard and the cards widget, and hands them to
the generator. `fonts/` holds the Barlow files that are redistributed. Snapshot tests live next
to the source.

### Plugin (`plugin/`)

A .NET Framework 4.8 class library named `OpenDash`, implementing `IPlugin` and
`IWPFSettingsV2`. It is code-only WPF and builds with the .NET SDK on any platform, which is
[ADR 0005](decisions/0005-plugin-builds-on-linux.md). On `Init` it reads its settings, compares the version of the embedded
dashboard with the one installed under `DashTemplates/openDash/`, extracts the embedded package
when the installed one is missing or older, and attaches one property per setting. The settings
panel is a WPF control built from SimHub's own styles. The plugin renders nothing and does not
implement `DataUpdate`. Details in
[research/simhub-plugin-sdk.md](research/simhub-plugin-sdk.md).

### Design (`design/`)

`tokens.json` is the source of truth for colour, type and spacing. `canvas/` holds the design
system artboards made with Claude Design, derived from the tokens; they are the reference for
the layout and are not consumed by the build.

## How a setting reaches the dashboard

A SimHub plugin cannot edit a dashboard that is being displayed, and SimHub offers no API to
reload one. Settings therefore never regenerate anything. Instead the plugin attaches each
setting as a property, `OpenDash.DeltaReference` for instance, and the generator emits bindings
that read the property and fall back to the default when the plugin is absent:

```
if(isnull([OpenDash.DeltaReference], 'session') = 'alltime',
   [PersistantTrackerPlugin.AllTimeBestLiveDeltaSeconds],
   [PersistantTrackerPlugin.SessionBestLiveDeltaSeconds])
```

Slots use SimHub's widget mechanism, which is how the commercial packages do it, and the
runtime screen switch was verified on SimHub 9.12.6 during the spike. The cards live
in a second file, `cards.djson`, with one screen per card; the main dashboard contains one
`WidgetItem` per slot pointing at that file, with `InitialScreenIndex` bound to the slot's
property. Changing the property changes the screen the widget shows, and the card is defined
once whatever the number of slots. The spike confirms that the screen switches at runtime; if
it does not, the fallback is to emit every card in every slot with `Visible` bindings, which is
known to work but multiplies the item count by the number of cards.

This is [ADR 0003](decisions/0003-plugin-settings-through-properties.md).

## Constraints this imposes

The pipeline is one-way. Edits made in DashStudio do not flow back to source and will be
destroyed on the next build, and the copy installed by the plugin is overwritten on the next
plugin update. DashStudio is for previewing and inspecting, never for authoring, and this must
be stated loudly in CONTRIBUTING when that file exists.

Layout is absolute. Every position is a pixel value resolved by the generator, and any
alignment or distribution logic lives in TypeScript.

Fonts are redistributed. SimHub bundles fonts into `_SHFonts/` inside the package, so shipping
the dashboard means shipping the font files, and every font must be under the OFL, MIT or a
similar licence. This is why the tokens specify Barlow and Barlow Condensed.

The format is undocumented. `$type` strings are internal SimHub class names, and the property
sets were established by reading real exports rather than a specification. The SimHub version
the output was tested against is recorded in the `.metadata` sidecar and in the README, and
every SimHub update is an occasion to re-run the spike checklist.

## Testing

Beyond the automated tests, `tools/irsdk-emulator` feeds scripted iRacing telemetry into
SimHub on the Windows VM through the same shared memory the sim uses, so that every card can
be checked with real values and screenshots without iRacing being installed. See
[testing-vm.md](testing-vm.md).

The generator is tested with snapshot tests: each card, the hero zone and the full 1920 by 480
build are serialised and compared to committed snapshots, so that a pull request shows the JSON
consequence of a TypeScript change. A golden file saved from DashStudio during the spike is
committed under `docs/research/samples/`, and a test asserts that the generator reproduces it
from the equivalent TypeScript. The validator checks colours, unique identifiers, screen and
slot counts, and that every `OpenDash` property read in a binding is declared in the settings
contract. The plugin has unit tests for settings serialisation and for the version comparison;
the rest of it is verified by hand on the Windows VM.

## Continuous integration and releases

One workflow with two jobs runs on every pull request, both on Linux. The dash job installs
with Bun, runs the tests, builds the `.simhubdash` and uploads it as an artifact. The plugin job
downloads that artifact into the plugin's resources, runs the plugin tests, builds the plugin
with the .NET SDK against the assemblies committed in `plugin/lib/` and uploads the DLL. Neither can run
SimHub, so visual review remains a human step: the reviewer installs the artifact on a SimHub
machine, or the author attaches a screenshot from the VM.

On a tag, a release workflow attaches `openDash.simhubdash` and `OpenDash-plugin.zip` to the
GitHub release. The dashboard version in the `.metadata` sidecar, the plugin assembly version
and the tag are the same string.

## Repository layout

```
design/
  tokens.json            source of truth for colour, type and spacing
  canvas/                design system artboards (Claude Design), derived from the tokens
packages/
  generator/             SimHub .djson emitter: node model, bindings, validator, serialiser
  dash/                  openDash: cards, hero, layouts, fonts, build script, snapshots
plugin/
  OpenDash/              C# project: settings, properties, installer, WPF panel
  OpenDash.Tests/        unit tests for the plugin's pure logic
  lib/                   SimHub reference assemblies, committed for CI
tools/
  irsdk-emulator/        synthetic iRacing telemetry for the test VM
docs/
  scope.md               what openDash is, and what is deliberately not built
  scope-mvp.md           the MVP contract, closed and superseded by scope.md
  architecture.md        this document
  decisions/             architecture decision records
  research/              format notes, SDK notes, competitor analysis, golden samples
  design/                brand and visual direction
```
