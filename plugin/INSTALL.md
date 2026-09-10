# Installing the openDash plugin

The plugin does two things: it installs the openDash dashboard into SimHub and it adds an
"openDash" page to SimHub's left menu where you choose what the dashboard shows. It reads no
telemetry and renders nothing; SimHub does that.

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
   should say "Up to date", which means the dashboard was extracted into
   `DashTemplates\openDash` on that start.
6. Assign the dashboard to a display. openDash is a normal SimHub dashboard from here on: in
   Dash Studio it is listed as "openDash" and you open it in a window, send it to a USB or HDMI
   display, or point a phone or tablet at it exactly like any other dashboard. Nothing in the
   plugin launches it; that is SimHub's job.

## Settings

Every change on the openDash page takes effect immediately on a running dashboard; there is
nothing to save and no restart.

| Section | Setting | Values |
|---|---|---|
| General | Shift lights on the dash | on, off (the rev bar stays; turn off if your DDU has physical LEDs) |
| Data | Position | Overall, Class |
| Data | Delta reference | Session best, All-time best |
| Data | Session progress | Auto, Laps, Time |
| Layout | Slot 01 to Slot 12 | any of the twelve cards; the picture shows where each slot sits on the dash |
| Dashboard | Reinstall | extracts the embedded dashboard again; your settings are kept |

The same card may be assigned to several slots. The page says so in amber and does not stop you.

The settings are stored by SimHub in `PluginsData\Common\OpenDash.GeneralSettings.json` and
are also visible to any dashboard or LED profile as the properties `OpenDash.ShiftLights`,
`OpenDash.PositionMode`, `OpenDash.DeltaReference`, `OpenDash.SessionProgress` and
`OpenDash.Slot01` to `OpenDash.Slot12`.

## Update

Close SimHub, replace `OpenDash.dll` with the new one, unblock it and start SimHub. When the
dashboard embedded in the new plugin is newer than the installed one, the plugin replaces
`DashTemplates\openDash` on that start and keeps the previous folder as
`DashTemplates\openDash_backup.zip`. Changes made to the dashboard in DashStudio are lost at
that point: the dashboard is generated from source and DashStudio is for looking, not editing.

## Uninstall

Close SimHub, delete `OpenDash.dll` from the SimHub folder and, if you want the dashboard gone
too, delete `DashTemplates\openDash`. The settings file named above can be deleted as well.
The fonts copied into `DashFonts` (Barlow) are harmless and shared with other dashboards.

## Troubleshooting

- SimHub never asked about the plugin, or shows an error about loading it: the file is
  blocked (step 3) or it is not in the SimHub folder itself (step 2).
- The Dashboard section says "Not installed": the plugin could not write to `DashTemplates`.
  Check that SimHub can write to its own folder, then click Reinstall.
- "Install failed": hover the status for the reason. Details are in SimHub's log,
  `Logs\SimHub.txt`, on the lines prefixed `[openDash]`.
- The dashboard shows the default layout although you changed the slots: the dashboard reads
  the settings through the plugin's properties, so the plugin has to be enabled; check
  SimHub's Settings > Plugins page.
