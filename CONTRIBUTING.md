# Contributing to OpenDash

Thank you for looking at this. The whole point of OpenDash is that a dashboard change is a
readable pull request, so most of this file is about keeping that true.

## The one rule

**Never author in DashStudio.** The `.djson` files are build output, gitignored, and rewritten
on every build; the copy the plugin installs is replaced on the next plugin update. DashStudio
is for inspecting the result and for finding out how SimHub behaves. If you change something
there and like it, bring the change back into the TypeScript.

## Setting up

```bash
bun install
bun run check      # typecheck + tests, must pass before a pull request
bun run build      # writes build/OpenDash.simhubdash
```

For the plugin you need the .NET 8 SDK. It builds on Linux, macOS and Windows:

```bash
cp build/*.simhubdash plugin/OpenDash/Resources/
dotnet test plugin/OpenDash.Tests
dotnet build plugin/OpenDash -c Release
```

Note that `bun.lock` is written at lockfile version 2, which a bun older than the one that wrote it
cannot read: such a bun reports "Unknown lockfile version", ignores the file and rewrites it. If your
`bun install` keeps rewriting the lockfile, that is why, and the remedy is to upgrade bun rather than
to commit the rewritten file. CI deliberately does not pin bun for this reason.

`global.json` pins that 8, and it is there for a reason rather than out of caution. A newer compiler
resolves `array.Reverse()` to the span overload in `System.MemoryExtensions`, which reverses in place
and returns void, where the .NET 8 compiler resolves it to LINQ. The result is code that compiles on
one machine and not on the other, which has cost this repository three separate fixes. With the pin,
a local build and the runner read the same source the same way. If you have only a newer SDK, `dotnet`
will say so plainly instead of compiling something different; install 8 rather than raising the pin.

There is also a pre-commit hook, which is not installed by default:

```bash
git config core.hooksPath .githooks
```

It refuses two things. Anything under `design/` it refuses outright, because the canvas is the design
source and is not edited from code. The author's drafts it refuses only while a draft is actually
open somewhere, which it works out by looking in every worktree and in the stash rather than by
consulting a list, so a file nobody is holding commits normally. When it does refuse, it says which
worktree holds the other copy, and `OPENDASH_COMMIT_ANYWAY=1` is the way past it when you are certain
that copy is stale.

## The face is being rebuilt

The list below describes the code as it is: cards, slots and a rung. The settled design is the
**zone model** — five parts, a catalogue and a wheel button per zone, and a page laid out for the
shape of its box rather than scaled to it. [docs/design/zones.md](docs/design/zones.md) is the
specification and [ADR 0006](docs/decisions/0006-the-zone-face.md) is why.

If you are adding something to the face, read those two first: a new card is work that is about to
be deleted, and a new **page** under `packages/dash/src/modules/` reaches the face, the companion
and the pit wall at once.

## Where things live

- `design/tokens.json` is the only place a colour, a font size or a spacing value is defined.
  `packages/dash/src/tokens.ts` reads it and exposes the typed `ds` object. Do not write hex
  values anywhere else; a test fails if the brand cyan reaches the dash face.
- `packages/dash/src/elements` are the atoms (label, numeral, rule, segment, band). They know
  the font metrics and turn a canvas line box into a SimHub text box.
- `packages/dash/src/components` are readouts, grids, the rev bar, the flag strip, the pit
  limiter and the gear and speed block.
- `packages/dash/src/cards` hold one module per card. A card is a function from a slot
  rectangle and a rung (L, M or S) to items. A card must fit every slot of every layout; a card
  that needs more room is two cards.
- `packages/dash/src/hero` composes the fixed hero zone from a geometry the layout provides.
- `packages/dash/src/layouts` hold one file per screen size. A layout declares its slot
  rectangles, its slot count and its hero geometry, and nothing else.
- `packages/dash/src/contract.ts` is the settings contract shared with the plugin. Every read of
  an `OpenDash.*` property goes through it, and the validator rejects a build that reads a
  property the contract does not declare.
- `packages/generator` knows nothing about OpenDash. It is the typed model of SimHub's scene
  graph, the NCalc helpers, the serialiser, the validator and the package writer, and it could
  serve any other dashboard project.
- `plugin/OpenDash` is the C# plugin. `Contract.cs` mirrors `contract.ts` and a test keeps the
  two card catalogues identical.
- `traces/` holds one recorded telemetry trace per emulator scenario, taken from a real SimHub on
  the Windows VM and committed so that everything downstream replays a file rather than needing a
  VM. A package that starts reading a property no trace carries fails `bun run check` and is
  answered with `bun run record <scenario>`; see [traces/README.md](traces/README.md).

## Before you start

[docs/scope.md](docs/scope.md) says what OpenDash is, what ships, and what is deliberately not
built. The last of those is a list of refusals rather than a backlog, so a change that falls
under one of its lines will be declined however well it is written. Should you believe a line
ought to move, argue that first, in an issue or a decision record, and the code afterwards.

## When a change is done

[docs/scope.md](docs/scope.md) ends with the definition of done, and it is the one that counts:
`bun run check` passes, `dotnet test plugin/OpenDash.Tests` passes if the plugin changed, the
snapshot diff has been **read** rather than merely refreshed, and whatever the change draws has
been seen on the Windows VM in real SimHub.

The last condition is the one that catches what the tests cannot. WPF clips silently, and a box
measured from the wrong face or from a sample narrower than the runtime value loses glyphs
without failing anything. `bun run dev` puts one package on the VM with live telemetry and
photographs it; `bun run shots` does the same for several at once. If you cannot run SimHub, say
so in the pull request rather than leaving it unsaid.

Which packages to photograph is not a matter of judgement, because a card is shared and an edit to
one of them reaches every face it appears on. `bun run affected` builds the branch and the commit
it forked from, compares the packages the two builds wrote, and prints the `bun run shots` command
that captures exactly those. CI runs the same comparison on every pull request and keeps the answer
in one comment, so a reviewer sees the reach of a change without running anything.

The same list answers a second question. `packages/dash/previews/<folder>.png` is the thumbnail
SimHub's dashboard list draws for a package, and it is a photograph of the build it was taken from,
so a face that has been redrawn goes on showing the old one. If you have the VM, refresh the
affected ones with `bun run previews --packages '<the same list>'` and say in the commit message
which faces moved. If you do not, the pictures stay as they are and a maintainer retakes them; the
build says `warning preview/missing` for a package that has none, and nothing fails.

The pull request template asks for exactly this and nothing else.

## Making a change

1. Change the TypeScript (or the C#).
2. Run `bun run check`. The snapshot tests will show the JSON consequence of your change; read
   the diff and make sure it is what you meant. If it is, refresh the snapshots with
   `bun test --update-snapshots` and commit the snapshot file with your change.
3. Build and look at the result in SimHub. CI attaches both artifacts to every pull request, so
   a reviewer can do the same. If you cannot run SimHub, say so in the pull request.
4. Keep formulas in the NCalc helpers (`packages/generator/src/ncalc.ts`) rather than as raw
   strings, and keep telemetry property names in one place per card.

## Adding a page

Add a module under `packages/dash/src/modules`, register it in `modules/index.ts` with the next
number, add its name to the catalogue in `contract.ts`, and cover it in `secondScreens.test.ts`.
The number is the value a zone setting takes, so numbers are never reused or reordered.

A page has to fit every shape it can be given: `wide`, `grid`, `tall narrow` and `tall`, plus the
pit wall's strips. It sheds its secondary rows before it shrinks its numerals, and it never scales.
The test builds every module into every box the build actually produces, so a page that does not
fit fails rather than overlapping its neighbour.

**Declare what it sheds.** Which fields a page keeps at each of the four shapes is a design
decision read off the catalogue artboard, not something the code works out from widths: add an
entry to `packages/dash/src/modules/shedding.ts` and the same row to the table in
[docs/design/zones.md](docs/design/zones.md). A page with nothing to shed — a drawing, or a page
that says it has no data — says so and why. `shedding.test.ts` fails on a page with no entry, on a
row the code and the document disagree about, and on an id no longer drawn.

A field that can be **missing** rather than shed is a different question: a value the sim does not
publish is removed and its rank closes over the hole, a telltale that is unlit keeps its place and
goes dim. Both are modes of `packages/dash/src/second/rank.ts`; §11 of the zones document says
which to reach for.

**Adding a card is not a thing to do any more.** `packages/dash/src/cards` is the slot model and
is retired once every face is drawn from zones.

## Adding a screen size

Add one file under `packages/dash/src/layouts` and register it in `layouts/index.ts`. Each aspect
ratio still needs a design decision, which is why the canvas under `design/canvas` draws every
planned size first, and why [docs/design/zones.md](docs/design/zones.md) carries every rectangle
of every face read off those artboards rather than rounded.

A rectangular face declares the rects of its five parts and nothing else. What each zone shows is
the plugin's business, and the pages themselves are written once and answer to whatever box they
are given.

## Verified SimHub behaviour

Before assuming how SimHub reads a property or renders an item, check
[docs/research/simhub-dash-format.md](docs/research/simhub-dash-format.md), which records what
was verified by decompiling SimHub 9.12.6 and by loading generated packages on the test VM.
When a SimHub update changes something, that document is where the change is recorded.

## Style

TypeScript strict, ES modules, small pure functions, a short comment at the top of every file
saying what it is for. Prose in docs and comments uses plain sentences without em-dashes.
Commit messages say what changed and why.

Text a user reads is held to [docs/design/voice.md](docs/design/voice.md), which is a different
register from this one: a caption says what a control does and stops, and the reasoning behind the
control belongs in a comment or a decision record instead.

## Licence

By contributing you agree that your contribution is licensed under the MIT licence of this
repository.

Anything redistributed inside a package must be under the OFL, MIT, Apache 2.0 or an equally
permissive licence, and its licence and notice must travel with the package. That is fonts today
and pictograms shortly: a `.simhubdash` is a redistribution exactly as a font is, and the licence
has to be in the zip rather than only in this repository.
