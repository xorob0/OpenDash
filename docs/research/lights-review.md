# The lights, reviewed against real cars and the field

**Status:** research and proposal, 15 September 2026. It answers the eighteen review notes left on the
design canvas (ten on the LEDs page, seven on the flag box page, one on the 850 × 480 page), records
what the research established, and lists the suggestions that follow, each with what it needs before
it can be built. Nothing in it is built. The drawings that go with it are on the canvas: every frame
chipped *proposed* on the LEDs, Flag box and face pages is an answer to one of the notes and is not in
the build.

Two documents on the branch `tim/dnr-comparison-and-design-pages` cover part of the same ground and
were read first: [dnr-comparison.md](dnr-comparison.md), which reads three DNR dashboards and twelve
DNR profiles against openDash, and [led-strip-redesign.md](led-strip-redesign.md), which answers the
nine LED notes with a slot model. This document agrees with both on the shape of the answer and
departs from the second in four places, which section 4 names. Where it does, the reason is a source
the earlier document did not have.

**What was read.** Twenty-one of iRacing's per-car user manuals, in full, for their dash pages; the
Bosch DDU 8, 9 and 10, MoTeC C127, AiM MX Strada, Cosworth and Stack manuals; FIA Standard 3504 in
both its editions, Appendix H, the 2025 and 2026 F1 technical regulations, the WEC drivers' notes;
ISO 2575, UN R48 and R121, MIL-STD-1472F, FAA AC 25.1322-1, WCAG 2.3.1 and the peripheral-vision
literature; the iRacing SDK header, pyirsdk and the release notes; SimHub's wiki, forum and issue
tracker; the open-source iFlag and its patterns; DNR's twelve profiles and the shared Javascript they
embed, read container by container; the three community profiles for the Porsche 991 GT3 R, a GSI
X-29 and an 8 × 8 matrix; and the product pages of Fanatec, Moza, Simucube, GSI, Cube Controls,
Ascher, Heusinkveld, Rexing, SimRep and Simagic. One caution applies to all of it: the iRacing
manuals document the sim's rendition of each dash, which is the right reference for a profile that
reads iRacing, and a claim about a real car below is a claim about that rendition.

## 1. What each note became

| Note, as written | Where it landed | What the sheet now says |
|---|---|---|
| ABS "should only be one of the leds; also there should be a version for 3x3 and 2x2" | LEDs §1, §5 | One LED per function on the ends, with the lamp assignment drawn for sides of five, four, three and two LEDs. |
| TC "same, should be a single led, like on most race car" | LEDs §5 | The aid lamp, innermost, one LED; it lights on intervention only, never on the dial. |
| Low fuel "should be blinking" | LEDs §5 | The car lamp, amber, 2 Hz. It was written to blink already; section 6 says why it may never have. |
| Water temperature "maybe different colors would be nice, or different leds used" | LEDs §5 | Oil pressure red at 4 Hz, temperature amber at 4 Hz, low fuel amber at 2 Hz, on one lamp; on five-LED sides a second lamp is free. |
| Oil pressure "should be blinking" | LEDs §5 | Red, 4 Hz, the fast tier. |
| Indicators "definetly should be blinking" | LEDs §5 | 2 Hz on the side lamp, in green, best effort: iRacing never fills them. |
| Spotter "should be a different color than the indicator" | LEDs §5, box §2 | A hue of its own, purple, on both surfaces, and a new token to carry it. |
| Blue flag "when i get a blue flag for 1 minutes i do not see my rpm" | LEDs §6, §7 | A flag lives on the race-control lamp and never touches the ladder; it flashes for ten seconds and then holds. |
| "all in all, this led setup is bad, go and search how other alternatives do it, search how it works for real car" | this document | Sections 3 to 7. |
| Ladder: "here it's ok if that's the fallback, but the leds should always be a mirror of what they are on iracing first" | LEDs §4 | The thresholds already are the car's own; the look is the next thing to mirror, from a per-car table, and the shift point becomes the whole bar in one colour. |
| Red "should be animated but stay on the last one" | Box §1 | Fills from the centre in four steps and holds; the format has no play-once, so the hold is a long last frame. |
| Black "i would do a smaller square for the second one" | Box §1 | The full outline then a six-by-six one, which is also what DNR draws. |
| Meatball "first one is not round enough, should be animated but stay on the last one" | Box §1 | Grows from a dot through two rounded steps to a rounder disc, and holds. |
| Blue "remove the arrow, full blue" | Box §1 | Full blue, held. |
| "missing an animated on when i get an x incident point" | Box §2 | An amber X for three seconds on every rise of the incident count, the picture every flag box since iFlag has used. |
| Spotter "animation should be optional here, and other colors should be used by default" | Box §2 | Purple, an overlay a flag never hides, animation off by default and a switch. |
| Gear "the color should be an option in the plugin; also even the showing of the current gear should be an option" | Box §3 | A colour choice with five flat colours beside the shift model; the two existing switches that already make the gear optional are now said on the sheet. |
| 850 × 480 "space could be optimized for readability … if it shows only the delta the delta should take almost the full width" | every face page, §3 | A page that keeps one field gives it the box: the numeral is cut from the width, capped by the height, on all eight sizes. |

## 2. What the research established

**The ends of a real dash are lamps.** The Mercedes-AMG GT3 stack names each of its eight LEDs; the
Bosch DDU 10 carries five per side and builds a pattern by selecting individual LEDs; AiM separates
ten shift LEDs from five to eight alarm LEDs, one alarm per LED; Cosworth carries ten and six; DNR
toned its ABS and TC down to one LED per side in February 2024. Where a real car does spend several LEDs on one thing, the extra LEDs carry a
magnitude (the BMW M4 GT3's five-lamp TC severity, lockup by axle and count on the GTP cars) or a
side; the Porsche 992 is the one exception, lighting its whole side cluster blue for traction control,
and even there nothing else is hidden underneath.

**Nothing takes the shift row for a flag.** Of twenty-one manuals, one (the IR18) puts a flag on its
LEDs, and it uses the side cluster. The F1 technical regulations mandate three cockpit lights, red,
blue and yellow, beside the rev LEDs; the WEC in-car marshalling display is a separate device; every
wheel on the sim market puts flags on the side LEDs. DNR is the one product that paints marshal flags
on side LEDs only and never on the ladder, and the two open community profiles set no
`ClearBackgroundWhenActive` on any flag container. What does take the whole shift cluster, in nearly
every car, is the pit limiter, in a colour the ramp does not use, usually flashing.

**The shift point is the whole bar in one colour.** No sampled car changes only its top third. The
majority turns every LED blue (Porsche 992 and 963, Cadillac, IR18, McLaren, Mustang, Lamborghini,
W12); the rest go all red (BMW, Mercedes, Ferrari, Acura, GR86). About half of them flash that row
and the rest hold it, so flashing at the blink RPM is openDash's own choice, justified by the sim
publishing a blink RPM for every car. Fill direction, band
widths and colours differ per car and match no three-equal-band ladder, which is why DNR and Lovely
carry per-car tables and why the sim publishes thresholds only, never a colour.

**Two blink rates, and blink means urgency.** MIL-STD-1472F allows at most two rates on a display,
at least 2 Hz apart, the faster at 3 to 5 Hz for a warning and the slower at 2 Hz or below for a
caution; drivers tell 1 Hz from 4 Hz and read the faster as more urgent. The current edition of FIA
Standard 3504 (version 2.0, March 2020) recommends 2 Hz for nearly every flag panel, the blue, the
white, the green, the red and the chequer included, 2 to 3 Hz for the black family, and static only
for slippery surface and the pit-entry boards; the 2019 edition recommended 2 Hz for the yellows,
4 Hz for the black family and nothing for the rest. A full 8 × 8 panel or a whole strip flashing
above 3 Hz at arm's length exceeds the area WCAG exempts from the photosensitivity threshold.

**The colours are the cars'.** Traction-control intervention is blue on every modern GT3 and GTP in
the sample (Porsche 992 and 963, Cadillac, Corvette, McLaren) and in DNR; lockup and the ABS
tell-tale are amber or magenta, and UN R121 makes the road-car ABS tell-tale yellow. ISO 2575 makes
red immediate danger, amber a caution, green a system working and white the colour with no
implication. No side cluster in any manual uses purple, which is why DNR's spotter is pink and
iFlag's is purple. A lone LED in peripheral vision loses hue, red against orange fastest, so a
condition is told from its neighbour by position and rhythm before colour.

**The flag box market has one ancestor.** The open-source iFlag draws black as an outline, DQ as a
white X, the meatball as a disc pulsing between two sizes, blue as iRacing's own icon, the spotter
as a purple bracket overlaid on whatever is underneath, and an X for three seconds on every rise of
the incident count. DNR moved to a red X for incidents in 2025 and pulses its black flag between an
eight-by-eight and a six-by-six outline. SimHub's matrix animation is frames with durations and
nothing else: no play-once, no hold, in the files, the wiki, the forum or the issue tracker. No 2 × 2
or 3 × 3 matrix product exists; the three-lamp cluster (Fanatec's FlagLEDs, F1's cockpit trio, the
IR18's caution cluster) is the real-world shape of that idea.

**What iRacing publishes, corrected.** `EngineWarnings` has carried an oil-temperature bit (0x0040)
since 2021 season 2, and pyirsdk lists mandatory and optional repair bits (0x0080, 0x0100), which the
repository's research notes and `NO_PROPERTY` deny; `SessionFlags` carries `dq_scoring_invalid`
(0x200000) beside `disqualify`; `CarLeftRight` is an int with seven values that SimHub exposes raw;
since 2024 season 1 the `DriverCarSL*` values are logged live for cars whose ladder moves with the
gear, so the mirror is per gear on those cars for nothing; and the Ligier JS P320 times its lights on
RPM acceleration, which no mirror can reproduce. `dcPitSpeedLimiterToggle` and `dcHeadlightFlash`
are momentary buttons, not states.

## 3. The strip

The design is on the LEDs page and is not repeated here; what follows is the reasoning behind the
parts that changed, and where the drawing departs from the earlier redesign document.

The ends become **lamps**: an ordered set of LEDs from the outside in, each with one owner role, and
within a role a rank. Outermost is the side lamp (the spotter, with the indicator beneath it), then
race control (a flag and nothing else), then the car lamp (oil pressure, then temperature, then
fuel), then the aid lamp (ABS, traction control intervening, DRS, push to pass), and on a five-LED
side a second aid lamp. Three LEDs share the car and aid roles by rank; two LEDs drop the aids and put
the flag beneath the car warning, because a driver feels ABS and hears traction control, whereas
nothing else on the rig reports oil pressure, and the flag has the box and the band. The centre
stays the ladder under all of it, and only the pit family may take the whole strip.

**Where this departs from led-strip-redesign.md.** First, the colours of the aid lamp: that document
kept ABS blue and traction control amber; the manuals and DNR have it the other way round, and UN R121
makes ABS yellow, so the sheet draws TC blue and ABS amber. Second, the TC lamp lights on intervention
only, never on the dial being above zero; a lamp that is on for the whole race says nothing, and on
iRacing, which publishes no intervention, it stays dark, which is honest. Third, the flag rates: that
document read the 2019 edition of FIA 3504 and held blue, white and green steady on its authority;
the 2020 edition flashes them at 2 Hz. The sheet reconciles the standard with the note that started
this: a flag flashes at 2 Hz for the first ten seconds and then holds, and the driver can shorten or
lengthen that with one setting. Fourth, the spotter: that document proposed a new hue and left the
box white; the sheet uses one purple token on both surfaces and makes the box's spotter an overlay
painted after the flags, as iFlag does, so that a car alongside is never hidden by a yellow.

**The shift flash.** The whole bar, one colour, 4 Hz, in every style, with blue as the default
because it is the majority convention and the one colour the ramp has not used; a car whose manual
says red gets red from the table. This also moves `shiftLights.flashHz` from 8 to 4, which is a
token on the canvas, and the rev bar on the face follows it.

**Movement decays.** `changed(10000, …)` is SimHub's own window function, used for exactly this by
the community and declared in the generator's function list; it is state, but SimHub's state, the
same distinction that already admits `Fuel_RemainingLaps`. Using it touches ADR 0009 and wants a
line there before the code.

**Dropped.** ERS charge, which cannot light on iRacing and is a percentage no lamp can carry; the
headlight flash, which reports the driver's own button; the both-sides spotter blink; the brake
gradient on the ends, because a group filled red by the pedal is a group on which an oil warning
cannot come on. The indicators stay, dark on iRacing.

**Brows.** A brow is the device people buy for the spotter and it is the one device that cannot show
one. The proposal derives two lamps at each end of a bare run of twelve or more, side lamp and race
control, and keeps the rest as the ladder. It reverses a recorded decision and needs a decision
record first.

## 4. The flag box

Red, the meatball and the black flag answer the notes directly and the drawings are on the sheet.
The format point matters more than the pictures: SimHub loops an animation and has no play-once, so
"animate then hold" is a long last frame (twenty seconds on the sheet; longer if the driver honours
it), after which the sequence replays, which on a race lap is the same thing. Whether a frame of
several minutes is honoured, and whether a container that becomes active again restarts at frame
zero, are the two things to watch on the VM before the frames are cut.

The incident notice is the X the field uses, in amber, for three seconds, through the same
`changed()` as the strip's flags. The spotter is purple, an overlay, and its growing bar is a switch
that is off by default. The gear's colour is a choice, `shift` or one of five flat colours; that is
Tim's ask, and it sits on the line ADR 0011 draws around personalisation, so the conservative
alternative, a two-value mode of `shift` and `plain`, is recorded here should the record be the
thing that decides.

Everything on the box stays at 2 Hz, which the current FIA edition also recommends; the one change is
the over-revving gear, which should come down from 8 Hz to 4 Hz with the rev bar it mirrors.

## 5. The face

The one-field rule on every size: a page that keeps one field gives that field the box, the numeral
cut from the width the way a drawing is cut from its box (rule 18), capped by the height, and no
longer taken from the density ramp, which is what held the delta at 64 px in every zone from 249 wide
to 737. The eighteen multi-field pages are the readability pass already written, and the sheets say
so rather than redrawing them.

## 6. Defects found on the way

These are not design questions and should be looked at before any of the above is built.

1. **The blink has never been visible, and this is confirmed.** SimHub's `CustomStatus` and
   `StaticColor` containers both derive from `StaticColorContainerBase`, whose result fills the run
   with `BlinkingColor` while blinking and `Color` otherwise, with `BlinkingColor` defaulting to
   transparent; the assembly the plugin builds against, `plugin/lib/SimHub.Plugins.dll`, decompiles
   locally and says so. Every openDash effect that blinks writes the same colour to both fields
   (traction control, DRS, push to pass, low fuel, the indicators, the limiter, speeding, the yellow
   flag), and `rungs()` writes the top band's own red as its blinking colour, so the over-rev flash
   of the ladder has never flashed either. The notes asking for blinking were observing this. The fix
   is two lines: an opaque black rather than the colour as the default blinking colour in
   `effectContainer`, and the same for the flashing rungs; transparent will not do, because the merge
   drops transparent pixels and the ladder underneath would show through the off phase.
2. **The Fanatec 3/9/3 is wired the other way.** Through Fanalab a Fanatec wheel presents its LEDs as
   the nine rev LEDs first, then the right flag LEDs from the outside in, then the left; DNR's profile
   carries a `RemapGroup` for exactly that order. openDash's 3/9/3 assumes left, centre, right, so on
   that device it lights the wrong physical LEDs.
3. **The strips ignore brightness and night mode**, and nothing gates on the ignition; both are the
   box's existing answers applied to the other artefact.
4. **Pit speeding is computed two ways**, and the box's way reads `PitLimiterSpeedMs`, which the
   repository's own research says carries `[DoNotExpose]`; if that is right the box's speeding never
   fires. One pit model for both surfaces, and a grep of the decompiled `GameManagerBase` for the
   attribute.
5. **Low fuel has three definitions**: the strip reads SimHub's own alert, the box reads laps against
   the panel setting, and the fuel centre bar blinks at five percent. One definition, in laps.
6. **Five near-white whole-strip states**: black, chequered, white, pit lane and the limiter are all
   white fills, told apart by nothing but a blink. The box refuses this with a test; the strip has no
   such test.
7. **The 3/10/3's extra runs** repeat the centre and never carry a lamp; DNR folds them into the sides.
8. **The strip reads six lossy flags** and so cannot show red, disqualification, the furled black,
   the meatball or debris, while the box ranks all fifteen; the two can disagree about which flag is
   out. The strip should rank the catalogue as the box does, with one care the box does not need:
   the catalogue reads the flag bits bare, which a matrix group tolerates, whereas a strip's status
   container answers a throwing formula with *on*, so on the strip every bit wants its `isnull()`.

## 7. The suggestions, ranked

Each line says what it needs: nothing, a design token on the canvas, a decision record, or a check on
the VM. The verification column comes from a second pass in which two independent reviewers tried to
refute each suggestion, one on its evidence and one on whether the data and the format can carry it.
That pass covered the first eight of the thirty synthesised suggestions before the session's usage
limit stopped it; the rows it reached say *held* and carry its corrections, the rest say *not run*.

| # | Suggestion | Needs | Verification |
|---|---|---|---|
| 1 | Give every blink an opaque black blinking colour, on the effects and on the flashing rungs (defect 1). | nothing | held, and the assembly decompiles locally, so no VM is needed |
| 2 | Wrap the strip trees in a brightness group and gate them on the ignition (defect 3). | nothing | not run |
| 3 | A `RemapGroup` for the Fanatec order on the 3/9/3 (defect 2). | a Fanatec owner to confirm | not run; read from DNR's own remap |
| 4 | The strip ranks `FLAG_CATALOGUE` through `SessionFlagsDetails`, as the box does (defect 8). | nothing | held; every bit wants `isnull()` on a strip, and a shared critical-only setting must not silently replace the box's |
| 5 | The slot model: one LED per function, the lamp table per side count, a test that no two conditions on one lamp share hue and rate. | nothing | held; the DNR change is dated 28 February 2024, and the MoTeC C125 has no side LEDs |
| 6 | Flags on the race-control lamp, never on the ladder; movement decays after `LedFlagHold` seconds through `changed()`. | ADR 0009 line | held; the F1 regulation mandates the three lamps and is silent on the rev LEDs, so it is a precedent rather than a prohibition |
| 7 | Two rates, 4 Hz and 2 Hz; `shiftLights.flashHz` from 8 to 4. | token | held; the indicators already run at 2 Hz, the upper edge of UN R48's band |
| 8 | Colours: TC blue, ABS amber, low fuel amber, P2P blue then green, spotter purple; `purpose.spotter` and `purpose.shift.blink` as new tokens, `purpose.alert.p2p` retargeted and `purpose.fuel.low` moved from red to amber. | tokens | held on the car manuals; on iRacing the TC lamp is dark whatever its colour |
| 9 | The shift point as the whole bar in one colour, blue by default. | token | held; the blinking colour must be opaque black, not transparent |
| 10 | A per-car appearance table (fill, steps, shift colour, limiter pattern) beside the shift-points table, from the manuals; Lovely's open car data is the machine-readable alternative and its licence is unchecked. | data | not run |
| 11 | A generic fallback ladder that spaces its rungs by the car's own spans rather than fixed thirds. | nothing | not run |
| 12 | One pit model for strip and box, with the centre as a speed-against-the-limit gauge under the limiter (defect 4). | VM for the property | held; the Porsche 963 grades pit speed in red and green rather than blue |
| 13 | One low-fuel definition in laps, `LightsLowFuelLaps` with the box's setting as fallback (defect 5). | nothing | not run |
| 14 | Read the missing `EngineWarnings` bits (fuel pressure, oil temperature, repair) and gate oil pressure on the engine running; correct the research notes. | nothing | not run |
| 15 | Derived ends on brows and bare runs of twelve or more; the 3/10/3's extra runs as sides (defect 7). | decision record | not run |
| 16 | The box: red and meatball grow then hold, black pulses between two outlines, blue full, incident X, spotter purple overlay with optional animation, gear colour choice. | tokens for purple | not run |
| 17 | Per-function switches for the strip (`LightsFlags`, `LightsSpotter`, `LightsAssists`, `LightsWarnings`), and a shared `LightsCriticalOnly`. | nothing | not run |
| 18 | A night floor for the critical set (red, DQ, pit speeding, oil pressure) above the general night level. | nothing | not run |
| 19 | A 3 × 3 or 2 × 2 status cluster as a strip profile with a fixed legend (the F1 trio on top, spotter and pit in the middle, aids below), if a device for it ever exists. | a device | not run |
| 20 | The one-field readability rule on the faces. | nothing | not run |

## 8. What is not decided

Whether openDash takes a sixth hue for the spotter; whether the gear colour is five colours or a
two-value mode under ADR 0011; whether the brows derive ends; whether `changed()` is admitted under
ADR 0009; whether the per-car appearance table is transcribed from the manuals or read from Lovely's
data; and what a frame of several minutes does on a real matrix. Each is named in the table above
with what decides it.

## 9. The second pass: twenty-nine further notes

Version 19 of the canvas drew a second round of notes the same evening, most of them on the face
pages rather than the lights. They are answered on version 20, and this section says where each one
went, since the canvas annotation points here.

**The faces.** Twenty of the notes say one thing in twenty ways: the per-size sheets drew each page
as a crude list of fields, and the drawings Tim likes are on the Zones artboard of the System page.
The sheets now embed the catalogue's own drawing for each page, reflowed to the zone's real
rectangle, and grown into it as one block until it meets the width, the height or a ceiling of
2.2, which is rule 20 applied to a drawing; the factor is on each card's chip, and a page whose
drawing does not fit its box at all says so rather than shrinking. That answers the delta's missing
bar, the sectors that did not look like sectors, the car model nobody needs, the opponents page that
was unusable, and the fuel page that lacked its stint time and consumption, because the catalogue's
drawings carry all of those. Zone A now fills its column: the gear is cut from the height, and the
speed page leads with the speed. What is still small after growth is small on the catalogue too
(the lap times' lead value, the lists' row height) and is the readability pass's to redraw. The
flag gains a second format, full screen over zones B, A and C, as a per-screen setting beside the
band. The 1920 × 480 note that the larger screens should carry more information rather than larger
text is what the catalogue's `wide` drawings do, and embedding them answers it.

**The System page.** The components artboard's anatomy said slots 1 to 6 and 7 to 12; it now says
zones B, A and C, with the caption rewritten for the zone model. The licence badge gains an
iRacing-coloured variant beside the grey ramp, drawn with the palette's own tokens: it reverses
the 11 September decision that the badge is achromatic, so it is proposed rather than taken, and the
relative and leaderboard cells would follow it.

**The plugin.** A light is now an instance, the way a screen is: the Lights tab lists the rig's
lights (a flag box, a wheel strip, a brow, and an Add a light button), each with its own settings
under a per-light name such as `OpenDash.Light1.Centre`, so two wheels differ; the strip's lamp
switches mirror the box's four families. Install lists the profiles beside the packages, one per
shape, with Install and Update buttons. Data's four settings fold into Screens as a "for every
screen" group, which is offered as the answer to the question of whether the tab stays.

**The flag box.** The chequered flag gains its second frame, the board's inverse, at 2 Hz; and the
two-box variant is drawn, one box mounted left answering only for the left and one mounted right
for the right, which the Mounted setting already provides.

Where the earlier decision and the note disagree (the licence colours; the flags taking the whole
face), the sheet draws both and the record says which is taken; nothing on version 20 is built.
