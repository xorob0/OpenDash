# Installing the OpenDash plugin

The plugin does two things: it installs the OpenDash dashboards into SimHub, one per screen
size plus the two companions and the two pit walls, and it adds an "OpenDash" page to SimHub's
left menu where you choose what each of them shows. It reads no telemetry and renders nothing;
SimHub does that.

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
4. Start SimHub. It notices the new plugin and asks whether to enable it; accept. If SimHub
   asks to restart, restart it.
5. "OpenDash" now appears in SimHub's left menu. Open it: the Dashboard section at the bottom
   should say "openDash <version> · 14 dashboards" and "Up to date", which means every dashboard
   was extracted into its own folder under `DashTemplates` on that start (`openDash`,
   `openDash 1280x480` and so on, see Sizes below).
6. Assign a dashboard to a display. Pick the size that matches the display from the Sizes table;
   OpenDash is a normal SimHub dashboard from here on: in Dash Studio the sizes are listed as
   "openDash", "openDash 1280x480" and so on, and you open one in a window, send it to a USB or
   HDMI display, or point a phone or tablet at it exactly like any other dashboard. Nothing in
   the plugin launches it; that is SimHub's job.

## The face

A face is **five parts**, and every rectangular size is the same five.

- The **rev bar** across the top, with its shift lights, in a recessed well.
- The **bar** below it, carrying what does not change during a lap: two fields at each end,
  chosen from a catalogue of ten, and between them the car settings your sim publishes.
- The **body**, which is **zone B, zone A and zone C** side by side. Zone A is the narrow middle
  column holding the gear, because the gear is read by reflex; B and C flank it and hold tables,
  which are read deliberately.
- **Band D** across the foot.

**Each zone shows one page at a time and a wheel button cycles it.** That is the whole idea, and
it is what replaced the twelve fixed slots of 0.1.x. Zone A chooses among four pages built around
the gear; zones B and C choose among the same twenty-one pages the companion has; band D chooses
among eight that suit a wide, short strip. The bar is not a zone and does not cycle: carrying what
stays still is what earns it the space.

A page is never scaled to fit. It is laid out for the shape of the box it is given and sheds its
secondary rows before it shrinks its numerals, so a bigger screen shows more in each zone rather
than the same thing larger.

## Sizes

| Dashboard | Screen | |
|---|---|---|
| openDash | 1920 x 480 | the reference face |
| openDash 1280x480 | 1280 x 480 | |
| openDash 1280x400 | 1280 x 400 | a shorter body, the same zones |
| openDash 1280x720 | 1280 x 720 | the tall body lets zone C list the field |
| openDash 850x480 | 850 x 480 | narrower zones, fewer cells in the car settings |
| openDash 800x480 | 800 x 480 | |
| openDash 800x286 | 800 x 286 | no bar: the height is not there |
| openDash 600x686 | 600 x 686 | portrait, zone A above B above C, one bar field per end |
| openDash 480 round | 480 x 480, round | still the twelve-slot face; see below |
| openDash 800 round | 800 x 800, round | still the twelve-slot face; see below |

Four more dashboards are not faces for the wheel but second screens, described below.

| Dashboard | Screen | What it is |
|---|---|---|
| openDash Companion | 850 x 480 | one module at a time, paged from a wheel button |
| openDash Companion portrait | 480 x 850 | the same, for a phone stood on end |
| openDash Pit wall | 1920 x 1080 | three pages for someone who is not driving |
| openDash Pit wall portrait | 1080 x 1920 | the same in one page, for a screen on its side |

**The two round faces are still the twelve-slot design.** What a round face should do with zones
is not decided, so they were left as they were rather than changed badly. Of the dashboards the
plugin installs they are the only ones the Layout section of the settings page applies to, and they
number their slots the left side first, then the right side, then the bottom.

**Every screen keeps its own settings.** A rig with a face on the wheel and another beside it
configures them apart: the zones, the bar and the glance of the 1920 face are separate from those
of the 850, and each has its own wheel buttons. The settings page has a **Screen** picker at the
top of the Zones section saying which one you are configuring.

## Wheel buttons

Cycling a zone is a wheel button, bound in SimHub the way any action is. The settings page has a
binder for each, in the Buttons section, so you do not have to go looking in Controls and events.

| Action | What it does |
|---|---|
| Zone A, B, C, D | advances that zone to its next enabled page |
| Quick glance | while held, shows one chosen page in one chosen zone, and returns on release |

There is a set of these **per screen**, named for the screen: the actions of the reference face are
`OpenDash.Face1920x480CycleZoneA` through `CycleZoneD` and `OpenDash.Face1920x480HoldQuickGlance`.
A rig with one screen binds five of them and can ignore the rest.

Which pages a zone cycles through is yours: each zone has a list of its pages with a tick against
the ones in the cycle, so a zone you want to hold on two pages steps between those two. The last
enabled page of a zone cannot be turned off, because a zone with nothing to show has nothing to
draw.

## The companion

The companion shows one module at a time: a big, calm page for a phone or a tablet beside the
wheel. There are twenty-one modules, listed on the OpenDash page under Companion, and each has
its own switch. A module that is off is skipped entirely.

Paging is SimHub's, not OpenDash's. In SimHub, open the device or window the companion runs on,
go to its "Controls and events" and bind a wheel button to **NextScreen** (and another to
**PreviousScreen** if you want to go back). Those are per-device bindings, so the button that
pages your companion does not page your dash.

Three modules are off when you install: **Energy**, **Damage** and **Track rivals**. iRacing
publishes no virtual energy, no damage values at all and nothing a segment-by-segment rival
comparison could be built from, so those pages say what they cannot show rather than drawing
zeros. Switch them on for a sim that does carry the data.

## The pit wall

The pit wall is for a screen someone watches rather than drives: the whole field with gaps,
intervals, sectors and stops, the driver's own lap next to it, and four **data zones** you
choose the contents of. The landscape dashboard has three pages (race, tower, telemetry) that
you page with the same NextScreen binding; the portrait one has a single page.

Zones A to D and the wide zone are set on the OpenDash page under Pit wall. Each can show any of
eleven pages (fuel, tyres, opponents, pit view, relative, leaderboard, lap history, web view,
inputs, radar, sectors); the wide zone on the tower page has six of its own. The **Web view**
page shows any web page you like: put an http or https address in the Web view address box. An
address that is neither is ignored.

The leaderboard is one list with a class chip on each row rather than a block per class. SimHub
exposes per-class rows only for your own class, so class headings would be a picture of data
that is not there.

## Settings

Every change on the OpenDash page takes effect immediately on a running dashboard; there is
nothing to save and no restart.

| Section | Setting | Values |
|---|---|---|
| General | Shift lights on the dash | on, off (the rev bar stays; turn off if your DDU has physical LEDs) |
| Data | Position | Overall, Class |
| Data | Delta reference | Session best, All-time best |
| Data | Session progress | Auto, Laps, Time |
| Zones | Screen | which face the settings below belong to |
| Zones | Zone A, B, C, D | the page each opens on, and which pages it cycles |
| Zones | The two fields at each end of the bar | any of the ten bar fields |
| Buttons | Zone A to Zone D | the wheel button that cycles that zone |
| Buttons | Quick glance | the zone and page a held button shows, and the button |
| Layout | Slot 01 to Slot 12 | any of the thirteen cards; **the two round faces only** |
| Companion | Module 01 to Module 21 | on, off; an off module is skipped when you page |
| Pit wall | Zone A to Zone D | any of the eleven zone pages |
| Pit wall | Wide zone | any of the six wide pages (tower page only) |
| Pit wall | Web view address | an http or https address, or empty |
| Dashboard | Check for updates | on, off; asks GitHub once a day and sends nothing about you |
| Dashboard | Reinstall | extracts every embedded dashboard again; your settings are kept |

The same page may sit in two zones at once. The page says so in amber and does not stop you: two
zones on the relative is a choice, not a mistake. The Layout section says the same of a card
assigned to two slots of a round face.

The settings are stored by SimHub in `PluginsData\Common\OpenDash.GeneralSettings.json` and are
also visible to any dashboard or LED profile as properties under the `OpenDash` prefix. The face
properties carry the screen they belong to: `OpenDash.Face1920x480ZoneA` is the page zone A of the
reference face is showing, `Face1920x480ZoneAPages` which of its pages are enabled,
`Face1920x480ZoneAStart` the one it opens on, `Face1920x480BarLeft1` a bar field and
`Face1920x480QuickGlance` the glance, with the same set for every other size. Alongside them are
`OpenDash.ShiftLights`, `OpenDash.PositionMode`, `OpenDash.DeltaReference`,
`OpenDash.SessionProgress`, `OpenDash.Slot01` to `OpenDash.Slot12` for the round faces,
`OpenDash.CompanionModule01` to `CompanionModule21`, `OpenDash.PitWallZoneA` to `PitWallZoneD`,
`OpenDash.PitWallWide` and `OpenDash.WebViewUrl`.

## Update

Close SimHub, replace `OpenDash.dll` with the new one, unblock it and start SimHub. When the
dashboards embedded in the new plugin are newer than the installed ones, the plugin replaces
each `DashTemplates\<name>` folder on that start and keeps the previous folder as
`DashTemplates\<name>_backup.zip` (for example `openDash_backup.zip` and
`openDash 1280x480_backup.zip`).

A dashboard you have edited in Dash Studio is **not** replaced silently. The plugin notices that
the folder no longer holds what it wrote, leaves it alone and says so; pressing Reinstall a second
time replaces it, and the copy it takes then is kept under a name no later update reclaims. A
dashboard OpenDash has never seen before is adopted as it is, because an edit made before OpenDash
started watching cannot be told from an untouched folder.

The plugin can also tell you when a newer release exists. It asks GitHub once a day, sends nothing
that identifies you, and can be switched off in the Dashboard section, in which case nothing is
fetched at all. Nothing is ever installed without being asked for.

**Coming from 0.1.x.** The face changed: what was twelve fixed slots is now four zones you cycle
with a wheel button, under the same dashboard names. Your old face is still published with each
release as `openDash slots <size>.simhubdash`, so you can install one by hand if you prefer it.
Your slot settings are not lost; they still drive the two round faces.

## Uninstall

Close SimHub, delete `OpenDash.dll` from the SimHub folder and, if you want the dashboards gone
too, delete the folders of the Sizes table under `DashTemplates` (`openDash`,
`openDash 1280x480` and the rest, plus their `_backup.zip` files). The settings file named above
can be deleted as well. The fonts copied into `DashFonts` (Barlow) are harmless and shared with
other dashboards.

## Troubleshooting

- SimHub never asked about the plugin, or shows an error about loading it: the file is
  blocked (step 3) or it is not in the SimHub folder itself (step 2).
- The status in the Dashboard section is the worst one across the fourteen dashboards; hover it
  to see each dashboard with its own status.
- The Dashboard section says "Not installed": the plugin could not write to `DashTemplates`, or
  one of the folders is missing. Check that SimHub can write to its own folder, then click
  Reinstall, which extracts every dashboard again.
- "Install failed": hover the status for the reason and the dashboard concerned. Details are in
  SimHub's log, `Logs\SimHub.txt`, on the lines prefixed `[OpenDash]`.
- A dashboard says it was left alone: OpenDash found changes in it and will not overwrite
  somebody's work without being told twice. Press Reinstall again to replace it; a copy is kept
  either way.
- The dashboard shows the default pages although you changed them: the dashboard reads the
  settings through the plugin's properties, so the plugin has to be enabled; check SimHub's
  Settings > Plugins page.
- A wheel button does nothing: check that you bound the action of the screen you are looking at.
  Each size has its own, named for it, so `Face1920x480CycleZoneB` moves the 1920 face and not the
  850 beside it.
- A zone will not stop on the page you want: that page is probably not ticked in the zone's page
  list, so the cycle steps past it.
- A round face does not show the card you assigned: a round face reads only its first slots, two
  on the 480 and six on the 800, so a card in a higher slot is never drawn. Assign the card to a
  lower slot number.
- The companion does not change page when you press the button: the binding is on the device,
  not in OpenDash. Open that device's "Controls and events" in SimHub and bind NextScreen.
- The track map or the radar is empty: both are drawn from SimHub's recorded outline of the
  track, which appears after a lap has been recorded there.
- A tyre pressure or a temperature reads `--`: iRacing reports pressures from the last pit stop
  only, and reports nothing at all before the car has been on track.
- A cell of the car settings strip is missing: your car does not publish that setting, and a strip
  closes over what it cannot show rather than drawing an empty box.
