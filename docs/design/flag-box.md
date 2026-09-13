# The flag box: what it draws, and what it will not

The 8x8 matrix profile is built from the same ordered catalogue the face ranks from
(`packages/dash/src/flags.ts`), drawn as 64-pixel pictures in
`packages/dash/src/leds/glyphs.ts`. This file is the other half of that code: which conditions
have a glyph, which do not, and why.

[ADR 0013](decisions/0013-lighting-hardware.md) is why openDash lights a box at all.
[simhub-leds-format.md](../research/simhub-leds-format.md) is the file format.

## The rule

**A drawn alert that never fires is worse than an absent one**, because nobody finds out until a
race. So a condition iRacing does not publish is not drawn, and it is listed below with the
reason rather than approximated from something nearby.

Detection is `SessionFlagsDetails`, SimHub's per-bit explosion of iRacing's `SessionFlags`
bitfield, which gives one boolean per bit and needs no bitwise operator. The six normalised
`Flag_*` properties are not used here: `Flag_Yellow` folds `yellow`, `yellowWaving`, `caution`
and `cautionWaving` into one, and `Flag_Black` is only the `black` bit, so a furled black and a
disqualification are invisible through them.

## What is drawn

Highest priority first. One picture at a time: a condition shows only when nothing above it is
raised.

| | Condition | iRacing bits | Critical | The picture |
|---|---|---|---|---|
| 1 | Red | `red` | yes | The whole box red. The only condition that takes it in one colour. |
| 2 | Disqualified | `disqualify` | yes | A cross, blinking. The one flag that ends the race whether the driver reacts or not. |
| 3 | Black | `black` | yes | An outline. |
| 4 | Black furled | `furled` | yes | The same flag rolled up: a bar rather than a field. |
| 5 | Meatball | `repair` | yes | An orange disc. Round, so it is not read as a flag of another colour. |
| 6 | Chequered | `checkered` | no | A checkerboard of two-pixel squares. |
| 7 | Full-course caution | `caution`, `cautionWaving` | yes | Yellow in bands. |
| 8 | Waved yellow | `yellowWaving` | yes | The yellow flag, blinking. |
| 9 | Yellow | `yellow` | yes | Solid yellow, steady. |
| 10 | Debris | `debris` | yes | Yellow with danger stripes. |
| 11 | Blue | `blue` | yes | Blue with an arrow that moves: two frames. |
| 12 | White | `white` | no | Solid white. In iRacing this is the last lap. |
| 13 | Green | `green` | no | Solid green. |
| 14 | Set | `startSet` | no | Two bars of the start gantry. |
| 15 | Ready | `startReady` | no | One bar of the start gantry. |

Colour is `purpose.flag.*` from `design/tokens.json`, resolved to a literal at build time. The
blink rate is `indicator.flagBand.flashHz`, so the box pulses at the same rate as the band on the
face.

### The four decisions sixty-four pixels forced

**Black is the absence of light.** A black field on an unlit matrix is nothing at all, so the
black flag is an outline and a disqualification is a cross, both in `purpose.flag.black` — which
is the near-white the face already outlines it with, for the same reason.

**Waving is blinking.** iRacing publishes `yellow` and `yellowWaving` separately and a driver has
to tell them apart at a glance. That is a pattern question rather than a colour one, and blinking
is the pattern with no pixels to spare.

**A full-course caution is banded.** The whole-track condition must never look like one corner's
yellow, and bands are legible at this size where a pace-car silhouette is not.

**Blue moves.** A blue flag with a moving arrow says which way to look. A static blue square says
a colour.

## Below the flags

In order: the pit family, the spotter, the three warnings, then the gear. Each layer's condition
excludes the layers above it, and the whole stack sits under "no flag is being shown".

**That ordering is the point.** A spotter warning that hides a yellow, or a low fuel light that
hides one for the rest of a stint, is the failure this ranking exists to prevent.

### The pit family

| Condition | Picture |
|---|---|
| Speeding in the lane | A double chevron in danger red, blinking. Outranks both limiter states: it is the one costing a penalty right now. |
| Limiter on, out of the lane | An exclamation mark, blinking. A mistake costing a second a corner. |
| Limiter on, in the lane | A two-pixel frame, steady. Quiet confirmation; the driver is doing the right thing. |

Speeding compares the car's speed with the lane limit **in metres per second**, which matters more
than it looks: `PitLimiterSpeed` is published through `KmhToLocalSpeedUnit`, so for a driver whose
SimHub speed unit is MPH a 60 km/h limit arrives as 37 — and against `SpeedKmh`, which is always
km/h, that reads as speeding from a standstill. Because speeding heads the exclusion chain, it
would have blacked out the spotter, the warnings and the gear for every imperial user rather than
merely lighting the wrong picture. `PitLimiterSpeedMs` is metres per second whatever the user has
set, and it is `isnull()`-wrapped with a speed nothing reaches, so a track that publishes no limit
means "not speeding" rather than "always speeding".

Comparing two published numbers is arithmetic over properties rather than state between frames, so
it is not computed telemetry.

The three differ in **shape**, not only in colour, which [XOR-78](https://linear.app/xorob/issue/XOR-78)
requires. Two of them nearly did not: `purpose.pitLimiter` resolves to pure white, the same value
as `purpose.flag.white`, so a filled panel for "limiter still on" would have been the white flag
with a blink — telling a driver *last lap* when you mean *your limiter is on*. The token is not
changed to fix that: `design/` is the design source and a colour is decided there, so the shapes
carry the difference. A test now refuses any two identical pictures anywhere in the profile.

### The spotter

A bar two pixels wide down the edge the car is on: left, right, or both edges at once.

Which side a box is *mounted* on is a per-matrix setting, not a guess. A box to the left of the
wheel lighting for a car on the right is worse than no box. `Both` is the single-box setup and
shows both edges.

**Three states, not four.** iRacing's `CarLeftRight` distinguishes clear, a car on one side, cars
on both sides and *two cars* on a side — but SimHub folds it into two booleans on the way through
(`CarLeftRight` 2, 4 and 5 all become `SpotterCarLeft`), so the two-car state does not survive.
Three are shipped rather than a fourth faked. The raw value would carry it; reading it has not been
verified, and a spotter that is wrong is worse than one that says less.

### The warnings

| Condition | Threshold | Picture |
|---|---|---|
| Oil too hot | `OpenDash.FlagBoxOilTemp`, default 120 °C | A disc, orange, blinking |
| Water too hot | `OpenDash.FlagBoxWaterTemp`, default 110 °C | Waves, orange, blinking |
| Low fuel | `OpenDash.FlagBoxLowFuelLaps`, default 2 | A tank emptying, yellow, blinking |

**Low fuel is measured in laps.** A litre threshold means nothing without knowing the car; laps
remaining means something in every car, and SimHub publishes `Fuel_RemainingLaps`.

**Temperatures are compared in SimHub's own unit.** A driver in Fahrenheit who sets 120 and gets a
Celsius threshold has been given a broken feature, and a threshold that silently converts is worse
than one that refuses. The *default* is chosen from `TemperatureUnit` inside the expression, so a
driver who never opens the panel gets 248 °F rather than 120 °F. A car that reports no temperature
reads as 0 and never trips a warning.

**No acknowledgement.** DNR lets a driver dismiss a low fuel alert. That needs memory between
frames and a button binding, so it is not here. What *is* here is the ordering: a standing warning
sits below the flags and cannot hide one.

### The countdown to your box, which is not drawn

The most loved thing on any flag box profile, and not ours to draw. It needs a distance to the
driver's own stall, and **iRacing publishes no such value**: the telemetry carries `PitSvFlags` and
`PitRepairLeft` but no distance, and `CarIdxLapDistPct` is a lap fraction for every car rather than
a distance to a stall. Deriving one from track position and the stall's position is computed
telemetry, which [scope.md](../scope.md) refuses until
[ADR 0009](../decisions/0009-does-the-plugin-compute.md) moves the line.

So it is written down here rather than quietly computed. If XOR-47 moves that line, this is the
first thing to build with it.

### The gear, underneath everything

`OpenDash.FlagBoxGear` (on by default) draws the gear filling the panel when nothing else is on
it. It is the resting state rather than a feature: every flag outranks it, and when they let go
it comes back. Off leaves the panel dark rather than showing something else.

The glyphs are a 5 by 7 font in source, centred in the panel, not text rendered small — the
bundled Barlow Condensed does not exist at eight pixels, and a thin face leaves one or two pixels
between a 6 and an 8 on a box read in peripheral vision. `R` and `N` have glyphs of their own.

The colour is the shift model: `shiftBands()` in `components/revSegments.ts` is the one place the
three bands are defined, and the rev bar and the gear both read it, so a driver with both learns
one relationship and reads it in two places. The redline band **blinks the digit** rather than
filling the panel behind it: a filled panel is a flag's vocabulary and the box has to keep those
apart. When [XOR-230](https://linear.app/xorob/issue/XOR-230) replaces SimHub's per-car bands with
the sim's own `DriverCarSL*` values it changes that one function, not this file.

There is no gear colour theme. Theming is refused in [scope.md](../scope.md) until ADR 0011 says
otherwise, and the argument there — two states a driver cannot tell apart is a bug whoever chose
the colours — is at its strongest on a device whose entire vocabulary is colour.

**Not judged on a diffused matrix at arm's length.** The font was designed against the constraint
and checked for pairwise distinctness in a test; whether a 6 reads as a 6 through a diffuser is
the check no VM can do.

### When nobody is racing

**No game running is dark.** Idle screens are a refusal in [scope.md](../scope.md), and a glowing
logo on somebody's desk when nothing is running is the hardest version of that refusal to defend.
The branch exists in the tree and is deliberately empty, so the code says so in one place.

**Ignition off is a dim mark**, four pixels of `purpose.shift.unlit` in the middle. The car being
switched off is a real condition, and a box that went fully dark for it would be indistinguishable
from a profile that failed to load. It is the smallest thing that is still visibly on.

Nothing reads a picture from the user's disk. Every glyph is frames in the file; a custom idle
image is personalisation, which ADR 0011 owes an answer before anything here builds it.

### Brightness, and night

| Property | Default | |
|---|---|---|
| `OpenDash.LightsBrightness` | 100 | Percent. SimHub's own device brightness applies on top. |
| `OpenDash.LightsNightBrightness` | 25 | Percent, when night mode is on. |
| `OpenDash.LightsNightMode` | off | A switch the driver flips, not a time of day we guess at. |

They are named `Lights*` rather than `FlagBox*` on purpose. A driver who owns a flag box probably
owns other lights, and "how bright are my lights, and is it night" is one answer for a rig rather
than one per device; a second profile would read these same three. A property name is a public
interface under ADR 0003, so the alternative is renaming one later.

Sixty-four LEDs at full output beside a wheel in a dark room is genuinely too bright, and no
amount of good colour choice fixes it. `purpose.*` decides hue; brightness decides how much of it
arrives. **The night default has not been judged in a dark room on real hardware**, which is the
one thing this ticket asked for that the VM cannot give; 25 is a starting point.

### Critical flags only

`OpenDash.FlagBoxCriticalOnly` is off by default: the box shows the whole catalogue until the
driver asks for quiet. A box that stays dark through a chequered flag is a surprise, and a
surprise is a worse default than a busy one.

With it on, the box keeps the flags that mean *slow down* or are *addressed to this car*, and
drops the news: the chequer, the white, the green and the start gantry. A suppressed flag stops
outranking the ones below it, so the box shows the next critical flag down rather than going
dark — a chequered flag over a blue flag shows blue, not nothing.

Both lists are in the file, under the two halves of the switch, so changing it reaches a running
SimHub without a rebuild.

## What is not drawn, and why

Everything a comparable flag box draws that openDash does not. Each line is a thing somebody will
ask for; the answer is that iRacing does not publish it, not that it was forgotten.

| Wanted | Why not |
|---|---|
| **Yellow per sector** | iRacing's `SessionFlags` has no per-sector yellow. Even if it did, eight pixels across cannot say *which* sector without inventing a legend the driver has not been taught. |
| **Virtual safety car** | iRacing has no VSC. `caution` is a full-course caution with the pace car deployed, which is drawn, and is not the same thing. |
| **Safety car, as its own picture** | The closest honest reading of `caution`/`cautionWaving` *is* the pace car being deployed, and it is drawn as the full-course caution. A second glyph would be the same condition twice. |
| **White for a slow car** | iRacing's `white` is the last lap and nothing else. There is no slow-car white in the bitfield. |
| **Incident, penalty, drive through, stop and go** | None of these is a `SessionFlags` bit. iRacing communicates them through the black flag and text; the box shows the black flag. |
| **One lap to green, ten to go, five to go** | `oneLapToGreen`, `tenToGo` and `fiveToGo` are published, and they are session information rather than flags. The screen has the room to say them in words and the box does not; drawing a numeral here would compete with the gear. |
| **Green held** | `greenHeld` is published and means the green is being withheld at a restart. It has no distinct picture that would not be mistaken for a green flag, which is the opposite of what it means. |
| **Crossed, random waving** | `crossed` and `randomWaving` are published, and neither has a documented meaning in iRacing's own reference. Drawing something for a condition nobody can define is how a box starts lying. |
| **Serviceable** | `servicible` is in the bitfield and iRacing's own header says it is *not a flag*: it reports whether the car may be serviced. |
| **Start go** | `startGo` and the green flag are the same instant. Green is drawn. |

## Four matrices, and what the file costs

SimHub composes up to four matrix contents (`MultiMatrixResult` holds `MatrixResult[4]`), and each
gets a group of its own with its own settings. A group's `StartPositionMatrix` is an *offset*
applied when its children's results are merged, so the subtree below it is written at matrix 1 and
shifted onto the right panel.

**The subtree is repeated once per matrix, and that is the whole of the file's size.** SimHub has
no way to bind which matrix a container paints — the position is a static property — so there is no
alternative to writing it four times. The profile is about 757 KB and 600 containers, roughly 16%
of the plugin DLL and about 40 KB once the release zip compresses it.

**It does cost frames, and an earlier version of this file claimed otherwise.** The claim was that a
group whose condition is false is not descended into. It is: `MatrixContainerBase.GetResult` calls
`GetGroupResult` unconditionally and passes its own truth down as `parentEnabled`, so children are
walked and their conditions evaluated every frame whether the parent is active or not — only the
*painting* is skipped. Six hundred containers means six hundred `IsActive` calls a frame, on all
four matrices, including the three nobody switched on.

That has not been measured on real hardware, and it is the first thing to measure. If it is too
slow, the fix is the one this file rejected on size grounds: fewer containers, by moving the gear's
four colour bands behind a `ScriptedContent` container or by accepting SimHub's own `GearContainer`
and its two colours.

Two things were done to keep it from being twice that. The critical-flags-only switch is a guard on
each non-critical flag rather than a second copy of the catalogue; and the gear, which is
forty-four glyphs, sits beside the switch rather than under both halves of it.

## Seen, or not

No 8x8 panel is plugged into the test VM and CI owns no hardware, which
[scope.md](../scope.md)'s definition of done states as an exception rather than leaving implied.
What can be checked is that the profile loads in real SimHub and that the glyphs are right in
SimHub's own matrix preview. Everything above is drawn from the bits SimHub publishes, read out
of the decompiled reader; **that every one of them fires when the sim raises it has not been
watched happen.**
