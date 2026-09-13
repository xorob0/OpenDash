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

## Seen, or not

No 8x8 panel is plugged into the test VM and CI owns no hardware, which
[scope.md](../scope.md)'s definition of done states as an exception rather than leaving implied.
What can be checked is that the profile loads in real SimHub and that the glyphs are right in
SimHub's own matrix preview. Everything above is drawn from the bits SimHub publishes, read out
of the decompiled reader; **that every one of them fires when the sim raises it has not been
watched happen.**
