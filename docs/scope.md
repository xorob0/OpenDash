# Scope

**Status:** current. Supersedes [scope-mvp.md](scope-mvp.md), which is closed and kept as the
record of what the MVP was.

This document describes openDash as it is today: what it is, what ships, what is deliberately
not built, and which of the MVP's refusals have since been reversed and by what. It is the
document a contributor or an agent should read first, and the one that has to be amended when
the answer to "what is openDash" changes.

## Product

openDash is an open-source dashboard package for [SimHub](https://www.simhubdash.com/),
released under the MIT licence. It consists of fourteen dashboards covering three kinds of
screen, together with a SimHub plugin that installs them and exposes the settings which decide
what they show. It is free, and bounties or donations may follow later.

The dashboards are generated from TypeScript and design tokens rather than drawn in SimHub's
editor. A generator emits the `.djson` scene graph, the build packs it into a `.simhubdash`, and
SimHub renders it natively. The reasoning is in [ADR 0001](decisions/0001-simhub-native-rendering.md)
and [ADR 0002](decisions/0002-djson-generated-from-source.md); the pipeline is in
[architecture.md](architecture.md).

| | |
|---|---|
| Renderer | SimHub native (DashStudio) |
| Telemetry | SimHub; the user supplies their own install |
| Supported sim | iRacing |
| Screens | ten dash faces, two companions, two pit walls |
| Plugin | .NET Framework 4.8, code-only WPF, builds on Linux |
| Licence | MIT |

Other sims will very probably work, because SimHub normalises the common fields into
`StatusDataBase`, and a module that reads something iRacing does not publish says so rather than
drawing a zero. They are nevertheless untested, and they must not be advertised as supported
until somebody has actually driven them. Concerning that audit, see XOR-51.

## What ships

Three kinds of screen, built from one set of parts and installed by one plugin.

### The face

The dashboard on the wheel or the dash: a hero zone holding the gear together with the
indicators that belong to no card, which is to say the rev bar with its shift lights, the flag
strip and the pit limiter, and beside it a grid of equal slots. Any card may be placed in any
slot from the plugin, and every slot is the same size so that every card fits every slot.

Ten faces ship. A face declares its own slot count and reads the first N slot settings, so a
smaller screen simply exposes fewer slots.

| Package | Size | Slots |
|---|---|---|
| `openDash` | 1920 x 480 | 12 |
| `openDash 1280x480` | 1280 x 480 | 8 |
| `openDash 1280x400` | 1280 x 400 | 8 |
| `openDash 1280x720` | 1280 x 720 | 12 |
| `openDash 850x480` | 850 x 480 | 6 |
| `openDash 800x480` | 800 x 480 | 6 |
| `openDash 800x286` | 800 x 286 | 4 |
| `openDash 600x686` | 600 x 686 | 6 |
| `openDash 800 round` | 800 x 800 | 6 |
| `openDash 480 round` | 480 x 480 | 2 |

Thirteen cards are available to a slot: speed, current lap, last lap, best lap, delta, position,
session, fuel, fuel laps, TC, ABS, tyre temperatures and tyre pressures.
`packages/dash/src/contract.ts` holds the catalogue and the default assignment, and
`plugin/OpenDash/Contract.cs` mirrors it, with a test on each side reading the other file so
that the two cannot drift.

### The companion

A phone or tablet beside the wheel showing one module at a time, in landscape at 850 x 480 or in
portrait at 480 x 850. There are twenty-one modules, each of which is its own SimHub screen with
its own switch in the plugin, and a module that is switched off is removed from SimHub's
next-and-previous ring rather than left as an empty page. Paging is a wheel button bound to
SimHub's own screen navigation.

### The pit wall

A screen for somebody who is not driving, at 1920 x 1080 or 1080 x 1920. Three landscape pages
and one portrait page carry the field with gaps, intervals, sectors, stints and stops, the
driver's own lap beside it, and data zones whose contents are plugin settings.

[second-screens.md](second-screens.md) describes the module model and both second screens in
full, including every case where a module is off by default because iRacing publishes none of
its data.

## The plugin

The plugin installs the embedded packages when one is missing or older than the embedded copy,
attaches every setting as a SimHub property under the `OpenDash` prefix, and draws a settings
page in SimHub's left menu. It does not render, does not read telemetry and does not compute
anything; [ADR 0003](decisions/0003-plugin-settings-through-properties.md) is why, and the
question of whether it should ever compute is open as XOR-47.

The settings are the shift lights, the position mode, the delta reference, the session progress
mode, twelve slot assignments, twenty-one companion module switches, five pit wall zone
assignments and a web view address. Because they are ordinary SimHub properties, another
dashboard or an LED profile can read them, and a change reaches the running dashboard at once
without restarting SimHub or reopening the dashboard.

Every expression that reads an `OpenDash` property wraps it in `isnull()` with the default, so a
user who installs only a `.simhubdash` gets the default layout and the default modes. That is a
standing requirement rather than a convenience: a package has to be a complete product on its
own.

## What is deliberately not built

This list is a set of refusals, not a backlog. Work that falls under one of these lines does not
belong in a pull request until the line is removed from this document.

**Our own renderer.** openDash renders through SimHub and will continue to.
[ADR 0001](decisions/0001-simhub-native-rendering.md) settled it, and reversing it would discard
everything SimHub already does for DDUs, USB screens, phones and the seventeen sims it reads.

**Authoring in DashStudio.** The `.djson` is build output. Anything edited in SimHub's editor is
overwritten by the next build, and a pull request that contains a hand-edited scene graph cannot
be reviewed. See [ADR 0002](decisions/0002-djson-generated-from-source.md).

**Computed telemetry of our own, on the dashboard.** A card shows a SimHub property. Anything
requiring openDash to keep state across laps, such as a fuel calculator or a stint estimate,
waits on XOR-47 deciding where that computation may live, since it cannot live in a scene graph.

**Licensing, activation or accounts.** openDash is MIT and there is nothing to unlock.

**Telemetry about the user.** Nothing leaves the user's machine. The update check contemplated
by XOR-29 and XOR-31 is an exception that has to be argued for in its own decision record,
stating exactly what is sent, and it has to be possible to switch off.

**Copying Lovely's visual design.** Lovely's licence forbids reuse of its UI design. openDash's
design is independently derived: do not copy its layouts, and do not use its screenshots in any
openDash material.

**Sims other than iRacing, as a supported claim.** They may work, and they are welcome to, but
nothing is advertised as supported before somebody has driven it and the bindings have been
audited.

## What the MVP refused and what reversed it

The MVP scope listed nine things as explicitly out of scope. Five of them have since been built,
and each reversal is recorded here so that a reader of the old document is not misled.

| The MVP refused | Reversed by | What is true now |
|---|---|---|
| Multiple dashboards | XOR-6, XOR-8 | Fourteen packages ship from one source tree |
| Multiple screen sizes | XOR-6 | Ten faces, each one layout file |
| Round DDUs | XOR-6 | 480 and 800 round faces ship |
| Phone and tablet layouts | XOR-8 | Two companion packages ship |
| Page navigation | XOR-8 | The companion pages through its modules with a wheel button |

Four of the nine still stand, and are restated as refusals above: theming and colour
customisation, idle or pit screens, network update checks, and computed telemetry of our own.
The first is the subject of the Personalisation project, the second of XOR-62 and XOR-53, and
the third of XOR-29; none of them is built, and until one is, the refusal is the current answer.

The stream overlay is neither built nor refused. Nobody has asked for it.

## Definition of done, for a change

A change is done when `bun run check` passes, when `dotnet test plugin/OpenDash.Tests` passes if
the plugin changed, when the snapshot diff has been read rather than merely refreshed, and when
whatever the change draws has been seen on the Windows VM in real SimHub. The last condition is
the one that catches what the tests cannot: WPF clips silently, and a box measured from the
wrong face or from a sample narrower than the runtime value loses glyphs without failing
anything. [CLAUDE.md](../CLAUDE.md) explains the traps and
[testing-vm.md](testing-vm.md) explains the VM.

## Related documents

[architecture.md](architecture.md) is how source becomes a `.simhubdash` and how a setting
reaches a running dashboard. [second-screens.md](second-screens.md) is the companion and the pit
wall. [decisions/](decisions/) holds the records that this document summarises, and a record
wins over this summary wherever the two disagree. [research/](research/) holds the format notes
verified against SimHub 9.12.6, which are the place to check before guessing at a property name.
[scope-mvp.md](scope-mvp.md) is closed, and is of historical interest only.
