# Installing the openDash plugin

The plugin does two things: it installs the openDash dashboards into SimHub, one per screen
size plus the two companions and the two pit walls, and it adds an "openDash" page to SimHub's
left menu where you choose what each of them shows. It reads no telemetry and renders nothing;
SimHub does that.

You need SimHub 9.12.6 or later on Windows. `OpenDash-plugin.zip` contains `OpenDash.dll` and
this file.

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
5. "openDash" now appears in SimHub's left menu. Open it: the Dashboard section at the bottom
   should say "openDash <version> · 14 dashboards" and "Up to date", which means every dashboard
   was extracted into its own folder under `DashTemplates` on that start (`openDash`,
   `openDash 1280x480` and so on, see Sizes below).
6. Assign a dashboard to a display. Pick the size that matches the display from the Sizes table;
   openDash is a normal SimHub dashboard from here on: in Dash Studio the sizes are listed as
   "openDash", "openDash 1280x480" and so on, and you open one in a window, send it to a USB or
   HDMI display, or point a phone or tablet at it exactly like any other dashboard. Nothing in
   the plugin launches it; that is SimHub's job.

## Sizes

Every size is its own dashboard with the same cards, hero and rev bar. Smaller faces have fewer
slots, and all sizes read the same slot settings: a face with N slots shows the cards of Slot 01
to Slot N and ignores the rest, so the 6-slot faces show what Slot 01 to Slot 06 are set to.

| Dashboard | Screen | Slots |
|---|---|---|
| openDash | 1920 x 480 | 12 |
| openDash 1280x480 | 1280 x 480 | 8 |
| openDash 1280x400 | 1280 x 400 | 8 |
| openDash 850x480 | 850 x 480 | 6 |
| openDash 800x480 | 800 x 480 | 6 |
| openDash 1280x720 | 1280 x 720 | 12 |
| openDash 800x286 | 800 x 286 | 4 |
| openDash 600x686 | 600 x 686 | 6 |
| openDash 480 round | 480 x 480, round | 2 |
| openDash 800 round | 800 x 800, round | 6 |

Four more dashboards are not faces for the wheel but second screens, described below.

| Dashboard | Screen | What it is |
|---|---|---|
| openDash Companion | 850 x 480 | one module at a time, paged from a wheel button |
| openDash Companion portrait | 480 x 850 | the same, for a phone stood on end |
| openDash Pit wall | 1920 x 1080 | three pages for someone who is not driving |
| openDash Pit wall portrait | 1080 x 1920 | the same in one page, for a screen on its side |

Slot numbering follows the picture on the openDash page: the left grid row by row, then the
right grid row by row. The 600x686 face numbers its grid row by row under the hero; the round
faces number the left side, then the right side, then the bottom.

## The companion

The companion shows one module at a time: a big, calm page for a phone or a tablet beside the
wheel. There are twenty-one modules, listed on the openDash page under Companion, and each has
its own switch. A module that is off is skipped entirely.

Paging is SimHub's, not openDash's. In SimHub, open the device or window the companion runs on,
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

Zones A to D and the wide zone are set on the openDash page under Pit wall. Each can show any of
eleven pages (fuel, tyres, opponents, pit view, relative, leaderboard, lap history, web view,
inputs, radar, sectors); the wide zone on the tower page has six of its own. The **Web view**
page shows any web page you like: put an http or https address in the Web view address box. An
address that is neither is ignored.

The leaderboard is one list with a class chip on each row rather than a block per class. SimHub
exposes per-class rows only for your own class, so class headings would be a picture of data
that is not there.

## Settings

Every change on the openDash page takes effect immediately on a running dashboard; there is
nothing to save and no restart.

| Section | Setting | Values |
|---|---|---|
| General | The rev bar | Shift lights, RPM bar, Off (off gives the bar's room back to the zones on a zone face; pick it if your DDU has LEDs of its own) |
| Data | Position | Overall, Class |
| Data | Delta reference | Session best, All-time best |
| Data | Session progress | Auto, Laps, Time |
| Layout | Slot 01 to Slot 12 | any of the thirteen cards; the picture shows where each slot sits on the 1920 x 480 dash, smaller faces use the first slots (see Sizes) |
| Companion | Module 01 to Module 21 | on, off; an off module is skipped when you page |
| Pit wall | Zone A to Zone D | any of the eleven zone pages |
| Pit wall | Wide zone | any of the six wide pages (tower page only) |
| Pit wall | Web view address | an http or https address, or empty |
| Dashboard | Reinstall | extracts every embedded dashboard again; your settings are kept |

The same card may be assigned to several slots. The page says so in amber and does not stop you.

The settings are stored by SimHub in `PluginsData\Common\OpenDash.GeneralSettings.json` and
are also visible to any dashboard or LED profile as the properties `OpenDash.RevBar`,
`OpenDash.PositionMode`, `OpenDash.DeltaReference`, `OpenDash.SessionProgress`,
`OpenDash.Slot01` to `OpenDash.Slot12`, `OpenDash.CompanionModule01` to `CompanionModule21`,
`OpenDash.PitWallZoneA` to `PitWallZoneD`, `OpenDash.PitWallWide` and `OpenDash.WebViewUrl`.
`OpenDash.ShiftLights` is still there too: it is the deprecated alias of `OpenDash.RevBar` and is
true only in the Shift lights state.

## Update

Close SimHub, replace `OpenDash.dll` with the new one, unblock it and start SimHub. When the
dashboards embedded in the new plugin are newer than the installed ones, the plugin replaces
each `DashTemplates\<name>` folder on that start and keeps the previous folder as
`DashTemplates\<name>_backup.zip` (for example `openDash_backup.zip` and
`openDash 1280x480_backup.zip`). Changes made to a dashboard in DashStudio are lost at that
point: the dashboards are generated from source and DashStudio is for looking, not editing.

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
  SimHub's log, `Logs\SimHub.txt`, on the lines prefixed `[openDash]`.
- The dashboard shows the default layout although you changed the slots: the dashboard reads
  the settings through the plugin's properties, so the plugin has to be enabled; check
  SimHub's Settings > Plugins page.
- A smaller face does not show the card you assigned: it has fewer slots than the picture on the
  openDash page and only reads the first ones (see Sizes). Assign the card to a lower slot
  number.
- The companion does not change page when you press the button: the binding is on the device,
  not in openDash. Open that device's "Controls and events" in SimHub and bind NextScreen.
- The track map or the radar is empty: both are drawn from SimHub's recorded outline of the
  track, which appears after a lap has been recorded there.
- A tyre pressure or a temperature reads `--`: iRacing reports pressures from the last pit stop
  only, and reports nothing at all before the car has been on track.
