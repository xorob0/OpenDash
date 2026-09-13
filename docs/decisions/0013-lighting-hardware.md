# ADR 0013: openDash lights hardware, and the flag box is where it starts

**Date:** 2026-09-13
**Status:** Accepted. Moves the "what ships" sentence in [scope.md](../scope.md) from fourteen
dashboards and a plugin to fourteen dashboards, a plugin and the lights beside them. Extends
[ADR 0002](0002-djson-generated-from-source.md) to a second kind of build output, and inherits
[ADR 0012](0012-update-checks.md)'s consent argument rather than restating it.

## Context

openDash ships screens. Everything in the tree assumes it: the generator emits a `.djson`, the
build packs a `.simhubdash`, the plugin extracts one when its embedded copy is newer, and every
refusal in [scope.md](../scope.md) is about what may or may not be drawn on a screen.

The rigs openDash is fitted to are not only screens. The same driver has an RPM strip above the
wheel, a button box whose buttons light, a brow strip over the monitor, ambient lights behind it
and — increasingly — an 8x8 matrix flag box on the desk, because it is the cheapest addressable
thing SimHub drives and the models are free to print. SimHub drives all of them through the same
plugin family, out of profile files it has never documented.

Daniel Newman Racing, the comparison that matters, ships twelve LED profiles beside its dashboards
and sells them as one product. A driver who buys a dash and a strip expects the two to agree, and
today openDash has nothing to say to the strip at all.

Nothing in the refusal list forbids this. The list covers our own renderer, DashStudio authoring,
computed telemetry, theming, idle screens, licensing and user telemetry, and an LED profile is none
of them. But "What ships" names fourteen dashboards and a plugin, and that sentence *is* the
product. It has to gain a clause before a `.ledsprofile` lands in `build/`, for the same reason the
zone face changed the document before it changed the code.

## Investigation

[XOR-224](../research/simhub-leds-format.md) went and established the format rather than assuming
one, by decompiling SimHub 9.12.6 and round-tripping hand-written files through SimHub's own
deserializer on the test VM. The findings that decide this record:

**A profile is plain JSON, and nothing about it is secret.** A `.ledsprofile` is one
`SimHub.Plugins.DataPlugins.RGBDriver.Settings.Profile` serialised with Newtonsoft at
`Formatting.Indented` — UTF-8, CRLF, two-space indent, no compression, no `$type` graph, no
checksum, no signature and no length prefix. The first bytes of a shipped profile are
`{\r\n  "CarChoices": [...`.

**It can be generated, and that was proven rather than inferred.** A minimal profile written by
hand — `LedContainers`, `Name` and `ProfileId`, with `CarChoices`, `GlobalBrightnessPreset`,
`TestLedsGameData`, `GameCode` and `CarChoice` omitted entirely — deserialised correctly through
SimHub's own `Profile` type: every container materialised, formulas intact, colours parsed, an 8x8
picture's pixels in the right cells. Missing properties keep their C# defaults, and re-serialising
produces the fully-populated canonical file, so SimHub normalises on first save. A generator has to
emit valid JSON with correct `ContainerType` strings and nothing more.

**An unknown effect degrades rather than failing the load.** A `ContainerType` SimHub does not
recognise becomes an `UnknownContainer` that keeps the original JSON and sets `IsEnabled: false`.
A profile generated against a newer catalogue than the SimHub reading it loses that effect and
keeps the rest, which is the forward-compatibility property a generated artefact needs.

**The null-safe rule is expressible, and SimHub's own profiles already rely on it.** Effect
conditions, colours, brightness, blink and whole-content generation are bound through
`SimHub.Plugins.OutputPlugins.Dash.GLCDTemplating.ExpressionValue` — the *same* type the `.djson`
bindings use, with the same NCalc `[PropertyName]` syntax and the same `isnull()` helper. Shipped
built-in profiles contain `"Expression": "isnull([Gear],0)='R'"` verbatim. So
`isnull([OpenDash.Flag], 0)` in a profile means exactly what it means in a dashboard, and a profile
installed without the plugin falls back to its defaults rather than going dark.

**There is no LED equivalent of a `.simhubdash`.** A profile is not part of a dashboard package. It
lives in the output plugin's own settings file — `PluginsData/Common/ArduinoRGBLedsSettings.json`
and its siblings — and is exchanged as a single `.ledsprofile`. Device-specific built-ins sit under
`DevicesBuiltInProfiles/<device guid>/{S1,S2,S3,Raw,buttons,matrix,extra}/`. Installing a profile
is therefore a different act from installing a dash, against a different path, for a device the
build cannot see.

**The output plugin owns its settings file and rewrites it.** `RGBLedsDriver` reads the file at
startup and writes it back on change, so a file edited underneath a running SimHub is overwritten.
Whatever installs a profile either writes before SimHub starts or hands the user a file to import.

**Two conventions, not one.** The strip driver derives `ContainerType` by trimming
`SimHub.Plugins.DataPlugins.RGBDriver.LedsContainers.` from the front of the type name and
`Container` from the end, so `Flags.YellowFlagContainer` is written `Flags.YellowFlag`. The
*matrix* driver (`RGBMatrixDriver.MatrixContainers`) uses the bare class name instead, so the same
file extension carries two naming schemes. An emitter that assumes one produces a profile of
disabled `UnknownContainer`s against the other.

**And one trap worth writing down.** Colours are `ColorTranslator.FromHtml`, which accepts a named
colour, `#RRGGBB`, `#AARRGGBB` and `"R, G, B"` — but **not** three-digit shorthand. `#F80` parses
to ARGB `00000F80`, a transparent near-black, rather than to orange. It fails silently, as an
unlit LED.

## Decision

**openDash ships LED profiles as generated build output, beginning with the 8x8 flag box, and
never installs one onto a user's device without being asked.**

Point by point, because the point of this record is that each is written down.

**Which hardware, and in what order.** The 8x8 matrix flag box first, then the RPM strip, then the
brow, the wheel and button LEDs, then ambient. The flag box goes first because it is the one where
openDash already owns the content: the flag colours are in `design/tokens.json` and the alert
catalogue is being written as one ordered list for the face, the companion and the pit wall anyway.
A 64-pixel box is that same list with a different renderer, and if the box and the screen can
disagree about which of two live flags wins, one of them is wrong. The RPM strip is second because
it is where [ADR 0014](0014-the-shift-model.md) pays for itself twice, on the strip and on the
screen, from one definition.

**Generated, not hand-authored.** XOR-224 answered the question that decides this, so
[ADR 0002](0002-djson-generated-from-source.md)'s reasoning applies unchanged and unamended: the
profile is build output, TypeScript and tokens are the source, and nobody edits a profile in
SimHub's LED editor and brings it back. The same consequences follow — a lighting change is a
readable diff, a colour comes from `design/tokens.json` and from nowhere else, and a profile found
in the tree that was not emitted by the build is a bug rather than a contribution.

This is the clause that matters most, because a hand-authored artefact with hex values in it is
exactly what `design/tokens.json` exists to prevent. Had the answer been no, this record would have
had to name who may edit the file and how a change to it is reviewed; it is yes, so it does not.

**Never silently installed.** A package installs itself: the plugin compares versions and extracts,
and the worst case is a dashboard a user can close. A device profile is not that. It attaches to a
*device someone owns*, it changes what that hardware does, and a driver who finds their strip
behaving differently after an update has had their rig altered without being asked.

So: openDash writes no profile to any device path unless a person has chosen it in the panel, per
device family, with the choice remembered and reversible. The default is off. A profile openDash
did not write is never overwritten, and a profile openDash did write is replaced only when the user
asks for the newer one — the same report-and-choose shape [ADR 0012](0012-update-checks.md) argues
for the update check, for the same reason, and one step more conservative because the default there
is on and here it is off.

The asymmetry is deliberate. An update *check* discloses an IP address to GitHub; writing a profile
changes hardware behaviour in a dark room at 200km/h.

**Complete without the plugin, like everything else.** Every property an emitted profile reads is
wrapped in `isnull()` with its default, which XOR-224 confirmed is expressible and which SimHub's
own profiles already do. A user who imports a `.ledsprofile` and never installs the plugin gets the
default behaviour, not an unlit strip. This is the standing requirement of
[ADR 0003](0003-plugin-settings-through-properties.md) reaching a second artefact, and it is not
negotiable for a profile any more than it is for a package.

**One sim, said plainly.** iRacing is what openDash supports, and a flag box is more tempting to
over-claim than a dash because flags look universal. They are not: the per-game flag handling DNR
carries for a dozen sims is exactly the work this project has refused to advertise without driving
it. A profile openDash ships says iRacing on it. Another sim very probably works, for the same
reason another sim very probably works on the screens, and it is untested in the same way.

**No custom effect in the plugin, for now.** SimHub's plugin SDK lets a plugin register its own
`LedsContainerBase` subclass, which would appear in the effect picker and could read anything. It
is rejected here, because an effect that only exists where the openDash plugin is installed makes
the profile unportable and breaks the rule above. Everything the flag box needs is expressible in
stock containers and NCalc expressions. This is the line most likely to move, and what would move
it is in "What would reopen this".

## What would reopen this

**A pattern stock containers cannot express.** The flag box is being built on
`Groups.CustomConditionalGroup`, `CustomStatus`, `Animation` and NCalc. If a case arrives that
genuinely needs state between frames — an animation that has to resume where it left off, a
countdown that has to survive a flag — then the choice is a custom effect in the plugin or dropping
the feature, and that is a new decision. Note that it collides with
[ADR 0009](0009-does-the-plugin-compute.md) and would have to be argued there as well.

**The default on the consent switch.** Off-by-default is chosen deliberately and is the line a
reasonable person could land the other side of. Nothing else here depends on it.

**A device openDash cannot describe.** The emitter assumes a strip whose LEDs can be addressed by
position and a matrix that is 8x8. Hardware that is neither — a ring, a 16x16 panel, a device
whose driver uses a third `ContainerType` convention — is a new shape and possibly a new record.

## Consequences

### Good

The whole `iflag` and `leds` block becomes writable, and with it the one thing openDash can do that
the comparison cannot do cheaply: the strip and the screen light at the same instant for the same
reason, out of one definition, because both are generated from the same source.

`design/tokens.json` stays the only place a colour is defined, and it now reaches hardware. A flag
that changes colour changes on the face, the companion, the pit wall and the box, in one diff.

The standalone promise survives intact on a second artefact. A `.ledsprofile` is a complete product
on its own, exactly as a `.simhubdash` is.

### Bad

openDash now writes outside its own `DashTemplates` folder, into paths owned by other SimHub
plugins, for devices the build cannot see and CI cannot own. That is a new class of failure and the
consent model is the only thing standing between it and someone's rig.

The definition of done gets weaker for this work, not stronger. "Seen on the VM in real SimHub" is
what catches what the tests cannot, and nobody in CI owns an 8x8 matrix. What replaces it is
weaker: SimHub's own deserializer, a preview renderer of our own, and an honest note in the pull
request saying the pixels were not seen lit.

Two `ContainerType` conventions and a colour parser that fails silently are both the kind of detail
that produces a profile which loads, reports no error, and lights nothing.

### Unresolved

**Which output plugins to emit for.** A profile is per output plugin — Arduino RGB, Nextion,
TM1638, the matrix driver — and whether `ForcedLedCount` and `Groups.RemapGroup` are enough to
cover the devices people own with one tree is not known. XOR-234's strip is where that gets
answered for real hardware shapes.

**Whether writing the settings file directly is ever safe**, or whether installation must always be
an import the user drives. The driver rewrites that file on change, which suggests the second; the
consent model above is written so that either implementation satisfies it.

**What `S1`, `S2`, `S3`, `buttons`, `matrix`, `extra` and `Raw` mean** as sections under a device's
built-in profiles. The names were read out of the assembly's string heap and the sectioning is
plainly per-device-feature, but which section a generated profile belongs in has not been
established.
