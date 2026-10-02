# Panel verification run (#529)

Every row of [docs/testing-panel.md](../../docs/testing-panel.md), run on 2026-10-02 on the VM (SimHub 9.12.6,
3840 × 2160) on branch `claude/503-plugin-panel-redesign`, with the plugin from `bun run package` (VERSION
0.3.0-rc.7; B:old was the same tree packaged with VERSION 0.3.0-rc.1, never committed).

Results: ✅ passed, ✅N passed by the named stand-in, ❌ fixed — failed, fixed on the branch and re-run,
⏭ not run (with why). 175 rows: 165 ✅, 7 ✅N, 2 ❌ fixed, 1 ⏭, after the second pass below.

Captures are beside this file. `<page>-<width>.png` are the eight pages at SimHub client widths of 700, 1000,
1600 and `max` (maximised at 3840), cropped to the panel; `<id>.png` and `<id>-<what>.png` are the row
captures. They are palette PNGs (128 colours) to keep the directory small.

## How the run was taken

- **Captures by hand, not by `bun run panel-shots`.** Two faults stopped panel-shots before its first picture
  and were fixed on the branch (74f22b33: PowerShell's `-f` given an `int[]`, and a pointer left on a control whose
  tooltip `MainWindowHandle` then reported as SimHub's window). A third is not fixable here: it finds the panel by UI
  Automation, and SimHub 9.12.6's main window exposes no automation children at all (`FindAll(Descendants)` is empty,
  `FromPoint` inside the panel returns the Window), so the measurement always says "SimHub is not showing OpenDash".
  The pictures were taken with the same library calls (`sizeSimHub`, `click`, `captureWindow`, VNC captures for
  hovers), cropped to the panel. `run.json` was therefore not written (RS-05).
- **The rig.** `bun scripts/rig.ts panel` left Rim's folder in place on a VM where an earlier start had written it:
  the wait for the folder passed at once and the delete beat Init. Fixed on the branch (20eadda9); HM-04 and the
  page captures were taken after the fix.
- **Typing works.** The row notes say a panel TextBox never takes focus over VNC; in this run it did: the search
  flyout, the alert thresholds and the Web view address all took keys, and a paste from the desktop clipboard
  filled the Edit sheet's Name.
- **Artboards.** The twelve `.dc.html` artboards were rendered in headless Chromium with an empty `support.js`, which
  draws their static structure and leaves templated rows as `{{…}}`; each page capture was compared with that and
  with the artboard copy. Layouts match the artboards; the copy differences seen (Real hardware for "Light the real
  hardware", Car's own rev lights, Centre display, Flag animation, Add an LED strip, the Shortcuts subtitle) are
  the ones docs/design/plugin.md's departures table lists. Two are not artboard-shaped: Shortcuts embeds SimHub's own
  ControlsEditor in place of the artboard's binding chip, verb and ×, by design (PanelShortcuts.cs), and SimHub draws
  its press type clipped ("ShortAndL…") inside that editor at every width (SH-01.png); and RS-04's column is SimHub's.
- **Unit suites (the U rows):** `bun run check` 3832 tests, 0 fail; `dotnet test plugin/OpenDash.Tests` 1591 passed,
  0 failed, 4 skipped (after the two new UpdateWording tests). A U row cites its suite as green in that run.

## Second pass

The same day, on the same branch, a second pass ran again the rows the first fixed or could not run (SC-07, UP-01,
HM-04 with the corrected rig, UP-03, FL-04, FL-05, FL-06, SC-29), after a fresh `bun run package` and `bun run vm
plugin --no-build --menu`. Its captures carry `-rerun` where a first-pass capture of the same name exists. The SimHub
window was sized to a 1600 px client and the captures cropped to the panel as before. Two things about driving it:
a VNC screenshot shows the frame from before the last click, so a second screenshot is the current one; and a
ChoiceButton's list (a WPF Popup, StaysOpen false) closes whenever SimHub loses focus, which the PrintWindow capture
script causes, so a list was opened and picked from with no capture in between and proved by the settings file.

- **Bug 3 found and fixed** (2ff98dfb): the dashed add tile's template had a Grid with no background, so WPF hit
  the tile only where its plus or its words drew; a click between them, or in an empty corner, fell through. That is
  why it "sometimes" ignored the pointer. Reproduced on the old build right after Remove Round's sheet closed (a
  click between the plus and "Add a screen" did nothing), then after the fix a click in the tile's empty top-left
  corner opened the sheet ([BUG-add-tile-fixed.png](BUG-add-tile-fixed.png)). That unblocked SC-29.
- **SC-29 ran** on a portrait pit wall added from the tile itself, not hand-written into the settings.
- **UP-03 is still not run**: the edit that points api.github.com at 127.0.0.1 in the guest's hosts file was refused
  again by this session's permission rules, and no other route to the same outcome was tried.
- **Unit suites:** `bun run check` 3832 tests, 0 fail; `dotnet test plugin/OpenDash.Tests` 1592 passed, 0 failed,
  4 skipped (with the new PanelKitTests row); `bun run package` green.

## Fixed on the branch in this run

| commit | what | rows |
|---|---|---|
| 20eadda9 | The panel rig takes Rim's folder out while SimHub is stopped, so Init cannot write it back | HM-04, FL-03 (fixture) |
| 74f22b33 | panel-shots formats its window rectangles element by element and parks the pointer after a click | tooling |
| 22908679 | A sheet's body starts under its title instead of halfway down the sheet | SC-07, LD-18, MX-13 |
| 3089738a | The release notes on Updates read the first sentence across CHANGELOG's line wrap | UP-01 |
| 6cc9253f | docs/testing-panel.md: six rows corrected to what the page draws (SB-02, SC-01, SC-24, SC-35, LD-03, LD-13) | — |
| 2ff98dfb | The dashed add tile answers the pointer across its whole face (second pass) | bug 3, SC-29 |

## Found and not fixed here

1. **panel-shots cannot see the panel** (scripts/panel-shots.ts `measurePanel`): UI Automation finds no children
   under SimHub 9.12.6's window, so `--menu-y` runs always stop at "SimHub is not showing OpenDash". It needs a
   measure that does not rely on automation peers (the panel's column from SimHub's client rectangle, as the
   captures here were cropped).
2. **The support report names SimHub "1.0.0.0"** (SettingsControl.Updates.cs `UpdatesSimHubVersion` reads
   SimHubWPF.exe's FileVersion, which is 1.0.0.0 in every SimHub build; the Uninstall key's DisplayVersion says 9.12.6).
3. **Screens' Add a screen tile sometimes ignores the pointer** ([BUG-add-tile.png](BUG-add-tile.png)): no hover, an
   arrow cursor and a dead click, while Edit, Duplicate and the cards beside it respond. Seen three times: on RF just
   after Remove Round's sheet closed, on R1's first visit, and on a one-screen rig after a restart, where leaving to
   LEDs and back did not clear it; on other visits the same tile worked at once. It blocked SC-29. **Fixed in the
   second pass (2ff98dfb):** the tile was hit only on its plus and its words, so it depended on where the click
   landed ([BUG-add-tile-fixed.png](BUG-add-tile-fixed.png)).
4. **A profile newer than the plugin is offered as an update**: with an rc.1 plugin over rc.7 profiles, Home says
   "Wheel rim's profile has an update" ([SB-08.png](SB-08.png)), Updates shows "Update available" with an Update press
   ([UP-01.png](UP-01.png)) and Matrix offers "Update" ([MX-01-update.png](MX-01-update.png)); pressing it would put the
   older profile in. Seen again in the second pass on a B:old build over an old settings file: "0/10/0's profile has
   an update" and "OpenDash Flag box has an update" ([UP-01-rerun.png](UP-01-rerun.png)).
5. **An unreadable settings file starts OpenDash as a first run and overwrites it** (second pass, FL-06,
   [FL-06-unreadable.png](FL-06-unreadable.png)): with `"Rig": 5` written into OpenDash.GeneralSettings.json and no
   `_Backups` copies, SimHub's `FromJsonFileWithVersionning` logged a Newtonsoft exception and handed the plugin
   default settings. The panel opened on "Nothing to fix" with no screens, no LEDs and no matrix, said nothing about
   the file, and the defaults were saved over it; the unreadable file survives only as SimHub's `_b2` copy. A driver
   whose file is damaged meets an empty rig with no word of why or where the old one is.

Smaller notes, not filed: Reinstall everything also installs an LED profile that was never installed (UP-08.png says
so in its line, under "OpenDash never installs a profile on its own"); Pit wall was counted as edited by Reinstall
everything on a VM whose Pit wall folder an earlier session had left (UP-09-ask.png), which could not be traced to a
change made here; the companion preview came back blank once after reselecting Phone and was back on the next
selection; the live card keeps its three-line height when it says only "No game running" (SB-04.png).

Second pass, not filed: a portrait pit wall added from the tile is named after its size, "1080 × 1920", with the
namespace "Screen" and the folder "OpenDash 1080 x 1920" (UniqueNamespace keeps letters only), where a second
landscape wall is offered "Pit wall (2)" ([SC-29-add.png](SC-29-add.png)); after Install it again (HM-04) Rim's card
reads "In SimHub" although Home says "Restart SimHub to load it", and its preview is blank until then; a face with a
0 × 0 size still draws on Rig at the 1280 × 480 shape ([FL-06-rig.png](FL-06-rig.png)); on B:old the Available card
did not appear until Check now, because the day's check had run minutes earlier under the rc.7 build, which had
nothing to offer (not a case a driver meets, since versions only go up).

## Not run

UP-03 only: pointing api.github.com at 127.0.0.1 in the guest's hosts file was refused by the session's permission
rules in both passes. Stand-in UpdateWordingTests / PanelUpdatesTests / DashboardInstallerTests, which pin "Could not
reach GitHub. You have …". FL-04, FL-05, FL-06 and SC-29, not run in the first pass, ran in the second.

## VM state left behind

**After the second pass** the VM is not back on the panel rig. The last two steps, reinstalling the branch DLL
(`bun run vm plugin --no-build --menu`) and `bun scripts/rig.ts panel`, were refused by the session's permission
rules after the UP-03 refusal and were not retried. So SimHub runs the B:old build (OpenDash.dll at 0.3.0-rc.1, built
from a copy of the branch with VERSION edited there, never in the repository; Download was not pressed, so nothing is
staged) over the old settings file FL-05 restored (eight faces, three strips, Matrix 1). OpenDash is in the left
menu. DashTemplates also holds "OpenDash 1080 x 1920" from SC-29. The next session should run `bun run vm plugin
--no-build --menu` from the branch and `bun scripts/rig.ts panel` before it trusts the VM.

First pass:

The panel rig is back on (`bun scripts/rig.ts panel`), keys unbound (`bun run vm unbind`, which also dropped six
stale OpenDash bindings an earlier session had left), the emulator stopped, the staged release DLL
(`OpenDash\OpenDash.dll.pending` and its swap script) deleted, and the branch DLL installed. LED profiles for Wheel
rim (Arduino RGB LEDs) and Strip (Sim-Lab Dash SD43-LED) and the OpenDash Flag box matrix profile remain installed in
SimHub from the LD, AL and MX rows.

## SB Sidebar and shell

| id | control | result | capture or proof |
|---|---|---|---|
| SB-01 | Wordmark + version | ✅ | [home-1600.png](home-1600.png): wordmark "openDash" and "0.3.0-rc.7", the VERSION built; PanelShellTests green |
| SB-02 | Search | ✅ | [SB-02.png](SB-02.png): the box draws "Search" with the New tag, no log error. Typing did work over VNC in the rail's flyout (contrary to the row's N): [SB-02-blue.png](SB-02-blue.png) "blue" → "Blue flag detail · Settings" first, then Flags · Rig and Flags · Matrix; [SB-02-nomatch.png](SB-02-nomatch.png) "No setting matches."; [SB-02-hit.png](SB-02-hit.png) clicking a hit opened Matrix. PanelSearchTests green |
| SB-03 | Live card | ✅ | [home-1600.png](home-1600.png): "Live · IRacing", Porsche 911 GT3 R (992), "spa gp · Race", green dot (E:race) |
| SB-04 | Live card, no game | ✅ | [SB-04.png](SB-04.png): emulator stopped, "No game running", grey dot. The card keeps its three-line height with one line in it |
| SB-05 | Nav items + counts | ✅ | [home-1600.png](home-1600.png): Home, Rig, sep, Screens 5 (dot, Rim missing), LEDs 2, Matrix 2, sep, Shortcuts 0, Settings; Night mode and Updates at the foot. LEDs wears its dot once a strip's item is drawn ([HM-05.png](HM-05.png), after Dash brow's profile was installed); Matrix no dot (#521). PanelNavTests green |
| SB-06 | Active item | ✅ | the eight page captures at each width: the accent bar sits on the page shown and the heading matches |
| SB-07 | Page remembered per sitting | ✅ | [SB-07.png](SB-07.png): Settings, then SimHub's Home, then OpenDash again: Settings shown |
| SB-08 | Page not persisted | ✅ | [SB-08.png](SB-08.png): after a SimHub restart (force-killed, then started by `vm plugin`) the panel opens on Home, though it was left on Settings |
| SB-09 | Night mode (sidebar foot) | ✅ | JSON LightsNightMode false → true on the sidebar switch; [SB-09-home.png](SB-09-home.png) (Quick controls switch on, slider becomes "Night brightness 25%"), [SB-09-rig.png](SB-09-rig.png) (header switch on, LED tiles dimmed, screens not), [SB-09-settings2.png](SB-09-settings2.png) (Settings › Lighting Night mode on, preview on Night at 25%, sidebar foot on). Flipped back: false |
| SB-10 | Updates badge | ✅ | B:old (VERSION 0.3.0-rc.1, never committed): [UP-01-ask.png](UP-01-ask.png) the Updates item wears the amber "0.3.0-rc.7" badge; [UP-06.png](UP-06.png) it turns to "Restart" once the download is staged. PanelNavTests green |
| SB-11 | Up to date | ✅ | [updates-1600.png](updates-1600.png): B:cur, no badge on Updates |
| SB-12 | Warn dots follow Home items | ✅ | [HM-04-after.png](HM-04-after.png): after Install it again the Rim item went and the Screens dot cleared; [HM-05.png](HM-05.png) the LEDs dot came with the strip item. PanelNavTests green |
| SB-13 | Icon rail | ✅ | [SB-13.png](SB-13.png) at SimHub 1000 px (panel 785): 56 px rail, hover "Screens · 5 · Needs attention", search icon "Searches every setting."; [SB-13-flyout.png](SB-13-flyout.png) the flyout opens with the box focused |

## HM Home

| id | control | result | capture or proof |
|---|---|---|---|
| HM-01 | Heading count | ✅ | [home-1600.png](home-1600.png) "1 thing to fix" over one item; PanelAttentionTests green |
| HM-02 | Nothing to fix | ✅ | [HM-04-after.png](HM-04-after.png) "Nothing to fix" and no card once Rim was installed again; [FL-02.png](FL-02.png) the same on a one-screen rig after restart. On `rig.ts clear` itself the two strip items stayed (their profiles had been installed by the LD rows), as they should. PanelAttentionTests green |
| HM-03 | Item: written, not restarted | ✅ | [HM-03.png](HM-03.png) after Duplicate (SC-06): "Main dash (2) is not in SimHub yet", "Restart SimHub to load it. Then assign "Main dash (2)" to its display in Dash Studio.", Open Main dash (2); [HM-03-open.png](HM-03-open.png) lands on Screens with it selected. PanelAttentionTests green |
| HM-04 | Item: folder missing | ✅ | [HM-04.png](HM-04.png) "Rim's dashboard is missing from SimHub", "Its settings are kept.", Install it again; [HM-04-after.png](HM-04-after.png) "Installed Rim's dashboard again. Restart SimHub to load it.", and the folder is back (Test-Path True, log "Installed OpenDash Rim 0.3.0-rc.7"). Second pass, with the rig fix (20eadda9): `rig.ts panel` left Rim's folder deleted and it stayed deleted 30 s after start; [HM-04-rerun.png](HM-04-rerun.png) the same item; [HM-04-rerun-after.png](HM-04-rerun-after.png) after Install it again, the line above and Test-Path True |
| HM-05 | Item: profile not selected | ✅N | [HM-05.png](HM-05.png) after installing Dash brow's profile: "Dash brow's profile is not selected", "Installed, but not selected in SimHub.", three steps, Check again; the answer starts "Checked again." ([LD-03.png](LD-03.png)). Resolved branch by stand-in: LedDeviceSurveyTests/PanelAttentionTests (no LED device on the VM). The item needs the profile installed first, which RF alone does not do |
| HM-06 | Item: matrix not shown | ✅N | never drawn on the VM (SimHub does not say which matrix a device shows, #521); PanelAttentionTests green |
| HM-07 | Item: update pending restart | ✅ | [HM-07.png](HM-07.png) after the download (B:old): "Restart SimHub to finish updating", "Until then you are running the old version.", Open Updates |
| HM-08 | Right now › Screens | ✅ | [home-1600.png](home-1600.png) under E:race: one row per screen with its current pages ("Lap times · Gear, speed, revs · Opponents · Sectors") and a green dot (Rim red while missing). Zone properties not read: the Available properties page was not opened in this run |
| HM-09 | Right now › LEDs | ✅ | [HM-09.png](HM-09.png) under E:shiftlights: both strips dark, which is the not-Ready branch (no Car Data, no profile selected); the lit branch is not reachable on the VM without a car table and a selected profile |
| HM-10 | Right now › Matrix | ✅ | [home-1600.png](home-1600.png) "Flag box · Matrix 1 · Gear" over the gear glyph, "Left pillar · Matrix 2 · Dark" dark; PanelEmulationTests green |
| HM-11 | Card links | ✅ | [HM-11.png](HM-11.png): Screens, LEDs and Matrix Open each land on their page |
| HM-12 | Brightness (Quick controls) | ✅ | [HM-12.png](HM-12.png): dragged to ~50, JSON LightsBrightness 100 → 49; with night on the slider reads "Night brightness" and the drag wrote LightsNightBrightness 25 → 49 (LightsBrightness unchanged); Settings › Lighting draws the same values ([SB-09-settings2.png](SB-09-settings2.png)) |
| HM-13 | Night mode (Quick controls) | ✅ | [HM-13.png](HM-13.png): Quick controls switch → LightsNightMode true, then false; the other three switches follow as in SB-09 |
| HM-14 | Flags and spotter › Open Rig | ✅ | [HM-14.png](HM-14.png): Open Rig lands on Rig with the Yellow flag chip picked |

## RG Rig

| id | control | result | capture or proof |
|---|---|---|---|
| RG-01 | Tiles for every device | ✅ | [rig-1600.png](rig-1600.png): 9 tiles (Wheel rim, Dash brow, Left pillar, Main dash, Flag box, Phone, Rim, Round, Pit wall); warning dot on Rim while missing, on Dash brow once its profile was installed and not selected ([HM-14.png](HM-14.png)). PanelRigMapTests green |
| RG-02 | Drag a tile | ✅ | [RG-02.png](RG-02.png): Main dash dragged 200 px right, JSON Rig[Main dash].LayoutX/Y 520,100 (every other tile's home position written beside it). Tiles may overlap: Main dash lands under Flag box |
| RG-03 | Reset layout | ✅ | [RG-03.png](RG-03.png): Reset layout puts every tile home and clears LayoutX/Y on every screen, strip and matrix |
| RG-04 | Preview, 18 chips | ✅ | [RG-04.png](RG-04.png): the 18 chips (7 flags, 3 spotter, 2 pit lane, 3 warnings, 3 revs), each repainting the tiles per PanelEmulation; PanelEmulationTests green |
| RG-05 | Real hardware | ✅ | [RG-05.png](RG-05.png): greyed, Soon tag, hover "Coming soon · #506"; JSON unchanged |
| RG-06 | Night mode (header) | ✅ | [SB-09-rig.png](SB-09-rig.png): LED tiles dim to the night value, screens do not; header switch agrees with the sidebar |
| RG-07 | Revs chips | ✅ | [RG-04.png](RG-04.png) Idle / Mid revs / Shift point: rev bars dark / green-amber / red and the flag box gear in the shift colour |
| RG-08 | Hint and Reset layout | ✅ | [rig-1600.png](rig-1600.png): "Drag to arrange like your rig" at the canvas foot, Reset layout in the header |

## SC Screens · AS Add a screen

| id | control | result | capture or proof |
|---|---|---|---|
| SC-01 | Cards | ✅ | [screens-1600.png](screens-1600.png): 5 cards with the kind's picture, name, kind and state (In SimHub, Missing for Rim), Add a screen last. The card carries the kind alone; the header carries "<kind> · <size>" (PanelScreens.CardMeta, as the artboard draws it), so the row is corrected. PanelScreensTests green |
| SC-02 | Select | ✅ | [SC-02.png](SC-02.png): Pit wall selected, header "Pit wall · 1920 × 1080", the pit pane (Page on screen, zone plan, Race zones list, Web view address, My class only, Flag display, Quick glance, Details) |
| SC-03 | Fix box | ✅ | [screens-1600.png](screens-1600.png) / [HM-04.png](HM-04.png): Rim reads Missing in red; Install it again restores it ([HM-04-after.png](HM-04-after.png)) |
| SC-04 | Edit sheet | ✅ | [SC-04.png](SC-04.png): Edit sheet with Name (prefilled), Size (1280 × 480), Dashboard › Reinstall, "Your settings and bindings are kept.", Cancel / Save; Cancel left the settings JSON identical |
| SC-05 | Rename via Edit | ✅ | [SC-05.png](SC-05.png): renamed Phone to Pocket through the sheet (the name pasted from the desktop clipboard), "Renamed to Pocket. Restart SimHub to see the new name in Dash Studio."; JSON Name changed, Namespace Companion and Folder "OpenDash Companion portrait" unchanged; renamed back |
| SC-06 | Duplicate | ✅ | [SC-06.png](SC-06.png): Duplicate on Main dash adds "Main dash (2)", namespace MainDash2, folder "OpenDash Main dash (2)" written, masks and flag format copied, card "Restart SimHub to load it" and the line "Added Main dash (2). Restart SimHub, then assign …" |
| SC-07 | Remove | ❌ fixed | [SC-07.png](SC-07.png) "Remove Round", "Removes the screen and its dashboard.", Keep it / Remove it; [SC-07-face.png](SC-07-face.png) a face's sheet adds its settings and "Any wheel button you bound to it stops working."; after Remove it the card and "OpenDash 480 round" folder are gone and Slots (the shared cards) kept. The one-line body sat halfway down the 1,800 px sheet ([SC-07-face.png](SC-07-face.png)); fixed in 22908679 and re-run: [SC-07-fixed.png](SC-07-fixed.png) has it under the title. PanelConfirmationTests green. Second pass on the branch build: [SC-07-rerun.png](SC-07-rerun.png) "Remove Round" with "Removes the screen and its dashboard." under the title; [SC-07-rerun-face.png](SC-07-rerun-face.png) a face's sheet (Remove Rim, kept) the same; [SC-07-rerun-after.png](SC-07-rerun-after.png) "Removed Round. Restart SimHub to take its dashboard out of Dash Studio.", the card gone, Test-Path of "OpenDash 480 round" False, the rig Main dash, Rim, Pit wall, Phone, and Slots still 12 0 1 … 10 |
| SC-08 | Click zones and bar | ✅ | [SC-08.png](SC-08.png) on Main dash: A, B, C, band D and the info bar each take the accent border and switch the aside. PanelFacePlanTests green |
| SC-09 | Zone page list | ✅ | [SC-09.png](SC-09.png) Rim zone C "4 of 21", ticked first with First on the start page; [SC-09-all.png](SC-09-all.png) Show all adds the unticked pages, "Not in iRacing" on Damage, Track rivals, Energy, and Soon rows Circle tracker and Launch |
| SC-10 | Tick/untick | ✅ | [SC-10.png](SC-10.png): ticking Track: "5 of 21", Face.Masks[2] 59392 → 63488 (bit 12); unticked again → 59392 |
| SC-11 | Last page stays | ✅ | [SC-11.png](SC-11.png): at one page the last box refuses, hover "A zone keeps at least one page."; JSON unchanged on the refused click |
| SC-12 | Untick the shown page | ✅ | [SC-12.png](SC-12.png): unticking the shown page (Relative) moved zone C forward to Opponents (Face.Zones[2] 14 → 15) and the live preview followed |
| SC-13 | Drag to reorder | ✅ | [SC-13.png](SC-13.png): dragging Gear by its grip moved it to the top: Face.Orders[2] rewritten, First moved to Gear, Zones[2] followed |
| SC-14 | Show all / Only ticked / All / None | ✅ | [SC-14.png](SC-14.png): All → mask 2097151 (21 of 21); None → only the start page kept (mask 8192, Starts unchanged); Only ticked / Show all shrink and grow the list |
| SC-15 | My class only | ✅ | [SC-15.png](SC-15.png): My class only on zone C → Face.ClassOnly [false,false,true,false], and back; zone A has no such row ([SC-08.png](SC-08.png)) |
| SC-16 | Next page / Previous page chips | ✅ | K:on: [SC-16.png](SC-16.png) Rim zone C "Next page · Keyboard · F7", "Previous page · Keyboard · F6" ([screens-1600.png](screens-1600.png) "Not bound" before); [SC-16-open.png](SC-16-open.png) the chip opens Shortcuts with Rim's Zone C row in view |
| SC-17 | Info bar fields | ✅ | [SC-17.png](SC-17.png): info bar Left, first → Lap: Face.BarFields [0,…] → [1,…], the preview draws "Lap" twice on the left |
| SC-18 | Rev bar | ✅ | [SC-18.png](SC-18.png): Rev bar Off → Rim RevBar "off"; the preview's bar goes dark but its well is still drawn (the face keeps the space; the row's "loses the well" overstates it) |
| SC-19 | Flag display | ✅ | [SC-19.png](SC-19.png) under E:yellow: Band D draws "FULL COURSE YELLOW" in band D; Full screen → FlagFormat "full" and the preview covers the page; back to "band" |
| SC-20 | Lap review | ✅ | JSON LapReview off → race → all → off on Races / Always / Off |
| SC-21 | Quick glance | ✅ | K:on: Rim's glance Zone C › Track (Face.QuickGlance 212), bound F8 as During ([SH-01.png](SH-01.png)); [SH-08.png](SH-08.png) F8 held shows Track in zone C, released it returns to Relative |
| SC-22 | Seven Soon rows | ✅ | [SC-22.png](SC-22.png): the seven Soon rows greyed, hovers "Coming soon · #746 / #96 / #383 / #112 / #321 / #114 / #319"; a click on a Soon switch left the JSON identical. PanelSoonTests green |
| SC-23 | Details | ✅ | [SC-23.png](SC-23.png): Details: SimHub name Rim, Folder OpenDash Rim, Properties OpenDash.Rim*, Version 0.3.0-rc.7 |
| SC-24 | Clash line | ✅ | [SC-24.png](SC-24.png): with zone C starting on Track and the glance on Zone C › Track, "Zone C and the quick glance both show the track." under the aside. The clash compares start pages, so the setup is "zone C starting on Track" |
| SC-25 | Pit: Page on screen | ✅ | [SC-25-tower.png](SC-25-tower.png) / [SC-25-telemetry.png](SC-25-telemetry.png): Race / Tower / Telemetry → PitWallPage 0 / 1 / 2; the zone plan and the zones list change with the page (the screen preview above is the dashboard's own picture and does not) |
| SC-26 | Pit: Race zones | ✅ | [SC-26.png](SC-26.png): Race zones Zone A → Relative: PitWallZones.RaceA 0 → 4; no Board row |
| SC-27 | Web view address | ✅ | [SC-27.png](SC-27.png) https://example.com/timing pasted → WebViewUrl written; [SC-27-ftp.png](SC-27-ftp.png) an ftp:// address is dropped and the box empties (WebViewUrl ""); [SC-27-hover.png](SC-27-hover.png) the empty box's hover "Sets what the web view shows, from an http or https address." |
| SC-28 | Pit: My class only / Flag display / Quick glance | ✅ | [SC-28.png](SC-28.png): My class only → PitWallClassOnly true; Flag display Full screen / Off / Bar → PitWallFlagFormat full / off / band. The glance select was not changed |
| SC-29 | Portrait layout A–D | ✅ | second pass, after the tile fix: Add a screen › Pit wall › Portrait ([SC-29-add.png](SC-29-add.png)) added "1080 × 1920" ([SC-29.png](SC-29.png)): Portrait layout A · Fuel, B · Tyres, C · Relative, D · Opponents, and no Page on screen row. Each zone's list ([SC-29-list.png](SC-29-list.png)) set A → Leaderboard, B → Radar, C → Sectors, D → Inputs: PitWallZones.PortraitA–D 0 1 4 2 → 5 9 10 8 at once. The installed "OpenDash 1080 x 1920.djson" reads OpenDash.ScreenPortraitA–D and no PitWall* property; after a restart Available properties shows ScreenPortraitA–D = 5, 9, 10, 8 ([SC-29-properties.png](SC-29-properties.png)) and Home's row reads "Leaderboard · Radar · Sectors · Inputs" ([SC-29-after.png](SC-29-after.png)). PanelScreensTests green |
| SC-30 | Phone: Modules | ✅ | [SC-30.png](SC-30.png): unticking Sectors → Modules[2] false, "17 of 21"; "Not in iRacing" beside Damage, Track rivals, Energy |
| SC-31 | Phone: First module | ✅ | JSON CompanionStart 0 → 1 and CompanionPage 1 at once on First module › Delta; reset to Lap times. The preview is a still picture and keeps showing the page it was taken on |
| SC-32 | Phone: Flag display | ✅ | under E:yellow: Full screen draws "FCY" over the page ([SC-32.png](SC-32.png)); Bar → CompanionFlagFormat "band" and the still, once retaken, shows the strip at the foot ([SC-32-bar.png](SC-32-bar.png)) |
| SC-33 | Phone: Quick glance + Next module | ✅ | [SC-30.png](SC-30.png): Quick glance select (Track), and the Next module paragraph with the crumbs "Controls and events › NextScreen". PanelCopyTests green |
| SC-34 | Round: Cards / Rev ring | ✅ | [SC-34.png](SC-34.png): Card 1 → Current lap: Slots[0] 12 → 0, and the clash line "Current lap is on Card 1 and Card 2."; Rev ring Off → RevBar "off", ShiftLights false (it is the rig's own, as its caption says); [SC-34-hover.png](SC-34-hover.png) "Coming soon · #146" on Zones instead of cards; restored |
| SC-35 | Card columns at width | ✅ | [screens-max.png](screens-max.png) and [screens-1600.png](screens-1600.png) 6 across, [screens-1000.png](screens-1000.png) 4 + 2, [screens-700.png](screens-700.png) 2 across, Add a screen last. The rule is "6, fewer below CardMinWidth" (PanelScreens.CardColumns), which gives 4 at a 785 px panel, so the row's "6 → 3 → 2" is corrected. PanelShellTests green |
| AS-01 | Open sheet | ✅ | [AS-01.png](AS-01.png): the sheet over the dimmed page with its close icon |
| AS-02 | Kind tiles | ✅ | [AS-02.png](AS-02.png): Pit wall and Companion swap Size for Orientation (Landscape \| Portrait); Round offers 480 round / 800 round; Dash face the eight sizes. PanelAddScreenTests green |
| AS-03 | Flags screen #739 | ✅ | [AS-01.png](AS-01.png): Flags screen greyed with Soon, "A second display for flags", hover "Coming soon · #739"; not selectable |
| AS-04 | Your displays #85 | ✅ | [AS-04.png](AS-04.png): Your displays greyed with Soon, hover "Coming soon · #85" |
| AS-05 | Size tiles + Name prefill | ✅ | [AS-05.png](AS-05.png): 1280 × 480 selected, Name prefilled, and the second-copy note "This 1280 × 480 can show different pages from any other 1280 × 480 on your rig." |
| AS-06 | Next steps | ✅ | AS-01 sheet foot: "Next steps", "Restart SimHub, then assign "Rim (2)" to this display in Dash Studio." |
| AS-07 | Cancel | ✅ | Cancel closed the sheet and the settings JSON is identical |
| AS-08 | Add screen | ✅ | R0 (`rig.ts empty`): [AS-08.png](AS-08.png) Add screen → card "Restart SimHub to load it", folder OpenDash 850x480 written, "Added Rim. Restart SimHub, then assign "Rim" to this display in Dash Studio.", Home "1 thing to fix" with "Rim is not in SimHub yet" ([FL-02-home.png](FL-02-home.png)). On R1 the same press reused a folder an earlier start had loaded, so the card read In SimHub at once |
| AS-09 | Add screen then restart | ✅ | graceful close and start: [AS-09.png](AS-09.png) the card reads In SimHub. The Dash Studio list was not opened |

## LD LEDs · AL Add an LED strip

| id | control | result | capture or proof |
|---|---|---|---|
| LD-01 | Cards | ✅ | [leds-1600.png](leds-1600.png): Wheel rim 3 · 9 · 3, Dash brow 15, Add an LED strip; states Not installed, then Not selected in SimHub once installed ([LD-brow-installed.png](LD-brow-installed.png)). Showing / Update available not reachable (no device shows a profile on the VM). PanelLedsTests green |
| LD-02 | Header chip and profile state | ✅ | [leds-1600.png](leds-1600.png) "Fanatec wheel · 3 · 9 · 3" with Install while not installed; [LD-brow-installed.png](LD-brow-installed.png) no press once current (Rename / Remove only) |
| LD-03 | Fix box | ✅N | [LD-brow-installed.png](LD-brow-installed.png) the fix box (Devices › Arduino RGB LEDs › Telemetry LEDs, Select "Dash brow", automatic switching Disabled), Check again → "Checked again. Dash brow's profile is not selected." ([LD-03.png](LD-03.png)). The [OpenDash] LED device lines are written at the survey when a line is new or changed (LedTargets.Report), so a Check again that finds nothing new logs nothing; the row is corrected. Resolved branch: LedDeviceSurveyTests |
| LD-04 | Preview, 6 chips | ✅ | [LD-04.png](LD-04.png) on Wheel rim: Shift point, Yellow, Blue, Car left, Pit limiter each draw their frame; Live dark while the profile is not installed, and lit from CarLights.Run on the installed Dash brow ([LD-04-brow-live.png](LD-04-brow-live.png)) under E:shiftlights |
| LD-05 | All devices at once | ✅ | [LD-05.png](LD-05.png): All devices at once opens Rig |
| LD-06 | Car's own rev lights | ✅ | [LD-06.png](LD-06.png) after the download: "Porsche 911 GT3 R (992) is in Car Data."; [LD-06-off.png](LD-06-off.png) flipped off: RpmStyle car → leftToRight, the line and the width row hidden; back to car. The "not in Car Data" branch needs a car without a table and was not driven |
| LD-07 | Rev light width | ✅ | [LD-07.png](LD-07.png) Actual size → LedMirrorFit "exact", back to "stretch"; hidden while the car's own lights are off ([LD-06-off.png](LD-06-off.png)) |
| LD-08 | Centre display | ✅ | Centre display → Brake: LedBars[Wheel rim].Centre "brake", back to "rpm" |
| LD-09 | SimHub device | ✅N | [LD-09.png](LD-09.png): "GRID BY SIM-LAB MPX has no LEDs OpenDash can reach. See SimHub's log." with the picker listing Arduino RGB LEDs and "Sim-Lab Dash SD43-LED · Not connected"; the none / gone states by stand-in LedDeviceSurveyTests |
| LD-10 | Brightness | ✅ | [LD-10.png](LD-10.png) 60% → LedBars[Wheel rim].Brightness 60; default label "Same as rig · 49%" follows LightsBrightness; back to null, "Wheel rim runs at the rig's brightness." |
| LD-11 | Reverse direction | ✅N | Reverse direction on Dash brow → Reversed true, ProfileShapeId "0-15-0-reversed", log "Removed 1 RGB LED profile(s)" then "Installed 1 …" (reinstalled), "Reversed Dash brow." The row is offered on a strip that can reverse (not the Fanatec rim). Hardware by stand-in: the NCalc remap is not exercised |
| LD-12 | Each LED in turn #747 | ✅ | [LD-12-15-16.png](LD-12-15-16.png): Each LED in turn greyed, hover "Coming soon · #747" |
| LD-13 | Effects | ✅ | [leds-1600.png](leds-1600.png) 15 switches on the 3·9·3 rim; [LD-brow.png](LD-brow.png) 9 on the 15-LED brow (Flags, both spotters, Pit lane, Pit limiter, Speeding, Low fuel, both turn signals) — PanelLeds.EffectsFor keeps exactly what the profile reads, so the row's "4 on the brow" is corrected; TC off → EffectsOff ["tc"] ([LD-13.png](LD-13.png)), back to [] |
| LD-14 | Flag animation / Full-strip spotter | ✅ | Flag animation and Full-strip spotter → FlagAnimation false, SpotterWhole true, and back; Full-strip spotter not drawn on the brow ([LD-brow.png](LD-brow.png)) |
| LD-15 | Pit limiter lights #509 | ✅ | [LD-12-15-16.png](LD-12-15-16.png): Pit limiter lights greyed, "Coming soon · #509" |
| LD-16 | Every strip Soon rows: Idle sweep #485, Engine start animation #743, Car data for AC, ACC and LMU #479 | ✅ | [LD-12-15-16.png](LD-12-15-16.png): Idle sweep #485, Engine start animation #743, Car data for AC, ACC and LMU #479, all greyed |
| LD-17 | Car Data | ✅ | [LD-17-downloading.png](LD-17-downloading.png) "Downloading…" while the page stayed live, then [LD-17.png](LD-17.png) "84 cars, updated just now." with the attribution and Update |
| LD-18 | Rename / Remove | ✅ | [LD-18.png](LD-18.png) "Remove Dash brow", "Removes the strip, its settings and its profile in SimHub.", Cancel / Remove it; strip gone from LedBars, log "Removed 1 RGB LED profile(s) from SimHub.", "Removed Dash brow and its profile." |
| AL-01 | Hardware tiles | ✅N | [AL-01.png](AL-01.png) Something else draws the Shape step; [AL-01-fanatec.png](AL-01-fanatec.png) Fanatec wheel fixes it to "3 · 9 · 3 Set by the wheel" and names it "Wheel rim (2)". Found in SimHub never shows on the VM (no Fanatec target): stand-in PanelLedsTests |
| AL-02 | LEDs at each end + LEDs in the centre | ✅ | [AL-02.png](AL-02.png) 2 at each end, 11 in the centre: preview and "15 LEDs in all, as 2 · 11 · 2." |
| AL-03 | SimHub device radios | ✅ | [AL-01.png](AL-01.png): GRID BY SIM-LAB MPX disabled with "No LEDs OpenDash can reach", "See SimHub's log." |
| AL-04 | Name | ✅ | [AL-01.png](AL-01.png) "Strip" for Something else; [AL-01-fanatec.png](AL-01-fanatec.png) "Wheel rim (2)" for a second Fanatec wheel |
| AL-05 | Footer | ✅ | [AL-01.png](AL-01.png) 'Installs "Strip" on Sim-Lab Dash SD43-LED.' |
| AL-06 | Add and install | ✅ | [AL-06.png](AL-06.png): Add and install → LedBars gains Strip 2-11-2 on device:f9295ce5…, log "Installed 1 RGB LED profile(s) into Sim-Lab Dash SD43-LED", "Added Strip. Restart SimHub, then select …" |
| AL-07 | Cancel | ✅ | Cancel on the sheet: settings JSON identical |

## MX Matrix

| id | control | result | capture or proof |
|---|---|---|---|
| MX-01 | Flag box profile line | ✅ | [matrix-1600.png](matrix-1600.png) profile absent: "OpenDash Flag box · Not installed · Install" ([MX-01-hover.png](MX-01-hover.png)); [MX-14.png](MX-14.png) once installed "OpenDash Flag box · 0.3.0-rc.7 · Reinstall". On B:old (an rc.1 plugin over the rc.7 profile) the line offers "Update" ([MX-01-update.png](MX-01-update.png)), which is bug 4 below. FlagBoxInstallPlanTests green |
| MX-02 | Your matrices + Add a matrix | ✅ | [MX-02.png](MX-02.png) Add a matrix sheet (Name "Matrix 3", where to set RGB Matrix content), then [MX-02-after.png](MX-02-after.png) a third card and "3 / 4"; a fourth fills the slots and the tile greys with "All four matrices are in use." ([MX-02-full.png](MX-02-full.png)); FlagBoxMatrixName gains "Matrix 3", "Matrix 4"; both removed again. PanelMatrixTests green |
| MX-03 | Fix box | ✅N | [MX-03.png](MX-03.png) Left pillar draws no fix box: SimHub does not say which matrix a device shows (#521). Stand-in: PanelMatrixTests |
| MX-04 | Preview chips | ✅ | [MX-04.png](MX-04.png): Idle display, Mid revs (the gear in the first shift colour), Yellow, Blue, Pit limiter, Car left, Low fuel, Chequered each draw the sheet's glyph; on Left pillar (Idle display Dark) seven chips, no Mid revs ([MX-03.png](MX-03.png)). PanelEmulationTests green |
| MX-05 | Flags / Pit lane / Spotter / Warnings | ✅ | [MX-05.png](MX-05.png): Flags, Pit lane, Spotter, Warnings off → FlagBoxFlags/Pit/Spotter/Warnings[0] false; all restored |
| MX-06 | Critical flags only | ✅ | Critical flags only → FlagBoxMatrixCriticalOnly[0] true, and back |
| MX-07 | Mounting side | ✅ | Mounting side Left → FlagBoxSide[0] "left", back to "both" |
| MX-08 | Spotter bar animation | ✅ | Spotter bar animation → FlagBoxSpotterAnimation true (one value for the rig; "Every matrix." under it), and back |
| MX-09 | Triggers link (Warnings) | ✅ | [MX-09.png](MX-09.png): Triggers opens Settings with Alerts marked in the page's index; the Alerts table is already inside the view at this height, so nothing scrolls |
| MX-10 | Idle display | ✅ | [MX-10.png](MX-10.png) Left pillar Idle display Gear → FlagBoxRest[1] "gear", FlagBoxMatrixGear[1] true, Shift colours / Car-specific / Redline sub-rows appear; Dark → back |
| MX-11 | Shift colours / Car-specific shift points / Redline flash | ✅ | [matrix-1600.png](matrix-1600.png) and [MX-base.png](MX-base.png) "Porsche 911 GT3 R (992) is in Car Data." under Car-specific shift points (before the download: "Car Data is not downloaded yet…"); Shift colours off → GearBands[0] false and hides the two rows under it; Car-specific and Redline flash → GearCarLadder / GearBlink false; all restored |
| MX-12 | RPM colour for everything #371 / SimHub device #363 / Priority order #505 | ✅ | [MX-12.png](MX-12.png): RPM colour for everything #371, SimHub device #363, Drag to reorder #505, greyed |
| MX-13 | Rename / Remove | ✅ | [MX-13.png](MX-13.png) "Remove Matrix 4": "Removes Matrix 4 and its settings. The flag box profile stays in SimHub.", Remove it; Left pillar removed the same way: FlagBoxMatrixName[1] null, Flags/Pit/Spotter/Warnings[1] false, Rest dark (the slot goes dark; AddMatrixPanel sets the rest afresh on the next add) |
| MX-14 | Reinstall (profile line) | ✅ | [MX-14.png](MX-14.png) Install → log "Installed 1 matrix profile(s) into SimHub, 0 of them replacing…", "Installed OpenDash Flag box. Select …"; [MX-14-reinstall.png](MX-14-reinstall.png) Reinstall → "1 of them replacing a copy already there", "Reinstalled OpenDash Flag box." |

## SH Shortcuts

| id | control | result | capture or proof |
|---|---|---|---|
| SH-01 | Groups | ✅ | [SH-01.png](SH-01.png): Main dash 9 rows, Rim 9, Pit wall 1, Phone 1 with the paging paragraph and crumbs, Lights (Night mode, Brightness up, Brightness down, Rig test Soon) "3 of 3" once bound ("0 of 3" before, [ST-17.png](ST-17.png)), Alerts (Alert dismissal Soon); the removed Round's group is gone. PanelShortcutsTests and ScreenActionsTests green |
| SH-02 | Filter | ✅ | [SH-02.png](SH-02.png): Bound shows the seven bound rows (Rig test and Alert dismissal left out), Not bound the rest |
| SH-03 | Bind | ✅ | [SH-03-binder.png](SH-03-binder.png) SimHub's "Pick a control" took F11 (pressed over VNC) for Rim › Zone A next page; [SH-03.png](SH-03.png) the row shows KeyboardReaderPlugin F11 and "4 of 9". After a graceful close PluginManagerSettings.json maps OpenDash.RimCycleZoneC to KeyboardReaderPlugin.F7 (bound by `vm bind`) |
| SH-04 | Press cycles | ✅ | [SH-04.png](SH-04.png): F7 walks Rim's zone C Leaderboard 3/21 → Relative 4/21 → Opponents 5/21 in the live preview (the zone's position is a property, not a setting: the JSON does not move) |
| SH-05 | Clash line | ✅ | [SH-01.png](SH-01.png) with F7 on Main dash zone B and Rim zone C: "Keyboard · F7 cycles zone B on Main dash and cycles zone C on Rim.", both rows outlined in amber |
| SH-06 | Order honoured | ✅ | [SH-04.png](SH-04.png): the presses follow the order dragged in SC-13 (Track, Gear, Leaderboard, Relative, Opponents) |
| SH-07 | Previous page | ✅ | [SH-04.png](SH-04.png): F6 (RimCycleZoneCBack) steps back from Opponents to Relative |
| SH-08 | Quick glance (Hold) | ✅ | [SH-08.png](SH-08.png): F8 held shows Track, released returns |
| SH-09 | Clear | ✅ | [SH-09-hover.png](SH-09-hover.png) Change / Clear / Add on the bound row; Clear removed the F11 mapping: the row reads Click to configure, "3 of 9", and after a graceful close no OpenDash.RimCycleZoneA is left in PluginManagerSettings.json |
| SH-10 | Night mode action | ✅ | N → LightsNightMode true; [SH-10.png](SH-10.png) the sidebar switch follows; N again → false |
| SH-11 | Brightness up / Brightness down | ✅ | F9 / F10: 79 → 100 (clamped) and 100 → 10 (clamped after eleven presses), +70 → 80; with night on F9 moved LightsNightBrightness 24 → 34 |
| SH-12 | Bindings survive restart | ✅ | graceful close (WM_CLOSE, gone in 2 s), start: [SH-12.png](SH-12.png) F7 still on Main dash zone B and Rim zone C, "7" in the sidebar |
| SH-13 | Removed screen | ✅ | [ST-17.png](ST-17.png) after Remove Round (SC-07): no Round group |

## ST Settings

| id | control | result | capture or proof |
|---|---|---|---|
| ST-01 | On this page | ✅ | [ST-light.png](ST-light.png): the index row (Race data, Flags, Alerts, Lighting, Appearance, Driver); Lighting scrolls the page to its end, where Lighting sits, and marks the link |
| ST-02 | Race data › Position | ✅ | [ST-02-11.png](ST-02-11.png); Position Overall / Class → PositionMode overall / class |
| ST-03 | Delta reference | ✅ | Delta reference Last lap → DeltaReference "lastlap"; back to session |
| ST-04 | Delta precision | ✅ | Delta precision Thousandths → DeltaPrecision "thousandths"; back |
| ST-05 | Session progress | ✅ | Session progress Laps / Time / Auto → SessionProgress laps / time / auto. E:untimed not driven |
| ST-06 | Driver names ×4 / Team names / Clock | ✅ | Driver names L. Byrne / B. Llam / Byrne Llam → DriverNameFormat initialSurname / initialFirstName / surnameFirst; Team names → DriverNameTeam true; Clock 2:32 PM → ClockFormat 12h; all restored. PanelDataTabTests green |
| ST-07 | Fuel target per lap #326 / Tyre display #325 (Temperature \ | ✅ | [ST-07-10.png](ST-07-10.png): Fuel target per lap #326, Tyre display #325, greyed |
| ST-08 | Units | ✅ | [settings-1600.png](settings-1600.png): Units "km/h · °C · psi · L", caption "Set in SimHub." |
| ST-09 | Flags › Blue flag detail | ✅ | Blue flag detail Class / Position and class / Nothing → BlueFlagDetail class / positionClass / none |
| ST-10 | Yellow flags #504 | ✅ | [ST-07-10.png](ST-07-10.png): Yellow flags greyed, "Coming soon · #504" |
| ST-11 | Flags in the pit lane | ✅ | Flags in the pit lane off → FlagsInPitLane false, and back. E:pit not driven (band D in the lane and the matrix pit glyph not looked at) |
| ST-12 | Alerts › Trigger | ✅ | [ST-12.png](ST-12.png) typed into the boxes over VNC: Low fuel 3 → FlagBoxLowFuelLaps 3 ("laps"), [ST-12-one.png](ST-12-one.png) 1 reads "lap"; Oil 100 / Water 95 → LightsOilTemp 100, LightsWaterTemp 95 and FlagBoxMatrixOilTemp / WaterTemp all four [100…] / [95…]; low fuel back to 2. PanelSettingsTests green |
| ST-13 | Alerts Soon rows and columns | ✅ | [ST-13.png](ST-13.png): Tyre wear and Pit window open #507, Incidents #508, Hybrid battery low #737, and the Screens / LEDs / Matrix / Races only columns #512; at 1000 px the columns fold into an "Alert display Soon" row ([settings-1000.png](settings-1000.png)) |
| ST-14 | Try (an alert row) | ✅ | [ST-14.png](ST-14.png): Try on Low fuel opens Rig with the Low fuel chip picked |
| ST-15 | Lighting preview | ✅ | [ST-15.png](ST-15.png) Day / Night: the preview dims to the night brightness; settings JSON identical |
| ST-16 | Brightness / Night brightness | ✅ | [ST-16.png](ST-16.png): Brightness dragged to 79, Night brightness to 24 → LightsBrightness 79, LightsNightBrightness 24 |
| ST-17 | Night mode / Night mode button | ✅ | Night mode switch → LightsNightMode true, false (SB-09 agreement); [ST-17.png](ST-17.png) the "Not bound" chip opens Shortcuts with the Lights group's Night mode row in view |
| ST-18 | Sim time of day and Screen dimming #740 / Appearance / Driver Soon rows | ✅ | [ST-18.png](ST-18.png): Sim time of day and Screen dimming #740, Theme #735, Name #484, greyed (Appearance and Driver rows) |
| ST-19 | Every shared property reachable | ✅ | PanelDataTabTests green |

## UP Updates

| id | control | result | capture or proof |
|---|---|---|---|
| UP-01 | Available | ❌ fixed | B:old: [UP-01.png](UP-01.png) "OpenDash 0.3.0-rc.7", "You have 0.3.0-rc.1", "Needs a SimHub restart. Your settings are kept.", Download, Release notes, Every release on GitHub. The notes line first read "The candidate that has something to say when there is nothing to instrument. A dashboard is" ([UP-01-ask.png](UP-01-ask.png)), the first wrapped line of CHANGELOG's paragraph; fixed in 3089738a and re-run on a fresh rc.1 build: [UP-01.png](UP-01.png) reads the whole first sentence. UpdateWordingTests and PanelUpdatesTests green. Second pass, B:old built from a copy of the branch with VERSION 0.3.0-rc.1: [UP-01-rerun.png](UP-01-rerun.png) after Check now, "OpenDash 0.3.0-rc.7", "You have 0.3.0-rc.1", "Needs a SimHub restart. Your settings are kept.", Download, Release notes reading the whole first sentence "The candidate that has something to say when there is nothing to instrument.", Every release on GitHub, and the sidebar badge "0.3.0-rc.7" |
| UP-02 | Up to date | ✅ | [UP-02.png](UP-02.png) B:cur, Check now: "You have the newest release, 0.3.0-rc.7." |
| UP-03 | Unreachable | ⏭ | not run in either pass: pointing api.github.com at 127.0.0.1 in the guest's hosts file was refused by the session's permission rules. Stand-in: UpdateWordingTests / PanelUpdatesTests / DashboardInstallerTests pin "Could not reach GitHub. You have …" |
| UP-04 | Check for updates off | ✅ | [UP-04.png](UP-04.png): switch off → CheckForUpdates false and Check now greys (nothing can be fetched); LastUpdateCheckTicks unchanged; switched back on |
| UP-05 | Last checked | ✅ | [UP-02.png](UP-02.png) "Last checked today, 08:23" after Check now (guest clock) |
| UP-06 | Download | ✅ | B:old: [UP-06-downloading.png](UP-06-downloading.png) "Downloading" with its bar (38%); [UP-06.png](UP-06.png) SimHub's "Restart SimHub to finish updating" dialog, the card "OpenDash is downloaded. Restart SimHub to finish updating." and the badge "Restart"; answered No ([UP-06-staged.png](UP-06-staged.png)). Then `bun run vm plugin --no-build --menu` with the branch build: [UP-06-after.png](UP-06-after.png) reads 0.3.0-rc.7 with no card and no badge, and the branch's own sheet fix is on screen ([SC-07-fixed.png](SC-07-fixed.png)), so it is the branch DLL that runs. The staged OpenDash.dll.pending and its swap script were then deleted by hand so that no later graceful close swaps the release in |
| UP-07 | In SimHub table | ✅ | [updates-1600.png](updates-1600.png) Up to date / Missing / Not installed; SC-06 card "Restart SimHub to load it"; [UP-01.png](UP-01.png) on B:old the profiles read "Update available" with an Update press. The profiles there were newer than the plugin (0.3.0-rc.7 over an rc.1 plugin) and were still offered as an update: see the bugs. PanelUpdatesTests green |
| UP-08 | Reinstall everything | ✅ | [UP-08.png](UP-08.png) Reinstall everything: asked first about the edited Main dash and Pit wall ("Replace anyway", [UP-09-ask.png](UP-09-ask.png)), then "Reinstalled 4 dashboards. Close and reopen your dashboards to see them." with every row Up to date; log "Previous copy kept as … _yours_20261002-082500.zip". It also installed Wheel rim's profile, which had never been installed (the line reads "Installed Wheel rim's profile …") |
| UP-09 | Kept copy | ✅ | OpenDash 1280x480.djson edited by hand (a byte appended), Reinstall everything: [UP-09.png](UP-09.png) "Kept copies — Your edited Main dash and Pit wall were kept. OpenDash replaced them." with Put mine back; [UP-09-back.png](UP-09-back.png) "Put back 2 dashboards." (the card stays, by ruling 5) |
| UP-10 | Copy a support report | ✅ | UP-10: "Support report copied. Paste it into your issue."; the desktop clipboard holds versions, the four screens, two strips, the matrix, Car Data and the OpenDash log tail; nothing sent. It names SimHub as "1.0.0.0": see the bugs |
| UP-11 | Open the log | ✅ | [UP-11.png](UP-11.png): Open the log opens Explorer on SimHub\Logs |
| UP-12 | Links: Every release on GitHub, Report an issue, Read the guide | ✅ | [UP-12-issue.png](UP-12-issue.png) Report an issue → Edge on github.com/xorob0/OpenDash/issues; [UP-12-guide.png](UP-12-guide.png) Read the guide → the README; Every release on GitHub is drawn in the Available card ([UP-01.png](UP-01.png)) |

## RS Responsive · FL Flows

| id | control | result | capture or proof |
|---|---|---|---|
| RS-01 | width ≈ 700 (`WINDOW_HELPER.Fit`) | ✅ | ≈700 (SimHub client 700, panel 485): the eight -700 captures: icon rail, one column, Screens cards 2-up, nothing clipped (Zone A's "Gear, speed, revs" is trimmed with an ellipsis inside its zone, by design). PanelShellTests green |
| RS-02 | ≈ 1000 | ✅ | ≈1000 (panel 785): the eight -1000 captures: rail (56 px), Screens cards 4-up (the rule gives 4 at this width, see SC-35), the zone aside stacked under the face plan, Settings' alert columns folded into an "Alert display" row |
| RS-03 | ≈ 1600 | ✅ | 1600 (panel 1372): the eight -1600 captures: full sidebar (216), the column fills the width, two blocks side by side on LEDs and Screens, row controls at the right edge, captions wrapped |
| RS-04 | maximised 3840 | ✅ | [screens-max.png](screens-max.png) and the other -max captures: SimHub 9.12.6 itself centres its plugin page in a column about 1,400 px wide at 3840 (its own "OpenDash" page title and the licence footer sit in the same column); the panel fills that column edge to edge, with no centring or cap of its own, no empty band on the right, the sheet at the column's right edge and the dash previews inside the window |
| RS-05 | record achieved widths | ✅ | achieved widths: SimHub took 700, 1000 and 1600 as asked (client 700 × 2120 etc.) and 3840 × 2120 maximised; the panel was 485, 785, 1372 and about 1400 wide. run.json was not written, since panel-shots could not run (see the bugs) |
| FL-01 | start, open | ✅ | R0, B:cur: [FL-01.png](FL-01.png) Home "Nothing to fix" with the Add a screen tile and "Add the screen your rig has." in place of Right now and Quick controls; [FL-01-screens.png](FL-01-screens.png) "No screens yet." over the tile; counts 0 everywhere; no wizard |
| FL-02 | Add screen → restart | ✅ | R0 → Add screen → restart: Home "1 thing to fix" ([FL-02-home.png](FL-02-home.png)) → "Nothing to fix" ([FL-02.png](FL-02.png)), Right now › Screens "Rim 850 × 480" with a green dot |
| FL-03 | start | ✅ | RF (Rim's folder deleted): [HM-04.png](HM-04.png) and [updates-1600.png](updates-1600.png) "Rim · dashboard · Missing" |
| FL-04 | install without --menu | ✅ | second pass: OpenDash taken out of the left menu (ShowInMainMenu false in PluginsActivation.json) and `bun run vm plugin --no-build` without --menu: the left menu has no OpenDash and gains "Additional plugins" ([FL-04-menu.png](FL-04-menu.png)); there the panel sits under an "OpenDash" tab, the same panel (Home, the five rows, Quick controls) a little narrower ([FL-04.png](FL-04.png)), and its pages work (FL-05 and FL-06 below were driven from it) |
| FL-05 | from the VM's _Backups | ✅ | second pass: the oldest settings file the VM's backups hold (the share's opendash-settings-out.json, 2026-09-27, eight faces, three strips, Matrix 1, without DeltaPrecision, ClockFormat, FlagsInPitLane or the matrix layout) written with SimHub stopped. After start the file keeps all eight faces with their namespaces and folders, the three strips (namespaces LedStrip, LedopenDash090, Led393) and Matrix 1, and gains DeltaPrecision hundredths, ClockFormat 24h, FlagsInPitLane true; no [OpenDash] warning or error in the log; the panel opens ([FL-05.png](FL-05.png)) on "1 thing to fix" with all eight screens, three strips and the matrix. SettingsTests (migrations) green |
| FL-06 | inject a bad settings value | ✅ | second pass, two values. A face with Width 0 and Height 0 ("Broken") written with SimHub stopped: SimHub starts, the installer refuses it and logs `ERROR [OpenDash] Installing OpenDash Broken failed: … mentions OpenDash.Face0x0 nowhere`, Home lists "Broken's dashboard is missing from SimHub", and Screens draws its card with "OpenDash no longer ships a 0 × 0 face. Your settings are kept." ([FL-06.png](FL-06.png)); Rig draws it too ([FL-06-rig.png](FL-06-rig.png)). No page threw, so the page-failed line ("This page could not be drawn. See SimHub's log.") was not reached; no settings value tried reaches it, since Normalise absorbs them. A value of the wrong type (`"Rig": 5`): SimHub survives and logs the Newtonsoft exception, but OpenDash starts on defaults and saves them over the file ([FL-06-unreadable.png](FL-06-unreadable.png)): see bug 5 |
| FL-07 | change, Stop-Process -Force | ✅ | Settings › Delta precision Thousandths, then Stop-Process -Force on SimHubWPF half a second later: the settings file already held "thousandths", and after the next start still did |
| FL-08 | display at 1280×800 | ✅ | precondition held: the display check (guiProblem) passed at 3840 × 2160 before the first click |
