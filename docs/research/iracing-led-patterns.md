# The car's own LED pattern, and where it can be got

**Last updated:** 2026-09-16
**Confidence:** the SimHub half was read out of the decompiled 9.12.6 `SimHub.Plugins.dll` — the
same assembly [simhub-leds-format.md](simhub-leds-format.md) was read from — and the container
bodies are quoted below. The iRacing half was established by enumerating every published table in
the [Lovely Car Data](https://github.com/Lovely-Sim-Racing/lovely-car-data) database for iRacing
(85 cars, fetched 2026-09-16) and counting; the numbers in this file are from that count, not from
an impression.

[simhub-led-sources.md](simhub-led-sources.md) says what drives an LED, property by property, and
concludes that iRacing publishes no colour. That conclusion is correct and this file does not
overturn it. What it adds is the other half of the sentence: **nobody gets the pattern from
telemetry, because it is not in telemetry — every project that mirrors it carries a measured
table**, and one of those tables is open.

## What the sim publishes, and what it does not

The four `DriverCarSL*` RPMs of [ADR 0014](../decisions/0014-the-shift-model.md) are the whole of
what iRacing says about its own shift lights: first light, shift, last light, blink. One set per
car. From them a strip can know *when* to light and never *what lights*.

Absent, at every layer — checked again for this file against the property scan in
[simhub-led-sources.md](simhub-led-sources.md):

- how many LEDs the car's own bar has,
- what colour each one is,
- which order they light in,
- whether they light one at a time, in blocks, or from both ends inwards,
- how fast the over-rev flash is,
- and whether any of the above changes with the gear.

The last one is the one that matters most and is easiest to miss. `DriverCarSL*` cannot vary with
the gear — it is one block in the session string — and **32 of the 85 measured cars have a
different table in different gears.** The Porsche 992 Cup's first gear lights its outer LEDs from
idle and its centre pair at 6 720 rpm; its sixth lights nothing below 8 600. A mirror built on the
four RPMs is a mirror of one gear of that car at best.

## Everyone solves it with a table

| Project | Table | Shape |
|---|---|---|
| [Daniel Newman Racing](https://www.danielnewmanracing.com/products/led-profiles) | 600+ cars across seven sims, per gear. Paid | A SimHub **plugin** holds the data; per-device profiles are thin and bind to it |
| [Lovely Car Data](https://github.com/Lovely-Sim-Racing/lovely-car-data) | 85 iRacing cars, per gear, per LED, per colour. CC BY-NC-SA 4.0 | Plain JSON per car, keyed on `DataCorePlugin.CarId` |
| [ShiftLines / kapps](https://github.com/shinev01/rpm-lights-iracing-kapps), ATSR Hub EVO | consume Lovely | A sync script pulls from upstream and bundles a snapshot |
| Fanatec App | "preloaded with precise LED patterns for a great number of popular cars" | Closed. Same idea, unreadable |
| Everything else | none | Falls back to the four RPMs, which is where openDash is today |

Two things follow. The first is that a measured table is not a shortcut somebody took; it is the
only known way. The second is that **the pattern vocabulary is not a list of styles to implement.**

## The taxonomy is one data shape

A car's bar is fully described by: for each LED, an RPM it lights at and a colour; for the car, a
blink colour and a blink interval. Every look anybody names falls out of the numbers:

| What a driver calls it | What it is in the table |
|---|---|
| left to right | thresholds ascending |
| meet in the middle | thresholds symmetric about the centre, outer LEDs lowest |
| three blocks | runs of LEDs sharing one threshold |
| one at a time | every threshold distinct |
| all red, or green-yellow-red | the colour array, nothing else |
| a gap in the bar | an LED whose colour is transparent |
| a single shift lamp | a car whose bar is one LED |

Counted over the 85 cars, taking each car's most-used gear:

- **49 ascending** (left to right), **30 symmetric** (meet in the middle), 6 neither.
- **38** have repeated thresholds somewhere, so blocks are the common case rather than the exception.
- **12** have at least one transparent LED: a real gap in the bar, e.g. the BMW M4 GT3's two.
- LED counts are 1 (5 cars), 5 (7), 6 (4), 8 (18), 9 (4), 10 (25), 12 (12), 15 (4), 16 (6).
- The blink interval is 0 for **47** cars — they do not flash at all — and 50 to 500 ms for the rest,
  most often 250 ms.

So openDash does not need a `meetInMiddle` code path, a `blocks` code path and a `oneAtATime` code
path. It needs one renderer over a table, and the pattern is data. The three styles the strip
already offers stay as what they are: a driver's preference, for a car with no table and for a
driver who wants one look in every car.

## The schema, as it really is

```jsonc
{
  "carName": "Porsche 992 Cup",
  "carId": "porsche992cup",          // DataCorePlugin.CarId, verbatim, spaces and all
  "carClass": "CUP",
  "ledNumber": 16,                   // LEDs on the car's own bar
  "redlineBlinkInterval": 200,       // ms; 0 means it does not blink
  "ledColor": ["#FF6495ED", "#FF00FF00", ...],   // [0] is the blink colour, then one per LED
  "ledRpm": [{
    "R": [8600, 7000, 7200, ...],    // [0] is the redline (blink point), then one per LED
    "1": [7900, 0, 0, 0, 2000, ...],
    "6": [9000, 8600, 8650, ...]
  }]
}
```

Facts a parser has to survive, each checked across all 85 files:

- **`ledColor` and each gear row are `ledNumber + 1` long**, index 0 being the redline/blink entry —
  except in `stockcars-fordtaurus03.json`, where `ledColor` is one short. One malformed file in 85
  is the rate to build for: a file that does not agree with itself is skipped, not thrown on.
- **Gear keys are `"R"`, `"N"` and `"1"`…`"8"`.** Every car has 1–4; 41 have an eighth.
- **A threshold of 0 is ordinary** — 110 gear rows have one. It means the LED is lit from idle, and
  a transparent colour is what makes such an LED a gap rather than a permanently-on light.
- **Colours are `#AARRGGBB` in every iRacing file** (878 of 878), although the format also allows
  HTML colour names, so both have to be read.
- **The redline is not always above the last LED's threshold** — 3 rows of 755 have it below. Nothing
  may assume the ordering the other 752 have.
- Cars are keyed by `carId`, which is SimHub's `DataCorePlugin.CarId` with its spaces intact
  (`"stockcars chevycamarozl12022"`); the *file name* is the hyphenated form. Match on the field,
  never on the path.
- Coverage is partial and always will be: a car released this month is in the table when somebody
  measures it, and the fallback is what every other car gets.

**Licence.** CC BY-NC-SA 4.0. This repository is MIT, and `data/shift-points.json` says in its own
`$meta` that openDash does not carry measurements it has not made. Both are reasons not to vendor a
copy, and [ADR 0017](../decisions/0017-the-cars-own-lights.md) records what is done instead: the
plugin fetches it onto the user's machine and openDash ships none of it.

## Where it can go, inside SimHub

Three doors, all real, and the choice between them is [ADR 0017](../decisions/0017-the-cars-own-lights.md)'s.

### `RPMSegments` lights each segment independently

This is the finding that says a per-car pattern is expressible in a profile at all. From
`RPMSegmentsContainer.SetResultBase`:

```csharp
foreach (LedSegment value in SegmentsReadonly.Values)
{
    if (num2 > value.StartValue && !BlinkExtensions.IsBlinking(flag, BlinkDelay))
        result.Graphics().Offset(num).Fill(value.LedCount, ...);
    num += value.LedCount;
}
```

`num2` is `data.Rpms` under `RpmMode.Rpms`. Each segment is tested against **its own** `StartValue`
with no monotonicity assumed anywhere, so a descending second half is as legal as an ascending
first one: one container with one segment per LED expresses any of the patterns above.

Its limit is the blink. `flag` is `data.RPMRedlineReached && BlinkEnabled` — SimHub's own redline,
computed from its per-car settings (see [simhub-led-sources.md](simhub-led-sources.md)), not the
table's. A mirror that used it would flash at the wrong RPM, and `BlinkDelay` is a constant in the
file, so a per-car interval means a per-car container.

### `DynamicColor` takes a colour from an expression

```csharp
Color color = base.NcalcEngine.ParseValueOrDefault(ColorFormula, flag ? BlinkingColor : Color.Black);
result.Fill(LedCount, color);
```

and the `Color` overload of `ParseValueOrDefault` evaluates the formula **as a string** and hands it
to `ColorConverter.ConvertFromString`, so `#AARRGGBB`, `#RRGGBB` and `Transparent` all arrive
intact. One `DynamicColor` per LED, reading one property per LED, puts the whole decision in
whatever fills that property — and nothing in the profile has to know what a car is.

### `ScriptedContent` takes the whole run at once

`ContentFormula` is parsed with the `Color[]` overload, which requires the expression to return an
`object[]` of strings (`ToColorArray` does `i as string`). One container for a whole run instead of
one per LED, at the cost of a Javascript body in a profile that is otherwise NCalc, and of a
marshalling assumption not yet tested on the VM. Kept as the collapse to make if the per-LED
property family proves too noisy.

## Not verified

- Whether a JS array from a `ScriptedContent` formula marshals to `object[]` as `ToColorArray`
  requires. Everything above about `DynamicColor` and `RPMSegments` was read from the bodies quoted;
  this one was not.
- Whether `LedResult.Fill` with `Transparent` composes over the layer underneath or blanks it. The
  mirror owns its whole run, so nothing currently depends on the answer.
- How `BlinkExtensions.IsBlinking` phases its blink across containers — irrelevant while the blink
  is computed by the plugin, and load-bearing the moment it is not.
- The tables themselves. They are somebody else's measurements, and openDash tests its renderer
  against synthetic fixtures rather than pinning numbers it did not take.
