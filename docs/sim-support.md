# Which sim shows what

**Status:** current. Audited on 2026-10-15 against `main` (#102), with Assetto Corsa's column from its
first drive (#592).

OpenDash 1.0 supports **iRacing** and **Assetto Corsa**. **Assetto Corsa Competizione** and **Le Mans
Ultimate** may work, and the plugin already answers for both the questions that differ by sim, but
neither has been driven, so they are untested and not supported. Every other sim SimHub reads is in the
same position, without even that. [scope.md](scope.md) states the rule: a sim is supported once somebody
has driven it and its column below is filled.

The table has a row for every page, card, module and lamp a package draws, grouped by where it is
drawn, and a column per sim. A page that appears in several places (a module is zone B and C of every
face, a companion page and a pit wall zone) has one row, under the modules.

## How to read a cell

Each cell opens on one of five words.

| Mark | Meaning |
|---|---|
| **Works** | The reading is drawn with the meaning it has on iRacing, or a difference the note names that a driver would not call wrong. |
| **Partly** | Drawn, and part of it is missing or means something else; the note says which part. |
| **Absent** | The sim publishes nothing for it, and the reading hides, reads `--` or says it is not published. It never draws a zero as though it were measured. |
| **Wrong** | Draws a value the sim did not give it. The note names the ticket that fixes it. |
| **Untested** | Nobody has driven the sim with the packages. The note says what the code expects there, read from SimHub's reader, and nothing more. |

## Where each column comes from

**iRacing** is the sim every binding was written against, the one the irsdk emulator feeds and the nine
traces under `traces/` record, and the one the face, the companion, the pit wall and the strips have
been seen on in real SimHub.

**Assetto Corsa** was driven by the author on 2026-10-15, who reported it working and will open a ticket
for anything found wrong afterwards. That drive is what the claim rests on, and it was reported as a
whole rather than reading by reading. The cells themselves are therefore not observations from it: each
is read from SimHub 9.12.6's AC reader ([research/sims.md](research/sims.md)) and from the bindings on
`main`, and checked against the seven AC traces under `traces/ac/` wherever a trace carries the reading.
The AC emulator plays SimHub's AC app, so the other cars are in those traces. A cell marked *source
only* is one it cannot play: TC and ABS in action, which SimHub reads from AC's UDP telemetry and
nothing else, are read from SimHub's source alone.

**ACC** and **Le Mans Ultimate** are read from SimHub's readers for the two games, the same research
page, and [ADR 0021](decisions/0021-a-dashboard-never-names-a-game.md)'s table of what the plugin
answers for each. Their drives are #961 and #962. Until then a cell whose answer depends on what the
game sends says **Untested**, and a cell the code settles on its own, a reading only iRacing publishes
for instance, takes one of the other marks.

Assetto Corsa's column depends on the fixes that landed for it on 2026-10-15: its flags on band D, the
flag box and the strips (#551), the track map's notice (#555), the wind, the sim clock and the track
state hidden rather than drawn as zeros (#1123, #1128), the delta reference (#575), the track's name
(#1124, #1150), the car alerts and the change notifications reading the plugin's `DriverInCar` (#561),
the strip and flag box profiles kept on a SimHub that files them per game family (#689), the pit view
saying the pit service is not published rather than drawing a stop set to do nothing (#564), the
incident count and the steering hidden rather than drawn as `0x` and a wheel held straight (#567), and
every limiter reading following AC's own limiter rather than the pit lane (#558). Le Mans Ultimate's
pit stop is timed in the box rather than down the whole lane (#1156).

## The table

### The face

| Reading | iRacing | Assetto Corsa | ACC | Le Mans Ultimate |
|---|---|---|---|---|
| Rev bar and shift lights | **Works.** The car's own shift points, from iRacing or from Car Data, else SimHub's bands. | **Works.** The car's ladder from Car Data where it has measured the car (#748), else SimHub's bands from the car's `ai.ini` upshift point. | **Untested.** Car Data's ACC cars, else SimHub's bands from the car's maximum RPM. | **Untested.** SimHub's bands from the car's maximum RPM. |
| Gear | **Works.** | **Works.** | **Untested.** | **Untested.** |
| Gear ghosts (the gears either side) | **Works.** iRacing declares the car's gear count. | **Works.** The count from the car's `drivetrain.ini`. | **Untested.** The count is learned: the highest gear SimHub has seen in the car. | **Untested.** The game declares the count. |
| Flag band (band D's takeover) | **Works.** All fifteen conditions. | **Partly.** Yellow, blue, black, white and the chequer (#551). AC has no green, and a single yellow cannot be told apart as a full course yellow, a waved yellow or debris. | **Untested.** SimHub's six one-word flags, the green and a sector yellow among them. | **Untested.** The plugin answers the yellow in your sector, the black, the white and the chequer (#551); blue and green are SimHub's. |
| Pit banner: Pit limiter, Engage limiter, Disengage limiter | **Works.** | **Works.** The limiter is read from AC itself through the plugin, not SimHub's member, which is the pit lane on AC, so Engage limiter can show (#558). | **Untested.** SimHub's limiter is the car's; the reminder can show (#1101). | **Untested.** As ACC; the plugin asks the game whether the car has a limiter. |
| Ignition off and engine off alerts | **Works.** | **Works.** "In the car" is the plugin's answer (#561). | **Untested.** The plugin's answer from the game's status. | **Untested.** The plugin's answer from `mInRealtime`. |
| Incident alert | **Works.** | **Absent.** No count is published, so it never fires. | **Absent.** | **Absent.** |
| Push to pass and headlight flash alerts | **Works** on the cars that have them. | **Absent.** Neither is published. | **Absent.** | **Absent.** |
| Change notifications | **Works.** Every setting the car publishes. | **Partly.** TC, ABS and brake bias; the rest are iRacing's own controls and never fire. Quiet in the menus (#561). | **Untested.** TC, ABS and brake bias. | **Untested.** TC, ABS and brake bias. |
| Lap time pop-up | **Works.** | **Works.** | **Untested.** | **Untested.** |

### The bar

| Reading | iRacing | Assetto Corsa | ACC | Le Mans Ultimate |
|---|---|---|---|---|
| Race time | **Works.** | **Works.** | **Untested.** | **Untested.** |
| Lap | **Works.** | **Works.** | **Untested.** | **Untested.** |
| Time left | **Works.** | **Works.** | **Untested.** | **Untested.** |
| Clock | **Works.** The rig's own clock, on every sim. | **Works.** | **Works.** | **Works.** |
| Simulated time | **Works.** | **Absent.** Reads `--` (#1123). | **Absent.** ACC's clock is published under a name the bar does not read. | **Absent.** As ACC. |
| Position | **Works.** | **Works.** | **Untested.** | **Untested.** |
| Class | **Works.** | **Partly.** SimHub gives no AC car a class, so the field is one class and this is the overall place. | **Untested.** Classes from SimHub's ACC table. | **Untested.** The game's own classes. |
| Incidents | **Works.** | **Absent.** The slot is left empty, since AC publishes no incident count (#567). | **Absent.** As AC. | **Absent.** As AC. |
| Air temperature | **Works.** | **Works.** | **Untested.** | **Untested.** |
| Track temperature | **Works.** | **Works.** | **Untested.** | **Untested.** |
| The car settings strip | **Works.** Every setting the car has. | **Partly.** TC, ABS and brake bias; a cell for a setting AC does not publish closes over. | **Untested.** TC, ABS and brake bias. | **Untested.** TC, ABS and brake bias. |

### Zone A

| Reading | iRacing | Assetto Corsa | ACC | Le Mans Ultimate |
|---|---|---|---|---|
| Gear, speed, revs | **Works.** | **Works.** | **Untested.** | **Untested.** |
| Gear alone | **Works.** | **Works.** | **Untested.** | **Untested.** |
| Speed | **Works.** | **Works.** | **Untested.** | **Untested.** |
| Track | **Works.** | **Works.** As the Track module. | **Untested.** | **Untested.** |

### Band D

| Reading | iRacing | Assetto Corsa | ACC | Le Mans Ultimate |
|---|---|---|---|---|
| Fuel | **Works.** | **Partly.** Fuel, time, margin, laps and use work; Refuel reads `--`, since AC publishes no pit order. | **Untested.** Refuel reads `--`. | **Untested.** Refuel reads `--`. |
| Energy | **Absent.** iRacing publishes no virtual energy; every field reads `--`. | **Absent.** | **Absent.** | **Absent.** LMU publishes it under its own names, which the page does not read yet (#962). |
| Stint | **Works.** | **Works.** | **Untested.** | **Untested.** The stop is timed in the box (#1156). |
| Tyres | **Works.** The carcass temperature iRacing measures at the last stop. | **Works.** The live core temperature (#556). The compound reads `--`, since SimHub gives AC none. | **Untested.** Live core temperatures. | **Untested.** The mean of the three tread temperatures. |
| Weather | **Works.** Air, track, wind and the track state. | **Partly.** Air and track; the wind and the track state hide (#1123). | **Untested.** Air and track; ACC's wind and grip are published under names the page does not read. | **Untested.** As ACC. |
| Sectors | **Works.** | **Works.** SimHub times the three sectors. | **Untested.** Whether the second sector is right is a question for the drive (research/sims.md). | **Untested.** |
| Relative | **Works.** | **Works** with SimHub's AC app; without it there is nobody around you. | **Untested.** Needs SimHub's broadcasting connection. | **Untested.** |
| Corners: incidents, track state, sim clock, local clock, DRS, P2P, spotter | **Works.** | **Partly.** The local clock and DRS work; the track state hides and the sim clock reads `--` (#1123); the spotter needs the app; P2P stays dark, rightly; incidents hide (#567). | **Untested.** | **Untested.** |

### The modules

Zones B and C of every face, the companion's pages and the pit wall's zones.

| Reading | iRacing | Assetto Corsa | ACC | Le Mans Ultimate |
|---|---|---|---|---|
| Lap times | **Works.** | **Works.** | **Untested.** | **Untested.** |
| Delta | **Works.** | **Works.** With the last lap chosen it draws the delta to your best and says so (#575). | **Untested.** As AC. | **Untested.** As AC. |
| Sectors | **Works.** | **Works.** | **Untested.** As band D's. | **Untested.** |
| Speedo | **Works.** | **Works.** | **Untested.** | **Untested.** |
| Fuel | **Works.** | **Works.** | **Untested.** | **Untested.** |
| Energy | **Absent.** Off by default. | **Absent.** Says the sim does not publish it. | **Absent.** | **Absent.** As band D's: LMU's own energy is not read yet (#962). |
| Tyres | **Works.** Pressures and temperatures from the last stop. | **Works.** Live pressures and core temperatures; no wheel is ticked for a change, since AC publishes no pit order. | **Untested.** | **Untested.** |
| Pit view | **Works.** | **Partly.** The pit time and its bar work; the service order says "Pit service · Not published by this sim" (#564), since AC's stop is set in the game's own pit app. | **Partly.** As AC; ACC's own pit menu is published under names the page does not read. | **Partly.** As AC; the pit time runs from when the car stops in the box (#1156). |
| Car settings | **Works.** | **Partly.** TC and ABS as the car's preset, and the brake bias; the engine map and the anti-roll bars hide. | **Untested.** TC and ABS as dialled. | **Untested.** TC and ABS as dialled. |
| Inputs | **Works.** | **Partly.** The pedals work; the steering dial hides, since AC's steering is not read yet (#567). | **Partly.** As AC. | **Partly.** As AC. |
| Session | **Works.** | **Partly.** The session's name is drawn as AC writes it, in capitals (`RACE`); incidents read `--` (#567). | **Untested.** As AC. | **Untested.** The game's own names (`Practice`, `Race`). |
| Radar | **Works** once SimHub has recorded the track. | **Works** with SimHub's AC app, and without a recorded lap. | **Untested.** Needs broadcasting. | **Untested.** |
| Track | **Works.** | **Works.** Says "Drive a lap to record it" until SimHub has the outline (#555), names the track as a driver knows it (#1124); the track state hides. | **Untested.** | **Untested.** |
| Leaderboard | **Works.** | **Partly.** With SimHub's AC app: names, places, gaps, laps and pit state; no car number, class or team. Without it, one row: you, named `Me`. | **Untested.** Needs broadcasting; no iRating. | **Untested.** No iRating. |
| Relative | **Works.** | **Partly.** As the leaderboard. | **Untested.** | **Untested.** |
| Opponents | **Works.** | **Partly.** As the leaderboard. | **Untested.** | **Untested.** |
| Gear | **Works.** | **Works.** | **Untested.** | **Untested.** |
| Stint | **Works.** | **Works.** | **Untested.** | **Untested.** As band D's. |
| Lap history | **Works.** | **Works.** | **Untested.** | **Untested.** |
| Damage | **Absent.** Off by default; says the sim does not publish it. | **Works** (#582). Where a light hit ends and a heavy one starts is provisional. | **Untested.** Expected to draw, as on AC. | **Absent.** |
| Track rivals | **Absent.** SimHub has no such value on any sim. | **Absent.** | **Absent.** | **Absent.** |
| Engine readings | **Works.** | **Absent.** AC publishes none of the six; each reads `--`. | **Untested.** The water temperature only. | **Untested.** The water and oil temperatures. |

### The companion

| Reading | iRacing | Assetto Corsa | ACC | Le Mans Ultimate |
|---|---|---|---|---|
| Header, dots and paging | **Works.** | **Works.** | **Untested.** | **Untested.** |
| Flag band and full-screen flag | **Works.** | **Partly.** As the face's flag band. | **Untested.** | **Untested.** |

### The pit wall

| Reading | iRacing | Assetto Corsa | ACC | Le Mans Ultimate |
|---|---|---|---|---|
| Header | **Works.** | **Partly.** The local clock, the session and the time left work; the wind and the track state hide, the sim clock reads `--` (#1123), and the incidents hide (#567). | **Untested.** | **Untested.** |
| Race | **Works.** | **Partly.** The table as the leaderboard: no number, class or tyre; whether each car's sectors fill is unclear. | **Untested.** | **Untested.** |
| Tower | **Works.** | **Partly.** As the race page. | **Untested.** | **Untested.** |
| Telemetry | **Works.** | **Partly.** Speed, revs, gear and the pedals work; the steering panel says it is not published by this sim (#567). | **Partly.** As AC. | **Partly.** As AC. |
| Portrait | **Works.** | **Partly.** As the race page. | **Untested.** | **Untested.** |
| Web view | **Works.** Not a reading. | **Works.** | **Works.** | **Works.** |
| Car telemetry | **Works.** | **Partly.** The pedal traces, TC, ABS and brake bias; the other settings hide. | **Untested.** | **Untested.** |

### The round faces

| Reading | iRacing | Assetto Corsa | ACC | Le Mans Ultimate |
|---|---|---|---|---|
| Rev arc | **Works.** | **Works.** | **Untested.** | **Untested.** |
| Flag ring | **Works.** | **Partly.** Yellow, blue, black, white and the chequer. | **Untested.** | **Untested.** The plugin's four answers, as the flag band. |
| Current lap, Last lap, Best lap | **Works.** | **Works.** | **Untested.** | **Untested.** |
| Delta | **Works.** | **Works.** As the Delta module. | **Untested.** | **Untested.** |
| Position, Session | **Works.** | **Works.** | **Untested.** | **Untested.** |
| Fuel, Fuel laps | **Works.** | **Works.** | **Untested.** | **Untested.** |
| TC, ABS | **Works.** | **Works.** The car's preset. | **Untested.** | **Untested.** |
| Tyre temps, Tyre pressures | **Works.** From the last stop, as the label says. | **Partly.** Live values, under a label that says they are from the last stop. | **Untested.** | **Untested.** |
| Speed | **Works.** | **Works.** | **Untested.** | **Untested.** |

### The themes

| Reading | iRacing | Assetto Corsa | ACC | Le Mans Ultimate |
|---|---|---|---|---|
| Porsche: band D's own page | **Works.** | **Partly.** The session reads `RACE` and `QUAL` (#1125) and the low-fuel alarm lights (#1126); the track state hides; the limiter pictogram follows the limiter (#558). | **Untested.** | **Untested.** |
| Porsche: limiter takeover | **Works.** | **Partly.** The speed reads; the lane's limit reads `--` and the body never turns red, since AC publishes no pit speed limit. It follows the limiter (#558). | **Untested.** The limit is SimHub's constant 60 km/h. | **Untested.** No limit is published. |
| Porsche: engine warnings | **Works.** | **Absent.** AC publishes no engine warnings; the warnings stay dark. | **Absent.** | **Absent.** |
| AiM: session cell | **Works.** | **Works.** A time attack reads `ATTACK` (#1127). | **Untested.** | **Untested.** |
| AiM: Pit Speed takeover | **Works.** | **Partly.** As the Porsche's: the limit reads `--`, and it follows the limiter (#558). | **Untested.** | **Untested.** |
| AiM: engine warnings | **Works.** | **Absent.** As the Porsche's. | **Absent.** | **Absent.** |

### The LED strips

| Reading | iRacing | Assetto Corsa | ACC | Le Mans Ultimate |
|---|---|---|---|---|
| Rev ladder | **Works.** As the face's shift lights. | **Works.** | **Untested.** | **Untested.** |
| Flag lamps | **Works.** | **Partly.** As the face's flag band. | **Untested.** | **Untested.** |
| DRS | **Works** on the cars that have it. | **Works.** | **Absent.** No ACC car has it. | **Absent.** |
| Push to pass | **Works** on the cars that have it. | **Absent.** Stays dark. | **Absent.** | **Absent.** |
| TC and ABS in action | **Partly.** iRacing reports neither; the plugin's wheelspin and lock estimate stands in while the strip asks for it. | **Works**, *source only*, once SimHub's UDP link to the game is up. | **Untested.** | **Untested.** |
| Low fuel | **Works.** | **Works.** | **Untested.** | **Untested.** |
| Temperature and oil pressure warnings | **Works.** | **Absent.** Stay dark. | **Absent.** | **Absent.** |
| Spotter | **Works.** | **Works** with SimHub's AC app. | **Untested.** | **Untested.** |
| Turn indicators | **Absent.** iRacing's reader does not fill them; they stay dark. | **Absent.** As iRacing. | **Untested.** | **Untested.** |
| Pit lane | **Works.** | **Works.** | **Untested.** | **Untested.** |
| Pit limiter | **Works.** | **Works.** Lights with the limiter, in the lane or out of it (#558). | **Untested.** | **Untested.** |
| Pit speeding | **Works.** | **Absent.** No pit speed limit. | **Untested.** SimHub's constant 60 km/h. | **Absent.** |

### The flag box

| Reading | iRacing | Assetto Corsa | ACC | Le Mans Ultimate |
|---|---|---|---|---|
| Flags | **Works.** All fifteen pictures. | **Partly.** The black bar, the standing yellow, the blue, the white and the chequer ([flag-box.md](flag-box.md)). | **Untested.** Six pictures. | **Untested.** Six, four of them the plugin's answers. |
| Pit limiter | **Works.** | **Works.** As the strip's (#558). | **Untested.** | **Untested.** |
| Pit speeding | **Works.** | **Absent.** AC publishes no pit speed limit, so it never shows. | **Untested.** SimHub's constant 60 km/h on every track. | **Absent.** No pit speed limit is published. |

### Between sessions

| Reading | iRacing | Assetto Corsa | ACC | Le Mans Ultimate |
|---|---|---|---|---|
| Idle screen | **Works.** It reads nothing a game publishes. | **Works.** | **Works.** | **Works.** |
| "Go into a session" notices | **Works.** | **Works.** AC always names its session. | **Untested.** | **Untested.** |

## What is left

No cell of Assetto Corsa's column says **Wrong**: every reading there works, works in part with the gap named, or shows its
not-available state. The **Absent** rows are what AC does not publish, and none of them draws a value.

Two **Partly** cells on Assetto Corsa are not what AC lacks but what the packages could say better, and
neither has a ticket yet: the round faces' tyre cards are captioned "last stop" over AC's live values,
and a car's sectors on the pit wall are unclear. The energy page reads only iRacing's names for virtual
energy, so it cannot draw Le Mans Ultimate's, which is #962's.

The pit wall's iRating column is only drawn where the sim publishes one (#1162). The race and portrait
boards are stamped twice, with the column and without it, and the player's own `iracingirating` being
null picks the second, whose driver column takes the width back; the opponents page hides its `iR`
label on the same test. So on Assetto Corsa, ACC and Le Mans Ultimate no row reads `--` under
"iRating", and on iRacing the boards are as they were.

What a driver will meet on AC that no binding can change is SimHub's AC app: with it switched off,
every page that lists other cars lists only you, which is SimHub's behaviour and not OpenDash's.
[research/sims.md](research/sims.md) keeps the questions only a drive can settle, the steering's unit
and sign among them.
