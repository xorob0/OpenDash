# MVP scope

> **Closed. Superseded by [scope.md](scope.md).**
>
> This document is kept as the record of what the MVP was and of the reasoning behind it. It no
> longer describes the product: openDash now ships fourteen packages across ten face sizes, two
> companions and two pit walls, and five of the lines this document lists as out of scope have
> since been reversed. The face it describes -- a hero zone beside a grid of equal slots -- was
> itself replaced by the zone model in [ADR 0006](decisions/0006-the-zone-face.md).
> [scope.md](scope.md) says which, and by what. Do not cut work from this
> document, and do not treat its refusals as current.

**Status:** signed off 2026-09-10, revised the same day after a scope review. The spike gate
below was passed the same day on the Windows VM, and the MVP was built against this document;
see the release notes and [architecture.md](architecture.md) for what shipped.

## Product

An open-source SimHub dashboard for iRacing, targeting a 1920 by 480 DDU, together with a
SimHub plugin that installs the dashboard and exposes a small set of settings which change what
the dashboard shows. It is free and MIT licensed; bounties and donations may follow later.

The dashboard is generated from TypeScript and design tokens rather than drawn in SimHub's
editor, so that a pull request is a readable diff and so that a feature change reaches every
future screen size at once. Rendering stays native to SimHub. See
[architecture.md](architecture.md) for the pipeline and [decisions/](decisions/) for the
reasoning behind it.

| | |
|---|---|
| Renderer | SimHub native (DashStudio) |
| Telemetry | SimHub; the user supplies their own install |
| Target sim | iRacing |
| Target screen | 1920 by 480, one screen |
| Plugin | .NET Framework 4.8; installs the dashboard and exposes its settings |
| Licence | MIT |

Other sims will probably work, because SimHub normalises the common fields into
`StatusDataBase`. They are nevertheless untested and unsupported in the MVP, and they must not
be advertised.

## Definition of done

The MVP is done when the author can drive a complete iRacing race on the physical 1920 by 480
display with openDash as the only dashboard, when every card described below shows a correct
value during that race, and when a fresh SimHub install can be brought to that state by
downloading the two release artifacts and following the README. A first outside contribution
would be a welcome signal, but it is not a condition.

## Deliverables

Three artifacts leave the repository at each tagged release. The first is
`openDash.simhubdash`, the packaged dashboard, which works on its own with default settings.
The second is `OpenDash-plugin.zip`, which contains `OpenDash.dll` and installation
instructions; the plugin embeds the same dashboard and installs it. The third is the source
itself, which is the artifact that matters for contributors.

## The dashboard

### Anatomy

The face is divided into a fixed hero zone and twelve equal slots. The hero zone holds one
readout, the gear, together with the indicators that belong to no card: the RPM bar with its
shift lights, the flag strip and the pit limiter. A module shows one thing, which is why speed
sits in a slot like every other value rather than beside the gear. Every field other than the
gear is a card, and any card can be placed in any slot from the plugin. The geometry of the slots, that is to say where they sit relative to
the hero zone and how large a card is, belongs to the design work in progress and is not fixed
by this document. What this document fixes is the count, twelve, and the rule that all slots
share one size so that every card fits every slot.

Future screen sizes will declare their own slot count, and a smaller screen will simply expose
fewer slots. Consequently the plugin exposes slot settings up to the largest count that any
layout declares, and each layout reads the first N of them.

### Hero zone

| Element | SimHub source | Behaviour |
|---|---|---|
| Gear | `Gear` | Largest element on the face, and the only readout of the hero zone. |
| RPM bar | `CarSettings_CurrentDisplayedRPMPercent` | Fill width proportional to the displayed RPM percentage. |
| Shift lights | `CarSettings_RPMShiftLight1`, `CarSettings_RPMShiftLight2`, `CarSettings_CurrentGearRedLineRPM`, `CarSettings_RPMRedLineReached` | Three colour bands on the bar (`state.good`, `state.caution`, `state.danger`), blinking at redline. Governed by the `ShiftLights` setting, so that owners of DDUs with physical LEDs can turn them off. Thresholds are SimHub's own per-car values, which users already tune in SimHub's Car Settings page. |
| Flag strip | `Flag_Black`, `Flag_Checkered`, `Flag_Yellow`, `Flag_Blue`, `Flag_White`, `Flag_Green` | One flag at a time, in that priority order, using the flag tokens. |
| Pit limiter | `PitLimiterOn` | `state.neutral` indicator, blinking. |

Property names are given in the short NCalc form of SimHub's `DataCorePlugin.GameData`
properties. Those listed for gear, RPM percentage, flags and the pit limiter are
confirmed in existing dashboards; the shift light thresholds are confirmed by the spike.

### Cards

| # | Card | Value | SimHub source | Format and colour |
|---|---|---|---|---|
| 0 | Current lap | Running lap time | `CurrentLapTime` | `m:ss.f` |
| 1 | Last lap | Last lap time | `LastLapTime` | `m:ss.fff`; `state.best` when it equals the session best |
| 2 | Best lap | Session best lap | `BestLapTime` | `m:ss.fff` |
| 3 | Delta | Live delta to the reference lap | `PersistantTrackerPlugin.SessionBestLiveDeltaSeconds` or `PersistantTrackerPlugin.AllTimeBestLiveDeltaSeconds`, per `DeltaReference` | `+0.00`; `state.good` when negative, `state.danger` when positive |
| 4 | Position | Position and car count | `Position` and `OpponentsCount`, or their class equivalents, per `PositionMode` | `3 / 24` |
| 5 | Session | Lap of total, or time left | `CurrentLap`, `TotalLaps`, `SessionTimeLeft`, per `SessionProgress` | `12 / 30` or `h:mm:ss` |
| 6 | Fuel | Fuel remaining | `Fuel` | One decimal; the unit follows SimHub |
| 7 | Fuel laps | Laps remaining on fuel | `DataCorePlugin.Computed.Fuel_RemainingLaps` | One decimal; `state.danger` below one lap |
| 8 | TC | Traction control level | `TCLevel` | Integer; `OFF` at zero, `--` when the car has none |
| 9 | ABS | ABS level | `ABSLevel` | Integer; `OFF` at zero, `--` when the car has none |
| 10 | Tyre temps | Four temperatures | `TyreTemperatureFrontLeft` and the three other corners | 2 by 2 grid, integer; the unit follows SimHub |
| 11 | Tyre pressures | Four pressures | `TyrePressureFrontLeft` and the three other corners | 2 by 2 grid, one decimal; the unit follows SimHub |
| 12 | Speed | Speed | `SpeedLocal`, `SpeedLocalUnit` | Rounded integer; the unit follows SimHub |

The card number is the value a slot setting takes. The default assignment is speed, then the
timing block, then the car values: slots 1 to 12 show cards 12, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 and
10. Speed leads so that even a two-slot round face shows it beside the gear. Tyre pressures is
the one card no slot shows by default, because iRacing only refreshes it in the pit stall and
the tyre temperature card already carries that reading.

Two caveats concern iRacing specifically. Tyre temperatures and pressures are not live, because
iRacing only refreshes them while the car is in its pit stall; the two tyre cards therefore show
the values from the last stop, exactly as the game itself does, and their label says so.
Moreover, TC and ABS levels are only exposed by cars that have adjustable systems in the
cockpit, which is why those cards have a "none" state.

"Laps remaining on fuel" is computed by SimHub, not by openDash. The MVP contains no computation
of its own, and anything that is not available as a SimHub property is out of scope.

### Units

Speed, fuel, temperature and pressure follow the unit preferences the user has set in SimHub.
The dashboard never hard-codes metric or imperial.

### Screens

One in-game screen. There is no idle screen, no pit screen and no page navigation in the MVP;
SimHub shows its own default when no game is running.

## The plugin

### Responsibilities

One could think that a plugin duplicates what SimHub already offers, since double-clicking a
`.simhubdash` installs it. In reality the plugin exists for the settings: it is the only way a
user can change what the dashboard shows without editing it, and it is the surface on which
every later feature will be configured. On startup it installs the embedded dashboard into
SimHub's `DashTemplates` folder when the dashboard is missing or when the embedded version is
newer than the installed one, and it exposes the settings below as SimHub properties which the
dashboard binds to. It also provides a settings panel in SimHub's left menu. That is the whole
of it: the plugin does not render, does not read telemetry and does not compute anything.

### Settings

Every setting is exposed as a SimHub property under the `OpenDash` prefix, with the name, the
type and the default given here, so that the dashboard and the plugin agree on a contract that
can be tested on both sides.

| Setting | Property | Values | Default |
|---|---|---|---|
| Shift lights | `OpenDash.ShiftLights` | `true`, `false` | `true` |
| Position mode | `OpenDash.PositionMode` | `overall`, `class` | `overall` |
| Delta reference | `OpenDash.DeltaReference` | `session`, `alltime` | `session` |
| Session progress | `OpenDash.SessionProgress` | `auto`, `laps`, `time` | `auto` |
| Slot assignment | `OpenDash.Slot01` to `OpenDash.Slot12` | a card number from 0 to 12 | the default assignment above |

`auto` shows laps when the session declares a lap count and time otherwise. The same card may
be assigned to several slots; the panel shows a warning when that happens and does not prevent
it.

### Panel

A single WPF page titled openDash in SimHub's left menu, built with SimHub's own control styles
so that it looks native. It has four sections: General (shift lights), Data (position, delta,
session), Layout (twelve card selectors and the duplicate warning) and Dashboard (installed
version and a reinstall button). Settings are persisted through SimHub's common settings
mechanism and take effect immediately, without restarting SimHub or reopening the dashboard.

### Behaviour without the plugin

The `.simhubdash` on its own is a complete product. Every expression that reads an `OpenDash`
property wraps it in a null fallback to the default above, so that a user who installs only the
dashboard gets the default layout and the default modes.

## Build infrastructure

The repository is a Bun monorepo with two TypeScript packages, `generator` and `dash`, and a C#
project under `plugin/`. The generator emits and validates SimHub scene graphs; the dash package
holds the cards, the layout and the fonts, and produces the `.simhubdash`. The plugin project
references SimHub's assemblies from `plugin/lib/`, which are committed so that CI can build
without a SimHub install.

GitHub Actions run on every pull request: the dashboard builds and its snapshot tests run on
Linux, the plugin builds on Windows, and both artifacts are attached to the run so that a
reviewer can install them. On a tag, the same two artifacts are attached to the release.

## Out of scope

Explicitly deferred. Do not let these creep in.

multiple dashboards · multiple screen sizes · theming and colour customisation · idle or pit
screens · page navigation · stream overlay · computed telemetry of our own (fuel prediction,
stint estimates) · phone and tablet layouts · round DDUs · licensing or activation · network
update checks · launching the dashboard or selecting a display from the plugin · any sim other
than iRacing

## Gate before build starts

**Passed on 2026-09-10 with SimHub 9.12.6.** Items 1 to 3 were verified with a hand-built
package rendered live, items 4 and 5 with a bound `WidgetItem` whose screen switched at
runtime (the fallback in item 5 is therefore not used), items 6 to 9 by decompiling SimHub and
by loading the package; the findings are recorded in
[research/simhub-dash-format.md](research/simhub-dash-format.md). Two facts changed the plan:
Barlow's digits are proportional, so numerals use SimHub's monospaced text cells, and the
shift-light properties are band progress values rather than bar percentages, which is
[ADR 0004](decisions/0004-rev-bar-model.md).

The spike proves the four things the plan rests on: a generated `.djson` loads, its bindings
resolve against replayed telemetry, a plugin property can drive the dashboard, and a slot can
switch card at runtime. It is estimated at two to three days on the Windows VM, and no
production work starts until it passes.

1. In DashStudio, build a dashboard with a text item bound to gear and a rectangle whose width
   is bound to RPM. Save it and commit the result under `docs/research/samples/` as the golden
   file.
2. Generate the same dashboard from TypeScript, import it, and confirm that it renders
   identically against a replayed iRacing session recorded on the author's rig.
3. Confirm the four binding kinds the MVP needs, `Text`, `Visible`, `BackgroundColor` and
   `Width`, all written in NCalc.
4. Build a minimal plugin exposing one property. Confirm that the dashboard reads it with an
   `isnull()` fallback, with the plugin present and with it absent.
5. Confirm that a `WidgetItem` whose `InitialScreenIndex` is bound to a plugin property
   switches screen when the property changes. If it does not, the fallback is to emit every
   card in every slot with `Visible` bindings, which is known to work but multiplies the item
   count.
6. Confirm that Barlow Condensed SemiBold and Bold resolve from `_SHFonts/` with `FontWeight`
   set to `SemiBold` and `Bold`, and check whether its digits are tabular by watching a lap
   time tick.
7. Confirm that a package with no `.ressources` file and no preview image imports cleanly, and
   note what SimHub shows as its thumbnail.
8. Confirm that extracting the dashboard folder under `DashTemplates/` from the plugin is
   equivalent to an import, bundled fonts included.
9. Confirm the property names the cards table leaves open: class position and class car count,
   the fuel unit, `SpeedLocalUnit`, and the tyre pressure unit.

If item 2 or item 3 fails, ADR 0002 is reopened before anything else is written. Item 5 only
changes the emission strategy.

## Test rig and development loop

Development happens on macOS, and SimHub is Windows only. A Windows VM reachable from a VPS
therefore runs SimHub with a recorded iRacing session, and no game is installed there. SimHub's
own telemetry recorder captures a session on the author's rig once, from the controls in
SimHub's title bar, and the replay drives the dashboard on the VM; the community has confirmed
that replay works on a machine where the game was never installed.

The loop for the dashboard is to build on the Mac, copy the `.simhubdash` to the VM, import it,
open it in a dashboard window and inspect it while the replay runs. The loop for the plugin is
to build on Windows, in CI or on the VM, copy `OpenDash.dll` into the SimHub folder, restart
SimHub and accept the new plugin prompt. Manual tests on the physical display are done on the
author's rig from release artifacts.

## Sizes after the MVP

One could expect a generator to make new sizes free. In fact each aspect ratio still needs a
design decision, because the composition that works at 4:1 does not survive at 8:3. What the
generator makes free is everything else: the cards, the bindings, the plugin and the tests are
reused untouched, and a new size is one layout function that arranges the same cards and
declares its own slot count.

The target list is the union of what Lovely and Daniel Newman Racing ship for DDU-class
displays. The priority order will be decided when the MVP is done.

| Resolution | Screen | Shipped by |
|---|---|---|
| 1280 by 480 | 10 in | Lovely XL and XLC |
| 1280 by 400 | 7.8 in | Lovely MXL |
| 850 by 480 | 5 in, including curved | Lovely, Curved, Rallye, Flags |
| 800 by 480 | 5 in Vocore, round faces with a cover | DNR Chrono |
| 1280 by 720 | 4 to 6.8 in wheel and dash screens | DNR Endurance, Speedway, Rally |
| 480 by 480 | round and square | Lovely Round and Square |
| 800 by 286 | nano | Lovely Nano |
| 480 by 850 and 370 by 850 | portrait and tower | Lovely Companion Portrait and Tower |
| 1920 by 1080 | secondary monitor | DNR Race Control |

DNR does not publish pixel sizes for the standard and 10 inch XL variants of Speedway and Rally;
they are expected to fall into the 1280 by 720 and 1280 by 480 rows.

## Beyond the MVP

The end goal, once the generator and the plugin have proven themselves, is a package comparable
to Daniel Newman Racing's, which [research/competitors.md](research/competitors.md) describes:
several dashboards for several display classes, adaptive zones offering a dozen or more pages
each, an alert engine for flags and penalties, change notifications for car settings, themes
and a dark mode, an idle screen, leaderboard and relative views, and LED profiles for the same
hardware, all of it configured from the plugin. Every one of those features is a card, a screen
or a setting in the model above, which is why the MVP invests in the slot mechanism and the
property contract rather than in breadth.
