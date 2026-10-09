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
> faces. The twelve-slot ones they replaced are still built as `OpenDash slots <size>`, and they are
> what they already were in practice: a comparison built for a rig from a local build, installed by
> nothing and published nowhere (#438), until #146 deletes them. The two round faces still ship on the card
> model, and that is a decision rather than a gap: a round face becomes zones on a ring before 1.0
> (#487), and until that lands it stays the design it is (#145).
>
> The distinction matters because of the rule at the end of the refusals: a line has to move here
> before the code that crosses it may be written. That is the reason this document changed first.

## Product

OpenDash is an open-source dashboard package for [SimHub](https://www.simhubdash.com/),
released under the MIT licence. It consists of fourteen dashboards covering three kinds of
screen, together with a SimHub plugin that installs them and exposes the settings which decide
what they show. It is free, and bounties or donations may follow later. The plugin is the only
way in: a release publishes `OpenDash-plugin.zip` and nothing a user could import by hand, and every
dashboard and every LED profile reaches SimHub through the plugin's panel (#438).

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
| Distribution | `OpenDash-plugin.zip` only; the packages and profiles travel inside it |
| Licence | MIT |

Other sims will very probably work, because SimHub normalises the common fields into
`StatusDataBase`, and a module that reads something iRacing does not publish says so rather than
drawing a zero. They are nevertheless untested, and they must not be advertised as supported
until somebody has actually driven them. Concerning that audit, see #102.

## What ships

Three kinds of screen, built from one set of parts and installed by one plugin, and the profiles
for the lights around them: a box of lights that is not a screen at all, and the LED strips on a
wheel, a dash or a monitor.

### The face

The dashboard on the wheel or the dash. It is **five parts**: the rev bar with its shift lights in
a recessed well, a bar of settled values, a body of zone B, zone A and zone C, and band D across
the foot.

Each zone shows **one page at a time from its own catalogue, and a wheel button cycles it**. Zone
A chooses among four pages built around the gear; zones B and C among the module pages that also
serve the companion and the pit wall; band D among eight that suit a wide short band, with a flag
taking the band over for three seconds when it comes out and then settling into the block at each
end until it clears. The bar is not a zone and does not cycle: it carries what
does not change during a lap, which is what earns it the space.

A page is never scaled. It is laid out for the **shape** of the box it is given, and it sheds its
secondary ranks before it shrinks its numerals, so a bigger screen shows more in each zone rather
than more regions of the same size.

Ten faces ship. Eight are rectangular and take the same five parts; the two round ones are still the
card model, on purpose, and what they become is noted below.

| Package | Size | |
|---|---|---|
| `OpenDash` | 1920 x 480 | the reference face |
| `OpenDash 1280x480` | 1280 x 480 | **the large size** |
| `OpenDash 1280x400` | 1280 x 400 | a shorter body, the same zones |
| `OpenDash 1280x720` | 1280 x 720 | the tall body lets zone C list the field |
| `OpenDash 850x480` | 850 x 480 | **the base size**: narrow zones, five settings in the bar |
| `OpenDash 800x480` | 800 x 480 | derived from 850 x 480 |
| `OpenDash 800x286` | 800 x 286 | no bar: the height is not there |
| `OpenDash 600x686` | 600 x 686 | portrait, A over B over C |
| `OpenDash 800 round` | 800 x 800 | the card model until #487 converts it; see below |
| `OpenDash 480 round` | 480 x 480 | the card model until #487 converts it; see below |

**The base size is 850 x 480 and the large size is 1280 x 480.** They are the pair anything that
has to pick a face picks: the size `bun run dev` opens when no package is named, the two the
README photographs, and the two a change to the face is looked at in before it is looked at
anywhere else. 850 x 480 is the base because it is the tightest face the design drew an artboard
for that still carries all five parts -- 800 x 480 is derived from it, 800 x 286 has no room for
the bar -- so its zones are the first real tall narrow in the repository and a page that survives
them survives anywhere. 1280 x 480 is the large one because it is the widest size a driver is
likely to own, and because the pair together shows what one capture cannot: the same page in a
274 x 328 zone and in a 469 x 320 one, stacked in the first and tabulated in the second, laid out
for its box rather than scaled into it.

`OpenDash` at 1920 x 480 stays **the reference face**, and that is a different job from being the
base. It is the widest artboard, the one [design/zones.md](design/zones.md) tabulates every other
size after, and the face the plugin's pre-face settings migrate into, which is why the code and the
canvas keep calling it that. None of that makes it the size to reach for when one face has to stand
for the product, and it is not one many people own.

**The package folders keep the small o**, and that is deliberate rather than an oversight. The
product is OpenDash, and everything a person reads says so; a folder name is a path on somebody's
disk, and Windows file names are case-insensitive but case-preserving, so renaming
`DashTemplates/OpenDash` to `DashTemplates/OpenDash` is not a rename the installer or SimHub would
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

**A round face becomes zones on a ring, before 1.0 (#487).** The rev arc it already has, zone A in the
middle, the rectangles its cards occupy today as small catalogue zones, and the flag on the ring:
no bar and no band. That is the answer #145 took, of three, and
[design/zones.md](design/zones.md) section 9 is the written form of it, with an amendment to
[ADR 0006](decisions/0006-the-zone-face.md) recording the date.

**Until that work is done the two round faces ship on the card model, deliberately.** They are
decided and not yet converted, which is a different thing from undecided, and it is why
`OpenDash.Slot01` to `Slot12` stay until #487 lands (#170). The card path is retired at 1.0: #487
converts the round faces and #146 deletes the path behind them, in that order, and the rule that
kept it through 1.0 was removed on 2026-09-29. The canvas owes two round artboards drawn on the new
model and the plugin panel owes a round picker before the conversion can be built.

### The companion

A phone or tablet beside the wheel showing one module at a time, in landscape at 850 x 480 or in
portrait at 480 x 850. Each module is its own SimHub screen with its own switch in the plugin, and
a module that is switched off is removed from SimHub's next-and-previous ring rather than left as an
empty page. Paging is a wheel button bound to SimHub's own screen navigation.

### The pit wall

A screen for somebody who is not driving, at 1920 x 1080 or 1080 x 1920. Three landscape pages
and one portrait page carry the field with gaps, intervals, sectors, stints and stops, the
driver's own lap beside it, and data zones whose contents are plugin settings.

[second-screens.md](second-screens.md) describes the module model and both second screens in
full, including every case where a module is off by default because iRacing publishes none of
its data.

### The idle screen

Every package carries one, and it is the screen SimHub shows when no game is running: the wordmark,
the time and one line saying that no game is running. It is not a package of its own and there is
nothing to point at it -- a display already configured with an OpenDash face idles on OpenDash.

What it does **not** draw is a reading a game has to publish. A last best lap, the car, the driver
and the session are all the sim's, and drawing them as dashes between sessions is the thing this
screen exists to stop: until #763 every racing screen claimed the idle role, so a rig at rest showed
a rev bar at zero over three zones of dashes. What a user may then do to it is #736, and the update
mark is #755.

### The flag box

An 8x8 LED matrix in a printed box, beside the screen rather than on it, showing the flag that is
out, the gear, the pit state, a car alongside and the warnings a driver would otherwise miss —
each as a 64-pixel picture, ranked in the same order the face ranks them and coloured from the
same `purpose.flag.*` tokens.

One profile ships, `OpenDash Flag box.ledsprofile`, built by `bun run build` like everything else
and embedded in the plugin like everything else. Like a strip's profile below, it is an artefact
**the plugin does not install by itself**: a profile paints hardware somebody owns, which is a thing
to be asked about rather than assumed. The Matrix page has a press that adds it to SimHub's own
matrix profiles through SimHub's own API, and it never presses itself. The file is written out as
well, as the fallback and as the thing you copy to another machine.
[ADR 0013](decisions/0013-lighting-hardware.md) is the reasoning and [flag-box.md](flag-box.md) is
the guide.

### The LED strips

A strip is a run of RGB LEDs on a wheel, along a dash or above a monitor, and what a driver knows
about it is how many LEDs it has and how they are grouped: sides of nought to four around a centre
of four to twelve, or a bare run of thirteen to twenty-five, which is what a brow is. The build
generates a profile for every one of those shapes, and the plugin embeds them all. A strip carries
the revs, the flags, a car alongside, the pit lane and the car's own warnings, and its rev lights
are the car's own wherever the table the driver downloads has the car
([ADR 0018](decisions/0018-the-cars-own-lights.md)).

**One profile per strip.** A strip is something the driver adds on the LEDs page and names, as a
screen is an instance ([ADR 0017](decisions/0017-a-screen-is-an-instance.md)), and adding it installs
its shape's profile rewritten as that strip's own, reading that strip's settings, into the SimHub LED
device the strip names ([ADR 0013](decisions/0013-lighting-hardware.md)). The LEDs page is where its
Rev lights, its centre and its effects are set, and where its Install and Update presses are; neither
presses itself, and nothing selects a profile on a device, because which profile somebody's hardware
runs is theirs to choose.

**What is still not claimed** is the rest of the LED families: wheel buttons, button boxes and
ambient lighting. Each is a different device with a different container vocabulary, OpenDash builds
no profile for any of them, and the honest position is the one ADR 0013 took for the strips before
they shipped.

## The plugin

The plugin installs the embedded packages when one is missing or older than the embedded copy,
attaches every setting as a SimHub property under the `OpenDash` prefix, and draws a settings
page in SimHub's left menu. It does not render, does not read telemetry and does not compute
anything; [ADR 0003](decisions/0003-plugin-settings-through-properties.md) is why, and the
question of whether it should ever compute is open as #98.

The Screens page does now show the screen it is configuring, and that is not a reversal of the sentence
above. The panel hosts SimHub's own renderer, handed the `.djson` under `DashTemplates` that the
driver's screen loads, reading the properties the panel has just written; nothing about the picture
is drawn by OpenDash. [ADR 0020](decisions/0020-the-panel-draws-what-it-configures.md) is the record,
and it states how far the line moved and what would move it further.

The settings are the shift lights, the position mode, the delta reference and the places the delta
is drawn to, the session progress mode, the four zones of the face (the page each shows, which pages
are enabled, and the page it opens on), the quick glance, the bar's four end fields, twelve card
slots, a switch for each companion module, five pit wall zone assignments and a web view address.
Because they are ordinary SimHub properties, another dashboard or an LED profile can read them, and
a change reaches the running dashboard at once without restarting SimHub or reopening the dashboard.

`OpenDash.Slot01` to `OpenDash.Slot12` are the twelve card slots. Of the faces the plugin installs
the only readers are the two round ones: `OpenDash 480 round` reads the first two and
`OpenDash 800 round` the first six, and every rectangular face the plugin installs is zones. The
eight `OpenDash slots <size>` packages of the banner above read them too, four to twelve each and
all twelve at 1920 x 480 and 1280 x 720, because they are the card faces the zone faces replaced;
the csproj keeps them out of the plugin's resources and no release publishes them, so they are
built for a comparison on a rig and are no reason to keep anything. The card path is retired at 1.0,
once #487 has converted the round faces (#146). The twelve are not deprecated and no release is promised to remove them before that; the release that
converts a round face to zones on a ring, #487, is the one that says what becomes of
them (#170).

Every expression that reads an `OpenDash` property wraps it in `isnull()` with the default, and it
stays that way, although what it answers to has changed. No package is offered without the plugin
any more (#438), so the wrapping is not a promise that one stands on its own. It is the answer to
the ordinary state of a rig on which the plugin is present and not yet enabled, which
[plugin/INSTALL.md](../plugin/INSTALL.md)'s troubleshooting describes, and a face drawing its
defaults there is a better answer than a face drawing nothing.

## What is deliberately not built

This list is a set of refusals, not a backlog. Work that falls under one of these lines does not
belong in a pull request until the line is removed from this document.

**Our own renderer.** OpenDash renders through SimHub and will continue to.
[ADR 0001](decisions/0001-simhub-native-rendering.md) settled it, and reversing it would discard
everything SimHub already does for DDUs, USB screens, phones and the seventeen sims it reads. The
preview in the settings panel is not an exception to this, since what draws it is SimHub's own
renderer; a picture of a dashboard produced by any code of ours still falls under this line.

**Authoring in DashStudio.** The `.djson` is build output. Anything edited in SimHub's editor is
overwritten by the next build, and a pull request that contains a hand-edited scene graph cannot
be reviewed. See [ADR 0002](decisions/0002-djson-generated-from-source.md).

**Computed telemetry of our own.** The plugin does not compute, and
[ADR 0009](decisions/0009-does-the-plugin-compute.md) is why the refusal turned out to be cheap to
keep: SimHub already publishes the fuel family and the delta family, iRacing publishes the one
delta SimHub lacks, the live delta to the last lap, and the rest of what the catalogue draws is
arithmetic over properties that exist, done in the expression. A five-lap average is
`PreviousLap_00` to `_04` and a division, not a state machine.

Two things fall outside that and are honestly labelled rather than quietly empty: virtual energy,
which only Le Mans Ultimate publishes, and strength of field, which SimHub does not expose at all.

The line moves if a derivation is shared widely enough to need a name, or if something genuinely
needs memory between frames. The first is a JavaScript binding before it is a plugin: a package no
longer stands on its own (#438), but a derivation in the expression still draws on a rig whose
plugin is not yet enabled, and one in the plugin does not.

**Personalisation that changes the layout.** Colour is no longer refused:
[ADR 0011](decisions/0011-personalisation.md) settled how far personalisation reaches, and the
colours, the frames and the idle screen are settings read through bindings like every other setting.
What stays refused is anything a binding cannot reach without giving up the guarantee that a glyph
is never clipped. A typeface, a font size, a spacing and a position are consumed by a layout
decision in TypeScript, and a value that arrives after the build cannot re-run it. Those are build
inputs, and a package built from a user's own tokens is #764. A setting that only changes how long a
text is, as the delta's precision does (#322), is not refused, because every length it can produce
was budgeted for when the box was cut; ADR 0011 records that condition.

The refusal is of a user's personalisation at runtime, and a **shipped car theme** falls outside it.
A car theme is decided in a pull request, resolved into its own packages when they are built and
measured by the same fit tests as every other package, so it is a build-time variant rather than
personalisation, and [ADR 0015](decisions/0015-car-themes.md) is the record that decides what one may
change and what it may never remove. A theme pull request consequently no longer falls under this
line, although it falls under the trade dress line below.

The one line the product holds underneath all of it is unchanged: two states a driver cannot tell
apart is a bug whoever chose the colours. A user may choose any colours they like, and OpenDash
says so when a choice collides rather than quietly shipping it.

**Idle and pit screens were a line here and are not one any more.** The idle screen ships: #763 gave
every package one, "What ships" above describes it, and what a user may do to it was already in the
runtime bucket of [ADR 0011](decisions/0011-personalisation.md), which is #736.

A page laid out *for* the pit lane was the other half of that line and is not built, and it is
scheduled rather than refused, so the answer this document gives is a ticket: #383 is the page that
opens on entering the lane and is gone on leaving it. Until it lands, every screen declares
`PitScreen` beside its in-game role, so SimHub keeps the dashboard up during a stop, and the pit
family on the face -- the limiter banner over zone A and the stop alerts over the same zone -- is what
a driver in the box reads.

**A way in that does not begin with the plugin.** A release publishes `OpenDash-plugin.zip` and
nothing a user could import by hand: no `.simhubdash`, no `.ledsprofile`, and no manifest listing
them. The build still writes all of it, because the plugin embeds it; what stopped is offering it as
a download (#438). It buys one writer into `DashFonts` instead of two, a strip profile installed in
the shape [ADR 0017](decisions/0017-a-screen-is-an-instance.md) gives it rather than the
pre-instance shape a hand import carried, and a support conversation that does not begin by
establishing which route a user took. It costs this, chosen rather than discovered: **a user who
cannot put a DLL into SimHub's own folder, or who will not, has no route in at all.**

**Licensing, activation or accounts.** OpenDash is MIT and there is nothing to unlock.

**Telemetry about the user.** Nothing about the user leaves their machine: no identifier, no
installation id, no usage counting, no error reporting. The one exception is the update check, and
it is argued for in [ADR 0012](decisions/0012-update-checks.md), which states in full what is sent.
What is sent is an anonymous request to GitHub asking what the newest release is, carrying the
user's IP address, which reaches GitHub and not us, and a `User-Agent` naming the product. It can
be switched off, and switching it off means nothing is fetched at all. The car's crest is the other
request a rig makes without a press ([#714](https://github.com/xorob0/OpenDash/issues/714), the trade dress
section below): one file, once, from the address on the panel, carrying the same `User-Agent` and the
user's IP address to that address's host, and only on a rig with a screen that draws the crest. Clearing
the address stops it and removes the copy.

**Copying a visual design.** OpenDash's design is independently derived, and every screenshot in
OpenDash material is its own.

**A manufacturer's trade dress.** A car theme ([ADR 0015](decisions/0015-car-themes.md)) is drawn
from a real car's instrument cluster, and the reference for it is usually an iRacing user manual,
which is iRacing's copyright and carries photographs and renders of a manufacturer's unit. What a
theme may take from it, and what it may not, is settled here once, so that a contributor can decide
for any element without asking.

A theme may take the arrangement, meaning which values sit together and in what order; the
register, meaning whether the face is dense or sparse, boxed or open, warm or cold; and the
colour logic, meaning which state is drawn in which colour. These are facts about how a driver
reads the car, the manuals describe them in prose, and reproducing them is what makes a theme feel
like the car. Moreover, as was decided on 2026-10-06 for the Porsche
([#205](https://github.com/xorob0/OpenDash/issues/205)), a theme may draw the elements by which a
driver identifies the cluster the car's way, so that a driver who puts the face beside the manual's
render recognises the car: setting boxes outlined each in its own colour and named by what they are,
and the car's own shift light colours drawn on the screen. That is an exception to the house rule that
colour means state, and it is confined to the theme that needs it; the colours of such boxes remain
the user's to change. One could think that a cluster's exact palette is a design rather than a fact,
and is therefore closer to the trade dress than anything else. It is taken nonetheless where it is how
a driver reads or finds something, and it is not taken where it is the manufacturer's brand rather
than the car's display, which is why Porsche's corporate red appears nowhere on the Porsche face.

A theme may never take a manufacturer's word mark, its logo or crest, a typeface licensed
to the manufacturer, a badge drawn as artwork, or any image from a manual or a photograph,
whether traced, cropped or redrawn from it. A manual is a reference to read and cite and never an
asset to extract. Where the car draws a crest on its display, the theme reserves that place and the
package leaves it empty ([#765](https://github.com/xorob0/OpenDash/issues/765)). Every face a theme draws
is under the OFL or MIT, for a reason that has nothing to do with trade dress: it is redistributed
inside `_SHFonts/` in every package, so a manufacturer's corporate face is unavailable whatever this
paragraph said, and a theme reaches its register with the faces OpenDash may legally ship.

**The crest is fetched, never shipped**, as was decided on 2026-10-08 for the Porsche
([#714](https://github.com/xorob0/OpenDash/issues/714)), amending the ruling of #765 that the place stays
empty. Nothing above about redistribution changes: no package, release or file of this repository
carries a mark. A theme may, by default, have the plugin fetch the car's crest into the user's own
folder, the way the car light tables are fetched ([ADR 0018](decisions/0018-the-cars-own-lights.md)), and
draw it in the place the car draws it. The plugin carries an address and the hash of the file expected
there, the panel shows the address and the user may change or clear it, and a cleared address or a fetch
that fails leaves the place empty as before. Two things the fetch does not answer are answered here
instead. Drawing a manufacturer's crest on OpenDash's screen, wherever the file came from, is a *use* of
the mark, which is a different question from shipping it; it is accepted knowingly, as a nominative
reference to the car whose display the theme draws, and the fetch is not offered as the reason it is
allowed. And the Porsche theme is free, so the question of a sold product drawing the mark, which is the
case trade mark law cares about most, does not arise for it; what a paid theme does is answered when one
exists, and until then the default fetch belongs to the free themes alone. The request is the one thing
OpenDash fetches at a start without a press: one file, from the address on the panel, on a rig with a
screen that draws the crest and no copy of it yet.

A theme is named after the display family as a driver says it, as a nominative reference to the
product and in OpenDash's own type, never styled as the manufacturer presents it. The Porsche theme is
`Porsche`, because the 992-era display it draws is shared by four cars; where a marque has several
unrelated displays, the model name distinguishes them. That name is the one that appears in the
package folder (`OpenDash Porsche 1280x480`, [ADR 0016](decisions/0016-themed-package-distribution.md)),
in SimHub's dashboard list, in the plugin's panel and in the title of the theme's ticket.

**Sims other than iRacing, as a supported claim.** They may work, and they are welcome to, but
nothing is advertised as supported before somebody has driven it and the bindings have been
audited.

**The invisible dash.** A screen capture item drawn behind the face, so that the screen reads as
transparent over the rig, is a feature of somebody else's product. SimHub does publish
`ScreenCaptureItem`, and [research/simhub-dash-format.md](research/simhub-dash-format.md) already
names it among the types the MVP did not need, so the refusal is a choice about focus rather than a
limitation of the format.

**The stream overlay.** A face drawn for a viewer on the other side of a broadcast is a different
product: a different reading distance, a different set of readings, and an audience that is not
holding a wheel. It is refused rather than left open. This line replaces the sentence that used to
stand at the foot of these refusals, which said the overlay was neither built nor refused because
nobody had asked for it; a standing "nobody has asked" is an invitation to ask.

**Vendor-specific wheel integrations.** The rotary modes, the bite point steps and the clutch
calibration overlays that the Precision Sim Engineering and Bavarian SimTec wheels are configured
with need hardware nobody here owns, and a configuration screen that cannot be driven on the VM is
worse than no screen at all. What SimHub publishes for any wheel is a separate matter and is not
refused by this line: a bite point or a clutch paddle that arrives as an ordinary property is
telemetry like any other, and a page may draw it.

**User-ordered leaderboard columns.** Rule 17 in [design/zones.md](design/zones.md) says that a page
answers to the shape of its zone, and a table whose column order the user fixes cannot shed from the
tail when the box narrows. The columns the tables are missing are a separate matter and are added
under #318; the ordering stays the model's.

## What the MVP refused and what reversed it

The MVP scope listed nine things as explicitly out of scope. Eight of them have since been reversed,
and each reversal is recorded here so that a reader of the old document is not misled.

| The MVP refused | Reversed by | What is true now |
|---|---|---|
| Multiple dashboards | #57, #59 | Fourteen packages ship from one source tree |
| Multiple screen sizes | #57 | Ten faces, each one layout file |
| Round DDUs | #57 | 480 and 800 round faces ship |
| Phone and tablet layouts | #59 | Two companion packages ship |
| Page navigation | #59, [ADR 0006](decisions/0006-the-zone-face.md) | The companion pages through its modules with a wheel button, and every zone of the face now cycles its own catalogue the same way |
| Network update checks | #80, [ADR 0012](decisions/0012-update-checks.md) | The plugin may ask GitHub what the newest release is. Nothing about the user is sent, it can be switched off, and nothing is ever installed without being asked for |
| Theming and colour customisation | #124, [ADR 0011](decisions/0011-personalisation.md) | Colour, frames and the idle screen are settings read through bindings; the typeface, the sizes and the spacings stay build inputs, and a narrower line took this one's place |
| Theming, as a shipped car theme | #193, [ADR 0015](decisions/0015-car-themes.md) | A car theme is a build-time variant, one package per size per theme, which may change the palette, the type, the chrome and the anatomy and may never remove a page or drop a field; it is not the personalisation the narrower line refuses |
| Idle or pit screens | #763, #383 | Every package carries an idle screen, which is what SimHub shows between sessions. No page is drawn for the pit lane yet; the limiter banner and the stop alerts on the face stand in its place until #383 lands |

One of the nine still stands and ADR 0011 left a narrower line behind the one it moved. The pit half
of the idle-and-pit line is not refused either: it is #383, and the refusals above say what the face
carries until that lands. Each is restated above with the record that would have to move it:

| Still refused | What would have to happen first |
|---|---|
| Personalisation that changes the layout | #764: a package built from the user's own tokens. Nothing at runtime re-measures a text box. A shipped car theme is a build-time variant and is not under this line ([ADR 0015](decisions/0015-car-themes.md)) |
| Computed telemetry of our own | Nothing. [ADR 0009](decisions/0009-does-the-plugin-compute.md) is written and accepted, and it confirmed the refusal rather than moving it |

None of them is built, and until one is, the refusal is the current answer. **A pull request that
falls under one of these lines is declined however well it is written**; the line moves first, in
its record, and the code follows.

## Definition of done, for a change

A change is done when `bun run check` passes, when `dotnet test plugin/OpenDash.Tests` passes if
the plugin changed, when the snapshot diff has been read rather than merely refreshed, and when
whatever the change draws has been seen on the Windows VM in real SimHub. The flag box is the one
exception, and it is a stated one: no 8x8 panel is plugged into the VM and CI owns no hardware, so
a profile is done when it loads in real SimHub and its glyphs were read in SimHub's own matrix
preview. That is weaker than seeing a panel light, which is why it is written here rather than
assumed. The last condition is
the one that catches what the tests cannot: WPF clips silently, and a box measured from the
wrong face or from a sample narrower than the runtime value loses glyphs without failing
anything. [CLAUDE.md](../CLAUDE.md) explains the traps and
[testing-vm.md](testing-vm.md) explains the VM.

## Related documents

[architecture.md](architecture.md) is how source becomes a `.simhubdash` and how a setting
reaches a running dashboard. [second-screens.md](second-screens.md) is the companion and the pit
wall. [flag-box.md](flag-box.md) is the 8x8 matrix, and [design/flag-box.md](design/flag-box.md)
is what each of its sixty-four-pixel pictures means. [decisions/](decisions/) holds the records that this document summarises, and a record
wins over this summary wherever the two disagree. [research/](research/) holds the format notes
verified against SimHub 9.12.6, which are the place to check before guessing at a property name.
[scope-mvp.md](scope-mvp.md) is closed, and is of historical interest only.
