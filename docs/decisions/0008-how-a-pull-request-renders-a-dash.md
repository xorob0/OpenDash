# ADR 0008: A pull request carries a native SimHub capture, and we do not build a preview renderer

**Date:** 2026-09-13
**Status:** Accepted. Reverses the recommendation XOR-18 was opened with, and is the record the
Visual verification project was blocked on; the rest of that project is a consequence of this
record rather than work that follows it.

## Context

A reviewer cannot see what a pull request changed. The diff is TypeScript, by
[ADR 0002](0002-djson-generated-from-source.md), and the thing the diff describes is a dashboard
rendered by somebody else's engine, by [ADR 0001](0001-simhub-native-rendering.md). Between the
two, a change to a card can reach every face in the repository and still read as four modified
lines.

The obvious answer is to render each pull request in CI, where it would be parallel and would
never queue. That answer is not available: SimHub's own web renderer is served by SimHub itself,
on a Windows machine, and a Linux runner has nothing to render with.
[ADR 0005](0005-plugin-builds-on-linux.md) deliberately removed Windows from the build, so there
is no runner to put SimHub on.

XOR-18 was opened with two ways out and a recommendation for the first of them. It was amended,
on the day it was opened, with a third. This record picks the third, which is not what the ticket
recommended, and the reasons are below.

**A. Our own preview renderer.** A TypeScript renderer for the scene graph the generator emits,
fed by a recorded telemetry trace and run in headless Chrome on an ordinary Linux runner. It is
deterministic, parallel, free per run, and able to draw any package at any frame.

**B. A self-hosted runner on the VPS**, driving SimHub's web renderer on the Windows VM.

**C. The author runs one command against the VM and attaches the result.**

## Investigation

The three options were priced against what is in the repository today rather than against what was
true when the ticket was written, because one of them has been built in the meantime.

**Option C is already merged and is already the house rule.** `bun run shots` claims the VM once
for a whole batch, installs every requested package in a single pass so that SimHub restarts once,
restarts the emulator once per scenario rather than once per capture, and writes
`build/shots/<nn>-<package>-<scenario>.png`. It captures with `PrintWindow`, so what lands in the
file is the dash window at its own size rather than a crop of a desktop. That was XOR-112, closed
on 2026-09-11, which the amendment to XOR-18 priced at two points and which therefore now costs
nothing further. Moreover it is already the standard of review: `architecture.md` has described
visual review as a human step in which the author attaches a capture from the VM since 2026-09-10,
and `CONTRIBUTING.md` names `bun run shots` among the conditions a change must meet before it is
merged. Option C is not a proposal; it is a description.

**Option A is thirteen points before it draws a frame and sixteen before that frame can be
believed, and its largest piece is a second implementation of somebody else's expression
language.** The preview renderer needs a trace to replay (XOR-19, three points), an NCalc evaluator
in TypeScript (XOR-20, five points) and the renderer itself (XOR-21, five points); the fidelity
check that polices the gap between the two renderers (XOR-24, three points) is what makes the
output worth looking at, and is therefore not optional. Only then does the video job (XOR-22) have
something to record.

The evaluator is the part worth dwelling on, because nothing in the repository evaluates a binding
today: `packages/generator/src/ncalc.ts` builds expression strings and
`packages/generator/src/ncalcFunctions.ts` checks the names and arities they use, and neither one
computes a value. A preview renderer has to compute values, which means reproducing `isnull`,
`if`, `format` with .NET format strings, `toshorttime`, `blink` and the rest, to SimHub's
semantics rather than to a reasonable reading of them.

**A divergence between our semantics and SimHub's is silent, and it has already happened once.**
`left([Class], 4)` shipped. The expression was well formed, the item was present, the geometry was
right and every test passed; SimHub's `left` takes three arguments, so the dispatch never matched,
no delegate was attached, and both leaderboards drew an empty block for months. That failure is
the entire risk of option A in miniature, and it occurred in the static case that the generator
can check by reading the string, which is the easy half. The header of `ncalcFunctions.ts` records
it, and the arity table exists because of it.

**The surface a preview renderer must cover is wider than "a deliberately small set".**
`packages/generator/src/model.ts` defines eleven item kinds, of which ten are constructed by the
dash package today: text, rectangle, ellipse, layer, widget, chart, linear gauge, radar, static map
and web page. It also defines twenty-six bindable targets, so a bound item can move, resize,
recolour, change its font size, blink or vanish, and each of those is a property the preview has
to honour in the same way SimHub does. The optimistic reading in the ticket holds for the text
case, where `design/advances.ts` carries advance widths read from the bundled fonts and `textFit`
already proves the boxes; it does not hold for the rest.

**Option B does not buy native fidelity, which is the only thing it could be bought for.** SimHub's
web renderer is not the native renderer a DDU uses, so B accepts a rendering model that is merely
closer rather than correct. It pays for that with a serialised queue on the VPS, which has two
virtual CPUs and four gigabytes of memory and is already hosting the Windows VM that would be doing
the rendering. Every pull request would wait for every earlier pull request, on a machine that also
has to be up.

**Pixel comparison is the weaker of the two available regression tests, not the stronger.** The
scene graph is absolute-positioned and is fully known at build time, so the failures a golden image
would catch are directly assertable instead: `layouts.test.ts` and `zoneFace.test.ts` assert with
`overlaps` that no slot, rule or hero box intersects another, `textFit.test.ts` measures every text
of every layout against its box, and `secondScreens.test.ts` builds every module into the seven box
shapes the packages use and checks every item against its frame. An assertion of that kind names
the item that moved and the box it left; a golden image reports that some bytes differ and leaves
the reader to find out which. The 1.6 megabyte snapshot file covers the rest of the JSON.

## Decision

**A pull request carries native SimHub captures, taken by the author with `bun run shots` and
attached to the pull request. openDash does not build a preview renderer.**

The captures come from the real engine on the real Windows build, at the dashboard's own size, with
the emulator replaying one of the pinned scenarios so that two captures of the same package show
the same state. There is no second renderer, so there is nothing to keep honest and no fidelity
check to write.

Where a reviewer would rather look than read, CI already attaches the built `.simhubdash` to every
run, and installing it is the same act as installing a release.

## What this costs, stated plainly

**It is serialised on one machine, and there is only one.** `bun run shots` refuses when another
session holds the claim, and a claim is only considered abandoned after ninety minutes. Two
contributors preparing evidence at the same time take turns.

**It is manual, and nothing fails when it is skipped.** No check enforces it; `CONTRIBUTING.md`
asks an author who cannot run SimHub to say so in the pull request rather than to leave it unsaid,
which is a social guarantee and not a mechanical one.

**It is still images.** A rule that only misbehaves over time is invisible in a still: a blink
phase, a chart trace filling, a zone advancing to its next page, a value that is correct in the
frame captured and wrong a second later. XOR-22 asked for a video on every pull request, and this
record does not deliver it.

**It excludes a contributor who does not have the VM**, which is the sharpest cost of the three,
because this is an MIT project that wants outside contributions and bounty claims. What is left for
such a contributor is the artefact CI already attaches, installed on their own SimHub, or a
maintainer running `bun run shots` on the branch. Neither is as good as a command they can run
themselves, and there is no honest way to describe that as adequate.

## What would reopen this

**Motion becoming the thing under review.** The decision rests on layout being what a reviewer
needs to see, which is true while the work is faces, zones and modules. It stops being true if the
next body of work is animation, and the answer then is more likely to be capturing video from the
VM than building a renderer.

**The batch outgrowing a single claim.** The default set is eighteen faces today, against the ten
XOR-112 was written for, because the zone faces joined the list, and nobody has timed the eighteen.
Growth of that kind is what would eventually make a parallel renderer pay for itself, and the
figure to watch is how long one claim holds the VM rather than how many packages exist.

**Somebody wanting the preview renderer for its own sake.** A renderer in the browser is also a
design tool and a way to look at a package without Windows, and that is a different justification
from this one. It would be a new decision rather than a reversal of this one, and it would still
owe the fidelity check.

## Consequences

### Good

Nothing is built, and what exists is ratified. The evidence a reviewer sees is produced by the
engine the driver will use, so the class of bug where the preview is right and the dashboard is
wrong cannot occur.

Sixteen points of speculative work are returned to the backlog, and the failure mode they carried
with them, which is a second renderer drifting quietly from the one that matters, is not acquired.

### Bad

Visual review stays a maintainer privilege, and the project's contribution story is weaker for it.

The gap between what CI proves and what a person has to check by hand does not narrow. CI proves
that the JSON is what the TypeScript says, that no box overlaps another and that no text exceeds
its frame; it does not prove that the result looks right, and after this record it still will not.

### The tickets this settles

XOR-24 has no subject and should be cancelled: it exists to prove a preview renderer is not lying,
and there is no preview renderer. XOR-21 and XOR-23 are not built. XOR-22 is not delivered as
written and needs rewriting around capture from the VM if it is to survive at all. XOR-19 and
XOR-20 lose the justification they were given here, though XOR-20 may still earn its place from
binding tests, which is a separate argument and not this one.

### Unresolved

Whether a capture should be required rather than requested. A check that refuses a pull request
touching `packages/dash/` without an attached image is writable, and it would convert the social
guarantee into a mechanical one at the cost of standing between an outside contributor and their
first merge. Nothing here depends on which way that goes.
