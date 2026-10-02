# Testing the settings panel

This is the scenario matrix for the panel rebuilt under #503: one table per page, one row per control, each row saying the fixture it needs, what to do, what to expect and how it is proven. It was drafted from the artboards before the pages were merged, so a row may name a control by its artboard word where the page uses the ruled one (docs/design/plugin.md lists those departures); fix the row, never the page, when they disagree. The routine around it is in [testing-vm.md](testing-vm.md) and [dev-loop.md](dev-loop.md); the captures a run produces come from `bun run panel-shots`.


Conventions. Fixtures: `R0` no `OpenDash.GeneralSettings.json` and `PluginsData\Common\_Backups\OpenDash.GeneralSettings_b*.json` deleted (first run); `R1` `bun scripts/rig.ts clear` (one 1280×480 face); `RF` `bun scripts/rig.ts panel` (Main dash 1280×480 stock ns, Rim second 1280×480 with its DashTemplates folder deleted, Pit wall 1920×1080, Phone 480×850, Round 480; bars Wheel rim 3-9-3-fanatec and Dash brow 15; matrices 1 Flag box, 2 Left pillar); `E:<name>` emulator scenario running (`bun run emulator start <name> --replace`), `E:none` stopped; `B:cur` the branch's `bun run package`; `B:old` a throwaway build with `VERSION` hand-edited to `0.3.0-rc.1` (never committed) so Updates sees the real newer release; `K:on` `bun run vm bind <action> <key>` (enables the keyboard reader, restarts SimHub), `bun run vm unbind` after.
How: `U` unit test on a pure model (file named); `V` VM capture/click (capture `build/panel/<id>.png`, promoted to `media/503/<id>.png` when cited); `J` JSON/log/property proof (`OpenDash.GeneralSettings.json` diff via `run_powershell ConvertFrom-Json`; the Available properties page; `[OpenDash]` lines in `Logs\SimHub.txt`; `PluginManagerSettings.json` mappings); `N` not verifiable on the VM, with the stand-in.
Two facts every row assumes: the panel saves on every change, so a JSON diff is valid at once; SimHub on the VM is force-killed, so nothing is proven by `End()` and persistence rows close SimHub gracefully first (WM_CLOSE through `WINDOW_HELPER.Close`, 20 s, then kill).
Deferred controls (#504 #505 #506 #507 #508 #509 #510 #511 #512) are verified only as greyed rows with the right hover; nothing behind them is exercised.

## SB Sidebar and shell
| id | control | setup | action | expected | how |
|---|---|---|---|---|---|
| SB-01 | Wordmark + version | RF, B:cur | Open OpenDash from SimHub's left menu | wordmark, the assembly version | U PanelShellTests; V |
| SB-02 | Search | RF | Click the box | Draws with placeholder; no log error | V; N typing (a panel TextBox never takes focus over VNC) → U PanelSearchTests (index covers every row label; "blue" → Settings › Flags › Blue flag detail; empty → no hits; a hit navigates + scrolls) |
| SB-03 | Live card | RF, E:race | Observe | game, car, track · session, green dot | V; J GameRunning |
| SB-04 | Live card, no game | RF, E:none | Stop emulator | no-game state, grey dot | V |
| SB-05 | Nav items + counts | RF | Observe | Home, Rig, sep, Screens 5 (dot), LEDs 2 (dot), Matrix 2 (dot), sep, Shortcuts N, Settings; Updates at the foot | U PanelNavTests; V |
| SB-06 | Active item | RF | Click each | accent bar moves; heading matches | V (8 canonical page captures) |
| SB-07 | Page remembered per sitting | RF | Go to Settings, leave OpenDash, return | Settings shown | V |
| SB-08 | Page not persisted | RF | Restart SimHub | Home | V |
| SB-09 | Night mode (sidebar) | RF | Flip | on; the same switch on Rig, Home quick controls and Settings › Lighting agrees; LightsNightMode true | J; V ×4 |
| SB-10 | Updates badge | B:old | Observe | amber version badge | V |
| SB-11 | Up to date | B:cur | Observe | no badge | V |
| SB-12 | Warn dots follow Home items | RF → restart | Observe | Screens dot clears when no item remains | U PanelNavTests; V |
| SB-13 | Icon rail | RF | Width ≈ 900 | 56 px rail, labels as tooltips, search icon opens the flyout | V |

## HM Home
| id | control | setup | action | expected | how |
|---|---|---|---|---|---|
| HM-01 | Heading count | RF (3 problems) | Open Home | "3 things to fix" | U PanelAttentionTests; V |
| HM-02 | Nothing to fix | R1 after restart | Open Home | "Nothing to fix", no card | U; V |
| HM-03 | Item: written, not restarted | R1, Add a screen, no restart | Home | "Restart SimHub to load it" item + button → Screens with it selected | U (written-since-start flag); V |
| HM-04 | Item: folder missing | RF (Rim folder deleted) | Home | Missing + "Install it again" | U; V; J folder reappears |
| HM-05 | Item: profile not selected | RF | Home | three steps + Check again | U over LedDeviceSurvey fixture; V wording; N resolved branch (no LED device on the VM) |
| HM-06 | Item: matrix content unset | RF | Home | "No matrix device in SimHub is set to matrix 2" → Matrix with Left pillar selected | U; V; N resolved branch |
| HM-07 | Item: update pending restart | B:old after Download | Home | restart item | U; V |
| HM-08 | Right now › Screens | RF, E:race | Observe | one row per screen with current pages, dot | U; V; J zone properties |
| HM-09 | Right now › LEDs | RF, E:shiftlights | Observe | strip lit from CarLights.Run while Ready, else dark; line | V; J LedMirror properties |
| HM-10 | Right now › Matrix | RF | Observe | idle glyph; matrix 2 dark | U PanelEmulationTests glyphs = build sheet; V |
| HM-11 | Card links | RF | Click | Screens / LEDs / Matrix | V |
| HM-12 | Brightness slider | RF | Drag to ~50 | numeral follows; LightsBrightness 50; Settings agrees; while night is on it edits LightsNightBrightness | V (`drag`); J |
| HM-13 | Night mode (quick controls) | RF | Flip | as SB-09 | J; V |
| HM-14 | Open Rig | RF | Click | Rig | V |

## RG Rig
| id | control | setup | action | expected | how |
|---|---|---|---|---|---|
| RG-01 | Tiles for every device | RF | Open | 9 tiles; warning dots on the two with issues | U PanelRigMapTests (tiles, default layout); V |
| RG-02 | Drag a tile | RF | `drag` Main dash 200 px right | moves; LayoutX/Y persisted | V; J |
| RG-03 | Reset layout | after RG-02 | Click | home positions; layout cleared | V; J |
| RG-04 | 18 scenario chips | RF | Click each | tiles repaint per PanelEmulation rules | U (scenario → colours; glyph names vs sheet); V (contact sheet of 18) |
| RG-05 | Real hardware greyed | RF | Hover | disabled, "Coming soon · #506"; nothing published | V; J no property change |
| RG-06 | Night mode (Rig header) | RF | Flip | LED tiles dim to LightsNightBrightness/100; screens do not dim; SB-09 agreement | V; J |
| RG-07 | Revs chips | RF, E:shiftlights | Idle / Mid / Shift | rev bars and gear colour follow | V |
| RG-08 | Hint | RF | Observe | "Drag to arrange like your rig" | V |

## SC Screens · AS Add a screen
| id | control | setup | action | expected | how |
|---|---|---|---|---|---|
| SC-01 | Cards | RF | Open | 5 cards (thumb by kind, name, kind, state) + add tile | U PanelScreensTests; V |
| SC-02 | Select | RF | Click Pit wall | header + pit pane | V |
| SC-03 | Fix box | RF (Rim) | Select Rim | steps | U; V |
| SC-04 | Edit sheet | RF | Click | prefilled; Cancel writes nothing | V; J hash unchanged |
| SC-05 | Rename via Edit | RF | name via JSON, Save | card/header renamed; namespace and folder unchanged | J; N typing → U name rules |
| SC-06 | Duplicate | RF | Click on Main dash | new card, own namespace/folder, settings copied, restart line | U DuplicateScreen tests; J; V |
| SC-07 | Remove | RF | Remove Round | confirmation names the folder and the dead buttons; card, settings, folder gone | U PanelConfirmationTests; V; J |
| SC-08 | Click zones and bar | Main dash | Click each | accent border; aside switches | U PanelFacePlanTests; V ×5 |
| SC-09 | Zone page list | zone C | Observe | ticked first in order, First badge, "4 of 21", Show all reveals unticked + Not in iRacing + Soon #320/#152 | U; V |
| SC-10 | Tick/untick | zone C | Untick Track | mask bit cleared; count; ZoneCPages property | J; V |
| SC-11 | Last page stays | zone A one page | Untick | refused | U; J unchanged |
| SC-12 | Untick the shown page | zone C on Track | Untick | zone snaps forward in order | J ZoneC |
| SC-13 | Drag to reorder | zone C | `drag` Relative below Leaderboard | order persisted; First moves; ZoneCPosition follows | J; V; then SH-07 |
| SC-14 | Show all / Only ticked / All / None | zone C | Toggle | list grows/shrinks; None keeps the start page | V; J |
| SC-15 | My class only | zone C | Flip | ClassOnly[C]; zone A has no row | J; V |
| SC-16 | Next/Previous page chips | RF, K:on | Observe | binding text or Not bound; click opens Shortcuts | U; V |
| SC-17 | Info bar fields | bar selected | Left, first → Lap | BarFields; property | J; V |
| SC-18 | Rev bar | Main dash | Off | RevBar off; dash loses the well | J; V |
| SC-19 | Flag display | Main dash, E:yellow | Full screen | FlagFormat; full-screen yellow | J; V |
| SC-20 | Lap review | Main dash | Races / Always / Off | LapReview | J |
| SC-21 | Quick glance | K:on | Track in Zone C; bind F8 on Shortcuts | setting; mapping PressType During; `captureWhileHeld(f8)` shows Track then returns | J; V |
| SC-22 | Seven Soon rows | Main dash | Hover each | greyed; "Coming soon · #N" | U PanelSoonTests; V; J unchanged on click |
| SC-23 | Details | Main dash | Expand | Name, Folder as stored, OpenDash.<ns>* | V |
| SC-24 | Clash line | zone C and glance on Track | Observe | FacePageClash warning, allowed | U; V |
| SC-25 | Pit: Page on screen | Pit wall | Tower | PitWallPage; picture and zone rows change | J; V ×3 |
| SC-26 | Pit: zones | Pit wall | Race › A → Relative | PitWallZones; property | J |
| SC-27 | Web view address | Pit wall | set via JSON; observe | shown; `ftp://` ignored | J; U |
| SC-28 | Pit: class / flag display / glance | Pit wall | Each | properties | J |
| SC-29 | Portrait layout A–D | RF + 1080×1920 | Set | four zone settings read by the portrait package | J; V |
| SC-30 | Phone modules | Phone | Untick Sectors | Modules[i]; "17 of 21"; Not in iRacing beside three | J; V |
| SC-31 | Phone: First module | Phone | Pick Delta | CompanionStart; forced at once | J |
| SC-32 | Phone: Flag display | Phone, E:yellow | Full screen | CompanionFlagFormat; companion shows it | J; V |
| SC-33 | Phone: Quick glance + paging line | Phone | Observe | CompanionQuickGlance select; PanelCopy.CompanionPaging verbatim + crumbs | U PanelCopyTests |
| SC-34 | Round | Round | Card 1 → Current lap; Rev ring Off; hover #146 | Slot01; RevBar; tooltip | J; V |
| SC-35 | Card columns at width | RF | Resize (RS) | 6 → 3 → 2 columns; add tile last | U PanelShellTests; V |
| AS-01 | Open sheet | RF | Add a screen | sheet over dimmed page, close icon | V |
| AS-02 | Kind tiles | sheet | Dash face / Pit wall / Companion / Round | size tiles follow the kind (Landscape|Portrait for pit wall and companion) | U PanelAddScreenTests; V |
| AS-03 | Flags screen #116 | sheet | Hover / click | greyed, tooltip, no selection | U; V |
| AS-04 | Your displays #85 | sheet | Hover | tooltip | V |
| AS-05 | Size tiles + name prefill | sheet | Click 850×480 | selected; name prefilled; second-copy note when true | U; V |
| AS-06 | Next steps | sheet | Observe | "Restart SimHub, then assign "{name}" to this display in Dash Studio." | U copy |
| AS-07 | Cancel | sheet | Click | closes, no write | V; J |
| AS-08 | Add | sheet, R1 | Click | card "Restart SimHub to load it"; folder written; Say(Added); Home gains HM-03 | J; V |
| AS-09 | Add then restart | AS-08 | stop + start | card In SimHub; listed in Dash Studio (`openDashboard` by name) | V; J |

## LD LEDs · AL Add LEDs
| id | control | setup | action | expected | how |
|---|---|---|---|---|---|
| LD-01 | Cards | RF | Open | Wheel rim 3 · 9 · 3, Dash brow 15, add tile; states | U PanelLedsTests; V |
| LD-02 | Header chip and profile state | Wheel rim | Observe | "Fanatec wheel · 3 · 9 · 3"; Install/Update/Reinstall verb | U; V |
| LD-03 | Fix box | Dash brow | Observe; Check again | steps; re-survey logged | V; J `[OpenDash] LED device` lines; N resolved branch |
| LD-04 | Preview chips ×6 | Wheel rim, E:shiftlights | Each | frames per rule; Live follows CarLights.Run | U; V; J |
| LD-05 | All devices at once | | Click | Rig | V |
| LD-06 | Car's own rev lights | E:race | Flip | RpmStyle car|leftToRight; car line in table / not in table (drive a car without a table via the emulator car name) / none when off | U; J; V ×3 |
| LD-07 | Width | on | Actual size | LedMirrorFit; hidden when off | J; V |
| LD-08 | Centre display | | RPM only | bar LedCentre | J |
| LD-09 | SimHub device | | Open | states: none / one / gone / not connected | U LedDeviceSurveyTests; V; N populated list |
| LD-10 | Brightness | | 60% | bar LedBrightness property; default label "Same as rig · N%" | J; U |
| LD-11 | Reverse direction | | Flip | ProfileShapeId -reversed; profile reinstalled; NCalc tester on the remap | J; N hardware |
| LD-12 | Each LED in turn #434 | | Hover | greyed | V |
| LD-13 | Effects grid | Wheel rim / Dash brow | Observe; flip TC | 15 switches with ends, 4 on the brow; LedEffectTc property false; profile EnabledFormula reads it | U; V; J NCalc tester |
| LD-14 | Flag animation / Full-strip spotter | | Flip | FlagAnimation, SpotterWhole; Full-strip hidden on the brow | J; V |
| LD-15 | Pit limiter lights #509 | | Hover | greyed | V |
| LD-16 | Every strip Soon rows #485 #300 #479 | | Hover | greyed | V |
| LD-17 | Lovely Car Data | network | Update | off the UI thread; status line; attribution; log | J; V |
| LD-18 | Rename / Remove | | Remove Dash brow | asks once; bar gone; profile uninstalled | U; J |
| AL-01 | Hardware tiles | | Fanatec / Something else | shape step flips; Found in SimHub only with a Fanatec target (never on the VM) | U PanelLedsTests; V; N badge |
| AL-02 | Ends + centre | Something else | 2 / 11 | preview + BarShapeNote; shape id exists in the census | U; V |
| AL-03 | Device radios | | Observe | declined disabled; VM empty state | U; V |
| AL-04 | Name | | Observe | "Wheel rim" / "Strip" | U; V |
| AL-05 | Footer | | Observe | 'Installs "{name}" on {device}.' | U |
| AL-06 | Add and install | | Click | bar added; profile installed; Say(BarAdded) | J; V |
| AL-07 | Cancel | | | no write | J |

## MX Matrix
| id | control | setup | action | expected | how |
|---|---|---|---|---|---|
| MX-01 | Profile line | B:cur; profile absent; B:old | Observe | Reinstall / Install / Update (primary) with FlagBoxInstallPlan.Summary | U FlagBoxInstallPlanTests; V ×3 |
| MX-02 | Cards + Add | RF | Observe; Add | third card, "3 / 4"; disabled at 4 | U PanelMatrixTests; J; V |
| MX-03 | Fix box | Left pillar | Observe; Check again | steps; re-survey | V; N resolved |
| MX-04 | Preview + 7 chips | Flag box | Each | glyph per chip from the sheet; idle gear in shift colour | U; V ×7 |
| MX-05 | Family toggles | | Flip each | FlagBoxMatrix1Flags/Pit/Spotter/Warnings | J; V |
| MX-06 | Critical flags only | | Flip | property | J |
| MX-07 | Mounting side | | Each | FlagBoxSide | J |
| MX-08 | Spotter bar animation | | Flip | FlagBoxSpotterAnimation (rig-wide; caption says so) | J |
| MX-09 | Thresholds link | | Click | Settings › Alerts | V |
| MX-10 | Idle display | | Gear | FlagBoxRest; sub-rows appear | J; V |
| MX-11 | Shift colours / Car-specific shift points / Redline flash | Gear | Flip each | GearBands / GearCarLadder / GearBlink; car line | U; J; V |
| MX-12 | #371 / #363 / #505 | | Hover | greyed | V |
| MX-13 | Rename / Remove | | Remove Left pillar | asks once; matrix 2 back to defaults | J |
| MX-14 | Reinstall | | Click | profile rewritten through SimHub's API; Say; log | J |

## SH Shortcuts
| id | control | setup | action | expected | how |
|---|---|---|---|---|---|
| SH-01 | Groups | RF | Open | Main dash 9 rows, Rim 9, Pit wall 1, Phone 1 + paging line, Lights (Night mode, Brightness up/down, Rig test #511), Alerts (#510); counts | U PanelShortcutsTests + ScreenActionsTests; V |
| SH-02 | Filter | K:on two bindings | Each | rows filter | U; V |
| SH-03 | Bind | K:on | Bind on Rim › Zone C, `press_keys('f7')` | ControlsEditor shows the key; mapping Target OpenDash.RimCycleZoneC Trigger KeyboardReaderPlugin.F7 | V; J (after graceful close) |
| SH-04 | Press cycles | SH-03 | press F7 | ZoneC property advances | J |
| SH-05 | Clash banner | F7 on two zones | Observe | banner names the trigger and both screens | U; V |
| SH-06 | Order honoured | SC-13 + SH-03 | press F7 ×3 | walks the new order | J |
| SH-07 | Previous page | bind F6 | press | steps back, wraps | J |
| SH-08 | Quick glance hold | SC-21 | `captureWhileHeld(f8)` | held shows, released returns | V |
| SH-09 | Clear | bound row | Clear | mapping removed | J |
| SH-10 | Night mode action | bind N | press | LightsNightMode flips; sidebar switch follows | J; V |
| SH-11 | Brightness up/down | bind F9/F10 | press | ±10, clamped 10..100; night value while night on | J |
| SH-12 | Bindings survive restart | SH-03 | graceful close, start | still bound | J; V |
| SH-13 | Removed screen | Remove Rim | Open | group gone | V |

## ST Settings
| id | control | setup | action | expected | how |
|---|---|---|---|---|---|
| ST-01 | On-this-page nav | | Click Lighting | scrolls; chip highlights | V |
| ST-02 | Position | | Class | PositionMode | J; V |
| ST-03 | Delta reference | | Last lap | DeltaReference lastlap | J |
| ST-04 | Delta precision | | Thousandths | DeltaPrecision | J |
| ST-05 | Session progress | | Laps / Time | property; E:untimed shows it | J; V |
| ST-06 | Driver names ×4 / Team names / Clock | | Each | properties | J; U PanelDataTabTests |
| ST-07 | #326 / #325 | | Hover | greyed | V |
| ST-08 | Units | | Observe | SimHub's units or "Set in SimHub" | V; J |
| ST-09 | Blue flag detail | | Each | property | J |
| ST-10 | Yellow flags #504 | | Hover | greyed | V |
| ST-11 | Flags in the pit lane | E:pit | Off | FlagsInPitLane false; band D quiet in the lane; matrix shows the pit glyph | J; V |
| ST-12 | Alert thresholds | | Low fuel 3 (via JSON), oil 100, water 95 | FlagBoxLowFuelLaps; all four FlagBoxMatrixNOilTemp/WaterTemp = the rig value | J; U PanelSettingsTests |
| ST-13 | Alert Soon rows/columns | | Hover | #507 #508 #110 #512 | V |
| ST-14 | Try | | Click | Rig with that chip | V |
| ST-15 | Lighting preview | | Night | dims to night brightness; writes nothing | V; J unchanged |
| ST-16 | Sliders | | Drag | LightsBrightness / LightsNightBrightness; preview follows | V; J |
| ST-17 | Night mode switch / button chip | | Flip; click chip | SB-09; Shortcuts opens at ToggleNightMode | J; V |
| ST-18 | #128 / Appearance / Driver Soon rows | | Hover | greyed | V |
| ST-19 | Every shared property reachable | | dotnet test | PanelDataTabTests green | U |

## UP Updates
| id | control | setup | action | expected | how |
|---|---|---|---|---|---|
| UP-01 | Available | B:old, network | Open | "OpenDash 0.3.0-rc.7", "You have 0.3.0-rc.1", Download, Release notes | U UpdateWordingTests + PanelUpdatesTests; V |
| UP-02 | Up to date | B:cur | Check now | line | U; V |
| UP-03 | Unreachable | hosts file 127.0.0.1 api.github.com | Check now | "Could not reach GitHub…" | V; revert hosts |
| UP-04 | Check toggle off | | Flip, Check now | nothing fetched; CheckForUpdates false | J |
| UP-05 | Last checked | after UP-02 | Observe | "Last checked today, HH:MM" | J; V |
| UP-06 | Download | B:old | Click | progress; staged DLL; restart dialog | V; J; then `bun run vm plugin` restores the branch DLL |
| UP-07 | In SimHub table | RF after AS-08; B:old profiles then B:cur | Observe | states per row | U; V |
| UP-08 | Reinstall everything | UP-07 | Click | rows Up to date; folders rewritten; backups kept | J; V |
| UP-09 | Edited dashboard kept | edit a .djson by hand, Reinstall | Observe | kept card; Put mine back | J; V |
| UP-10 | Support report | | Click | clipboard holds versions, rig, devices, log tail; nothing sent (`Get-Clipboard` in run_in_desktop) | J |
| UP-11 | Open the log | | Click | Explorer/Notepad window on the log | V |
| UP-12 | Links | | Click | browser windows | V |

## RS Responsive · FL Flows
| id | setup | action | expected | how |
|---|---|---|---|---|
| RS-01 | RF | width ≈ 700 (`WINDOW_HELPER.Fit`) | icon rail; one column; cards 2-up; nothing clipped | U PanelShellTests; V ×8 |
| RS-02 | RF | ≈ 1000 | full rail; stacked or narrowed aside; cards 3-up | U; V |
| RS-03 | RF | ≈ 1600 | the column fills the width beside the 216 px sidebar; two blocks sit side by side; row controls sit at the right edge; captions wrap at 620 | U; V |
| RS-04 | RF | maximised 3840 | as RS-03 at 3840: no empty band on the right, the sheet at the control's right edge, no picture taller than the window can show | V |
| RS-05 | RF | record achieved widths | SimHub may refuse 700; record the floor in run.json | J |
| FL-01 | R0, B:cur, --menu | start, open | Home "Nothing to fix"; Screens add tile + EmptyRig; no wizard | V; U |
| FL-02 | R0 | Add → restart | AS-08/AS-09; Home 1 → 0 | V; J |
| FL-03 | RF minus Rim folder | start | HM-04; Updates row Missing | V; J |
| FL-04 | not in left menu | install without --menu | under Additional plugins; identical | V |
| FL-05 | old settings file | from the VM's _Backups | migrations hold; panel opens | U; V |
| FL-06 | panel throws | inject a bad settings value | fallback text; log; SimHub survives | J; V |
| FL-07 | killed mid-change | change, Stop-Process -Force | setting present after start | J |
| FL-08 | display at 1280×800 | | guiProblem refuses; setres.ps1 | precondition |

Run records: `media/503/panel-verification.md` (`id | ✅/❌/⏭ | capture | note`) beside the cited captures; `build/panel/run.json` carries provenance.
