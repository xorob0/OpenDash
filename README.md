# openDash

An open-source sim racing dashboard for [SimHub](https://www.simhubdash.com/), released under
the MIT licence.

> **Status: alpha.** Fourteen packages are built: ten dash faces from 1920 by 480 down to a
> 480 px round DDU, two companion screens and two pit wall screens, plus a SimHub plugin that
> installs them all and exposes their settings. Everything below has been verified on
> SimHub 9.12.6. See [docs/scope-mvp.md](docs/scope-mvp.md) for what version 1 of the face is,
> and [docs/second-screens.md](docs/second-screens.md) for the companion and the pit wall.

## What makes this different

Existing SimHub dashboards ship as `.simhubdash` binaries. One cannot diff them, one cannot
review a pull request against them, and every screen size is hand-maintained as a separate
copy.

openDash treats the dashboard as compiled output. The source of truth is TypeScript together
with a set of design tokens; a generator emits the `.djson` scene graph that SimHub renders and
packs it into a `.simhubdash`. A small SimHub plugin installs that package and exposes a
handful of settings, so that the user can choose which card sits in which slot without touching
the dashboard itself.

That buys three things. Contributors can actually contribute, because a pull request is a
TypeScript diff rather than an opaque blob. A feature change reaches every screen size at once,
because cards are shared components and a size is only a layout that arranges them. Finally,
one set of design tokens drives the dashboard and the plugin panel, so colours cannot drift
between them.

Because rendering stays native to SimHub, everything SimHub already does well is kept: HDMI
DDUs, Vocore and USBD480 USB screens, phones and tablets, and the seventeen or so sims it
reads.

## Install

Each release carries one `.simhubdash` per screen size and one `OpenDash-plugin.zip`.

- **Dashboard only.** Double-click the `.simhubdash` for your screen; SimHub imports it. You get
  the default layout and the default modes, and no settings page.
- **Dashboard and plugin.** Unzip `OpenDash-plugin.zip`, copy `OpenDash.dll` into SimHub's
  install folder, unblock it, start SimHub and accept the new plugin. The plugin installs the
  dashboard for you and adds an "openDash" page to SimHub's left menu. The full procedure is
  in [plugin/INSTALL.md](plugin/INSTALL.md).

In both cases openDash is a normal SimHub dashboard afterwards: assign it to a display from
Dash Studio like any other.

## The dashboard

The face is a fixed hero zone and twelve equal slots. The hero holds what a driver reads by
reflex: the gear, a fifteen segment rev bar with SimHub's per-car shift lights, the
flag strip and the pit limiter. Every other field is a card, and any card can be placed in any
slot from the plugin: speed, current, last and best lap, delta, position, session progress,
fuel, fuel laps, TC, ABS, tyre temperatures and tyre pressures.

The plugin settings are SimHub properties (`OpenDash.ShiftLights`, `OpenDash.PositionMode`,
`OpenDash.DeltaReference`, `OpenDash.SessionProgress`, `OpenDash.Slot01` to `Slot12`), so
other dashboards and LED profiles can read them too. Every change applies to the running
dashboard immediately.

## The second screens

Two more kinds of screen are built from the same modules and installed by the same plugin.

The **companion** is a phone or tablet beside the wheel showing one module at a time: lap times,
delta, sectors, speedo, fuel, tyres, pit view, car settings, inputs, session, radar, track,
leaderboard, relative, opponents, gear, stint and lap history. Twenty-one modules, each with its
own switch in the plugin, paged with a wheel button through SimHub's own screen navigation.

The **pit wall** is a 1920 by 1080 screen for someone who is not driving: three pages carrying
the whole field with gaps, intervals, sectors, stints and stops, the driver's own lap next to it,
and four data zones whose contents are plugin settings. A portrait version covers a screen on its
side.

Three modules ship off because iRacing publishes none of their data, and they say so rather than
drawing zeros. [docs/second-screens.md](docs/second-screens.md) lists every such case and why.

## Build from source

Requirements: [Bun](https://bun.sh) 1.x for the dashboard and the .NET 8 SDK for the plugin.
No Windows machine is needed to build either.

```bash
bun install
bun run check          # typecheck and tests
bun run build          # build/openDash.simhubdash, build/openDash/ and build/manifest.json
```

```bash
cp build/*.simhubdash plugin/OpenDash/Resources/
dotnet test plugin/OpenDash.Tests
dotnet build plugin/OpenDash -c Release   # plugin/OpenDash/bin/Release/net48/OpenDash.dll
```

The generated `.djson` is never committed and never edited by hand. DashStudio is for looking
at the result, not for authoring it: anything changed there is overwritten by the next build.

## Repository layout

```
design/
  tokens.json          Design tokens, source of truth for all colour, type and spacing
  canvas/              Design system canvas artboards (Claude Design), derived from the tokens
packages/
  generator/           TypeScript library that emits SimHub .djson scene graphs (no openDash knowledge)
  dash/                openDash itself: tokens in code, elements, components, cards, hero, layouts, build
    src/second/        the shared second-screen parts: fields, chips, gauges, traces, tables
    src/modules/       the 21 companion modules, which are also the pit wall's zone pages
    src/screens/       the companion and pit wall packages
plugin/
  OpenDash/            C# SimHub plugin: installer, properties, settings panel (builds on Linux)
  OpenDash.Tests/      Unit tests for the plugin's pure logic
  lib/                 SimHub reference assemblies, committed so CI can build without SimHub
tools/
  irsdk-emulator/      Synthetic iRacing telemetry feed for testing dashboards without the sim
docs/
  scope-mvp.md         What version 1 is, and what it is not
  second-screens.md    The companion and the pit wall, and what they deliberately do not show
  architecture.md      How source becomes a .simhubdash, and how a setting reaches it
  decisions/           Architecture decision records
  research/            Format notes verified against SimHub 9.12.6, SDK notes, competitor analysis
  design/              Brand and visual direction
  testing-vm.md        The Windows VM that runs SimHub for tests
```

## Testing

`bun test` covers the generator (serialisation, validation, packaging) and the dashboard
(tokens, geometry, formulas, the settings contract, snapshots of every card and of the full
face, and every text of every package measured against the box SimHub clips it to). `dotnet test` covers the plugin's version comparison, settings and card catalogue. On
top of that the dashboard and the plugin are checked by hand on a Windows VM running SimHub,
where [tools/irsdk-emulator](tools/irsdk-emulator/README.md) feeds scripted iRacing telemetry
so that every card can be seen with real values.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md). The short version: change the TypeScript, run
`bun run check`, look at the snapshot diff, and open a pull request. CI builds both artifacts
for every pull request so a reviewer can install them.

## Prior art and credit

This project exists because others documented the path first.

- [Blumlaut/simhub-dashes](https://github.com/Blumlaut/simhub-dashes) proved that a
  `.simhubdash` is a zip, and that CI can build releases from source in git.
- [DahlDesignDash](https://github.com/andreasdahl1987/DahlDesignDash) runs Prettier over
  `.djson` in CI to keep diffs readable, and shows how a plugin's properties drive a dashboard.
- [Lovely Dashboard](https://github.com/Lovely-Sim-Racing/lovely-dashboard) is the reference
  for what a mature SimHub dash ecosystem looks like.

Lovely's licence explicitly forbids reuse of its UI design. openDash's visual design is
independently derived: do not copy Lovely layouts, and do not use its screenshots in any
openDash material.

Barlow and Barlow Condensed are redistributed under the SIL Open Font Licence 1.1; see
`packages/dash/fonts/OFL.txt`.

## Licence

MIT, see [LICENSE](LICENSE).
