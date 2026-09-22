# The flag box: what it draws, and what it will not

The 8x8 matrix profile is built from the same ordered catalogue the face ranks from
(`packages/dash/src/flags.ts`), drawn as 64-pixel pictures in
`packages/dash/src/leds/glyphs.ts`. This file is the other half of that code: which conditions
have a glyph, which do not, and why.

[ADR 0013](decisions/0013-lighting-hardware.md) is why OpenDash lights a box at all.
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

The table carries the canvas's own alert number beside each rank, and the band the face draws in
band D, so that the two surfaces can be read against one another on one page. Band D draws all
fifteen; `docs/design/zones.md` describes the band itself.

| | Condition | iRacing bits | Critical | Canvas | The picture | The band |
|---|---|---|---|---|---|---|
| 1 | Red | `red` | yes | 3 | The whole box red. The only condition that takes it in one colour. | Filled `purpose.flag.red`, "RED FLAG". |
| 2 | Disqualified | `disqualify` | yes | 4 | A cross, blinking. The one flag that ends the race whether the driver reacts or not. | Outlined `purpose.flag.black`, "DISQUALIFIED". |
| 3 | Black furled | `furled` | yes | 5 | The same flag rolled up: a bar rather than a field. | Outlined, "BLACK FLAG · FURLED". |
| 4 | Black | `black` | yes | 6 | An outline. | Outlined, "BLACK FLAG". |
| 5 | Meatball | `repair` | yes | 18 | An orange disc. Round, so it is not read as a flag of another colour. | Filled `purpose.flag.orange`, "MEATBALL". |
| 6 | Full-course caution | `caution`, `cautionWaving` | yes | 7 | Yellow in bands. | Filled `purpose.alert.safetyCar`, "SAFETY CAR". |
| 7 | Waved yellow | `yellowWaving` | yes | 9, 10 | The yellow flag, blinking. | Filled yellow, "WAVED YELLOW", flashing. |
| 8 | Yellow | `yellow` | yes | 11 | Solid yellow, steady. | Filled yellow, "YELLOW FLAG", steady. |
| 9 | Debris | `debris` | yes | 19 | Yellow with danger stripes. | Filled `purpose.flag.debris`, "DEBRIS". |
| 10 | Blue | `blue` | yes | 20 | Blue with an arrow that moves: two frames. | Filled `purpose.flag.blue`, "BLUE FLAG". |
| 11 | White | `white` | no | 16 | Solid white. In iRacing this is the last lap. | Filled white, "WHITE · LAST LAP". |
| 12 | Green | `green` | no | 21 | Solid green. | Filled green, "GREEN FLAG", on `Flag_Green` rather than on the bit. |
| 13 | Set | `startSet` | no | 22 | Two bars of the start gantry. | Outlined green, "GREEN · SET". |
| 14 | Ready | `startReady` | no | 22 | One bar of the start gantry. | Outlined green, "GREEN · READY". |
| 15 | Chequered | `checkered` | no | 23 | A checkerboard of two-pixel squares. | The same board, half the band high, with no name. |

Colour is `purpose.flag.*` from `design/tokens.json`, resolved to a literal at build time. The
blink rate is `indicator.flagBand.flashHz`, so the box pulses at the same rate as the band on the
face.

### Where the rank departs from the canvas's numbering

One rule governs it: **no condition the critical-flags switch can silence outranks one it cannot.**
The switch guards the non-critical half of the list, so a non-critical condition ranked above a
critical one would mean that turning the switch *off* hid a flag, which is the opposite of what the
switch says. Two entries move for that rule and one for a second reason.

**The chequer, numbered 23, was second of the list.** It hid a yellow thrown at a race finishing
under one, and it hid the blue flag of a car being lapped on the last lap. It is last now, which is
both the canvas's own rank and what the rule asks for.

**The white, numbered 16, sits below the blue at 20** and below the meatball, rather than above
them: the white is news and those two are addressed to this car.

**The meatball, numbered 18, keeps the fifth rank.** That one is not the rule but the same reading
as the list's shape: a flag calling this car in outranks a condition of the track. Besides, the LED
strip draws it on the black family's lamp, its orange aliasing the caution amber, so it could not be
ranked between the two yellows without a second lamp to put it on. It is the one rank the canvas
and the code disagree about, and it wants the author's arbitration.

### What the band does that the box does not, and the reverse

**The band has no duration.** The canvas asks for a configurable three seconds per alert and
`design/tokens.json` states it as `indicator.alert.durationMs`, and neither is read: a duration
needs a clock, and neither NCalc nor the plugin has one under
[ADR 0009](decisions/0009-does-the-plugin-compute.md). Every condition shows for exactly as long as
its bits are set. The green flag is the one place that hurt, because iRacing holds `green` for the
whole green-flag stint and band D would have been a solid green bar over the fuel page for an entire
race; SimHub passes `Flag_Green` through a `GreenLimiter` and reports it only shortly after the flag
is raised, which is the only clock there is, so the band reads that property where it reads bits
everywhere else. The box keeps the bit, a lit green lamp costing nothing.

**The debris flag's danger stripes are not drawn on the band.** The canvas gives the alert
catalogue two patterns, the chequer and the stripes, and the band draws the first. A debris flag is
a yellow band named "DEBRIS", which says it on every face that writes a name and reads as a plain
yellow on the nano, which writes none. The second pattern is a piece of work of its own.

**The band has no critical-flags switch.** Sixty-four pixels are the only thing a driver with a box
has, which is what the switch is for; a driver who wants band D quieter turns the flag format off.

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

In order: the pit family, the three warnings, then the gear. Each layer's condition excludes the
layers above it, and the whole stack sits under "no flag is being shown".

**That ordering is the point.** A low fuel light that hides a limiter warning for the rest of a
stint is the failure this ranking exists to prevent.

The spotter is deliberately not in that list any more. It is an overlay painted after everything
else on the panel, and it is described under [The spotter](#the-spotter) below.

### The pit family

| Condition | Picture |
|---|---|
| Speeding in the lane | A double chevron in danger red, steady. Outranks both limiter states: it is the one costing a penalty right now. |
| Limiter on, out of the lane | An exclamation mark, steady. A mistake costing a second a corner. |
| Limiter on, in the lane | A two-pixel frame, steady. Quiet confirmation; the driver is doing the right thing. |

**Nothing below the flags moves.** Blinking is the flag layer's own vocabulary, where it says that a
yellow is being waved rather than shown, so a pit picture or a warning that blinked would be making
the flags' distinction about something that is not a flag. The three pit states and the three
warnings are consequently steady, and `flagBox.test.ts` holds all nine states below the flags to it.

Speeding compares the car's speed with the lane limit **in metres per second**, which matters more
than it looks: `PitLimiterSpeed` is published through `KmhToLocalSpeedUnit`, so for a driver whose
SimHub speed unit is MPH a 60 km/h limit arrives as 37 — and against `SpeedKmh`, which is always
km/h, that reads as speeding from a standstill. Because speeding heads the exclusion chain, it
would have blacked out the warnings and the gear for every imperial user rather than merely
lighting the wrong picture. `PitLimiterSpeedMs` is metres per second whatever the user has
set, and it is `isnull()`-wrapped with a speed nothing reaches, so a track that publishes no limit
means "not speeding" rather than "always speeding".

Comparing two published numbers is arithmetic over properties rather than state between frames, so
it is not computed telemetry.

The three differ in **shape**, not only in colour, which [#129](https://github.com/xorob0/OpenDash/issues/129)
requires. Two of them nearly did not: `purpose.pitLimiter` resolves to pure white, the same value
as `purpose.flag.white`, so a filled panel for "limiter still on" would have been the white flag
with a blink — telling a driver *last lap* when you mean *your limiter is on*. The token is not
changed to fix that: `design/` is the design source and a colour is decided there, so the shapes
carry the difference. A test now refuses any two identical pictures anywhere in the profile.

### The spotter

A bar two pixels wide down the edge the car is on: left, right, or both edges at once.

**It is an overlay rather than a rank**, and that is a correction rather than a refinement. It used
to be the second layer below the flags, so any flag at all hid a car alongside, and a car alongside
blanked the warnings and the gear beneath it. It is now the last container of the panel, gated on
that panel's own Spotter switch and on nothing else: no flag bit, no pit condition, and nothing
below it excludes it in turn. Its frames light two columns of an edge and leave the rest of the
panel absent, and SimHub drops an absent pixel when it merges rather than clearing what is under
it, so a standing yellow keeps columns three to six while the bar says which side. The consequence
worth stating is that on a rig with one box the gear now shows through the middle of the panel
while a car is alongside, which is the behaviour this is for.

The composition itself has not been watched on hardware. No 8x8 panel is plugged into the test rig,
so "a transparent pixel does not clear what is beneath it" rests on the format notes rather than on
something anybody has seen.

**The bar holds by default and grows only if asked.** `OpenDash.FlagBoxSpotterAnimation` is off
unless the driver turns it on, which is the reverse of the flags' own switch and is the box's one
rule applied: movement means act, and a car alongside informs. On, the bar grows inwards over three
steps of one, two and three columns at the flag band's own rate, the middle step being the held
picture. The bars are white. The canvas draws them purple, `#C86BFF`, which `design/tokens.json`
does not hold; the colour is the author's to add there rather than this file's to type in.

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
| Oil too hot | `OpenDash.FlagBoxMatrix<N>OilTemp`, default 120 °C | An oil can, orange, steady |
| Water too hot | `OpenDash.FlagBoxMatrix<N>WaterTemp`, default 110 °C | Waves, orange, steady |
| Low fuel | `OpenDash.FlagBoxLowFuelLaps`, default 2 | A fuel pump, yellow, steady |

The two temperatures are a panel's own and the fuel threshold is the rig's, which is why only the
first two carry a matrix in their names: one number answers "am I low" for the strip, the rev bar
and the faces as well as for every box, whereas a panel mounted where the driver cannot see it may
reasonably want a different temperature from the one in front of them.

Two of the three pictures are **the telltale ISO 2575 registers** rather than a shape invented for
the grid, because the one a driver already knows from the road car is worth more than the one that
sits more comfortably in eight by eight. The oil lamp was a disc until it was noticed that a disc is
the meatball's own shape in a second orange, which is the confusion costing most of all, the
meatball being an instruction to come in and the oil lamp not; the fuel lamp was a tank outline,
which is a rectangle inside a rectangle and therefore the limiter frame's vocabulary. The water
lamp keeps its waves, which is what the artboard draws.

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

So it is written down here rather than quietly computed. If #98 moves that line, this is the
first thing to build with it.

### The gear, underneath everything

`OpenDash.FlagBoxMatrix<N>Rest`, set to `gear` on the first panel and to `dark` on the other three,
draws the gear filling the panel when nothing else is on it. It is the resting state rather than a
feature: every flag outranks it, and when they let go it comes back. `dark` leaves the panel dark
rather than showing something else.

`OpenDash.FlagBoxMatrix<N>Gear` was a second switch over that same thing, and the panel offered
both. The gear drew where the resting state was `gear` *and* the switch was on, so a panel resting
dark ignored the switch and a panel resting on the gear was decided by the switch alone: one setting
spelled twice, and a pair a driver could not tell apart. The resting state is what survives, since it
names the decision rather than one of its answers, which is the shape `RevBar` settled for
`ShiftLights`. The switch stays attached as its deprecated alias and `flagBoxMatrix().rest()` resolves
through it, because [ADR 0003](../decisions/0003-plugin-settings-through-properties.md) makes a
published property a public interface; the plugin collapses the pair into the resting state on load,
writing the conjunction those two containers used to compute between them.

The glyphs are a 5 by 7 font in source, centred in the panel, not text rendered small — the
bundled Barlow Condensed does not exist at eight pixels, and a thin face leaves one or two pixels
between a 6 and an 8 on a box read in peripheral vision. `R` and `N` have glyphs of their own.

The colour is the shift model: `shiftBands()` in `components/revSegments.ts` is the one place the
three bands are defined, and the rev bar and the gear both read it, so a driver with both learns
one relationship and reads it in two places. Over-rev **blinks the digit** rather than filling the
panel behind it: a filled panel is a flag's vocabulary and the box has to keep those apart.

What it blinks on is not the redline band. The band is *entered* at `Last` and the flash begins at
`max(Blink, Last)`, and it stops in the last gear, where asking for a shift that does not exist is
noise; this file used to say the band did the blinking, and while it said so the digit strobed
against a solid bar and went on strobing in top gear. The threshold is `overRevEither` in
`shift.ts`, carried on the band model as `ShiftBand.blink` — an expression saying *when* rather
than a boolean saying *that* — and `flagBox.test.ts` compares the digit's emitted flash against the
rev bar's top segment's, string for string.

[#281](https://github.com/xorob0/OpenDash/issues/281) had earlier replaced SimHub's per-car bands
with the sim's own `DriverCarSL*` values, and it changed that one function and not this file: a band
is now entered on the car's own ladder where the car publishes one and on SimHub's bands where it
does not ([ADR 0014](../decisions/0014-the-shift-model.md)). The digit, a rev segment and an LED on
a strip therefore change colour on the same frame for the same reason, and flash on the same one.

**Two switches sit over that colour, and they ask different questions.**
`OpenDash.FlagBoxMatrix<N>GearBands` decides whether the digit is banded at all. Off, it stays in
the resting colour at any engine speed, which is what a driver with a rev bar in front of them is
asking for when they ask the panel to stop lighting up: the gear becomes a readout of the gear and
nothing besides. The flash goes with it, since a band that is never entered cannot flash, and so
this is the switch above `GearBlink` rather than a second spelling of it.

`OpenDash.FlagBoxMatrix<N>GearCarLadder` decides where the bands come from, and it is the digit's
half of what the strips have had since [ADR 0018](../decisions/0018-car-light-tables.md): the
measured tables, where the rig has fetched them and the car has a row in them. It is on by default
for the reason the strips' own `car` style is, namely that somebody who has sat in the car and
written down when each light comes on has described it better than four published numbers can.
Moreover, a rig whose strip is banded on one ladder and whose digit is banded on another would be
two answers to one question.

A table is a row of thresholds per gear rather than a ladder of four RPMs, so the bands cannot be
an expression and the plugin computes them: `CarLightMirror.Stage` counts how many of that gear's
LEDs are lit and reports the third of the bar the count falls in, which is the same rule
`stageOf` applies to the rev bar's fifteen segments. It is published as
`OpenDash.CarLadderStage`, with `-1` standing for the absence of an answer, and the digit falls
back through it to the ladder the sim publishes without anybody being told. The over-rev is
published beside it as `OpenDash.CarLadderOverRev`, and it is the table's own redline for the gear
the car is in, which is a threshold of its own here as everywhere.

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

`OpenDash.FlagBoxMatrix<N>CriticalOnly` is off by default: the box shows the whole catalogue until the
driver asks for quiet. A box that stays dark through a chequered flag is a surprise, and a
surprise is a worse default than a busy one.

With it on, the box keeps the flags that mean *slow down* or are *addressed to this car*, and
drops the news: the chequer, the white, the green and the start gantry. A suppressed flag stops
outranking the ones below it, so the box shows the next critical flag down rather than going
dark — a chequered flag over a blue flag shows blue, not nothing.

Both lists are in the file, under the two halves of the switch, so changing it reaches a running
SimHub without a rebuild.

## What is not drawn, and why

Everything a comparable flag box draws that OpenDash does not, and, since band D reads this same
list, everything the canvas's alert catalogue numbers that no surface raises. Each line is a thing
somebody will ask for; the answer is that iRacing does not publish it, not that it was forgotten.

| Wanted | Why not |
|---|---|
| **Engine off, ignition off** (canvas 1 and 2) | They are not `SessionFlags` bits and they are not flags. `EngineWarnings` and the ignition state are published and belong to the telltales and to the pit alerts, where the canvas also puts them; drawing them in the flag rank would put a car state above a red flag. |
| **Double yellow** (canvas 9) | iRacing publishes one yellow and one waved yellow. There is no double yellow in the bitfield, and the canvas's own drawing of it is two stacked bands, which band D has no room for. |
| **Push to pass, headlight flash** (canvas 24 and 25) | `PushToPass` is published and is a car state rather than a flag; the headlight flash is not published at all. Both rank below every flag in the canvas, so neither would ever reach a band that draws fifteen above it. |
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
alternative to writing it four times. Measured on a clean `bun run package` of 0.2.0-rc.1, the
profile is **941,382 bytes (919 KiB) and 697 containers**, which is **12.6%** of the 7,457,280-byte
plugin DLL that embeds it and about **43 KB** once deflated into the release zip.

Those numbers moved when the gear's flash was corrected, and the correction is why. `Gear redline`
used to hold the eleven gear glyphs directly; it now holds two groups, `Gear redline over-rev` and
`Gear redline steady`, and the eleven glyphs are written under each — the two-frame set that blinks
and the one-frame set that does not. That is 46 containers per matrix where there were 22, which
across the four matrices is 96 more: the profile went from 601 containers and about 757 KB to the
697 and 919 KiB above.

**It does cost frames, and an earlier version of this file claimed otherwise.** The claim was that a
group whose condition is false is not descended into. It is: `MatrixContainerBase.GetResult` calls
`GetGroupResult` unconditionally and passes its own truth down as `parentEnabled`, so children are
walked and their conditions evaluated every frame whether the parent is active or not — only the
*painting* is skipped. Six hundred and ninety-seven containers means six hundred and ninety-seven
`IsActive` calls a frame, on all four matrices, including the three nobody switched on.

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
