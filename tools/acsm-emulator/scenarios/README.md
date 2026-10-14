# Assetto Corsa scenarios

A scenario is the telemetry the emulator publishes. `race.json` is the base and carries the field,
the car and the drivers that move the values; everything else `extends` it and overrides what it
needs. They are the twins of [the irsdk emulator's](../../irsdk-emulator/scenarios/README.md) where
Assetto Corsa publishes the state, with the same names, so that `--scenario yellow` means the same
picture on either sim and the two can be put side by side.

## The capture scenarios

Everything that decides what the dash draws is **pinned**, and the drivers that would move it are
switched off in the timeline, so a screenshot taken at any moment shows the same state.

| | What it holds still |
|---|---|
| `green` | A clean racing lap, P3 of 24, with no flag out. AC has no green flag: `Graphics.Flags` is `AC_NO_FLAG` on a clear track, and SimHub marks `Flag_Green` not available on AC, so this is the absence of every other flag. TC goes from 3 to 5 at twenty seconds, as on iRacing. |
| `yellow` | A yellow-flag lap on low fuel: `AC_YELLOW_FLAG`, 2.1 litres, a hot right front and a cold left front, worn right-hand tyres, TC 7 and ABS 4, and P6, the player put behind five cars of the field. AC's yellow is one value, with no waving or caution beside it. |
| `pit` | In the box, in the pit lane, the raw limiter on, stationary in neutral, P9. AC publishes no service order, so unlike iRacing's `pit` there is no pit menu to draw, and pit view says the order is not reported while its pit time counts (#564). `GameData.PitLimiterOn` reads on here, but because SimHub answers it with the pit lane on AC (#558), which `race` shows from the other side. The dashes read the plugin's `OpenDash.PitLimiterOn`, which is the raw limiter (#1122). |

## The others

| | What it does |
|---|---|
| `race` | The base: Spa, 24 cars, a 30-lap race with 12 done, the player in a Porsche 911 GT3 R in P3. Revs, gears, the lap, fuel, the flags (one at a time) and the raw limiter all move, the leader goes through the pits at 90 s, and damage appears at the front at 45 s. The limiter toggles on track, and `GameData.PitLimiterOn` stays off while it does: on AC SimHub reads the pit lane there, not the limiter. The plugin's `OpenDash.PitLimiterOn` follows the toggle, so the limiter lamp and strip light and band D raises "Disengage limiter" every five seconds (#1122). |
| `notc` | The race, timed rather than lap-counted, with `Physics.TC` and `Physics.Abs` held at 0, so SimHub reads TC and ABS level 0 and a dash shows its no-data state. On AC that is also what a car without the systems reads. |
| `quali` | A timed qualifying run in that car. SimHub names the session from `Graphics.Session` with `AC_` taken off: `QUALIFY`, in capitals, where iRacing says `Lone Qualify`. Outside a race AC's position is the order of best laps, and the player's 1:36.284 is the best of the field, so P1. |
| `timeattack` | `quali` in AC's time attack: SimHub writes `TIME_ATTACK`, eleven cells, the widest session name any of the sims writes, which is what the AiM's Session cell has to hold (#1127). iRacing has no time attack, so like `noapp` it has no twin there. |
| `shiftlights` | One slow sweep, 1200 to 9600 over twenty seconds, then at forty seconds the Mazda MX-5 Cup (MaxRpm 7200, `ai.ini` UP 6800, five gears), and the Porsche again at eighty, looping every 120 s. AC publishes no shift-light ladder, so SimHub's lights come from `StaticInfo.MaxRpm` and the car's redline; the swap shows whether they follow the car. |
| `noapp` | `green` without SimHub's AC app, which is an AC install SimHub has not configured: no `Local\acpmf_simhub_v2`, so SimHub's leaderboard is the player alone, named `Me`, and every gap, relative and position table reads a field of one. iRacing has no equivalent; its field is in the session string whatever is installed. |

## Traces

None is committed yet: the scenarios were added by a branch that could not claim the VM, and every
one of them is on Assetto Corsa's untraced list in [scripts/sims.ts](../../../scripts/sims.ts).
`bun run record --sim ac race green yellow pit quali notc noapp timeattack` records them into
`traces/ac/`, and each name comes off the list in the commit that adds its trace. `shiftlights` stays on it: its point
is the car swap at forty seconds, which a twenty-second trace would not hold.

## Checking one

```bash
export PATH="$HOME/.dotnet:$PATH"; export DOTNET_ROOT="$HOME/.dotnet"
dotnet build tools/acsm-emulator/devcheck -c Release
cd tools/acsm-emulator
dotnet devcheck/bin/Release/net8.0/acs.dll --selfcheck pit
```

The self-check prints the TC and ABS levels SimHub will read from the car's curves and the player's
place, so a scenario meant to show P6 and TC 7 can be checked to do so before the VM is involved.

## Adding one

Extend the nearest existing scenario. If it is meant to be photographed, pin every value the dash reads
and disable the drivers that would move them, then say in a comment at the top what state it holds and
why that state is worth a picture. The player's place comes from the field, so it is set by where the
player is on the lap (`Graphics.NormalizedCarPosition` and `Graphics.CompletedLaps`) against the cars
of `race.json`, not by pinning `Graphics.Position`.
