# Scenarios

A scenario is the telemetry the emulator publishes. `race.json` is the base and carries the whole
field, the session YAML and the drivers that move the values; everything else `extends` it and
overrides what it needs.

## The four capture scenarios

These exist to be photographed. Everything that decides what the dash draws is **pinned**, and the
drivers that would move it are switched off in the timeline, so a screenshot taken at any moment
shows the same state — which is what makes a capture from one pull request comparable with a
capture from the next.

| | What it holds still |
|---|---|
| `green` | A clean racing lap, P3 of 24. The baseline every other capture is compared against. The flag is held green from the first tick; `race.json` cycles the flags for the first two minutes, which is useful for watching the flag band and useless for a capture. |
| `yellow` | A yellow-flag lap on low fuel: the flag band out, fuel under a lap, a hot right front and a cold left front, and P6 overall. |
| `pit` | In the stall with the limiter on and the service order set to four tyres, fuel and a tear-off. The car is stationary, so the rev sweep, the gear model, the lap timer and the fuel burn are all off. Three other cars are put in the pit lane so the table has PIT chips in it. |
| `gallery` | The green lap arranged so every page has something to draw, for the website's captures: a longer session, two cars held at an exact gap for the radar, a pass through the pit lane so the stint counters start, the next stop's service order pinned, and the heading swept a full turn per lap so SimHub records a closed outline. Photograph it after two laps have gone by; `bun run shots` waits for them. |
| `clip` | The gallery lap with the rev sweep run from 5000 to 9100 over three seconds, so a six-second clip catches two gear changes and the shift lights at the top of each sweep. `bun run clips` uses it. |
| `quali` | A timed qualifying run in a car with no in-car TC or ABS, so the settings strip has to close over two cells that are simply not there, and the session counts time rather than laps. It selects the weekend's qualifying session, session 1 of `race-session.yaml`, so SimHub names it `Lone Qualify` and the pit wall header reads it; until #307 it was a timed race under a qualifying name. |

## The scenario that moves on purpose

| | What it does |
|---|---|
| `shiftlights` | One slow RPM sweep, 1200 to 9600 over twenty seconds, against a 7800 / 8400 / 8800 / 9000 ladder — slow enough to see which LED changes at which threshold. At forty seconds the car changes to one with a much lower ladder and five gears, and at eighty it changes back. That swap is the point: it is what tells a mirror (ADR 0014) apart from arithmetic over the redline, and it is what the generated `.ledsprofile` files are verified against. Nothing else moves — the flag is held green, the limiter is off, and the pedals are pinned — because a strip that is also cycling flags says nothing about its revs. |
| `cars` | The race of `race.json` with the player changing car: the Porsche 911 GT3 R (992) at the start, the Ferrari 296 GT3 at 45 s, the Porsche at 90 s and the Ferrari at 135 s. It is the scenario for SimHub's per-car playlists (#199), which match the player's `CarPath`, so only the player's `CarPath`, `CarID` and screen names change, through `{{PlayerCarPath}}`, `{{PlayerCarID}}`, `{{PlayerCarScreenName}}` and `{{PlayerCarScreenNameShort}}` in `race-session.yaml`, whose defaults in `race.json` are the Porsche every other scenario drives. |

The four shift RPMs, the redline and the forward-gear count are `{{placeholders}}` in
`race-session.yaml` rather than literals, so a scenario can hand the mirror a different car mid-run.
`race.json` carries the defaults every other scenario inherits.

`bun run dev --scenario green` is the usual way to reach one, and `bun run shots` walks all four.

## The states the other scenarios cannot reach

| | What it holds still |
|---|---|
| `nosession` | The game running with no session named, which is the state #406's notice is for: zones B and C and band D's fuel page read `… · Go into a session` while the rest of the face draws normally. It extends `green`, so it holds as still as a capture. |
| `untimed` | A lap-counted race that publishes no session clock, which is what iRacing publishes for one. It is the scenario for the laps form of #387's fuel margin, for the session page counting laps rather than time, and for #439's `∞` where a clock would go. Also `green`'s held lap. |

Neither has a committed trace; `UNTRACED_SCENARIOS` in [scripts/emulator.ts](../../../scripts/emulator.ts)
says why, and `bun run record nosession untimed` is what removes them from that list.

`inSession()` is `GameRunning` **and** a session type that is not blank, and every other scenario
here inherits `race-session.yaml`, which names Practice, Lone Qualify and Race — so the notice had no
scenario at all until this one. Stopping the emulator is not the same state: that makes `GameRunning`
false and SimHub switches to the idle screen of #113 instead.

What blanks the name is one field, `SessionType` of the session whose `SessionNum` matches the
telemetry's, which is where SimHub's iRacing reader takes `GameData.SessionTypeName` from. It is
`{{RaceSessionType}}` in the template for that reason; the file's own comment records the rest,
including why the telemetry's `SessionNum` is left alone.

`untimed` is there because iRacing publishes no clock for a session that has none: `SessionTime` is
`unlimited` in the session string and `SessionTimeRemain` is a week, 604800 s, which is
`Irsdk.UnlimitedTime` here and the `UNTIMED_SECONDS` the dash compares against. The clock driver
leaves a remaining time at or above the sentinel alone rather than counting it down.

**Why `untimed` is a variant rather than a correction to `race.json`.** The base race fixture is
wrong about iRacing and knowingly left that way: it is a 30-lap race that also publishes a
forty-five minute `SessionTime` and twenty-five minutes of `SessionTimeRemain`, which makes it a
race the clock would end before the laps ran out — eighteen laps at ninety-eight seconds is more
time than it has left — so the dash draws the timed form of everything and the lap count is
decorative. Two things stop it being corrected in place, and both are why the variant exists
instead.

The first is that five committed traces are recordings of it (`race`, `green`, `pit`, `yellow`,
`shiftlights`) and the honest way to change a trace is a re-record on the VM. Until that happens a
correction would leave the shipped captures, previews and clips showing a session clock the scenario
no longer publishes.

The second is the bug the correction had behind it, which this scenario is what found. `zones/bar.ts`
drew its `raceTime` and `timeLeft` fields as `clock(sessionTimeLeft())` with no `isTimedSession()`
guard, unlike the session card, the session module, the pit wall header and the pit wall's own time
field, which all had one — and `raceTime` is the default of the bar's first slot. So a week of time
left was drawn as `168:00:00`, seven digit cells in a budget of six, and WPF took the last glyph off
it, on every package and in every iRacing lap race. Correcting the base fixture in place would have
put that into every capture in the same breath.

Fixed in #439, and the scenario is now what shows the fix: every surface reads `sessionClock()` and
draws `∞` where a session has no clock, so `untimed` photographs `RACE ∞` and `LEFT ∞` with nothing
clipped. The session module and the session card follow `SessionProgress`, which is `auto` by
default and resolves to laps here, so those two draw the lap unless the setting is forced to `time`.
`-:--:--` belongs to neither this scenario nor `nosession`: it is what a clock reads where
`SessionTimeLeft` is at or below zero, and `nosession` blanks the session type while inheriting the
base fixture's running clock, so no scenario committed here publishes the placeholder. The entry in
[docs/dev-loop.md](../../../docs/dev-loop.md)'s list of silent failures says the same, since that is
where somebody looking at a surprising capture looks.

## The other scenarios

`race.json` is the base: a GT3/GT4 race at Spa, 24 cars, 30 laps with 12 done, the player at
CarIdx 12 in a Porsche 911 GT3 R, overall P3 and class P2. Its drivers move the revs, the gear,
the lap time, the fuel and the flags, which is what you want when watching behaviour rather than
capturing it.

`flagbox.json` walks the whole flag box catalogue one state at a time: fifteen flags in priority
order, the pit family, the spotter on each side, the three warnings, then a gear sweep through the
redline. Six seconds apart, in a fixed order, and it **loops** (the `loop` key, in seconds), so the
catalogue can be watched twice without restarting the emulator and two runs are comparable. It is
the only scenario that drives states nothing on the screen shows, and
[docs/design/flag-box.md](../../../docs/design/flag-box.md) says what each one should look like.

`alerts.json` is the same walk for the screens: the twenty conditions of the alert catalogue that
band D, the companion's strip and the pit wall's band draw, in their rank, seven seconds each so
that a condition is seen with the whole band and then settled into the block at each end of it. The
five car alerts are driven through what SimHub reads them from -- `Voltage` to 0 for the ignition,
the stalled bit of `EngineWarnings`, from which SimHub computes `EngineStarted`,
`PlayerCarMyIncidentCount`, `CarIdxP2P_Status` at the player's
index and `dcHeadlightFlash` pressed and released -- and the session's incident limit is 17, through
the `IncidentLimit` placeholder, so the incident is counted against one. After the walk come the
cases the ranking is for, several conditions out at once, and then the two places nothing should
draw on band D: the garage, where there is no voltage and so, to SimHub, no ignition, and the pit
lane, where the ignition and the stall are the pit family's instead. It loops after 220 s, and
[docs/design/flag-box.md](../../../docs/design/flag-box.md) says what each state should look like.

`notc.json` is the same field in a car with no `dcTractionControl` or `dcABS` at all — SimHub then
reports level 0 and the dashes have to show `--` rather than a zero — in a timed race rather than
a lap-counted one. `quali.json` extends it and moves the telemetry to the qualifying session.

## Checking one

```bash
export PATH="$HOME/.dotnet:$PATH"; export DOTNET_ROOT="$HOME/.dotnet"
cd tools/irsdk-emulator
dotnet devcheck/bin/Release/net8.0/IrsdkEmulator.dll green --selfcheck
```

`--selfcheck` builds the whole shared-memory image, runs the drivers for three seconds and
validates the result without needing Windows. A scenario that does not pass it will not be worth
photographing either.

## Adding one

Extend the nearest existing scenario rather than starting from the catalogue. If it is meant to be
photographed, pin every variable the dash reads and disable the drivers that would move them —
`{"at": 0, "driver": {"type": "flagCycle", "enabled": false}}` and friends — then say in a comment
at the top of the file what state it holds and why that state is worth a picture.
