# The plugin panel

**Status:** current. This is the written record of the design that settled on the canvas on
2026-09-12. The canvas is the source:
[`design/canvas/Plugin.dc.html`](../../design/canvas/Plugin.dc.html) draws every tab and every
screen kind, and [`design/canvas/PluginComponents.dc.html`](../../design/canvas/PluginComponents.dc.html)
draws the controls it is built from. Metrics live in `panel` and `control` in
[`design/tokens.json`](../../design/tokens.json).

The panel that shipped was one scrolling page of eight sections, grouped by the kind of setting
rather than by anything the user was trying to do, and every value in it was global. A rig with
two faces and a pit wall monitor therefore had one set of zones between all three, so changing
what the main display showed changed the small one with it.

Two things follow from that, and they are the whole of this design. **A screen is the unit**, and
**the panel is three tabs rather than one scroll**.

---

## 1. A screen is the unit

A rig is a set of screens. Each one runs an openDash dashboard, and each one owns what it shows
and the wheel buttons that cycle it. The first tab lists them as cards, one selected at a time,
and everything under the cards belongs to the selected screen.

There are three kinds, and the pane below the cards changes with the kind.

| Kind | What it is | What it carries |
|---|---|---|
| **Face** | A DDU, at one of the eight sizes openDash ships | Zones B, A and C, band D, the two bar ends, and a wheel button per zone |
| **Pit wall** | A monitor beside you, or behind the wall | Four data zones across three pages, one wide zone, and the web view address |
| **Companion** | A phone or a tablet | Which of the twenty-one modules stay in the rotation, and the button that advances them |

A screen arrives named after its size and is renamed to whatever the rig calls it, so the cards
read "Main DDU", "Rim" and "Pit wall" rather than three sets of dimensions. The name is the
user's and reaches nothing but the panel.

### Where a screen comes from

The Install tab lists every package openDash can install, one per size. Adding one installs it
into SimHub's `DashTemplates` and creates its settings; removing one takes both away. There is
no separate notion of a profile, because a screen the user does not have installed is not a
screen, and one they do have is exactly one package.

## 2. A face is configured on a picture of itself

The face pane is a picture of the face at 0.44, drawn from the real rectangles in
[zones.md](zones.md), and the picture is the control rather than an illustration above one. Each
zone box carries what that zone opens on and how many pages its button cycles through, and each
end of the bar carries its field. Nothing is repeated in a list underneath.

This is the part of the panel a driver actually returns to, so it is first on the first tab, and
zone A is drawn as the narrow column it really is instead of a third of the face.

Two rows sit under the picture. The wheel buttons come first, one per zone, and then the quick
glance, which is the zone and page a held button shows. A warning appears, and never blocks, when
two zones are set to the same page.

## 3. Buttons belong to a screen, not to the plugin

A binding says which screen it cycles, because a rig with two faces has two sets of zones and a
button can only advance one of them. In practice a driver binds the face in front of them and
leaves the second one alone, which is why the second face staying still is the useful default
rather than a limitation.

## 4. What stays global

The Data tab holds the four settings that mean the same thing everywhere: the shift lights, the
position mode, the delta reference and the session progress. A lap time compares against the same
lap on the rim as it does on the pit wall, so making these per screen would offer a choice nobody
wants and would let two screens disagree about a fact.

## 5. The settings contract

Each screen reads its own properties, under a prefix derived from what the screen is. This is not
a new idea in the contract so much as the completion of one, since the pit wall and the companion
already carry `PitWallZoneA` and `CompanionModule01`; what the shipped contract lacks is the same
treatment for a face, which owns the unprefixed names as though there could only ever be one.

| Screen | Prefix | Properties |
|---|---|---|
| A face | `Face1920x480`, `Face850x480`, and so on | `ZoneA` to `ZoneD`, `ZoneAPages` to `ZoneDPages`, `ZoneAStart` to `ZoneDStart`, `BarLeft1`, `BarLeft2`, `BarRight1`, `BarRight2`, `QuickGlance` |
| The pit wall | `PitWall` | `ZoneA` to `ZoneD`, `Wide`, and `WebViewUrl` unprefixed |
| The companion | `Companion` | `Module01` to `Module21` |

The prefix is concatenated rather than separated by a dot, so a binding reads
`[OpenDash.Face1920x480ZoneB]`. SimHub already puts one dot in front of every property name, and
whether its expression parser accepts a second one inside the name is unverified; the flat form
needs no such answer and matches what the pit wall and the companion have always done.

The generator bakes the prefix into the package it is emitting, which it can do because it
already knows which size it is building. Nothing is rewritten at install time and
[ADR 0003](../decisions/0003-plugin-settings-through-properties.md) is unchanged: these are still
properties the plugin attaches and bindings read, with a fallback to the default when the plugin
is absent.

The plugin declares the properties of the screens that exist rather than of every size it could
install, so the property list stays proportional to the rig instead of running to several hundred
names.

The landscape and portrait packages of the pit wall share the `PitWall` prefix, and the two
companion packages share `Companion`, since those pairs are one screen in two orientations rather
than two screens.

### The boundary

Two faces of the **same** size share one prefix and therefore one set of zones. A rig of two
1920 by 480 panels is consequently still configured as one, which is the same limitation the
shipped panel has for every rig. It is worth being explicit that this is a deliberate stopping
point rather than an oversight: giving a second panel of the same size its own settings means
generating a package per screen instead of per size, and the common rig is a large face with a
small one beside it, which this design serves. Should it prove necessary, the way out is a
suffixed prefix chosen when the screen is added.

## 6. Empty and error states

A fresh install has no screens, so the Screens tab shows the add card alone over a line saying
that nothing is installed yet, and the Install tab is what the user is sent to. A package whose
install failed keeps its card and carries the failed status, because removing it from the list
would hide the thing that needs fixing.
