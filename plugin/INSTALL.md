# Installing the OpenDash plugin

The plugin does two things: it adds an "OpenDash" entry to SimHub's left menu, whose pages are where
you add the screens your rig has and choose what each of them shows, and it installs into SimHub the dashboard
of each screen you add there, and of no other. It reads no telemetry and renders nothing; SimHub
does that.

You need SimHub 9.12.6 or later on Windows. `OpenDash-plugin.zip` contains `OpenDash.dll`,
this file and `OFL.txt`, the licence of the Barlow typefaces the dashboards ship.

## Install

1. Close SimHub.
2. Unzip `OpenDash-plugin.zip` and copy `OpenDash.dll` into SimHub's install folder, the one
   that contains `SimHubWPF.exe` (by default `C:\Program Files (x86)\SimHub`). Plugins live
   directly in that folder, not in a subfolder.
3. Unblock the file. Windows marks files downloaded from the internet and .NET refuses to load
   a blocked plugin. Right-click `OpenDash.dll`, choose Properties, tick "Unblock" at the bottom
   of the General tab and click OK. If there is no "Unblock" box, the file is not blocked.
   The same from PowerShell: `Unblock-File "C:\Program Files (x86)\SimHub\OpenDash.dll"`.
4. Start SimHub. It notices the new plugin and shows a window titled "New plugins have been
   detected !", in which OpenDash is listed with a switch at the right of its row, and that switch
   is off. Switch it on, whereupon a second switch, "Show in left main menu", appears under the
   description, off as well; switch that one on too, then press **Ok**. SimHub does not ask to
   restart, and none is needed.
5. "OpenDash" now appears in SimHub's left menu. If the second switch was left off, it is instead a
   tab of **Additional plugins**, an entry of the same menu, where it works in the same way; the
   same two switches are also under **Add/remove features**, at the foot of that menu, where the
   second can be switched on later. Open the **Screens** page from OpenDash's sidebar. It starts
   empty: the plugin writes no dashboard until you add a screen, so a first start puts nothing new in
   SimHub's dashboard list. Press **Add a screen**, choose its kind, the size that matches the
   display from Sizes below and its name, and press **Add screen**: the plugin writes that one
   dashboard into its own folder under `DashTemplates` (`OpenDash 1280x480` for the first screen of
   that size). Add one screen per display, then restart SimHub, which reads its list of dashboards
   only when it starts. Until then each new screen's card reads "Restart SimHub to load it".
6. Assign each dashboard to its display. OpenDash is a normal SimHub dashboard from here on: in
   Dash Studio each one is listed under the name you gave its screen, and you open one in a window,
   send it to a USB or HDMI display, or point a phone or tablet at it exactly like any other
   dashboard. Nothing in the plugin launches it; that is SimHub's job.

## The pages

OpenDash's sidebar lists eight pages. The search box above them finds any setting by its name and
opens the page it is on.

| Page | What is on it |
|---|---|
| **Home** | What needs attention on your rig, each with its fix; what each screen, strip and matrix is doing right now; quick controls for brightness and night mode |
| **Rig** | Your screens, strips and matrices as tiles you arrange, and a Preview that plays a flag, the spotter, the pit lane, a warning or the revs on all of them at once |
| **Screens** | A card for each screen and the Add a screen tile; below the cards, everything the selected screen shows |
| **LEDs** | Your LED strips, each with its profile, its SimHub device and its effects; the rev lights; Car Data, under Every strip |
| **Matrix** | The flag box profile, on the title's line; your matrices, and what may take each one over |
| **Shortcuts** | Every wheel button in one list: a card for each screen, then Lights and Alerts |
| **Settings** | What every screen shares: Race data, Flags, Alerts and Lighting |
| **Updates** | A newer release when there is one, Check for updates, the In SimHub table, Reinstall everything and Support |

A greyed row tagged Soon is one a later release builds, and its hover names the ticket. A control
tagged New is new in this release.

## The face

A face is **five parts**, and every rectangular size is the same five.

- The **rev bar** across the top, with its shift lights, in a recessed well. The lights come on at
  the RPMs **your sim publishes for the car you are driving**, so a car released this morning is
  right with nothing to set up; for a car that publishes none, the bar falls back to SimHub's own
  per-car bands, the ones you tune on SimHub's Car Settings page. Nothing in the plugin chooses
  between the two and there is no setting for it: the bar asks the car, every frame. Each screen can
  also do without it: switched off, the well goes with it, the bar rises into its room and the body
  grows by what it gained. The band keeps its place, because it is measured from the bottom edge.
- The **bar** below it, carrying what does not change during a lap: two fields at each end,
  chosen from a catalogue of ten, and between them the car settings your sim publishes.
- The **body**, which is **zone B, zone A and zone C** side by side. Zone A is the narrow middle
  column holding the gear, because the gear is read by reflex; B and C flank it and hold tables,
  which are read deliberately.
- **Band D** across the foot.

**Each zone shows one page at a time and a wheel button cycles it.** That is the whole idea, and
it is what replaced the twelve fixed slots of 0.1.x. Zone A chooses among four pages built around
the gear; zones B and C choose among the same pages the companion has; band D chooses
among eight that suit a wide, short strip. The bar is not a zone and does not cycle: carrying what
stays still is what earns it the space.

A page is never scaled to fit. It is laid out for the shape of the box it is given and sheds its
secondary rows before it shrinks its numerals, so a bigger screen shows more in each zone rather
than the same thing larger.

**With no game running, the screen shows the openDash wordmark, the time and "no game running".**
That idle screen is inside every dashboard and there is nothing to set up or point at: SimHub
switches to it by itself, and back to the face when a game starts.

## Sizes

| Dashboard | Screen | |
|---|---|---|
| OpenDash | 1920 x 480 | the reference face |
| OpenDash 1280x480 | 1280 x 480 | **the large size** |
| OpenDash 1280x400 | 1280 x 400 | a shorter body, the same zones |
| OpenDash 1280x720 | 1280 x 720 | the tall body lets zone C list the field |
| OpenDash 850x480 | 850 x 480 | **the base size**: narrower zones, fewer cells in the car settings |
| OpenDash 800x480 | 800 x 480 | |
| OpenDash 800x286 | 800 x 286 | no bar: the height is not there |
| OpenDash 600x686 | 600 x 686 | portrait, zone A above B above C, one bar field per end |
| OpenDash 480 round | 480 x 480, round | still the twelve-slot face; see below |
| OpenDash 800 round | 800 x 800, round | still the twelve-slot face; see below |

Take the one that matches your display. If nothing matches exactly, **850 x 480 is the base
size** and the one to try first: it is the common wheel-mounted DDU, and it is the tightest face
that still carries all five parts. **1280 x 480 is the large size**, for a wider DDU. SimHub scales
whichever face you assign to whatever display you assign it to, so a mismatched size is not broken,
only drawn at the wrong proportions and the wrong density; take the nearest shape rather than the
biggest number.

Four more dashboards are not faces for the wheel but second screens, described below.

| Dashboard | Screen | What it is |
|---|---|---|
| OpenDash Companion | 850 x 480 | one module at a time, paged from a wheel button |
| OpenDash Companion portrait | 480 x 850 | the same, for a phone stood on end |
| OpenDash Pit wall | 1920 x 1080 | three pages for someone who is not driving |
| OpenDash Pit wall portrait | 1080 x 1920 | the same in one page, for a screen on its side |

**The two round faces are still the twelve-slot design, on purpose.** A round face becomes zones on
a ring before 1.0 (#487) -- the rev arc it already has, one zone in the middle of the disc, its card
rectangles as small zones of their own, and the flag on the ring -- and until that work is done the
two ship as they are. Of the dashboards the plugin installs they are the only ones set up on the
Screens page in cards rather than in zones, Card 1 to Card 12, numbered the left side first, then the
right side, then the bottom. The cards are shared by every round screen.

**Every screen keeps its own settings.** A rig with a face on the wheel and another beside it
configures them apart: the zones, the bar and the glance of the 1920 face are separate from those
of the 850, and each has its own wheel buttons. The Screens page draws a card for each screen, and
what is set below the cards belongs to the one selected.

## Changing a screen

Each screen's card has three presses.

- **Edit** changes the screen's name, size or orientation, or reinstalls its dashboard. **Save**
  saves your changes, and your settings and bindings are kept: a screen's properties and wheel
  buttons keep their names whatever the screen is called. The sheet's Dashboard row has
  **Reinstall**, which reinstalls this screen's dashboard, at its saved name and size. When you have
  edited that dashboard in Dash Studio, the row says so first; reinstalling replaces your version and
  keeps a copy, which **Put mine back** on the Updates page restores.
- **Duplicate** adds another screen set up like this one.
- **Remove** removes the screen, its dashboard and its settings. Any wheel button you bound to it
  stops working.

## Wheel buttons

Cycling a zone is a wheel button, bound in SimHub the way any action is. The **Shortcuts** page lists
every one, a card for each screen, with SimHub's own binder on each row, so you do not have to go
looking in Controls and events. On the Screens page, a face's Next page and Previous page rows show
what each zone is bound to, and a press on the binding opens the Shortcuts page.

| Row | What it does |
|---|---|
| Zone A to Band D · next page | advances that zone to its next ticked page |
| Zone A to Band D · previous page | steps that zone back to its previous ticked page |
| Quick glance | while held, shows one chosen page in one chosen zone, and returns on release |

The Shortcuts page's **Lights** card binds **Night mode**, **Brightness up** and **Brightness down**,
for every strip and matrix. Its filter shows every row, the bound ones or the ones not bound, and a
line under a row says when one button is bound to two things.

A quick glance is always bound as a hold, whatever press type you pick in SimHub's binding
dialog: SimHub only tells OpenDash a button was released under that press type, and a glance bound
any other way would appear and vanish in the same frame.

There is a set of these **per screen**, named for the screen: the actions of the reference face are
`OpenDash.Face1920x480CycleZoneA` through `CycleZoneD`, `CycleZoneABack` through `CycleZoneDBack`,
and `OpenDash.Face1920x480HoldQuickGlance`. A rig with one screen binds the ones it wants and can
ignore the rest.

Which pages a zone cycles through is yours: on the Screens page each zone has a list of its pages
with a tick against the ones in the cycle, in the order you drag them to, so a zone you want to hold
on two pages steps between those two. The last ticked page of a zone cannot be unticked, because a
zone keeps at least one page.

## The companion

The companion shows one module at a time: a big, calm page for a phone or a tablet beside the
wheel. The modules are listed under Modules when the companion is selected on the Screens page,
and each has its own tick. A module that is off is skipped entirely. First module is the one a
session opens on.

Paging is SimHub's, not OpenDash's. Tap the left or right half of the screen to change module. For
a wheel button, open the device or window the companion runs on in SimHub, go to its "Controls and
events", and bind **NextScreen**, with **PreviousScreen** to go back. Those bindings belong to that
device, so the button that pages your companion does not page your dash. The Screens page says the
same under the companion's Next module.

The **quick glance** is OpenDash's. Choose its module under Quick glance on the Screens page and bind
its button on the Shortcuts page, on the companion's card: hold the button and the companion shows that module, even one you have switched off, and
release it and the companion goes back to the module it was on, wherever you had paged to. For a
second after the release a tap does nothing while it moves back.

Three modules are off when you install: **Energy**, **Damage** and **Track rivals**. iRacing
publishes no virtual energy, no damage values at all and nothing a segment-by-segment rival
comparison could be built from, so those pages say what they cannot show rather than drawing
zeros. Switch them on for a sim that does carry the data.

## The pit wall

The pit wall is for a screen someone watches rather than drives: the whole field with gaps,
intervals, sectors and stops, the driver's own lap next to it, and **data zones** you choose the
contents of. The landscape dashboard has three pages (race, tower, telemetry) and shows the one
chosen in its Page on screen row on the Screens page: it does not change while you race, and
NextScreen does not page it. The portrait one has a single page, whose zones are set under Portrait
layout.

The landscape zones are set on the same page, under Zones, in a group per page: zones A and B of the race
page, the wide zone and zones A and B of the tower page, and zones A to C of the telemetry page.
Each can show any of eleven pages (fuel, tyres, opponents, pit view, relative, leaderboard, lap
history, web view, inputs, radar, sectors); the wide zone has six of its own. The **Web view**
page shows any web page you like: put an http or https address in the Web view address box. An
address that is neither is ignored.

The leaderboard is one list with a class chip on each row rather than a block per class. SimHub
exposes per-class rows only for your own class, so class headings would be a picture of data
that is not there.

## The flag box

If you have an **8x8 LED matrix** on an Arduino — the printed box a lot of people have beside the
screen — OpenDash drives it too: the flag that is out, the gear, the pit state, a car alongside,
and the warnings you would otherwise miss.

Install it from the top of OpenDash's **Matrix** page. The line beside the title names the profile,
"OpenDash Flag box", with the version SimHub holds or "Not installed", and a press. Press
**Install**. The press says what it will do before you press it, the dot beside the line says what
SimHub holds now, and hovering the line says the rest. Then press **Add a matrix**, name it and
press **Add a matrix** again. The sheet says the steps left: select "OpenDash Flag box" on your
matrix's device in SimHub and set RGB Matrix content to the matrix's number, 1 for the first. A box
with no matrix stays dark.

**OpenDash never installs it on its own.** A profile paints hardware you own, so it is asked about
once rather than assumed. It also only ever recognises its own profile, so one you made yourself is
never touched. When OpenDash carries a newer profile than SimHub holds, the press reads **Update**,
and once SimHub holds the current one it reads **Reinstall**; either press replaces the copy in
SimHub, including any changes you made to it there. While your rig has a matrix, the profile is also
a row of the In SimHub table on the Updates page.

If SimHub's matrix settings cannot be reached, the line offers no press. OpenDash writes the profile
to `SimHub\OpenDash\OpenDash Flag box.ledsprofile` and shows its path under the line, with
**Copy to SimHub's import folder** beside it, and you can import that file through SimHub's own
profile import. A copy of that file you have edited is left as it is; delete it and restart SimHub
for OpenDash's current one.

Before any of that, set the matrix's **rotation** and **serpentine** on the device in SimHub. Those
belong to SimHub rather than to OpenDash, because the right values depend on which corner your data
cable enters — and if they are wrong, the picture comes out sideways or shredded and the profile
looks broken when it is not.

[docs/flag-box.md](https://github.com/xorob0/OpenDash/blob/main/docs/flag-box.md) is the full
guide: what every picture means, what the box does not do and why, and what to check when it looks
wrong.

## Wheels made with FanaBridge

FanaBridge is a third-party SimHub plugin that puts Fanatec wheels into SimHub's Devices view,
with profiles of its own for the wheels it knows and a wizard for the ones it does not.
**Whether an OpenDash strip reaches a FanaBridge wheel is not yet known.** It has been reported
that a strip cannot be added to wheels made with the wizard, and the cause is under investigation
in [#437](https://github.com/xorob0/OpenDash/issues/437). Nothing below is a fix; it is what to
look at, and what a report needs.

- **Is the wheel in SimHub's Devices view, and can SimHub's own LED editor save a profile to it?**
  If SimHub's own editor cannot, OpenDash will not either: a strip goes into the list that editor
  shows.
- **Is it in OpenDash's LED device list** on the LEDs page's *Add an LED strip* sheet? A device
  with some sign of LEDs that OpenDash sees and does not offer is named under that list as having
  no LEDs OpenDash can reach. Every device in SimHub's Devices view, named there or not, has a line
  in SimHub's log, `Logs\SimHub.txt`: one beginning `[OpenDash] LED device not offered` with the
  reason, or `[OpenDash] LED device offered`.
- **Are the wheel's built-in profiles switched off?** A device that ships its own profiles and
  lists them shows only those, so OpenDash's is installed and not listed. When OpenDash sees them
  switched on, the line after adding the strip ends in the caution colour with *Turn off built-in
  profiles on your device, or OpenDash's profiles will not be listed.* Switch them off on the wheel's LED
  page in SimHub and select the strip's profile there.
- **Does the strip survive a SimHub restart?** A strip whose SimHub device row reads *Device not in
  SimHub* after a restart is pointed at a device id SimHub no longer has.

If it still does not work, add to #437 what each of those showed, the `[OpenDash] LED device` lines
from the log, the exact line OpenDash printed after adding the strip, your FanaBridge version, and
whether the wheel is one FanaBridge supports or one made with its wizard. The same test on a wheel
FanaBridge supports natively, if you have one, is the most useful comparison there is.

## Settings

Every change on OpenDash's pages takes effect immediately on a running dashboard; there is nothing
to save and no restart. The exception is a screen's Edit sheet, whose changes take effect when you
press Save.

| Where | Setting | Values |
|---|---|---|
| Screens, a face's zones | Zone A, Zone B, Zone C, Band D | the page each opens on, which pages it cycles, and in what order |
| Screens, a face's zones | My class only | on, off; a zone's lists show your own class alone |
| Screens, a face's Info bar | The two fields at each end of the bar | any of the ten bar fields |
| Screens, a face | Rev bar | On (the car's own shift lights, SimHub's bands where the car publishes none), Off (gives the bar's room back to the zones; pick it if your wheel has shift lights of its own) |
| Screens, a face | Flag display | Band D, Full screen |
| Screens, a face | Lap review | Off, Races, Always; shows your last lap for four seconds after the line |
| Screens, a face | Quick glance | the zone and page a held button shows |
| Screens, a round screen | Card 1 to Card 12 | any of the thirteen cards, shared by every round screen |
| Screens, a round screen | Rev ring | On, Off, for every round screen, the speedo wherever it is shown and any face whose own Rev bar you have not set; a rectangular card face calls the same row Rev bar |
| Screens, a companion | Modules | each module on or off; an off module is skipped when you page |
| Screens, a companion | First module | the module a session opens on |
| Screens, a companion | Flag display | Off, Bar, Full screen |
| Screens, a companion | Quick glance | the module a held button shows |
| Screens, a pit wall | Page on screen | Race, Tower, Telemetry |
| Screens, a pit wall | Zones | zones A and B of each page, and C of Telemetry: any of the eleven zone pages; the wide zone of Tower: any of the six wide pages |
| Screens, a pit wall | Portrait layout | the zones of a portrait pit wall |
| Screens, a pit wall | Web view address | an http or https address, or empty |
| Screens, a pit wall | My class only | on, off |
| Screens, a pit wall | Flag display | Off, Bar, Full screen |
| Screens, a pit wall | Quick glance | the zone and page a held button shows |
| Shortcuts, a screen's card | Each zone's next page and previous page, and Quick glance | the wheel button, button box or key bound to it |
| Shortcuts, Lights | Night mode, Brightness up, Brightness down | the wheel button, button box or key bound to it |
| Settings, Race data | Position | Overall, Class |
| Settings, Race data | Delta reference | Session best, All-time best, Last lap (iRacing's own delta to the lap before this one; level until a lap has been completed) |
| Settings, Race data | Delta precision | Hundredths, Thousandths; the delta takes the same room either way |
| Settings, Race data | Session progress | Auto, Laps, Time |
| Settings, Race data | Driver names | Liam Byrne, L. Byrne, B. Liam, Byrne Liam |
| Settings, Race data | Team names | on, off; names the team instead of the driver, and keeps the driver where the sim has no team |
| Settings, Race data | Clock | 14:32, 2:32 PM |
| Settings, Flags | Blue flag detail | Nothing, Class, Position and class |
| Settings, Flags | Flags in the pit lane | on, off |
| Settings, Alerts | Low fuel | under a number of laps |
| Settings, Alerts | Oil temperature, Water temperature | over a number in SimHub's unit; empty for the default, 120 °C (248 °F) for oil and 110 °C (230 °F) for water |
| Settings, Lighting | Brightness, Night brightness | the LEDs' brightness by day and at night; SimHub's device brightness applies on top |
| Settings, Lighting | Night mode | on, off |
| Updates | Check for updates | on, off; asks GitHub once a day and sends nothing about you |

The same page may sit in two zones at once. The page says so in amber and does not stop you: two
zones on the relative is a choice, not a mistake. The cards of a round screen say the same of a
card in two of them.

The settings are stored by SimHub in `PluginsData\Common\OpenDash.GeneralSettings.json` and are
also visible to any dashboard or LED profile as properties under the `OpenDash` prefix. The face
properties carry the screen they belong to: `OpenDash.Face1920x480ZoneA` is the page zone A of the
reference face is showing, `Face1920x480ZoneAPages` which of its pages are enabled,
`Face1920x480ZoneAStart` the one it opens on, `Face1920x480BarLeft1` a bar field and
`Face1920x480QuickGlance` the glance, with the same set for every other size. Alongside them are
`OpenDash.ShiftLights`, `OpenDash.PositionMode`, `OpenDash.DeltaReference`,
`OpenDash.DeltaPrecision`, `OpenDash.SessionProgress`, `OpenDash.RevBar`, `OpenDash.Slot01` to
`OpenDash.Slot12`, `OpenDash.CompanionModule01` to `CompanionModule21`, `OpenDash.PitWallPage`, the
pit wall's zones named for their page and letter (`OpenDash.PitWallRaceA`, `PitWallTowerWide`,
`PitWallTelemetryC` and the rest) and `OpenDash.WebViewUrl`.

`OpenDash.Slot01` to `OpenDash.Slot12` are read by two of the dashboards the plugin installs:
`OpenDash 480 round` reads the first two and `OpenDash 800 round` the first six. They are the card in
each slot of those faces, and they stay: the round faces keep the twelve-slot design on purpose, and
the release that gives them zones is the one that will say what happens to the twelve properties. The
`OpenDash slots <size>` faces that earlier releases published read them as well, four to twelve each,
so one you installed by hand from such a release is reading the twelve too.

`OpenDash.RevBar` is `shift` or `off`, and it is what the Revbar row of a round face writes. The round
faces and the companion's speedo read it; a zone face reads its own, `OpenDash.Face1920x480RevBar` on the
reference face, once its own Revbar row has been set, and the rig's until then. A settings file that still
says `rpm`, the plain bar earlier releases offered, reads as `shift`. `OpenDash.ShiftLights` is the
deprecated alias kept beside it, true only in the `shift` state, so a dashboard or an LED profile
written against it still reads.

## Update

Close SimHub, replace `OpenDash.dll` with the new one, unblock it and start SimHub. When the
dashboards embedded in the new plugin are newer than the installed ones, the plugin replaces the
`DashTemplates\<name>` folder of every screen on your rig on that start, a second screen of the
same size included, and keeps the previous folder as `DashTemplates\<name>_backup.zip` (for
example `OpenDash 1280x480_backup.zip`, and `OpenDash Rim_backup.zip` for a second 1280x480 screen
named Rim).

A dashboard you have edited in Dash Studio is **not** replaced silently. The plugin notices that
the folder no longer holds what it wrote and leaves it alone. It says so in SimHub's log and on the
Updates page, where the dashboard's row of the In SimHub table reads "Update available" and its
hover reads "You have edited it. Reinstall everything replaces it." Pressing **Reinstall
everything**, under the table, asks first: its line names each dashboard you edited, and the press
reads **Replace anyway**. Pressing that replaces them, and the copy each one took is kept under a
name no later update reclaims. A Kept copy card then shows on the Updates page, and **Put mine
back** on it restores your version. A dashboard OpenDash has never seen before is adopted as it is, because an edit
made before OpenDash started watching cannot be told from an untouched folder.

The plugin can also tell you when a newer release exists. It asks GitHub once a day, sends nothing
that identifies you, and can be switched off with **Check for updates** on the Updates page, in
which case nothing is fetched at all; **Check now** asks at once. Nothing is ever installed without
being asked for. A newer release shows as a card at the top of the Updates page, headed with its
version, with its release notes and **Download**. Download fetches the new plugin with the
dashboards inside it, and the card then reads "OpenDash is downloaded. Restart SimHub to finish
updating." A dialog offers to close SimHub and start it again there and then. That start puts the
new plugin in place and brings the dashboards up to date as described above, and it replaces one
you edited only if you pressed **Replace anyway** when Download asked.

**Coming from 0.1.x.** The face changed: what was twelve fixed slots is now four zones you cycle
with a wheel button, under the same dashboard names. Your old face is no longer published: the plugin
is the only way in, and the twelve-slot faces are built only so that the two designs can be compared
from a local build until the card faces are retired. Your slot settings are not lost; they still
drive the two round faces, and an `OpenDash slots <size>` face you installed by hand from an earlier
release keeps reading them.

## Uninstall

Close SimHub, delete `OpenDash.dll` from the SimHub folder and, if you want the dashboards gone
too, delete the folders of the Sizes table under `DashTemplates` (`OpenDash`,
`OpenDash 1280x480` and the rest) and the folder of any second screen of a size, which is
`OpenDash` followed by the name you gave it, with the `.zip` copies kept beside them. The settings
file named above can be deleted as well. Deleting it alone does not remove the settings, however,
since SimHub keeps copies of it in `PluginsData\Common\_Backups`, named
`OpenDash.GeneralSettings_b*.json`, and restores a missing settings file from them at its next
start; delete those copies with it, or the previous rig comes back. The fonts copied into
`DashFonts` (Barlow and openDash Display) are harmless and shared with other dashboards; an update
that replaces one of them sets the older copy aside in `DashFonts\_Backups`, where it can be
deleted.

## Troubleshooting

- SimHub never asked about the plugin, or shows an error about loading it: the file is
  blocked (step 3) or it is not in the SimHub folder itself (step 2).
- A screen's card reads "Restart SimHub to load it": SimHub reads its list of dashboards only when
  it starts. Restart SimHub, then assign the dashboard to its display in Dash Studio.
- Home lists what needs attention on your rig, each with its fix. Start there when something is
  wrong.
- Home reads "OpenDash could not read its settings" and the rig is empty: the settings file was
  damaged, for example by a hand edit, and OpenDash started on its defaults. It kept the file as
  `PluginsData\Common\OpenDash.GeneralSettings.unreadable.json`. Close SimHub, correct that file or
  take an earlier one from `PluginsData\Common\_Backups`, save it as `OpenDash.GeneralSettings.json`
  and start SimHub.
- The In SimHub table on the Updates page has a row for each dashboard on your rig and each LED and
  matrix profile, with the version SimHub holds and its state. Hover a row for the step that
  changes it.
- A dashboard's row reads "Missing" or "Not installed": its folder is gone from `DashTemplates`, or
  the plugin could not write it there. Check that SimHub can write to its own folder, then press
  **Reinstall everything**, which writes every dashboard on your rig again.
- A row reads "Install failed": its hover says to see SimHub's log, `Logs\SimHub.txt`, where the
  reason is on the lines prefixed `[OpenDash]`. **Open the log**, under Support on the Updates page,
  opens it, and **Copy a support report** copies the versions, the devices and the last 200 log lines
  to paste into an issue.
- A dashboard's hover says you have edited it: OpenDash will not overwrite somebody's work without
  being told twice. Press **Reinstall everything**, then **Replace anyway**, to replace it; a copy
  is kept either way.
- The dashboard shows the default pages although you changed them: the dashboard reads the
  settings through the plugin's properties, so the plugin has to be enabled; check its switch
  under **Add/remove features**, at the foot of SimHub's left menu.
- A wheel button does nothing: check that you bound the action of the screen you are looking at.
  Each screen has its own, so `Face1920x480CycleZoneB` moves the 1920 face and not the 850 beside
  it.
- A zone will not stop on the page you want: that page is probably not ticked in the zone's page
  list, so the cycle steps past it.
- A round face does not show the card you assigned: a round face reads only its first cards,
  Card 1 and Card 2 on the 480 and Card 1 to Card 6 on the 800, so a card in a higher one is never
  drawn. Assign it to a lower card number.
- The companion does not change page when you press the button: the binding is on the device,
  not in OpenDash. Open the "Controls and events" of the device or window the companion runs on
  and bind NextScreen there; a button bound on your dash's device pages the dash and not the
  companion.
- A wheel is in SimHub's Devices view but not in the LED device list when adding a strip: SimHub's
  log says why on the line beginning `[OpenDash] LED device not offered`, and the list names it
  underneath when OpenDash found any sign of LEDs on it. For a wheel made with FanaBridge, see
  [Wheels made with FanaBridge](#wheels-made-with-fanabridge).
- The track map or the radar is empty: both are drawn from SimHub's recorded outline of the
  track, which appears after a lap has been recorded there.
- A tyre pressure or a temperature reads `--`: iRacing reports pressures from the last pit stop
  only, and reports nothing at all before the car has been on track.
- A cell of the car settings strip is missing: your car does not publish that setting, and a strip
  closes over what it cannot show rather than drawing an empty box.
