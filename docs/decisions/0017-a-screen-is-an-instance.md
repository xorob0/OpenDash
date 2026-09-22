# ADR 0017: A screen is an instance the user added, not a resolution

**Date:** 2026-09-13
**Status:** Accepted. Amends [ADR 0003](0003-plugin-settings-through-properties.md), which made a
property name a public interface, and [ADR 0011](0011-personalisation.md), whose "nothing
regenerates a package on the user's machine" line is narrowed rather than broken. Settles the shape
of [#176](https://github.com/xorob0/OpenDash/issues/176).

## Context

Two screens of the same size cannot be configured apart, and the reason is not in OpenDash's design.

`Contract.FacePrefix` keys a face's settings by its size, so `Faces["Face1280x480"]` is one entry
whatever the rig holds. That was deliberate — [#175](https://github.com/xorob0/OpenDash/issues/175) put
it there so that a 1920 on the dash and an 850 on the rim stopped sharing one set of zones — and it
fixed the case it was aimed at. It does not reach the case where the two screens are the same size,
and a driver with a wheel screen and a dash screen of the same model is an ordinary rig, not an
exotic one.

The reason it does not reach is worth stating exactly, because it decides what a fix can be.

**A package carries its property names as literals.** The generator resolves every binding at build
time, so `OpenDash 1280x480` ships with the string `OpenDash.Face1280x480ZoneA` written into it. The
built package on 2026-09-13 contains **716 such occurrences** across its four `.djson` files:

| file | occurrences of `OpenDash.Face1280x480…` |
|---|---|
| `OpenDash 1280x480.djson` | 460 |
| `zoneface-module-469x320.djson` | 128 |
| `zoneface-module-469x361.djson` | 128 |

**SimHub properties are global to the process.** There is no per-window, per-display or per-instance
scope; `PluginManager` holds one dictionary, and `[OpenDash.Face1280x480ZoneA]` means the same thing
to every dashboard open on every monitor.

Put together, those two facts give one conclusion, and it is a conclusion about SimHub rather than
about us:

> One package folder is one settings namespace. N independently configured screens require N folders
> under `DashTemplates`.

Every candidate fix therefore has to put a second folder on disk. What is left to decide is what
writes it, what the namespace is called, and what the user is asked.

## What was decided against

**Variant packages, built and shipped per slot.** Emit `OpenDash 1280x480 (screen 1)` through
`(screen 4)`, each reading `Face1280x480S1…` and so on. It needs no new machinery at all, and it is
rejected for the reason [ADR 0011](0011-personalisation.md) already rejected variant packages: four
slots across eight face sizes is thirty-two packages before a single theme or typeface exists, which
is a release nobody can navigate and a dashboard manager nobody can read. It also caps the rig at
four and asks the user to know which slot they are on.

**A "screen index" property the package reads indirectly.** A binding resolves one literal property
name through reflection ([ADR 0011](0011-personalisation.md) records `BindingHelper.InitBinding`
doing `GetProperty(propertyName)`), and NCalc has no indirection that would let `ZoneA` be looked up
through a second property. There is no version of this that works.

**Regenerating the package on the user's machine.** Rejected by [ADR 0003](0003-plugin-settings-through-properties.md)
and re-rejected by [ADR 0011](0011-personalisation.md); it means porting the generator to C# or
shipping a JavaScript runtime. Nothing here revisits that.

**Keying settings by the folder the user assigned in SimHub.** The plugin cannot see which dashboard
is open on which display, and SimHub's display assignment is not exposed to a plugin. There is
nothing to key on.

## The decision

**A screen is an instance the user added. It has a name they chose, a kind, a size, a namespace
allocated once at creation, and a folder under `DashTemplates` of its own. A rig is any number of
them, in any mix of sizes, including several of the same size.**

### The namespace, and who gets the stock one

A screen's namespace is what its properties and its wheel-button actions are called.

**The first screen at a size takes that size's stock namespace and the stock folder.** Adding a
1280x480 when the rig has none gives `Face1280x480` and the folder `OpenDash 1280x480`, and the
package is extracted byte for byte as it is embedded. This is the whole of today's behaviour, and it
is deliberate that the common rig — one screen per size — produces exactly the files it produces
now.

**A second screen at a size gets a namespace slugged from its name, and a folder of its own.** Name
it "Rim" and it is `OpenDash.RimZoneA`, in `DashTemplates/OpenDash Rim`, with those 716 tokens
rewritten on the way in.

A slug is the name's letters and digits, in the name's own case, with everything else dropped. It is
rejected when it collides with a namespace the rig already holds or with a reserved one
(`Face<w>x<h>`, `Companion`, `PitWall`), and a numeral is appended until it does not. Letters and
digits only, because `contract.ts` records that whether SimHub's parser accepts a second dot inside
a property name is unverified, and this is not the place to find out.

### Renaming is a label, and never moves the namespace

A namespace is frozen at creation. Renaming a screen changes what the panel shows and nothing else:
not the properties, not the actions, not the folder.

This is the ADR 0003 consequence applied to a name a user can edit. A property name is a public
interface — people bind to `OpenDash.*` from their own dashboards and read it in SimHub's property
browser — and a rename that silently re-pointed it would break those bindings with no diagnostic,
which is this format's specialty. It also keeps a rename from needing a rewrite, a folder move and a
SimHub restart to be correct.

The cost is a screen renamed from "Main dash" to "Rim" whose properties still say `MainDash`. The
panel answers that by showing the namespace in the screen's own pane, so it is a stated fact rather
than a discovery.

### The rewrite is an install step, not a build step

The generator is untouched. It emits one package per size carrying that size's stock namespace, and
the standalone `.simhubdash` a user downloads without the plugin is exactly what it is today. ADR
0003's promise that a package alone is a complete product is unaffected.

What is new is that the installer can extract a package **under a chosen folder name, with one
namespace token rewritten, and with the screen's name written into the `Title` of its
`.djson.metadata`**. That last part is not decoration: SimHub's dashboard list shows `Title`, and
two screens of the same size are today two entries a user cannot tell apart.

This narrows ADR 0011's "nothing regenerates a package on the user's machine". Nothing does. A
rewrite of one distinctive token in an already-built scene graph is not a regeneration: no layout is
recomputed, no text is re-measured, and every `textFit` guarantee still holds because no box and no
glyph moved. The line that stands is the one that matters — geometry is a build input, and the
plugin does not compute one.

The rewrite is verifiable, which is why it is acceptable at all. After it, no occurrence of the
stock namespace may remain, and the count of the new namespace must equal the count of the old. A
test asserts both.

## Consequences

### Good

A rig is whatever the user has. Three 1280x480 screens, or a 1920 and two 850s, or one of
everything; the panel is a list of what they added rather than a catalogue of what exists.

Two screens of a size are finally distinguishable **in SimHub**, not only in our panel, because the
folder and the title carry the name. That was a real problem that nobody had written down: the
dashboard picker showed one `OpenDash 1280x480` and the user guessed.

The wheel-button actions become per instance for the same reason the properties do, so a second face
can sit still while the one in front of the driver cycles.

`packages/dash/test/declared-properties.txt` does not change, and this is the load-bearing part of
why the change is affordable. The pin is the set of properties **a built package reads**, and a
built package reads its stock namespace. Extra instances are created by a user at runtime and are
additive; they are not pinnable and do not belong in the pin. Both halves of the contract keep
checking exactly what they check now.

### Bad

The plugin now writes a file it did not author byte for byte, so a package on disk can differ from
the embedded one for a legitimate reason. `FolderFingerprint` must record what the installer
actually wrote rather than what the resource contains, or every instanced screen reads as "edited"
and the update path refuses to touch it.

A user who renames or deletes a `DashTemplates` folder by hand desyncs the rig. The panel keeps the
screen's card, marks it, and offers to write the package back; removing the card would hide the
thing that needs fixing.

**A screen cannot be added without restarting SimHub to see it.** SimHub reads its template list
once, at startup ([dev-loop.md](../dev-loop.md)). The panel says so at the moment of adding. This is
not new — it is true of every install today — but it becomes visible, because adding a screen is now
something a user does mid-session rather than something that happened before they ever opened
SimHub.

The action list grows with the rig rather than with the catalogue. That is an improvement on today,
where it is five entries per face size that ships whether or not the user owns one, but it means the
list changes as screens are added, and SimHub holds bindings by action name. A removed screen's
action stops being registered, and a button bound to it goes quiet. The panel warns before a remove
that this will happen.

### Unresolved

**Whether SimHub tells a plugin what displays it is driving.** The add flow is much better if the
sizes the machine actually has are offered first, and
[#85](https://github.com/xorob0/OpenDash/issues/85) already says the whole improvement over a flat list
rests on it. Nothing in `docs/research/simhub-plugin-sdk.md` says whether the SDK exposes it. Until
somebody checks, the add flow offers the full catalogue and nothing is ordered cleverly.

**What happens to a screen whose size stops shipping.** A face size removed from `FACE_SIZES` leaves
an instance with a package that can no longer be written. It should keep its settings and read as
unavailable, but nothing implements that yet and no test covers it.

**The round faces.** [ADR 0006](0006-the-zone-face.md) leaves them on the card model and nothing here
changes that. A round face is an instance like any other; what its pane draws is still undecided.
