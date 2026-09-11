# Working on openDash

Notes for an AI assistant working in this repository. Read
[CONTRIBUTING.md](CONTRIBUTING.md) for the engineering conventions; this file is about how to
work here.

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

```bash
bun run dev                      # cold VM to a dash rendering live telemetry, ~2.5 min
bun run dev 'openDash Pit wall' --scenario notc
```

[docs/dev-loop.md](docs/dev-loop.md) is the whole loop on one page and is the place to start. It
covers `bun run vm` for the VM and SimHub, `bun run emulator` for the telemetry, why opening a
dashboard has to be clicked, and the four traps that fail silently: session 0 has no desktop,
SimHub reads its template list once at startup, Bun does not deliver signals to a handler, and
GDI+ will not write to the share. [docs/testing-vm.md](docs/testing-vm.md) describes the VM itself
and is what to read when something in it breaks.

To look at a whole package quickly, open it in Dash Studio's editor and drag the Overview
panel's splitter up: the Overview draws every screen as a live thumbnail, which is the fastest
way to see twenty-one modules or four pit wall pages at once.

## Design

`design/` is the design source and is not edited from code. When the build and the canvas
disagree, say so rather than quietly changing either: the canvas is a Claude Design artifact the
author owns. `design/tokens.json` is the only place a colour, a size or a spacing value is
defined.
