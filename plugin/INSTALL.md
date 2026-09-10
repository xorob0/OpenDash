# Installing the openDash plugin

The plugin does two things: it installs the openDash dashboards into SimHub, one per screen
size, and it adds an "openDash" page to SimHub's left menu where you choose what the dashboard
shows. It reads no telemetry and renders nothing; SimHub does that.

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
   should say "openDash <version> · 10 dashboards" and "Up to date", which means every dashboard
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

Slot numbering follows the picture on the openDash page: the left grid row by row, then the
right grid row by row. The 600x686 face numbers its grid row by row under the hero; the round
faces number the left side, then the right side, then the bottom.

## Settings

Every change on the openDash page takes effect immediately on a running dashboard; there is
nothing to save and no restart.

| Section | Setting | Values |
|---|---|---|
| General | Shift lights on the dash | on, off (the rev bar stays; turn off if your DDU has physical LEDs) |
| Data | Position | Overall, Class |
| Data | Delta reference | Session best, All-time best |
| Data | Session progress | Auto, Laps, Time |
| Layout | Slot 01 to Slot 12 | any of the twelve cards; the picture shows where each slot sits on the 1920 x 480 dash, smaller faces use the first slots (see Sizes) |
| Dashboard | Reinstall | extracts every embedded dashboard again; your settings are kept |

The same card may be assigned to several slots. The page says so in amber and does not stop you.

The settings are stored by SimHub in `PluginsData\Common\OpenDash.GeneralSettings.json` and
are also visible to any dashboard or LED profile as the properties `OpenDash.ShiftLights`,
`OpenDash.PositionMode`, `OpenDash.DeltaReference`, `OpenDash.SessionProgress` and
`OpenDash.Slot01` to `OpenDash.Slot12`.

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
- The status in the Dashboard section is the worst one across the ten dashboards; hover it to
  see each dashboard with its own status.
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
