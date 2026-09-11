# Working on openDash

Notes for an AI assistant working in this repository. Read [docs/scope.md](docs/scope.md) for
what openDash is and what it refuses to be, and [CONTRIBUTING.md](CONTRIBUTING.md) for the
engineering conventions; this file is about how to work here. Note that
[docs/scope-mvp.md](docs/scope-mvp.md) is closed: it describes the MVP and several of its
refusals have since been reversed, so do not take its scope lines as current.

## Commit as you go

Commit whenever a piece of work stands on its own, without being asked, and push to `main`.
A good commit is one green checkpoint: the tree passes `bun run check` and, when the plugin
changed, `dotnet test plugin/OpenDash.Tests`. Do not wait for the end of a long task, and do not
batch unrelated changes into one commit; if a change turns out to be wrong, a reader should be
able to revert exactly it.

Never commit a red tree. If something has to be left broken, say so in the message.

## The checks

```bash
bun run check                                   # typecheck and tests
bun run build                                   # every .simhubdash into build/
bun run package                                 # build + plugin with the packages embedded
dotnet test plugin/OpenDash.Tests               # plugin unit tests
```

Snapshots are meant to be read. When one changes, look at the diff before refreshing it with
`bun test --update-snapshots` and say in the commit message what moved and why.

## What the renderer will not forgive

SimHub hands every text box to WPF as `MaxTextWidth` and `MaxTextHeight`, and WPF silently clips
whatever does not fit. A box sized to the exact text loses its last glyph. `packages/dash/test/textFit.test.ts`
and `packages/dash/test/secondScreens.test.ts` measure every text of every package against its
box using the advances in `packages/dash/src/design/advances.ts`, which are read from the bundled
fonts; keep them passing rather than adjusting them. Measure a run in the weight it is drawn in:
Barlow Condensed Bold is wider than SemiBold, and a box measured from the wrong face clips.

The second screens add a second rule of the same kind. A module is a function of a rectangle, so
it has to fit whatever rectangle it is given: `secondScreens.test.ts` builds every module into
the seven box shapes the packages use and checks every item against the frame. When a module
cannot fit, it must shrink or drop a field, never draw outside; `fitFields` and `fieldsThatFit`
in `packages/dash/src/second/field.ts` are how.

The format facts that are easy to get wrong live in
[docs/research/simhub-dash-format.md](docs/research/simhub-dash-format.md). They were verified
by decompiling SimHub 9.12.6 and by loading generated packages on the test VM. Check there
before guessing at a property name or a default.

## Testing on the VM

A Windows VM with SimHub runs beside this repository and is reachable through the `winvm` MCP
tools; [docs/testing-vm.md](docs/testing-vm.md) explains it and
[tools/irsdk-emulator](tools/irsdk-emulator/README.md) feeds it scripted iRacing telemetry so
cards can be seen with real values. Two things to know: the emulator must be started in the
interactive desktop session, and only one copy may run at a time, since two writers to the
shared memory make SimHub drop the connection.

The VM's SSH can stop answering while the GUI keeps working, which takes `run_powershell`,
`run_in_desktop` and the `simhub_*` helpers with it. Everything can still be done over VNC:
`/opt/winvm/shared` is the Windows desktop's `Shared` folder, so a script left there can be
double-clicked from Explorer. `shared/opendash/deploy.bat` installs every package in that folder
into `DashTemplates`, copies the fonts and restarts SimHub. Note that `Z:` exists only in the
interactive session and the SSH session sees the share as `\\host.lan\Data`, so a script that
has to work in both should use `$PSScriptRoot`.

To look at a whole package quickly, open it in Dash Studio's editor and drag the Overview
panel's splitter up: the Overview draws every screen as a live thumbnail, which is the fastest
way to see twenty-one modules or four pit wall pages at once.

## Design

`design/` is the design source and is not edited from code. When the build and the canvas
disagree, say so rather than quietly changing either: the canvas is a Claude Design artifact the
author owns. `design/tokens.json` is the only place a colour, a size or a spacing value is
defined.
