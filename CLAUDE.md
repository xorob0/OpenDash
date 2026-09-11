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
measures every text of every layout against its box using the advances in
`packages/dash/src/design/advances.ts`, which are read from the bundled fonts; keep it passing
rather than adjusting it.

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

## Design

`design/` is the design source and is not edited from code. When the build and the canvas
disagree, say so rather than quietly changing either: the canvas is a Claude Design artifact the
author owns. `design/tokens.json` is the only place a colour, a size or a spacing value is
defined.
