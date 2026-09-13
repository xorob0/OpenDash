# Scope

**Status:** current. Supersedes [scope-mvp.md](scope-mvp.md), which is closed and kept as the
record of what the MVP was.

This document describes what OpenDash is: what it ships, what is deliberately not built, and
which of the MVP's refusals have since been reversed and by what. It is the document a
contributor or an agent should read first, and the one that has to be amended when the answer to
"what is OpenDash" changes.

> **The face has been rebuilt.** The zone model described below is the settled design
> ([ADR 0006](decisions/0006-the-zone-face.md), [design/zones.md](design/zones.md)), and since
> 0.2.0-rc.1 it is what the names in the table below install: the eight rectangular faces are zone
> faces, and the twelve-slot ones they replaced are published as `openDash slots <size>` for anyone
> who wants the old design back. The two round faces are still the card model, because what a round
> face does with zones is not decided (XOR-94).
>
> `README.md` and `plugin/INSTALL.md` still describe the twelve-slot face and are now wrong about
> the product a user installs; correcting them is XOR-99, and until it lands this document is the
> one to trust.
>
> The distinction matters because of the rule at the end of the refusals: a line has to move here
> before the code that crosses it may be written. That is the reason this document changed first.

## Product

OpenDash is an open-source dashboard package for [SimHub](https://www.simhubdash.com/),
released under the MIT licence. It consists of fourteen dashboards covering three kinds of
screen and the LED profiles that light the hardware around them, together with a SimHub plugin
that installs them and exposes the settings which decide what they show. It is free, and
bounties or donations may follow later.

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
| Lights | LED profiles for the hardware beside the screen; the 8x8 flag box first |
| Plugin | .NET Framework 4.8, code-only WPF, builds on Linux |
| Licence | MIT |

Other sims will very probably work, because SimHub normalises the common fields into
`StatusDataBase`, and a module that reads something iRacing does not publish says so rather than
drawing a zero. They are nevertheless untested, and they must not be advertised as supported
until somebody has actually driven them. Concerning that audit, see XOR-51.

## What ships

Three kinds of screen and the lights beside them, built from one set of parts and installed by
one plugin.

### The face

The dashboard on the wheel or the dash. It is **five parts**: the rev bar with its shift lights in
a recessed well, a bar of settled values, a body of zone B, zone A and zone C, and band D across
the foot.

Each zone shows **one page at a time from its own catalogue, and a wheel button cycles it**. Zone
A chooses among four pages built around the gear; zones B and C among the twenty-one that also
serve the companion and the pit wall; band D among eight that suit a wide short band, with a flag
taking the band over while one is out. The bar is not a zone and does not cycle: it carries what
does not change during a lap, which is what earns it the space.

A page is never scaled. It is laid out for the **shape** of the box it is given, and it sheds its
secondary ranks before it shrinks its numerals, so a bigger screen shows more in each zone rather
than more regions of the same size.

Ten faces ship. Eight are rectangular and take the same five parts; the two round ones are not yet
decided and are noted below.

| Package | Size | |
|---|---|---|
| `openDash` | 1920 x 480 | the reference face |
| `openDash 1280x480` | 1280 x 480 | |
| `openDash 1280x400` | 1280 x 400 | a shorter body, the same zones |
| `openDash 1280x720` | 1280 x 720 | the tall body lets zone C list the field |
| `openDash 850x480` | 850 x 480 | narrow zones, five settings in the bar |
| `openDash 800x480` | 800 x 480 | derived from 850 x 480 |
| `openDash 800x286` | 800 x 286 | no bar: the height is not there |
| `openDash 600x686` | 600 x 686 | portrait, A over B over C |
| `openDash 800 round` | 800 x 800 | still on the card model; see below |
| `openDash 480 round` | 480 x 480 | still on the card model; see below |

**The package folders keep the small o**, and that is deliberate rather than an oversight. The
product is OpenDash, and everything a person reads says so; a folder name is a path on somebody's
disk, and Windows file names are case-insensitive but case-preserving, so renaming
`DashTemplates/openDash` to `DashTemplates/OpenDash` is not a rename the installer or SimHub would
notice as one. A user could end up with either spelling depending on what created the folder, and a
user with both would see two entries in Dash Studio. The cost of the inconsistency is one reader
raising an eyebrow; the cost of the rename is somebody's dashboard list.

The same reasoning covers the other names only a machine reads. The condensed faces ship under the
family name `openDash Display`, which every `.djson` asks for by that exact string and which the
build writes into the font files themselves, so the spelling is a key rather than a word; dashboard
titles are the same kind of key. Changing one of those is changing an identifier, and it is worth
doing only when something breaks without it.

`packages/dash/src/contract.ts` holds the catalogues and the defaults, and
`plugin/OpenDash/Contract.cs` mirrors it, with a test on each side reading the other file so
that the two cannot drift.

[docs/design/zones.md](design/zones.md) is the full specification: every rectangle of every face,
every page of every catalogue, and the shape model. [ADR 0006](decisions/0006-the-zone-face.md) is
why the model changed from twelve equal slots, which is what shipped in 0.1.0.

**What a round face does with zones is not decided.** The two round artboards are still drawn on
the slot model and the only zone-era rule touching them is that a round face uses its ring instead
of a band. They keep building on the card path until the question is answered.

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

### The lights

The hardware around the screen: an 8x8 matrix flag box, an RPM strip, a brow strip above the
monitor, the LEDs in a wheel or button box, and ambient lights behind the rig. SimHub drives all of
them out of profile files, and openDash generates those profiles the same way it generates the
scene graph — from TypeScript and `design/tokens.json`, so that a flag is the same colour on the
box as it is on the face, and a shift light comes on at the same instant on the strip as it does on
the rev bar.

[ADR 0013](decisions/0013-lighting-hardware.md) is the record, and it settles three things worth
repeating here. A profile is **build output**, so nobody edits one in SimHub's LED editor and
brings it back. A profile is **never written to a device without being asked**: the choice is per
device family, off by default, remembered and reversible, because a profile changes what hardware
someone owns does. And a profile is a **complete product on its own**, every property read wrapped
in `isnull()` with its default, so a user who imports one without the plugin gets the default
behaviour rather than an unlit strip.

[research/simhub-leds-format.md](research/simhub-leds-format.md) is the format, verified against
SimHub 9.12.6.

The flag box ships first, because it is the one whose content openDash already owns: the flag
colours are in `design/tokens.json` and the alert catalogue is one ordered list for the face, the
companion and the pit wall. A 64-pixel box is that list with a different renderer.

## The plugin

The plugin installs the embedded packages when one is missing or older than the embedded copy,
attaches every setting as a SimHub property under the `OpenDash` prefix, and draws a settings
page in SimHub's left menu. It does not render, does not read telemetry and does not compute
anything; [ADR 0003](decisions/0003-plugin-settings-through-properties.md) is why, and the
question of whether it should ever compute is open as XOR-47.

The settings are the shift lights, the position mode, the delta reference, the session progress
mode, the four zones of the face (the page each shows, which pages are enabled, and the page it
opens on), the quick glance, the bar's four end fields, twenty-one companion module switches,
five pit wall zone assignments and a web view address. Because they are ordinary SimHub properties, another
dashboard or an LED profile can read them, and a change reaches the running dashboard at once
without restarting SimHub or reopening the dashboard.

Every expression that reads an `OpenDash` property wraps it in `isnull()` with the default, so a
user who installs only a `.simhubdash` gets the default layout and the default modes. That is a
standing requirement rather than a convenience: a package has to be a complete product on its
own.

## What is deliberately not built

This list is a set of refusals, not a backlog. Work that falls under one of these lines does not
belong in a pull request until the line is removed from this document.

**Our own renderer.** OpenDash renders through SimHub and will continue to.
[ADR 0001](decisions/0001-simhub-native-rendering.md) settled it, and reversing it would discard
everything SimHub already does for DDUs, USB screens, phones and the seventeen sims it reads.

**Authoring in DashStudio.** The `.djson` is build output. Anything edited in SimHub's editor is
overwritten by the next build, and a pull request that contains a hand-edited scene graph cannot
be reviewed. See [ADR 0002](decisions/0002-djson-generated-from-source.md).

**Computed telemetry of our own.** The plugin does not compute, and
[ADR 0009](decisions/0009-does-the-plugin-compute.md) is why the refusal turned out to be cheap to
keep: SimHub already publishes the fuel family and the delta family, and the rest of what the
catalogue draws is arithmetic over properties that exist, done in the expression. A five-lap
average is `PreviousLap_00` to `_04` and a division, not a state machine.

Two things fall outside that and are honestly labelled rather than quietly empty: virtual energy,
which only Le Mans Ultimate publishes, and strength of field, which SimHub does not expose at all.

The line moves if a derivation is shared widely enough to need a name, or if something genuinely
needs memory between frames. The first is a JavaScript binding before it is a plugin, because the
standalone package is the property worth defending.

**Theming and colour customisation.** OpenDash ships one opinionated look, resolved at build time
into literal values in the `.djson`. Nothing a user can change reaches a colour, a typeface or a
size. How far personalisation could ever reach into a generated package is
[ADR 0011](decisions/0011-personalisation.md), and it has to be written before any of it is built,
because the one line the product holds is that two states a driver cannot tell apart is a bug
whoever chose the colours.

**Idle and pit screens.** Every screen already declares `IdleScreen`, so SimHub shows the racing
face with no data in it between sessions, which is arguably worse than SimHub's own default. A
screen with idle content is a real gap and is XOR-62; it is a refusal today rather than a plan.

**Licensing, activation or accounts.** OpenDash is MIT and there is nothing to unlock.

**Telemetry about the user.** Nothing about the user leaves their machine: no identifier, no
installation id, no usage counting, no error reporting. The one exception is the update check, and
it is argued for in [ADR 0012](decisions/0012-update-checks.md), which states in full what is sent.
What is sent is an anonymous request to GitHub asking what the newest release is, carrying the
user's IP address, which reaches GitHub and not us, and a `User-Agent` naming the product. It can
be switched off, and switching it off means nothing is fetched at all.

**Copying Lovely's visual design.** Lovely's licence forbids reuse of its UI design. OpenDash's
design is independently derived: do not copy its layouts, and do not use its screenshots in any
OpenDash material.

**Sims other than iRacing, as a supported claim.** They may work, and they are welcome to, but
nothing is advertised as supported before somebody has driven it and the bindings have been
audited.

## What the MVP refused and what reversed it

The MVP scope listed nine things as explicitly out of scope. Six of them have since been reversed,
and each reversal is recorded here so that a reader of the old document is not misled.

| The MVP refused | Reversed by | What is true now |
|---|---|---|
| Multiple dashboards | XOR-6, XOR-8 | Fourteen packages ship from one source tree |
| Multiple screen sizes | XOR-6 | Ten faces, each one layout file |
| Round DDUs | XOR-6 | 480 and 800 round faces ship |
| Phone and tablet layouts | XOR-8 | Two companion packages ship |
| Page navigation | XOR-8, [ADR 0006](decisions/0006-the-zone-face.md) | The companion pages through its modules with a wheel button, and every zone of the face now cycles its own catalogue the same way |
| Network update checks | XOR-29, [ADR 0012](decisions/0012-update-checks.md) | The plugin may ask GitHub what the newest release is. Nothing about the user is sent, it can be switched off, and nothing is ever installed without being asked for |

Three of the nine still stand. Each is restated above with the record that would have to move it:

| Still refused | What would have to happen first |
|---|---|
| Theming and colour customisation | ADR 0011, and this line moving with it. The Personalisation project is unmergeable until it does |
| Idle and pit screens | XOR-62 and XOR-53, behind the same record |
| Computed telemetry of our own | Nothing. [ADR 0009](decisions/0009-does-the-plugin-compute.md) is written and accepted, and it confirmed the refusal rather than moving it |

None of them is built, and until one is, the refusal is the current answer. **A pull request that
falls under one of these lines is declined however well it is written**; the line moves first, in
its record, and the code follows.

The stream overlay is neither built nor refused. Nobody has asked for it.

## Definition of done, for a change

A change is done when `bun run check` passes, when `dotnet test plugin/OpenDash.Tests` passes if
the plugin changed, when the snapshot diff has been read rather than merely refreshed, and when
whatever the change draws has been seen on the Windows VM in real SimHub. The last condition is
the one that catches what the tests cannot: WPF clips silently, and a box measured from the
wrong face or from a sample narrower than the runtime value loses glyphs without failing
anything. [CLAUDE.md](../CLAUDE.md) explains the traps and
[testing-vm.md](testing-vm.md) explains the VM.

**For a change that lights hardware, that last condition cannot be met and something weaker takes
its place**, because nobody in CI owns an 8x8 matrix or an RPM strip. A generated profile is done
when it loads through SimHub's own deserializer without becoming an `UnknownContainer`, when the
preview renderer draws what it should draw, and when the pull request says plainly that the pixels
were not seen lit. That is a weaker bar than the screens are held to, and it is stated here rather
than left to be discovered, so that a reviewer knows which of the two they are reading.

## Related documents

[architecture.md](architecture.md) is how source becomes a `.simhubdash` and how a setting
reaches a running dashboard. [second-screens.md](second-screens.md) is the companion and the pit
wall. [decisions/](decisions/) holds the records that this document summarises, and a record
wins over this summary wherever the two disagree. [research/](research/) holds the format notes
verified against SimHub 9.12.6, which are the place to check before guessing at a property name.
[scope-mvp.md](scope-mvp.md) is closed, and is of historical interest only.
