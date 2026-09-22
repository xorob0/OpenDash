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
| `quali` | A timed qualifying run in a car with no in-car TC or ABS, so the settings strip has to close over two cells that are simply not there, and the session counts time rather than laps. |

## The scenario that moves on purpose

| | What it does |
|---|---|
| `shiftlights` | One slow RPM sweep, 1200 to 9600 over twenty seconds, against a 7800 / 8400 / 8800 / 9000 ladder — slow enough to see which LED changes at which threshold. At forty seconds the car changes to one with a much lower ladder and five gears, and at eighty it changes back. That swap is the point: it is what tells a mirror (ADR 0014) apart from arithmetic over the redline, and it is what the generated `.ledsprofile` files are verified against. Nothing else moves — the flag is held green, the limiter is off, and the pedals are pinned — because a strip that is also cycling flags says nothing about its revs. |

The four shift RPMs, the redline and the forward-gear count are `{{placeholders}}` in
`race-session.yaml` rather than literals, so a scenario can hand the mirror a different car mid-run.
`race.json` carries the defaults every other scenario inherits.

`bun run dev --scenario green` is the usual way to reach one, and `bun run shots` walks all four.

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

`notc.json` is the same field in a car with no `dcTractionControl` or `dcABS` at all — SimHub then
reports level 0 and the dashes have to show `--` rather than a zero — in a timed race rather than
a lap-counted one. `quali.json` extends it.

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
