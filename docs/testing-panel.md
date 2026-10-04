# Testing the settings panel

This is the scenario matrix for the panel rebuilt under #503: one table per page, one row per control, each row saying the fixture it needs, what to do, what to expect and how it is proven. A row names a control by the word the page draws, which is the one in `Panel*.cs`; where the artboard says something else, docs/design/plugin.md's departures table says why. When a row and the page disagree, fix the row, never the page. The routine around it is in [testing-vm.md](testing-vm.md) and [dev-loop.md](dev-loop.md); the captures a run produces come from `bun run panel-shots`.


Conventions. Fixtures: `R0` `bun scripts/rig.ts empty` (first run: `OpenDash.GeneralSettings.json`, its `PluginsData\Common\_Backups\OpenDash.GeneralSettings_b*.json` copies and every OpenDash folder in DashTemplates deleted); `R1` `bun scripts/rig.ts clear` (no screens, every other setting kept); `RF` `bun scripts/rig.ts panel` (Main dash 1280×480 stock ns, Rim second 1280×480 with its DashTemplates folder deleted, Pit wall 1920×1080, Phone 480×850, Round 480; bars Wheel rim 3-9-3-fanatec and Dash brow 15; matrices 1 Flag box, 2 Left pillar); `E:<name>` emulator scenario running (`bun run emulator start <name> --replace`), `E:none` stopped; `B:cur` the branch's `bun run package`; `B:old` a throwaway build with `VERSION` hand-edited to `0.3.0-rc.1` (never committed) so Updates sees the real newer release; `K:on` `bun run vm bind <action> <key>` (enables the keyboard reader, restarts SimHub), `bun run vm unbind` after. Every fixture claims the VM first (`bun run vm claim`).
How: `U` unit test on a pure model (file named); `V` VM capture/click (capture `build/panel/<id>.png`, promoted to `media/503/<id>.png` when cited); `J` JSON/log/property proof (`OpenDash.GeneralSettings.json` diff via `run_powershell ConvertFrom-Json`; the Available properties page; `[OpenDash]` lines in `Logs\SimHub.txt`; `PluginManagerSettings.json` mappings); `N` not verifiable on the VM, with the stand-in.
Two facts every row assumes: the panel saves on every change, so a JSON diff is valid at once; SimHub on the VM is force-killed, so nothing is proven by `End()` and persistence rows close SimHub gracefully first (WM_CLOSE through `WINDOW_HELPER.Close`, 20 s, then kill).
Deferred controls (#504 #505 #506 #507 #508 #509 #510 #511 #512) are verified only as greyed rows with the right hover; nothing behind them is exercised.

## SB Sidebar and shell
| id | control | setup | action | expected | how |
|---|---|---|---|---|---|
| SB-01 | Wordmark + version | RF, B:cur | Open OpenDash from SimHub's left menu | wordmark, the assembly version | U PanelShellTests; V |
| SB-02 | Search | RF | Click the box | Draws with the placeholder "Search"; no log error | V (typing reaches the rail's flyout box over VNC, as #529 found); U PanelSearchTests (index covers every row label; "blue" → "Blue flag detail · Settings"; no match → "No setting matches."; a hit navigates + scrolls) |
| SB-03 | Live card | RF, E:race | Observe | "Live · <game>", car, track · session, green dot | V; J GameRunning |
| SB-04 | Live card, no game | RF, E:none | Stop emulator | "No game running", grey dot | V |
| SB-05 | Nav items + counts | RF | Observe | Home, Rig, sep, Screens 5 (dot), LEDs 2 (dot), Matrix 2, sep, Shortcuts N, Settings; Night mode and Updates at the foot. Matrix wears no dot for a dark matrix until #521 | U PanelNavTests; V |
| SB-06 | Active item | RF | Click each | accent bar moves; heading matches | V (8 canonical page captures) |
| SB-07 | Page remembered per sitting | RF | Go to Settings, leave OpenDash, return | Settings shown | V |
| SB-08 | Page not persisted | RF | Restart SimHub | Home | V |
| SB-09 | Night mode (sidebar foot) | RF | Flip | on; the Night mode switch on Rig's header, Home's Quick controls and Settings › Lighting agrees; LightsNightMode true | J; V ×4 |
| SB-10 | Updates badge | B:old | Observe | amber version badge; "Restart" once a download is staged | U PanelNavTests; V |
| SB-11 | Up to date | B:cur | Observe | no badge | V |
| SB-12 | Warn dots follow Home items | RF → restart | Observe | Screens dot clears when no item remains | U PanelNavTests; V |
| SB-13 | Icon rail | RF | Width ≈ 900 | 56 px rail, labels as tooltips ("<page> · <count> · Needs attention"), the search icon ("Searches every setting.") opens the flyout | V |

## HM Home
| id | control | setup | action | expected | how |
|---|---|---|---|---|---|
| HM-01 | Heading count | RF | Open Home | "N things to fix", N the items drawn ("1 thing to fix" for one) | U PanelAttentionTests; V |
| HM-02 | Nothing to fix | R1 after restart | Open Home | "Nothing to fix", no card | U; V |
| HM-03 | Item: written, not restarted | R1, Add screen, no restart | Home | "<name> is not in SimHub yet", step "Restart SimHub to load it", "Open <name>" → Screens with it selected | U (written-since-start flag); V |
| HM-04 | Item: folder missing | RF (Rim folder deleted) | Home | "Rim's dashboard is missing from SimHub", "Its settings are kept.", "Install it again" | U; V; J folder reappears |
| HM-05 | Item: profile not selected | RF | Home | "<strip>'s profile is not selected", three steps + Check again; the answer starts "Checked again." | U over LedDeviceSurvey fixture; V wording; N resolved branch (no LED device on the VM) |
| HM-06 | Item: matrix not shown | RF | Home | "Left pillar is not shown in SimHub", "No matrix device in SimHub is set to matrix 2.", "Open Left pillar" → Matrix with Left pillar selected | U; N on every rig until #521 (SimHub does not say which matrix a device shows, so the item is never drawn) |
| HM-07 | Item: update pending restart | B:old after Download | Home | "Restart SimHub to finish updating", "Open Updates" | U; V |
| HM-08 | Right now › Screens | RF, E:race | Observe | one row per screen with current pages, dot | U; V; J zone properties |
| HM-09 | Right now › LEDs | RF, E:shiftlights | Observe | strip lit from CarLights.Run while Ready, else dark; line | V; J LedMirror properties |
| HM-10 | Right now › Matrix | RF | Observe | "Matrix 1 · Gear" over the idle glyph; matrix 2 dark | U PanelEmulationTests glyphs = build sheet; V |
| HM-11 | Card links | RF | Click each "Open" | Screens / LEDs / Matrix | V |
| HM-12 | Brightness (Quick controls) | RF | Drag to ~50 | numeral follows; LightsBrightness 50; Settings agrees; while night is on it edits LightsNightBrightness | V (`drag`); J |
| HM-13 | Night mode (Quick controls) | RF | Flip | as SB-09 | J; V |
| HM-14 | Flags and spotter › Open Rig | RF | Click | Rig, on a flag chip unless one was picked this sitting | V |

## RG Rig
| id | control | setup | action | expected | how |
|---|---|---|---|---|---|
| RG-01 | Tiles for every device | RF | Open | 9 tiles; warning dots on the two with issues | U PanelRigMapTests (tiles, default layout); V |
| RG-02 | Drag a tile | RF | `drag` Main dash 200 px right | moves; LayoutX/Y persisted | V; J |
| RG-03 | Reset layout | after RG-02 | Click | home positions; layout cleared | V; J |
| RG-04 | Preview, 18 chips | RF | Click each (Flags, Spotter, Pit lane, Warnings, Revs) | tiles repaint per PanelEmulation rules | U (scenario → colours; glyph names vs sheet); V (contact sheet of 18) |
| RG-05 | Real hardware | RF | Hover | greyed, "Coming soon · #506"; nothing published | V; J no property change |
| RG-06 | Night mode (header) | RF | Flip | LED tiles dim to LightsNightBrightness/100; screens do not dim; SB-09 agreement | V; J |
| RG-07 | Revs chips | RF, E:shiftlights | Idle / Mid revs / Shift point | rev bars and gear colour follow | V |
| RG-08 | Hint and Reset layout | RF | Observe | "Drag to arrange like your rig"; "Reset layout" in the header | V |

## SC Screens · AS Add a screen
| id | control | setup | action | expected | how |
|---|---|---|---|---|---|
| SC-01 | Cards | RF | Open | 5 cards (thumb by kind, name, kind, state: In SimHub / Missing / Restart SimHub to load it; the header carries "<kind> · <size>") + Add a screen tile | U PanelScreensTests; V |
| SC-02 | Select | RF | Click Pit wall | header + pit pane | V |
| SC-03 | Fix box | RF (Rim) | Select Rim | Missing; "This screen's settings are kept."; Install it again | U; V |
| SC-04 | Edit sheet | RF | Click Edit | Name, Size or Orientation, Dashboard › Reinstall; prefilled; Cancel writes nothing | V; J hash unchanged |
| SC-05 | Rename via Edit | RF | name via JSON, Save | card/header renamed; namespace and folder unchanged | J; N typing → U name rules |
| SC-06 | Duplicate | RF | Click on Main dash | new card, own namespace/folder, settings copied, restart line | U DuplicateScreen tests; J; V |
| SC-07 | Remove | RF | Remove Round | sheet "Remove Round": "Removes the screen and its dashboard." (a face also names its settings and "Any wheel button you bound to it stops working."); Remove it / Keep it; card and folder gone, the shared Cards kept | U PanelConfirmationTests; V; J |
| SC-08 | Click zones and bar | Main dash | Click each | accent border; aside switches | U PanelFacePlanTests; V ×5 |
| SC-09 | Zone page list | zone C | Observe | ticked first in order, First tag, "4 of 21", Show all reveals unticked + Not in iRacing + Soon Circle tracker #320 / Launch #152 | U; V |
| SC-10 | Tick/untick | zone C | Untick Track | mask bit cleared; count; ZoneCPages property | J; V |
| SC-11 | Last page stays | zone A one page | Untick | refused; hover "A zone keeps at least one page." | U; J unchanged |
| SC-12 | Untick the shown page | zone C on Track | Untick | zone snaps forward in order | J ZoneC |
| SC-13 | Drag to reorder | zone C | `drag` Relative below Leaderboard | order persisted; First moves; ZoneCPosition follows | J; V; then SH-07 |
| SC-14 | Show all / Only ticked / All / None | zone C | Toggle | list grows/shrinks; None keeps the start page | V; J |
| SC-15 | My class only | zone C | Flip | ClassOnly[C]; zone A has no row | J; V |
| SC-16 | Next page / Previous page chips | RF, K:on | Observe | binding text or "Not bound"; click opens Shortcuts | U; V |
| SC-17 | Info bar fields | bar selected | Left, first → Lap | BarFields; property | J; V |
| SC-18 | Rev bar | Main dash | Off | RevBar off; dash loses the well | J; V |
| SC-19 | Flag display | Main dash, E:yellow | Band D → Full screen | FlagFormat; full-screen yellow | J; V |
| SC-20 | Lap review | Main dash | Races / Always / Off | LapReview | J |
| SC-21 | Quick glance | K:on | Track in Zone C; bind F8 on Shortcuts | setting; mapping PressType During; `captureWhileHeld(f8)` shows Track then returns | J; V |
| SC-22 | Seven Soon rows | Main dash | Hover each | greyed; "Coming soon · #N" (Rev fill under the lights, Spotter at the rev bar ends, Pit page in the pit lane, Pop-ups, Edge lights for the delta, Screen care, Fit) | U PanelSoonTests; V; J unchanged on click |
| SC-23 | Details | Main dash | Expand | SimHub name, Folder as stored, Properties OpenDash.<ns>*, Version | V |
| SC-24 | Clash line | zone C starting on Track (its First page) and Quick glance on Zone C › Track | Observe | FacePageClash warning under the zone aside, allowed | U; V |
| SC-25 | Pit: Page on screen | Pit wall | Race / Tower / Telemetry | PitWallPage; picture and zone rows change | J; V ×3 |
| SC-26 | Pit: Race zones | Pit wall | Zone A → Relative | PitWallZones; property; no Board row (the race board is fixed) | J |
| SC-27 | Web view address | Pit wall | set via JSON; observe | shown; `ftp://` ignored; empty hover "Sets what the web view shows, from an http or https address." | J; U |
| SC-28 | Pit: My class only / Flag display / Quick glance | Pit wall | Each | properties | J |
| SC-29 | Portrait layout A–D | RF + 1080×1920 | Set | four zone settings read by the portrait package | J; V |
| SC-30 | Phone: Modules | Phone | Untick Sectors | Modules[i]; "17 of 21"; Not in iRacing beside three | J; V |
| SC-31 | Phone: First module | Phone | Pick Delta | CompanionStart; forced at once | J |
| SC-32 | Phone: Flag display | Phone, E:yellow | Full screen | CompanionFlagFormat; companion shows it | J; V |
| SC-33 | Phone: Quick glance + Next module | Phone | Observe | CompanionQuickGlance select; PanelCopy.CompanionPaging verbatim + crumbs Controls and events › NextScreen | U PanelCopyTests |
| SC-34 | Round: Cards / Rev ring | Round | Card 1 → Current lap; Rev ring Off; hover Zones instead of cards #146 | Slot01; RevBar; tooltip | J; V |
| SC-35 | Card columns at width | RF | Resize (RS) | 6 → 4 → 2 columns (six, fewer where a card would fall under CardMinWidth); Add a screen tile last | U PanelShellTests; V |
| AS-01 | Open sheet | RF | Add a screen tile | sheet over dimmed page, close icon | V |
| AS-02 | Kind tiles | sheet | Dash face / Pit wall / Companion / Round | Size tiles follow the kind; Orientation (Landscape \| Portrait) for pit wall and companion | U PanelAddScreenTests; V |
| AS-03 | Flags screen #116 | sheet | Hover / click | greyed, "A second display for flags", tooltip, no selection | U; V |
| AS-04 | Your displays #85 | sheet | Hover | tooltip | V |
| AS-05 | Size tiles + Name prefill | sheet | Click 850×480 | selected; Name prefilled; second-copy note (PanelAddScreen.Note) when true | U; V |
| AS-06 | Next steps | sheet | Observe | "Restart SimHub, then assign "{name}" to this display in Dash Studio." | U copy |
| AS-07 | Cancel | sheet | Click | closes, no write | V; J |
| AS-08 | Add screen | sheet, R1 | Click | card "Restart SimHub to load it"; folder written; Say(Added); Home gains HM-03 | J; V |
| AS-09 | Add screen then restart | AS-08 | stop + start | card In SimHub; listed in Dash Studio (`openDashboard` by name) | V; J |

## LD LEDs · AL Add an LED strip
| id | control | setup | action | expected | how |
|---|---|---|---|---|---|
| LD-01 | Cards | RF | Open | Wheel rim 3 · 9 · 3, Dash brow 15, Add an LED strip tile; states Showing / Not selected in SimHub / Update available / Installed / Not installed | U PanelLedsTests; V |
| LD-02 | Header chip and profile state | Wheel rim | Observe | "Fanatec wheel · 3 · 9 · 3"; Install, Update, or no press when current | U; V |
| LD-03 | Fix box | Dash brow | Observe; Check again | steps; "Checked again." line; an `[OpenDash] LED device` line is logged when a device is new or its line changed | V; J `[OpenDash] LED device` lines; N resolved branch |
| LD-04 | Preview, 6 chips | Wheel rim, E:shiftlights | Live / Shift point / Yellow / Blue / Car left / Pit limiter | frames per rule; Live follows CarLights.Run | U; V; J |
| LD-05 | All devices at once | | Click | Rig | V |
| LD-06 | Car's own rev lights | E:race | Flip | RpmStyle car\|leftToRight; "<car> is in Lovely Car Data." / "… is not in Lovely Car Data." (drive a car without a table via the emulator car name) / no line when off | U; J; V ×3 |
| LD-07 | Rev light width | on | Actual size | LedMirrorFit; hidden when off | J; V |
| LD-08 | Centre display | | RPM / Brake / Throttle and brake / Fuel | bar LedCentre | J |
| LD-09 | SimHub device | | Open | states: none ("No SimHub device has LEDs. …") / one ("Goes to <device>.") / gone ("Device not in SimHub") / not connected | U LedDeviceSurveyTests; V; N populated list |
| LD-10 | Brightness | | 60% | bar LedBrightness property; default label "Same as rig · N%" | J; U |
| LD-11 | Reverse direction | | Flip | ProfileShapeId -reversed; profile reinstalled; NCalc tester on the remap | J; N hardware |
| LD-11b | Fanatec compatibility mode | Wheel rim; then Dash brow | Flip off, then on; on the brow, observe | Fanatec false / true; ProfileShapeId 3-9-3 / 3-9-3-fanatec; profile reinstalled; header chip "Strip ·" / "Fanatec wheel ·"; Reverse direction hidden while on; no row on the brow (a bare run has no Fanatec wiring) | U LedBarTests, PanelLedsTests; J; N hardware |
| LD-12 | Each LED in turn #434 | | Hover | greyed | V |
| LD-13 | Effects | Wheel rim / Dash brow | Observe; flip TC | 15 switches with ends, 9 on the brow (PanelLeds.EffectsFor); LedEffectTc property false; profile EnabledFormula reads it | U; V; J NCalc tester |
| LD-14 | Flag animation / Full-strip spotter | | Flip | FlagAnimation, SpotterWhole; Full-strip hidden on the brow | J; V |
| LD-14b | Infer wheel spin and wheel lock | Wheel rim / Dash brow | Flip off, then on; on the brow, observe | bar LedInferSlip false / true, on by default; no row on the brow (a bare run has no TC or ABS lamp); with it on and a wheel spun on iRacing, OpenDash.WheelSpin true and the TC lamp lit | U PanelLedsTests, SlipEstimateTests; J; N hardware |
| LD-15 | Pit limiter lights #509 | | Hover | greyed | V |
| LD-16 | Every strip Soon rows: Idle sweep #485, Engine start animation #300, Car data for AC, ACC and LMU #479 | | Hover | greyed | V |
| LD-17 | Lovely Car Data | network | Download (Update once a copy is held) | off the UI thread; "Downloading…" then the status line; attribution; log | J; V |
| LD-18 | Rename / Remove | | Remove Dash brow | asks once (Remove it); strip gone; profile uninstalled | U; J |
| AL-01 | A Fanatec device ticks Fanatec compatibility mode | FanaBridge wheel added | Open; pick the Arduino, then the wheel again, under SimHub device; then flip the switch on with the Arduino picked, and pick another device not named Fanatec | No Hardware step: the sheet opens on the Fanatec wheel with Fanatec compatibility mode on at 3 · 9 · 3, the ends offering 1 to 5 and no None; the Arduino turns it off and offers None again; the wheel turns it on; once flipped on by hand it stays on across a device not named Fanatec; name "Wheel rim" while on (#686) | U PanelLedsTests; V |
| AL-01b | Strip lands in the wheel's own list | FanaBridge wheel added, its page opened once | Add and install on the wheel, then Devices, the wheel, Telemetry LEDs | the new profile is in the list without a restart, and the log reads "LED module reached through its settings page" (#686) | V |
| AL-02 | LEDs at each end + LEDs in the centre | Something else | 2 / 11 | preview + BarShapeNote; shape id exists in the census | U; V |
| AL-03 | SimHub device radios | | Observe | declined disabled with "No LEDs OpenDash can reach"; VM empty state | U; V |
| AL-04 | Name | | Observe | "Wheel rim" / "Strip" | U; V |
| AL-05 | Footer | | Observe | 'Installs "{name}" on {device}.' | U |
| AL-06 | Add and install | | Click | strip added; profile installed; Say(BarAdded) | J; V |
| AL-07 | Cancel | | | no write | J |

## MX Matrix
| id | control | setup | action | expected | how |
|---|---|---|---|---|---|
| MX-01 | Flag box profile line | B:cur; profile absent; B:old | Observe | Reinstall / Install / Update with FlagBoxInstallPlan.Summary | U FlagBoxInstallPlanTests; V ×3 |
| MX-02 | Your matrices + Add a matrix | RF | Observe; Add a matrix | third card, "3 / 4"; disabled at 4 with "All four matrices are in use." | U PanelMatrixTests; J; V |
| MX-03 | Fix box | Left pillar | Observe | "Not shown in SimHub", the matrix number in the steps | U; N on every rig until #521 (the fix box waits on which matrix a device shows) |
| MX-04 | Preview chips | Flag box | Idle / Mid revs / Yellow / Blue / Pit limiter / Car left / Low fuel / Chequered | glyph per chip from the sheet; Mid revs draws the gear in the first shift colour and shows only while Idle display is Gear (7 chips on Dark) | U; V ×8 |
| MX-05 | Flags / Pit lane / Spotter / Warnings | | Flip each | FlagBoxMatrix1Flags/Pit/Spotter/Warnings | J; V |
| MX-06 | Critical flags only | | Flip | property | J |
| MX-07 | Mounting side | | Left / Right / Both sides | FlagBoxSide | J |
| MX-08 | Spotter bar animation | | Flip | FlagBoxSpotterAnimation (rig-wide; caption says so) | J |
| MX-09 | Triggers link (Warnings) | | Click | Settings › Alerts | V |
| MX-10 | Idle display | | Gear | FlagBoxRest; sub-rows appear | J; V |
| MX-11 | Shift colours / Car-specific shift points / Redline flash | Gear | Flip each | GearBands / GearCarLadder / GearBlink; car line | U; J; V |
| MX-12 | RPM colour for everything #371 / SimHub device #363 / Priority order #505 | | Hover | greyed | V |
| MX-13 | Rename / Remove | | Remove Left pillar | asks once ("Removes Left pillar and its settings. The flag box profile stays in SimHub.", Remove it); matrix 2 back to defaults | J |
| MX-14 | Reinstall (profile line) | | Click | profile rewritten through SimHub's API; Say; log | J |

## SH Shortcuts
| id | control | setup | action | expected | how |
|---|---|---|---|---|---|
| SH-01 | Groups | RF | Open | Main dash 9 rows, Rim 9, Pit wall 1, Phone 1 + paging line, Lights (Night mode, Brightness up, Brightness down, Rig test #511), Alerts (Alert dismissal #510); counts leave the greyed rows out ("0 of 3" on Lights) | U PanelShortcutsTests + ScreenActionsTests; V |
| SH-02 | Filter | K:on two bindings | All / Bound / Not bound | rows filter | U; V |
| SH-03 | Bind | K:on | Bind on Rim › Zone C, `press_keys('f7')` | ControlsEditor shows the key; mapping Target OpenDash.RimCycleZoneC Trigger KeyboardReaderPlugin.F7 | V; J (after graceful close) |
| SH-04 | Press cycles | SH-03 | press F7 | ZoneC property advances | J |
| SH-05 | Clash line | F7 on two zones | Observe | the line names the trigger and both screens | U; V |
| SH-06 | Order honoured | SC-13 + SH-03 | press F7 ×3 | walks the new order | J |
| SH-07 | Previous page | bind F6 | press | steps back, wraps | J |
| SH-08 | Quick glance (Hold) | SC-21 | `captureWhileHeld(f8)` | held shows, released returns | V |
| SH-09 | Clear | bound row | Clear in SimHub's binder | mapping removed | J |
| SH-10 | Night mode action | bind N | press | LightsNightMode flips; sidebar switch follows | J; V |
| SH-11 | Brightness up / Brightness down | bind F9/F10 | press | ±10, clamped 10..100; night value while night on | J |
| SH-12 | Bindings survive restart | SH-03 | graceful close, start | still bound | J; V |
| SH-13 | Removed screen | Remove Rim | Open | group gone | V |

## ST Settings
| id | control | setup | action | expected | how |
|---|---|---|---|---|---|
| ST-01 | On this page | | Click Lighting | scrolls; link highlights | V |
| ST-02 | Race data › Position | | Class | PositionMode | J; V |
| ST-03 | Delta reference | | Last lap | DeltaReference lastlap | J |
| ST-04 | Delta precision | | Thousandths | DeltaPrecision | J |
| ST-05 | Session progress | | Auto / Laps / Time | property; E:untimed shows it | J; V |
| ST-06 | Driver names ×4 / Team names / Clock | | Each | properties | J; U PanelDataTabTests |
| ST-07 | Fuel target per lap #326 / Tyre display #325 (Temperature \| Pressure) | | Hover | greyed | V |
| ST-08 | Units | | Observe | SimHub's units; caption "Set in SimHub." | V; J |
| ST-09 | Flags › Blue flag detail | | Nothing / Class / Position and class | property | J |
| ST-10 | Yellow flags #504 | | Hover | greyed | V |
| ST-11 | Flags in the pit lane | E:pit | Off | FlagsInPitLane false; band D quiet in the lane; matrix shows the pit glyph | J; V |
| ST-12 | Alerts › Trigger | | Low fuel 3 (via JSON, "under 3 laps"; 1 reads "lap"), Oil temperature 100, Water temperature 95 | FlagBoxLowFuelLaps; all four FlagBoxMatrixNOilTemp/WaterTemp = the rig value | J; U PanelSettingsTests |
| ST-13 | Alerts Soon rows and columns | | Hover | Tyre wear and Pit window open #507, Incidents #508, Hybrid battery low #110, Alert display #512 (the Screens / LEDs / Matrix / Races only columns) | V |
| ST-14 | Try (an alert row) | | Click | Rig with that chip | V |
| ST-15 | Lighting preview | | Day / Night | Night dims to night brightness; writes nothing | V; J unchanged |
| ST-16 | Brightness / Night brightness | | Drag | LightsBrightness / LightsNightBrightness; preview follows | V; J |
| ST-17 | Night mode / Night mode button | | Flip; click the binding chip | SB-09; Shortcuts opens at ToggleNightMode | J; V |
| ST-18 | Sim time of day and Screen dimming #128 / Appearance / Driver Soon rows | | Hover | greyed | V |
| ST-19 | Every shared property reachable | | dotnet test | PanelDataTabTests green | U |

## UP Updates
| id | control | setup | action | expected | how |
|---|---|---|---|---|---|
| UP-01 | Available | B:old, network | Open | "OpenDash 0.3.0-rc.7", "You have 0.3.0-rc.1", "Needs a SimHub restart. Your settings are kept.", Download, Release notes | U UpdateWordingTests + PanelUpdatesTests; V |
| UP-02 | Up to date | B:cur | Check now | line | U; V |
| UP-03 | Unreachable | hosts file 127.0.0.1 api.github.com | Check now | "Could not reach GitHub…" | V; revert hosts |
| UP-04 | Check for updates off | | Flip, Check now | nothing fetched; CheckForUpdates false | J |
| UP-05 | Last checked | after UP-02 | Observe | "Last checked today, HH:MM" | J; V |
| UP-06 | Download | B:old | Click | "Downloading" bar; staged DLL; restart dialog; the card turns Staged and the badge "Restart" | V; J; then `bun run vm plugin` restores the branch DLL |
| UP-07 | In SimHub table | RF after AS-08; B:old profiles then B:cur | Observe | Item / Version / State per row: Up to date, Update available, Missing, Not installed, Restart SimHub to load it | U; V |
| UP-08 | Reinstall everything | UP-07 | Click | rows Up to date; folders rewritten; backups kept; the line starts "Reinstalled N dashboards." | J; V |
| UP-09 | Kept copy | edit a .djson by hand, Reinstall everything | Observe | "Kept copy" card, "Your edited <name> was kept."; Put mine back puts it back, its `_yours_` zip goes and the card with it; the replaced folder is `<folder>_backup.zip` (#608) | J; V |
| UP-10 | Copy a support report | | Click | "Support report copied. Paste it into your issue."; clipboard holds versions, rig, devices, log tail; nothing sent (`Get-Clipboard` in run_in_desktop). The SimHub line names the installed release ("SimHub: 9.12.6, in C:\Program Files (x86)\SimHub\"), never 1.0.0.0 (#641) | J |
| UP-11 | Open the log | | Click | Explorer/Notepad window on the log | V |
| UP-12 | Links: Every release on GitHub, Report an issue, Read the guide | | Click | browser windows | V |

## RS Responsive · FL Flows
| id | setup | action | expected | how |
|---|---|---|---|---|
| RS-01 | RF | width ≈ 700 (`WINDOW_HELPER.Fit`) | icon rail; one column; cards 2-up; nothing clipped | U PanelShellTests; V ×8 |
| RS-02 | RF | ≈ 1000 | full rail; stacked or narrowed aside; cards 3-up | U; V |
| RS-03 | RF | ≈ 1600 | the column fills the width beside the 216 px sidebar; two blocks sit side by side; row controls sit at the right edge; captions wrap at 620 | U; V |
| RS-04 | RF | maximised 3840 | as RS-03 at 3840: no empty band on the right, the sheet at the control's right edge, no picture taller than the window can show | V |
| RS-05 | RF | record achieved widths | SimHub may refuse 700; record the floor in run.json | J |
| FL-01 | R0, B:cur, --menu | start, open | Home "Nothing to fix", the add tile and "Add the screen your rig has." in place of Right now and Quick controls; Screens "No screens yet." over the Add a screen tile; no wizard | V; U |
| FL-02 | R0 | Add screen → restart | AS-08/AS-09; Home 1 → 0 | V; J |
| FL-03 | RF minus Rim folder | start | HM-04; Updates row Missing | V; J |
| FL-04 | not in left menu | install without --menu | under Additional plugins; identical | V |
| FL-05 | old settings file | from the VM's _Backups | migrations hold; panel opens | U; V |
| FL-06 | panel throws | inject a bad settings value | fallback text; log; SimHub survives | J; V |
| FL-07 | killed mid-change | change, Stop-Process -Force | setting present after start | J |
| FL-08 | display at 1280×800 | | guiProblem refuses; setres.ps1 | precondition |

Run records: `media/503/panel-verification.md` (`id | ✅/❌/⏭ | capture | note`) beside the cited captures; `build/panel/run.json` carries provenance.
