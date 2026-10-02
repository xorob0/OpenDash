# The settings panel

What the plugin draws in SimHub's left menu.

**A sidebar of pages, one per thing on the rig, and a screen is the unit.** Since
[#503](https://github.com/xorob0/OpenDash/issues/503) the panel is a sidebar and the page it opens
beside it. The four tabs it replaced (Rig, Data, Lights, Install) grouped settings by their kind. That
was honest about the settings model and wrong about the driver: somebody who came to change their
wheel found its rows on three tabs. A rig is still a set of screens the user added, each owning the
zones it shows and the buttons that cycle them, so two faces and a pit wall are configured apart
([ADR 0017](../decisions/0017-a-screen-is-an-instance.md)).

The design is the twelve #503 artboards on the author's canvas: `Sidebar`, `Main` (Home), `Rig`,
`Screens`, `AddScreen`, `Leds`, `AddLeds`, `Matrix`, `Shortcuts`, `Settings`, `Updates` and `Map`.
Where the build and an artboard disagree, [voice.md](voice.md) decides the words, and the
[departures table](#departures-from-the-artboards) records every place the built word differs.

## The map

| page | what is on it |
|---|---|
| **Home** | what needs fixing, what each device is showing right now, brightness, night mode and a way into the Rig page |
| **Rig** | every screen, strip and matrix as a tile on a canvas laid out like the rig, and the Preview chips that paint them |
| **Screens** | the screen cards, the selected screen's live preview and its editor, and the Add, Edit, Remove and Duplicate sheets |
| **LEDs** | one card per strip, the selected strip's settings, the Add an LED strip sheet, and Every strip: the rev light width and Lovely Car Data |
| **Matrix** | the flag box profile in the header, one card per matrix, the selected matrix's preview, priority and layers |
| **Shortcuts** | every action a button can be bound to, in one list: each screen's zones and quick glance, the rig's lights, and the alerts |
| **Settings** | what is the same on every screen and device: race data, flags, alerts, lighting, appearance and the driver |
| **Updates** | the plugin's version and the update check, what OpenDash has written into SimHub, Reinstall everything, the kept copies, support |

The sidebar lists Home to Settings in one column, with a rule after Rig and after Matrix
(`PanelNav.GapAfter`): Home and Rig are about the whole rig, Screens to Matrix are its devices, and
Shortcuts and Settings span all of them. Updates is pinned to the sidebar's foot under night mode.

Above the pages sit the mark and version, the search box and the live card. Search finds every row on
every page by its label and by keywords (`PanelSearch`), and opens the page scrolled to the row. The
live card names the sim, the car and the track while a session runs.

Screens, LEDs and Matrix carry a count of what the rig has, zero included. Shortcuts counts the actions
somebody has bound, and shows no count when the bindings could not be read (`PanelNav.Count`). The
Updates item carries "Restart" while an update waits for SimHub to close, or the version on offer
(`PanelNav.UpdatesBadge`).

### Where everything moved

From the four tabs to the pages. The `Map` artboard draws the same table; where the build placed a
row differently, this is the build.

| on the four tabs | now |
|---|---|
| Rig: the screen cards, add and remove | Screens |
| Rig: a zone's pages and their order | Screens, the zone's aside |
| Rig: the rev bar, flag display, lap review and info bar of a face | Screens, the face's editor |
| Rig: the folder and property names | Screens, the screen's Details |
| Rig: a face's wheel buttons, the quick-glance binders | Shortcuts, with a chip on Screens that opens each binding |
| Data: position, delta, session, names, clock | Settings, Race data |
| Data: blue flag detail | Settings, Flags |
| Data: the rev bar | Screens, the face's Rev bar row |
| Lights: brightness, night brightness, night mode | Settings, Lighting, and Home's quick controls; night mode also in the sidebar's foot |
| Lights: low fuel, oil and water thresholds | Settings, Alerts |
| Lights: the strips, their centre and rev style | LEDs, one card per strip |
| Lights: the car tables and the rev light width | LEDs, Every strip |
| Lights: matrix 1 to 4 | Matrix, one card per matrix |
| Lights: critical flags only, show the gear | Matrix, the selected matrix's layers and Idle display |
| Install: the flag box profile | Matrix's header, and a row on Updates |
| Install: the dashboards, the strip profiles, the plugin, Reinstall | Updates |

## The frame

### Three widths

The panel reads its own width and lays out in one of three modes (`PanelShell.Layout`):

| mode | control width | sidebar | main column |
|---|---|---|---|
| **Full** | 1000 px and up (`FullFrom`) | 216 px, with labels | 44 px gutter; two blocks side by side where the content has 760 px (`TwoColumnFrom`) |
| **Rail** | 760 to 1000 px (`RailFrom`) | a 56 px rail of icons | 32 px gutter; every block stacked |
| **Compact** | under 760 px | the rail | 20 px gutter; card grids of at most two columns |

On the rail an item's label, count, badge and amber dot move into its tooltip
(`PanelNav.RailTooltip`). The live card is its dot alone, and night mode is an icon toggle. The search
box is an icon that opens the search; its tooltip is "Searches every setting."

A resize rebuilds the page once the window has been still for 150 ms (`PanelShell.ResizeSettleMs`), and
only when the layout moved or the build read a width that moved. A page that read the width only up to
a cap is left alone past it, so dragging a 4K window does not reload the live preview.

### Fill the width

**The main column takes every pixel SimHub gives the panel.** The artboards are drawn 1200 wide as a
frame, not as a maximum. Past 1200 the column keeps growing beside the sidebar with no ceiling and
nothing centred. Rows, tables and card grids stretch to its right edge.

**Prose keeps a measure.** A caption or paragraph wraps at 620 (`PanelShell.ProseMaxWidth`, the
artboards' `.cap`), a row's caption at 520 (`RowCaptionMaxWidth`), and a message line or a long caption
that asks for it at 880 (`SettingsControl.BodyWidth`), which also bounds the live preview.

**A picture drawn at a fixed size shrinks to a narrower column and is never enlarged.** It is set
against the left edge, never centred. Every fixed-size picture goes through `Ui.FitWidth`.

## Attention

Home leads with what a driver cannot see from the seat. `PanelAttention.Find` turns plain facts into
issues, most urgent first, in the order a driver has to act:

| rule | title | page | press |
|---|---|---|---|
| a screen's folder is gone | "Rim's dashboard is missing from SimHub" | Screens | Install it again |
| a screen written since SimHub started | "Rim is not in SimHub yet", then "Restart SimHub to load it." and the Dash Studio step | Screens | Open Rim |
| a strip's profile installed and not selected | "Dash brow's profile is not selected", then the steps in SimHub's menus | LEDs | Check again |
| a matrix no device shows | "Left pillar is not shown in SimHub" | Matrix | Open Left pillar |
| screens an older OpenDash made | "1 screen came with an older OpenDash" | Screens | Open Screens |
| a strip's profile is older than the build's | "Dash brow's profile has an update" | LEDs | Open LEDs |
| the flag box profile is older | "OpenDash Flag box has an update" | Matrix | Open Matrix |
| an update waits for SimHub to close | "Restart SimHub to finish updating" | Updates | Open Updates |
| a newer release is on offer | "OpenDash 0.5.1 is available" | Updates | Open Updates |

A missing folder comes before a restart, because restarting will not bring it back. Both come before
the lights, because a screen is what most rigs have. The out-of-date profile and the waiting update
come last, because the rig works while they wait.

**A fact the panel could not read produces no issue.** SimHub calls can fail. A panel that guessed
would put a warning on a rig that is fine, which is worse than saying nothing about one that is not.
The matrix rule waits on this today: SimHub's public surface does not say which content a matrix
device shows, so no rig draws it until [#521](https://github.com/xorob0/OpenDash/issues/521).

**Each issue puts an amber dot on its page's sidebar item** (`PanelNav.Warns`). The Updates item wears
the dot only for what its badge does not already say (`PanelNav.UpdatesWarns`). Home's title counts
them: "Nothing to fix", "1 thing to fix", "3 things to fix".

**Check again asks SimHub and redraws. It never installs anything.** Its line says what it found, in
the strip's name: "Checked again. Dash brow's profile is selected."

## Card states

A device card says one state, with a dot, in one word per state across every page that draws it.
Home's line under a device reads the same constant as the card.

| card | state | ink | constant |
|---|---|---|---|
| screen | In SimHub | green | `PanelScreens.InSimHub` |
| screen | Restart SimHub to load it | amber | `PanelCopy.RestartToLoad` |
| screen | Missing | red | `PanelScreens.Missing` |
| strip | Showing | green | `PanelLeds.Showing` |
| strip | Not selected in SimHub | amber | `PanelLeds.NotSelected` |
| strip | Update available | amber | `PanelLeds.UpdateAvailable` |
| strip | Installed (up to date; the selection could not be read) | secondary | `PanelLeds.Installed` |
| strip | Not installed | secondary, picture dark | `PanelLeds.NotInstalled` |
| matrix | Showing | green | `PanelMatrix.Showing` |
| matrix | Not shown in SimHub | amber, picture dark | `PanelMatrix.NotShown` |
| matrix | its content and side, "Matrix 2 · Left", while neither can be read | secondary | `PanelMatrix.CardLine` |

A state is never trimmed. On a card too narrow for it, it wraps to a second line
(`PanelKit.CardStateLineHeight`). A state SimHub could not be asked for is drawn as no state, never as a
good one.

## Home

The attention card, then Right now, then the quick controls (`Main` artboard).

**Right now** is one card per kind of device, each with an Open link to its page. A screen's line says
its state. A strip's line says what it shows, "Car's own rev lights · Porsche 911 GT3 R", or its
state. A matrix's line says what it rests on, "Matrix 1 · Gear", or its state. The pictures are live
where the page can draw them.

**The quick controls** are the brightness in force (the night one while night mode is on), night mode,
and "Flags and spotter" with a press that opens the Rig page on a flag.

**An empty rig is the add tile and its sentence alone**, under the attention card when there is
something to fix. It stands in place of both Right now and the quick controls. A rig with no screens is
somebody's first minute with OpenDash, and there is no wizard to dismiss and no "never show this
again": the empty state stops appearing exactly when it stops being true.

### What the artboard does not settle, and what the panel does

- **A strip's line is one line**, trimmed with its whole text on hover. The artboard wraps it. The
  live line carries the car's name, which nothing caps, so the card keeps one height as a session
  starts and ends. The matrix line wraps, as the artboard draws it.
- **A strip whose profile this build does not carry** reads "Not installed" and draws its picture
  dark. Check again says "Checked again. This build ships no profile for Dash brow."
- **Search on an empty rig.** Search cannot tell that the rig is empty, so all of Home's entries
  stay. Right now lands on the add tile; the quick controls land at the top of a page short enough to
  show it all.
- **The matrix states wait on [#521](https://github.com/xorob0/OpenDash/issues/521).** The "is not
  shown" fix row, the amber matrix line over a dark picture and a healthy matrix's green dot are
  built and drawn on no rig until SimHub says which content a device shows.

## Rig

A canvas of tiles, one per screen, strip and matrix, that the driver drags to match the rig, and the
Preview chips under it (`Rig` artboard). A chip paints every tile with one moment of a race: a flag,
the spotter, the pit lane, a warning or the revs. The chips are `PanelEmulation.Groups`.

**The Rig page draws schematics only.** Every tile is the panel's own drawing of what that device
shows, painted by `PanelRigMap`. It never hosts SimHub's renderer: one live dashboard at a time is the
Screens page's ([ADR 0020](../decisions/0020-the-panel-draws-what-it-configures.md)).

A face draws the flag in band D, the limiter as a block over zone A, and low fuel as the dash's own
pop-up over zone A ("Fuel" in the low-fuel ink). A matrix tile draws the flag box's frame; a strip tile
draws the strip's frame. Oil and water temperature reach only a matrix, because a face has no state for
them.

### What the artboard does not settle, and what the panel does

- **Band D carries flags only.** The artboard writes PIT LIMITER on the band; the dash draws the
  limiter as a block across the top of zone A, over a full-screen flag too, and the tile draws what the
  dash draws (`PanelRigMap.FaceBandFor`). On the round tile the block sits above the gear, widened so
  its words fit inside the ring. A block too narrow for its words is drawn with none, never cut.
- **The band is drawn empty at rest.** The artboard's band reads its content; "Band D" on an idle tile
  is the panel talking to itself.
- **The canvas never shrinks below 0.7** (`PanelRigMap.MinScale`). Past that the room scrolls
  sideways inside the frame, and the wheel passes on to the page. A tile dragged to the edge scrolls
  the canvas with it, and an arrow key brings a tile into view with its focus ring.
- **The frame is the room's own width**, so the outline, the dotted ground and the drag room are one
  rectangle. When the page does not scroll, Reset layout overhangs the frame by up to 17 px.
- **An arrangement kept in a narrow window is drawn where it was kept** on a wider canvas. The
  settings do not hold the width it was kept at.
- **Matrices flank the screens by their mounting side.** Left-mounted on the left, right-mounted on
  the right, and those mounted on both sides fill the left flank first.
- **A tile's hover** is its whole name when the name is trimmed, and "Name · Needs attention" when it
  warns (`PanelRigMap.TileTooltip`).

## Screens

The screen cards, then the selected screen: its name and actions, its live preview, and its editor
(`Screens` artboard). The sheets are `AddScreen` and the Edit, Remove and Duplicate presses in the
screen's header.

**The live preview is SimHub's own renderer**, the installed dashboard drawn by the engine the
driver's screen uses ([ADR 0020](../decisions/0020-the-panel-draws-what-it-configures.md)). One at a
time: the selected screen's, let go on every page change. A missing package draws no preview, and the
fix box under the card says why.

**A face is configured on a picture of itself**, because "zone C" means nothing until you see where
zone C is. The picture is drawn to that face's proportions (`PanelFacePlan`): the rev segments, the
info bar where the face has one, zones B, A and C across the body, and band D at the foot. Each part
is a press that opens its aside: the info bar's fields, or a zone's pages as a list to tick and drag
into order, with its next-page and previous-page bindings as chips. Under the picture: Rev bar, Flag
display, Lap review, the quick glance, and Details.

A pit wall draws the page on screen and its zones, the web view address and the portrait layout. A
companion draws its modules to tick and order, and its first module. A round screen draws its cards on
the disc and the Rev ring; its cards are shared by every round screen.

**Details** holds the SimHub name, the folder, the properties namespace and the version. The namespace
is there because [ADR 0017](../decisions/0017-a-screen-is-an-instance.md) freezes it at creation and a
rename does not move it, so a screen called "Rim" whose properties say `MainDash` can say so.

**Adding.** Kind, size, orientation and name, as steps. Then once: "Added Rim. Restart SimHub, then
assign "Rim" to this display in Dash Studio." SimHub reads its template list once at startup
([dev-loop.md](../dev-loop.md)), so this cannot happen live, and the assignment is in another part of
SimHub entirely. The name is prefilled from the size, and from the size and a numeral when the rig
already has one.

**Removing** says first that it removes the screen, its dashboard and its settings, and that any wheel
button bound to it stops working, because the action is no longer registered.

**A screen whose folder is gone keeps its card**, marked Missing, with a press that writes the
dashboard back. Dropping it would destroy the zone setup behind it and hide the thing that needs
fixing.

**The quick glance is bound as a hold**, whatever press type the binding dialog offers. SimHub calls
an action's start on press and its end on release only for that press type, and a glance means
nothing as a tap.

**A page shown in two zones is said, not prevented.** The clash line sits under the zone aside. Two
zones on one page is a thing people do on purpose.

### What the artboard does not settle, and what the panel does

- **The aside takes the rest of the width**, from a least of 316, beside the picture's column
  (`PanelFacePlan.PictureWidthFor`). At the artboard's 1200 frame this is the artboard's layout. On a wide
  window the aside card stretches, where the artboard's grid would give the picture the extra room.
- **A glance's binding chip is cut at 240 px** (`PanelScreens.GlanceChipMax`), with the whole binding
  in its hover. The cap is what bounds the widest line of controls.
- **A hover appears on cut text only.** A binding chip, a zone or band cell, the info bar, a card's
  name and a disc card show their whole text where the cell actually cuts it, measured as the tooltip
  opens. Otherwise a chip says only "Opens the Shortcuts page."
- **A rectangular slots screen is a "Card face"** (`PanelScreens.CardFace`), with a rectangle thumb,
  no disc, and a rig-wide row titled "Rev bar". A round screen's is "Rev ring".
- **The edit sheet leads with the screen's own size** when no package offers it, as an option that
  means no resize (`PanelAddScreen.EditSizes`).
- **A pit wall's Race zones have no Board row.** The race board is fixed in the pit wall's layout and
  has no setting to write, so there is nothing for the row to do (`PanelScreens.PitWallZones`).
- **The Add sheet's sizes follow the artboard's order** (`PanelAddScreen.SizeOrder`), and a size the
  artboard does not draw comes after them. The edit sheet reads the same order.
- **The round plan's card numbers are its own**, tighter than the artboard's, so that six cards fit on
  the disc.

## LEDs

The strip cards and the Add an LED strip tile, then the selected strip: its header with the profile's
state and press, the preview with the Preview chips, Rev lights, This strip and Effects, and Every
strip at the foot (`Leds` and `AddLeds` artboards).

**The header's press is a button, and it never presses itself.** It carries the verb (Install,
Update) and the state says what SimHub holds now, so pressing it is never a guess. Update's hover is
`FlagBoxInstallPlan.Replaces`, "Replaces the copy in SimHub, including your changes to it.", because a
profile inside SimHub's settings gives OpenDash nothing to fingerprint and the press has to say what it
costs.

**Adding a strip is four steps**: hardware, shape, the SimHub device the profile goes to
(`PanelLights.BarDeviceTitle`), and a name. A
strip names its device, because every LED device in SimHub keeps its own profile list
([ADR 0013](../decisions/0013-lighting-hardware.md)). The sheet installs the profile as it adds the
strip, and the line after it says the step OpenDash does not take: select it on the device.

**The car's own rev lights are a switch** (#369), and on by default
([ADR 0018](../decisions/0018-the-cars-own-lights.md)). A driver who wants one look in every car turns
it off and picks a style. The tables are Lovely Car Data, fetched rather than shipped and CC BY-NC-SA
4.0, so Every strip names the project and the licence beside the download.

**Rotation, serpentine and presets are not here.** Rotation and serpentine are SimHub device settings,
and two places for them would disagree. A preset is a set of values with its own storage and its own
migration; OpenDash has no store, since a setting is a SimHub property.

### What the artboard does not settle, and what the panel does

- **A strip with no lamps at its ends gets nine effect switches**, not the artboard's four: flags,
  spotter left and right, pit lane, pit limiter, speeding in the pit lane, low fuel, and the turn
  signals. The profile installed for such a strip reads all nine.
- **A car alongside lights a bare strip.** With Car left, Car right or Both sides chosen, the preview
  fills the run in the caution ink for each side switched on, as the installed profile does. The
  artboard draws nothing.
- **The Full-strip spotter lights the whole strip**, the ends and the centre, for a car on a side that
  is switched on, because the installed profile does (`rpmStrip.ts` draws it over the whole run). The
  artboard lights only the centre's half on the car's side.
- **The car line says nothing outside iRacing**, because the car data the plugin reads is iRacing's.
  When nothing was ever downloaded it says "Lovely Car Data is not downloaded yet. Download it under
  Every strip." (`PanelLeds.CarTablesMissing`).
- **A failed download beside a working copy** says "Could not download a newer copy." and the count
  and age of the copy that still works, as voice.md's "Could not reach GitHub. You have 0.3.0-rc.4."
  does. With no copy on disk it is `PanelLights.CarTablesDownloadFailed`.
- **A strip SimHub gives no device for** says so in the header: "Nothing here can install this
  strip's profile. See SimHub device." (`PanelLeds.AwaitsDevice`).
- **The Something else tile** draws twelve LEDs in the border ink (`PanelLeds.AnyStripFrame`).
- **The header's strip name wraps** rather than being cut, and each card's hover is the strip's full
  name.

## Matrix

The flag box profile in the header, the matrix cards and the Add a matrix tile, then the selected
matrix: its preview with the Preview chips, Priority, the layers (Flags, Pit lane, Spotter, Warnings),
Idle display, Shift colours and Redline flash, and the rows every matrix shares (`Matrix` artboard).
[flag-box.md](flag-box.md) is what the box draws.

**One card per matrix**, because a screen owns its settings and a device is the same shape of thing.
People do own two boxes, one in each corner of a monitor stand, and that has to be configurable without
either box guessing.

**Mounting side is asked, not inferred.** A box to the left of the wheel that lights for a car on the
right is worse than no box, so the side has no clever default.

**A threshold of 0 means not set.** The plugin then publishes nothing and the profile applies its own
default, chosen from SimHub's temperature unit. The thresholds live on Settings, under Alerts; the
Warnings layer links there as "Triggers".

### What the artboard does not settle, and what the panel does

- **An eighth chip, Mid revs.** It is drawn only when Idle display rests on the gear, and it is the
  only chip where Shift colours shows, since the box at rest draws the gear whatever the switch says
  (`PanelMatrix.PreviewScenarios`).
- **SimHub's own label for the content number**, "RGB Matrix content" (`PanelMatrix.ContentField`),
  where the artboard reads "Matrix content: 2". The removal line uses it: "A device with RGB Matrix
  content set to 2 stays dark."
- **The title line's hover** gives the select step when the profile is up to date ("Select it on your
  matrix's device in SimHub and set RGB Matrix content to 2."), and the Updates page's words when it is
  older or failed.
- **The Remove sheet names the matrix** (`PanelMatrix.RemoveCaption`), because the sheet's title trims
  a long name.
- **The card list is a group named "Your matrices"**, not a tab list: the cards are presses that rebuild
  the page.

## Shortcuts

Every action a button can be bound to, in one list (`Shortcuts` artboard): a card per screen with its
zones' next and previous page and its quick glance, the rig's Lights (night mode, brightness up and
down), and Alerts. A filter shows all, bound, or not bound. The binder in each row is SimHub's own
editor, so a binding made here is the binding SimHub holds.

**Bound per screen**, so a second face can stay still while the one in front of the driver cycles.

### What the artboard does not settle, and what the panel does

- **The binder slot is 520 px**, against the artboard's 396, because SimHub prints a long joystick
  binding on one line wider than 396. Below 860 px of content a row stacks: the binder goes under the
  name and press.
- **The press word is hidden once a row is bound**, because SimHub's editor prints each binding's own
  press type. A held glance row keeps "Hold".
- **The glance rows carry captions** the artboard does not draw (`PanelCopy.FaceGlance`,
  `PitWallGlance`, `CompanionGlance`), each ending "Bound as a hold, whatever press type you pick."
- **A portrait pit wall keeps its card only while its glance is still bound**, with the caption "Does
  nothing on a portrait pit wall." (`PanelShortcuts.PortraitGlanceCaption`).
- **The companion's card carries a count and "Companion · 480 × 850"**, so the cards' counts add up to
  the sidebar's. Its paging crumbs are Controls and events › NextScreen, the same two the Screens page
  draws.
- **A card's line gives the kind and size, less whatever the name already says.** A card named "Pit
  wall" reads "1920 × 1080".
- **The counts leave greyed rows out**: Lights reads "1 of 3" where the artboard has "1 of 4", and
  Alerts has no count where the artboard has "0 of 1".
- **A clash is a one-line live region**, announced only when a line is new. A clashing row's binder
  slot is outlined; every slot keeps a clear 1 px border, so marking a row moves nothing.

## Settings

What is the same everywhere, in six sections: Race data, Flags, Alerts, Lighting, Appearance and Driver
(`Settings` artboard). A lap time means the same thing on the rim as on the pit wall, and so does a
name.

**The four name formats are shown as what they make of one name.** "Initial and surname" next to
"Surname and initial" is two fragments to decode, where `L. Byrne` next to `B. Liam` is the answer.
The row carries no caption. The examples stay mixed-case, although a list draws a name upper-cased,
because four shouted examples would read as a fifth choice about case
([zones.md](zones.md); [#385](https://github.com/xorob0/OpenDash/issues/385)).

**The clock is two worked examples of one time**, `14:32` against `2:32 PM`, and its caption says the
one thing the control cannot: the sim's time of day follows it too
([#324](https://github.com/xorob0/OpenDash/issues/324)).

**Delta reference has a third value, Last lap**, iRacing's own live delta to the lap before
([#322](https://github.com/xorob0/OpenDash/issues/322)). **Delta precision** sits under it because it
qualifies the same number, and its values are words, Hundredths and Thousandths. Neither resizes a box:
every box that draws the delta is cut for three places
([ADR 0011](../decisions/0011-personalisation.md)).

**The alert table** has one row per alert: its trigger, a Try press, and four greyed columns (Screens,
LEDs, Matrix, Races only) that [#512](https://github.com/xorob0/OpenDash/issues/512) will make answer.
A temperature is in SimHub's unit, so a Fahrenheit rig types Fahrenheit, and the box's placeholder
shows the default.

**Lighting** holds brightness, night brightness, night mode and the button that toggles it, over a
preview of a strip and a matrix by day and by night.

### What the artboard does not settle, and what the panel does

- **Six rows keep their captions**: Position, Delta reference, Delta precision, Session progress, Team
  names and Blue flag detail. The artboard draws them bare; `PanelDataTabTests` pins the captions.
- **The Clock row and Brightness's caption** ("SimHub's device brightness applies on top.") are the
  build's own.
- **The lighting preview is set against the left edge**, through `Ui.FitWidth`, where the artboard
  centres it. Nothing on the panel is centred.
- **Tyre wear's example is "over [70%]"**, the percent sign inside the example, where the artboard puts
  it after the box. A lone % after a numeral is drawn joined to it everywhere on the panel.

## Updates

<a id="install"></a>

The plugin's card (version, release notes, Download), the update check, the In SimHub table with
Reinstall everything under it, the kept copies, and Support (`Updates` artboard).

**The In SimHub table** has one row per dashboard, LED profile and matrix profile OpenDash wrote, with
its version and state. An older light profile whose device SimHub lists gets a small Update press in
its row, and Home's strip-update fix lands on the first row that offers one.

**Reinstall everything** installs every dashboard on the rig again, and each older or missing LED and
matrix profile. A dashboard the driver edited is kept as a copy first.

**The kept card** shows whenever a kept copy exists. Put mine back writes it back, and leaves a folder
edited since as it is and says so, since putting back keeps no copy of what it replaces.

**Support** copies a report to paste (versions, devices and the last 200 log lines; nothing is sent),
opens SimHub's log, and links the issue tracker and the guide.

**The check asks GitHub for the newest release once a day**, and nothing else leaves the machine
([ADR 0012](../decisions/0012-update-checks.md)).

### What the artboard does not settle, and what the panel does

- **A light row the page cannot know reads "Unknown"**: SimHub's LED or matrix settings out of reach,
  and the flag box's row then too. Its hover gives the reason.
- **A screen this build ships nothing for** reads "Installed", as its card does, with the hover "This
  build ships no 1280 × 480 face."
- **No "Update checks are off." line.** With checks off only the switch says so; the line shows only
  while a check runs and for the answer to a press (`PanelUpdates.CheckLine`). "Not checked yet" is for
  a rig that never checked.
- **The version column goes below 480 px of content** (560 with a row press), and the version follows
  the row's kind in the name cell (`PanelUpdates.VersionInName`).
- **The flag box's select step is the Matrix page's form**: "Select "OpenDash Flag box" on your
  matrix's device in SimHub and set RGB Matrix content to 1."
- **The built-in profiles note names its device** when a line names more than one: "Turn off built-in
  profiles on Moza, or OpenDash's will not be listed." (`PanelUpdates.BuiltInOn`).
- **The download bar's head reads "Downloading"** (`PanelUpdates.Downloading`), not the kit's
  "Installing".
- **A download that does not finish says so on whatever OpenDash page is showing.** One that finishes
  says itself only on Updates or through the restart dialog.

## Soon

A greyed row is a promise with a ticket. It carries the Soon tag, its switch or choice drawn off, and
the hover "Coming soon · #381" (`PanelSoon.Tip`). Its title is a noun phrase. `PanelSoon.All` is the
registry, in the order the pages draw them, and `PanelSoonTests` holds it.

| page | row | ticket |
|---|---|---|
| Screens | Rev fill under the lights | [#381](https://github.com/xorob0/OpenDash/issues/381) |
| Screens | Spotter at the rev bar ends | [#96](https://github.com/xorob0/OpenDash/issues/96) |
| Screens | Pit page in the pit lane | [#383](https://github.com/xorob0/OpenDash/issues/383) |
| Screens | Pop-ups | [#112](https://github.com/xorob0/OpenDash/issues/112) |
| Screens | Edge lights for the delta | [#321](https://github.com/xorob0/OpenDash/issues/321) |
| Screens | Screen care | [#114](https://github.com/xorob0/OpenDash/issues/114) |
| Screens | Fit | [#319](https://github.com/xorob0/OpenDash/issues/319) |
| Screens | Circle tracker | [#320](https://github.com/xorob0/OpenDash/issues/320) |
| Screens | Launch | [#152](https://github.com/xorob0/OpenDash/issues/152) |
| Screens, Add sheet | Flags screen | [#116](https://github.com/xorob0/OpenDash/issues/116) |
| Screens, Add sheet | Your displays | [#85](https://github.com/xorob0/OpenDash/issues/85) |
| Screens | Zones instead of cards | [#146](https://github.com/xorob0/OpenDash/issues/146) |
| LEDs | Each LED in turn | [#434](https://github.com/xorob0/OpenDash/issues/434) |
| LEDs | Idle sweep | [#485](https://github.com/xorob0/OpenDash/issues/485) |
| LEDs | Engine start animation | [#300](https://github.com/xorob0/OpenDash/issues/300) |
| LEDs | Car data for AC, ACC and LMU | [#479](https://github.com/xorob0/OpenDash/issues/479) |
| LEDs | Pit limiter lights | [#509](https://github.com/xorob0/OpenDash/issues/509) |
| Matrix | RPM colour for everything | [#371](https://github.com/xorob0/OpenDash/issues/371) |
| Matrix | SimHub device | [#363](https://github.com/xorob0/OpenDash/issues/363) |
| Matrix | Priority order | [#505](https://github.com/xorob0/OpenDash/issues/505) |
| Rig | Real hardware | [#506](https://github.com/xorob0/OpenDash/issues/506) |
| Shortcuts | Rig test | [#511](https://github.com/xorob0/OpenDash/issues/511) |
| Shortcuts | Alert dismissal | [#510](https://github.com/xorob0/OpenDash/issues/510) |
| Settings | Fuel target per lap | [#326](https://github.com/xorob0/OpenDash/issues/326) |
| Settings | Tyre display | [#325](https://github.com/xorob0/OpenDash/issues/325) |
| Settings | Yellow flags | [#504](https://github.com/xorob0/OpenDash/issues/504) |
| Settings | Tyre wear | [#507](https://github.com/xorob0/OpenDash/issues/507) |
| Settings | Pit window open | [#507](https://github.com/xorob0/OpenDash/issues/507) |
| Settings | Incidents | [#508](https://github.com/xorob0/OpenDash/issues/508) |
| Settings | Hybrid battery low | [#110](https://github.com/xorob0/OpenDash/issues/110) |
| Settings | Alert display | [#512](https://github.com/xorob0/OpenDash/issues/512) |
| Settings | Sim time of day | [#128](https://github.com/xorob0/OpenDash/issues/128) |
| Settings | Screen dimming | [#128](https://github.com/xorob0/OpenDash/issues/128) |
| Settings | Theme | [#99](https://github.com/xorob0/OpenDash/issues/99) |
| Settings | Colour vision | [#129](https://github.com/xorob0/OpenDash/issues/129) |
| Settings | Colours | [#127](https://github.com/xorob0/OpenDash/issues/127) |
| Settings | Name | [#484](https://github.com/xorob0/OpenDash/issues/484) |
| Settings | Race number | [#484](https://github.com/xorob0/OpenDash/issues/484) |
| Settings | Logo | [#484](https://github.com/xorob0/OpenDash/issues/484) |
| Settings | Idle screen background | [#104](https://github.com/xorob0/OpenDash/issues/104) |

When a ticket lands, its row loses the tag and the hover, and leaves the registry in the same commit.

## New

A control that is new in this release carries the New tag after its title. The tag is drawn for one
release and removed at the next cut ([voice.md](voice.md#new-is-for-one-release)). Today it marks what
rc.7 could not do, which is more than the artboard tags:

- The sidebar's search; the Rig and Shortcuts pages, on their titles.
- Screens: Duplicate, the zone list's drag to reorder, every previous-page row, the pit wall's
  portrait layout, and the companion's Flag display and Quick glance.
- Shortcuts: night mode, brightness up and down, the companion's quick glance, every previous-page row.
- LEDs: a strip's own Brightness, Reverse direction and Effects.
- Matrix: the preview.
- Settings: Delta reference, Delta precision, Clock, Units and the Night mode button.
- Updates: Copy a support report, Open the log, and Every release on GitHub
  (`PanelUpdates.NewTagged`).

## Departures from the artboards

The canvas is the author's and is not edited from code, so every place the built word differs from an
artboard's is a row here, with the constant that holds it. A row marked *ruling n* is one of the twelve
rulings of #524, taken under the standing rule that [voice.md](voice.md) beats artboard copy. Each is a
constant on the page that owns it, read by every other page that draws it.

| artboard | built | why |
|---|---|---|
| Sidebar: three rising bars | the mark, `media/logo.svg` | [brand.md](brand.md) rejected the bars. `Ui.Mark` |
| every artboard: NEW, SOON and eyebrows in capitals | "New", "Soon", sentence case | [brand.md](brand.md): a label is in sentence case. `PanelSoon.NewTag`, `PanelSoon.Tag` |
| Sidebar: a tracked eyebrow on the live card | one trimming line, no tracking | WPF has no letter spacing, and a tracked eyebrow cannot trim. `PanelShell.LiveEyebrowHeight` |
| Sidebar: the live card at 83.3 px | 85 px | its lines sit on whole pixels, and nothing below moves when a session starts. `PanelShell.LiveCardHeight` |
| Sidebar: no rail | the rail, with night mode as a crescent toggle | the artboards draw one width; the icon sheet has no moon. `PanelIcons.Night` |
| Sidebar: the version and counts at 500 | SemiBold | the plugin does not bundle the 500 face. `PanelShell.VersionSize` |
| Sidebar: the search's label "Search settings" | "Search", and "Searches every setting." on the rail | the artboard's placeholder is "Search"; the rail has none. `PanelSearch.Placeholder`, `PanelSearch.RailTooltip` |
| every artboard: a 1200 px frame | the column fills the window | the frame is not a maximum. `PanelShell.ProseMaxWidth` |
| Main: "Try a flag or the spotter" | "Flags and spotter" | a label is a noun phrase. `PanelHome.TryTitle` |
| Main: "Dash brow isn't showing openDash" | "Dash brow's profile is not selected" | no contractions, and the wordmark is not spelled in a sentence. `PanelAttention.Find` |
| Main: "Its profile is installed but not selected on the device." | "Installed, but not selected in SimHub." | the title names the profile; the caption says where. `PanelAttention.UnselectedDetail` |
| Main: "Pick openDash Dash brow" | Select "Dash brow" | SimHub's own verb, and the name the profile is listed under. `PanelAttention.SelectSteps` |
| Main: "Car-specific · Porsche table" | "Car's own rev lights · Porsche 911 GT3 R" | the #369 switch's own noun; "table" is internal vocabulary. `PanelHome.CarLightsLine` |
| Main: "Profile not selected" | "Not selected in SimHub" | the LEDs card's word. `PanelHome.StripNotSelected` |
| Main: "Matrix 1 · gear" | "Matrix 1 · Gear" | the Idle display control's own case. `PanelLights.RestLabels` |
| Main: "send "openDash Rim" … from Dash Studio" | "Then assign "Rim" to its display in Dash Studio." | the dashboard is listed under the screen's name; one phrasing on Screens, Home and Updates. `PanelScreens.RestartDetail` |
| Main, Screens, Updates: "Rim isn't in SimHub yet", "Not in SimHub yet", "Waiting for a restart" | "Restart SimHub to load it" on the Screens card and fix box, Home's line and step, and the Updates row; Home's title still names the screen, "Rim is not in SimHub yet" | one phrase for one state; a card too narrow for it wraps it, never trims it (*ruling 1*). `PanelCopy.RestartToLoad` |
| Updates: "Unknown" for a strip whose profile the build does not ship | "Not installed", as the LEDs card and Home say it | one word for one state; the flag box's row, and any row while SimHub cannot be read, keep "Unknown" (*ruling 2*). `PanelLeds.NotInstalled` |
| LEDs, Updates, Main: "strip" and "LED profile" side by side | both stay: a *strip* is the device ("Add an LED strip", "No strips yet"), an *LED profile* is the file SimHub loads ("This build ships no LED profiles.") | two things, two nouns (*ruling 3*). `PanelUpdates.StripKind`, `PanelLightRows.NoProfiles` |
| Updates: "Your edited Rim was kept" as the card's title | heading "Kept copy" or "Kept copies", and "Your edited Rim was kept." as the first line under it | a heading is a noun (*ruling 4*). `PanelUpdates.KeptClause` |
| Updates: the kept card | shown whenever a kept copy exists; Put mine back leaves a folder edited since as it is and says so | the build used to hide it once the folder was edited again (*ruling 5*). `PanelUpdates.ShowsKept` |
| Rig: "What to emulate"; Matrix, LEDs, Settings: "What to preview" | "Preview" on all four; Rig's "Emulation" goes | a heading is a noun, and one noun for one set of chips (*ruling 6*). `PanelRigMap.ScenariosName` |
| Settings: the alert table's "When" | "Trigger"; Matrix's Warnings link is its plural, "Triggers" | a heading is a noun, and it covers Pit window open, which has no threshold (*ruling 7*). `PanelSettings.ThresholdColumn` |
| Matrix, Main: "No device in SimHub shows matrix 2", "is dark", "Matrix 2 · not set" | "Not shown in SimHub" on the Matrix card, Home's line, the Matrix fix box's title and Home's issue ("Left pillar is not shown in SimHub"); the matrix number is in the steps | one phrase for one state (*ruling 8*). `PanelMatrix.NotShown` |
| AddScreen: the second-copy note | the build's wording | ruled to stay as the build words it (*ruling 9*). `PanelAddScreen.Note` |
| Settings: the greyed "Tyres show" row's "Temperature" / "then Pressure" | "Temperature" \| "Pressure" | two nouns, no fragment; #325 owns the words once it builds the row (*ruling 10*). `PanelSoon.TyreDisplay` |
| Settings: "under [2] laps" for every value | "under [1] lap", "under [2] laps" | the unit agrees with its number (*ruling 11*). `PanelSettings.LapsUnitFor` |
| LEDs: the strip header's Update hover, "Updates this strip's profile in SimHub." | "Replaces the copy in SimHub, including your changes to it.", as on Matrix and Updates | the press says what it costs, one way on every page (*ruling 12*). `FlagBoxInstallPlan.Replaces` |
| Rig: "Limiter on" | "Pit limiter" | the word the rest of the panel uses. `PanelEmulation.Groups` |
| Rig: "Oil hot", "Water hot" | "Oil temperature", "Water temperature" | the Settings rows' names for the same alerts. `PanelSettings.OilTempTitle` |
| Rig: "Light the real hardware" | "Real hardware" | a greyed title is a noun phrase. `PanelRigMap.RealHardwareTitle` |
| Rig: a face tile's band reads its content | the band is drawn empty at rest | "Band D" is the panel talking to itself. `PanelRigMap.FaceBandFor` |
| Rig: PIT LIMITER written on band D | a limiter block over zone A | the dash draws it there. `PanelRigMap.FaceBandFor` |
| Rig: "Fuel · 12.4 L" on the band | "Fuel" as the dash's pop-up over zone A | the dash draws a pop-up, and the panel does not have the laps figure. `PanelRigMap.FuelPopUp` |
| Screens: "Flags" | "Flag display" | voice.md's own example. `PanelScreens.FlagDisplayTitle` |
| Screens: "Show the last lap after the line" | "Lap review" | a label is a noun phrase. `PanelScreens.LapReviewTitle` |
| Screens: the round block's cards | "Cards" | the artboard's noun, never the settings model's "Slots". `PanelScreens.CardsTitle` |
| Screens: "No button" in a zone cell and band D | "Not bound" | the Shortcuts page's word for the same fact. `PanelBindings.NotBound` |
| LEDs: "Add LEDs" | "Add an LED strip" | voice.md's own example beside "No strips yet". `PanelLights.AddBar` |
| LEDs: "Use the car's own rev lights" | "Car's own rev lights" | a switch names the thing (#369). `PanelLeds.CarRevLightsTitle` |
| LEDs: "Width", under "Rev lights" | "Rev light width" | one noun for the car's lights across the row and its caption, and a search result that says which width. `PanelLeds.MirrorFitTitle` |
| LEDs: "Centre shows" | "Centre display" | voice.md's own example. `PanelLeds.CentreDisplayTitle` |
| LEDs: "Flags animated" | "Flag animation" | a label is a noun phrase. `PanelLeds.FlagAnimationTitle` |
| LEDs: "Spotter uses the whole strip" | "Full-strip spotter" | voice.md's example of a switch labelled as a sentence. `PanelLeds.SpotterTitle` |
| Matrix: "The car's own shift points" | "Car-specific shift points" | voice.md's name for the car's tables, which is also Lovely Sim Racing's. `PanelMatrix.CarShiftPointsTitle` |
| Matrix: "At rest" | "Idle display" | "at rest" is the panel talking to itself. `PanelMatrix.IdleDisplayTitle` |
| Matrix: "Cars on" | "Mounting side" | a label is a noun phrase. `PanelMatrix.MountingSideTitle` |
| Matrix: "Slide in" | "Spotter bar animation" | a switch names the thing. `PanelMatrix.SpotterAnimationTitle` |
| Matrix: "Flash at redline" | "Redline flash" | a label is a noun phrase. `PanelMatrix.RedlineFlashTitle` |
| Matrix: "Matrix content: 2" | "RGB Matrix content" | SimHub's own label for the field. `PanelMatrix.ContentField` |
| Matrix: "Colour everything by RPM", switched on | "RPM colour for everything", switched off | a greyed title is a noun phrase, and nothing is on until #371 lands. `PanelSoon.RpmColourForEverything` |
| Matrix: "Arduino matrix" on the greyed device press | "Choose a device" | nothing is bound until #363 lands. `PanelMatrix.SimHubDeviceButton` |
| Shortcuts: "A wheel button, a button box, a key or a touch. SimHub saves them." | "A wheel button, a button box or a key." | a touch is not bound here, and the panel does not describe mechanism. `PanelShortcuts.IntroCaption` |
| Shortcuts: "Previous page, zone in focus" | "Band D · previous page", one row per zone | the zone in focus is a concept the panel has nowhere else. `PanelShortcuts.ZoneRow` |
| Shortcuts: "Run the Rig test", "Dismiss the alert on screen" | "Rig test", "Alert dismissal" | a greyed title is a noun phrase. `PanelSoon.RigTest`, `PanelSoon.AlertDismissal` |
| Shortcuts: the clash line's "Fine if you meant it." | dropped; a zone mid-sentence is "zone D" | voice.md has no asides; one way to write a zone in running text, as Screens writes it. `PanelShortcuts.Clashes` |
| Shortcuts: the filter's name "Show" | "Filter" | a name is a noun. `PanelShortcuts.FilterTitle` |
| Shortcuts: "Devices › Phone" | "Controls and events › NextScreen" | the card's name is OpenDash's, not a device SimHub lists. `PanelShortcuts.ControlsAndEventsCrumb` |
| Settings: "Delta against" | "Delta reference" | a label is a noun phrase. `PanelDataTab.DeltaTitle` |
| Settings: "Delta decimals · 0.00 \| 0.000" | "Delta precision · Hundredths \| Thousandths" | the values are words, with no digits. `PanelDataTab.DeltaPrecisionLabels` |
| Settings: "Show team names" | "Team names" | a switch names the thing. `PanelDataTab.TeamNameTitle` |
| Settings: "Next to a blue flag" | "Blue flag detail" | a noun phrase, not a prepositional fragment. `PanelDataTab.BlueFlagTitle` |
| Settings: the oil and water captions | "In SimHub's unit.", the default as the placeholder | the row says which unit to type. `PanelSettings.TemperatureCaption` |
| Settings: "over [70] %" | "over [70%]" | a lone % after a numeral is joined to it. `PanelSettings.Alerts` |
| Updates: "Something wrong?" | "Support" | a heading is never a question. `PanelUpdates.SupportTitle` |
| Updates: "Download 0.5.1" | "Download" | the version is in the card's heading. `PanelConfirmation.UpdateLabel` |
| Updates: "The last update replaced it." | "OpenDash replaced it. Your copy is still here." | an update, Reinstall everything and the Screens page's reinstall each keep a copy, and the card cannot tell which. `PanelUpdates.KeptCaption` |
| Updates: "Repair everything" | "Reinstall everything" | the press reinstalls, and its hover and the Screens page say so; not yet ruled. `PanelConfirmation.ReinstallLabel` |
| Screens: "In band D" | "Band D" | the zone's name as the picture draws it; not yet ruled. `PanelScreens.FlagLabels` |
| Updates: "Car tables:" in the support report | "Lovely Car Data:" | one name for the data. `PanelUpdates.Report` |

**Two groups carry two words each, as the artboards do**, and are left for the author since the canvas
is theirs: the rig's night mode and brightness are "Lights" on Shortcuts (its rig group) and "Lighting"
on Settings (its section); low fuel, oil and water are "Warnings" on Rig (its scenario group) and on a
Matrix panel (its layer), and "Alerts" on Settings and Shortcuts. voice.md's one-word-per-thing rule
would pick one noun per group.

**Copy no artboard carries**, each the build's and each pinned: the Add sheet's note for a second
companion, the Edit sheet's Dashboard caption ("Reinstalls this screen's dashboard, at its saved name and
size."), Save's and Duplicate's hovers, the Missing fix box's "This screen's settings are kept.", the
rig-wide Rev bar's caption, the web view's empty hover, the LEDs page's device captions
(`PanelLights.OneDeviceCaption`), and the Updates page's "Not checked yet".

### Waiting on the author

Questions the pages raised and no ruling has settled:

- Rev ring on a round screen and Rev bar on a card face: two names for one rig-wide setting.
- The "Orientation" step title on the Add sheet for a pit wall or companion.
- Both fixed pit wall panels reading "Leaderboard" (`PanelRigMap.BoardLabel`).
- The Matrix priority: whether Spotter is ranked or an overlay over everything, as the flag box draws
  it ([#505](https://github.com/xorob0/OpenDash/issues/505)).
- The Mid revs chip on Matrix, and its pair's name: the Rig page says Idle and Mid revs.
- The Rig frame: framed at the room's width, or dots painted on a stretching frame.
- The companion's paging crumbs: whether the trail stays, since a companion in a window is not under
  Devices.
- Whether "Car's own rev lights" becomes "Car-specific", the word the Matrix row and the rev style
  chooser use.

## The geometry is not in the token file

Every colour the panel draws is a `Theme` constant, and `ThemeTests` holds each one against the token it
mirrors in `design/tokens.json`. The geometry is not tokens. These are owed to the token file, and this
section is the record that they are missing rather than forgotten:

| family | today | read off |
|---|---|---|
| the frame: sidebar 216, rail 56, gutters 44/32/20, nav item 40, live card 85, the widths 1000 and 760 | `PanelShell` | `Sidebar` and every page's `<main>` |
| the measures: 620, 520, 880 | `PanelShell.ProseMaxWidth`, `PanelShell.RowCaptionMaxWidth`, `SettingsControl.BodyWidth` | `.cap` |
| a section's gap: 28, 26, 22, 18 by page | `PanelShell.SectionGap` | each page's `<main>` |
| the device card: padding, name, state line 11/6/6 | `PanelKit` | `.scard`, `.dcard`, `.mcard` |
| the chips: 26, 28, 30 high | `PanelKit` | `.chip`, `.key` |
| the fix box, the sheet's steps, the segmented button | `PanelKit` | `.fix`, `.step`, `.seg` |
| the progress bar | `PanelMetrics` | `Updates` |

`PanelShellTests` and `PanelKitTests` pin each number. #176 named `panel.tabs`, `control.tab` and
`control.screenCard`; the tab bar and the old screen card are gone, and the token names are the
author's to choose.

## Requests to the shell and the kit that were not applied (#523)

The page agents' requests to the shell and kit were applied after the merge. Two were not:

- **Record that `PanelLightRows.DotHex` (Updates) reads `PanelMatrix.ProfileRow`.** That read no longer
  happens. `DotHex` is a fixed ink for each state and reads no page's table, so `ProfileRow` belongs to
  the Matrix page alone. The ownership table in `SettingsControl.cs` says so instead.
- **Drop `RowAction.Style` as read by no drawn row.** The Matrix page's profile press reads it since the
  merge, to choose between its primary and its ghost press, so it stays. Only `PanelCopy.ScreenRow`, which
  nothing drew, went.
