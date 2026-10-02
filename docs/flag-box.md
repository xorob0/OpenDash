# The flag box

An 8x8 LED matrix beside your screen, showing the flag that is out, the gear, the pit state, a car
alongside, and the warnings you would otherwise miss. This page takes you from a matrix in an
anti-static bag to a box showing flags.

[design/flag-box.md](design/flag-box.md) is what every picture means and why.
[ADR 0013](decisions/0013-lighting-hardware.md) is why OpenDash ships one at all.

## What you need

An **8x8 WS2812b matrix** on an Arduino — 64 addressable LEDs on one data pin, wired to **D6**.
These are sold ready made by several small makers and printed by plenty of people from the free
models. SimHub drives them natively; OpenDash only supplies what they show.

You also need SimHub itself and OpenDash's plugin, which is where the profile comes from: no
release publishes the file on its own. Every setting the profile reads has a default built in, so it
draws before anything on the panel has been changed.

## 1. Add the device in SimHub, and set its position first

**Do this before anything else.** It is the step everybody gets wrong, and the symptom is that the
profile looks broken.

In SimHub, open **Arduino / RGB LEDs** and add your matrix. Then set, on the device:

| Setting | What goes wrong if it is not right |
|---|---|
| **Rotation** | Everything is sideways or upside down. The gear is the giveaway: a `3` that reads as an `E` is a rotation, not a bug. |
| **Serpentine** | Alternate rows run backwards. A chequered flag comes out as diagonal stripes and the gear looks shredded. |

Which values are right depends on **the corner your data cable enters**, which is a fact about
your box and not about the profile. That is why OpenDash does not have rotation or serpentine on
its own settings page: two places to set them would be two places to disagree, and SimHub's are the
ones the hardware actually obeys.

Get a solid colour showing on the panel through SimHub's own test before going further. If that is
wrong, nothing below will be right.

## 2. Install the profile

Open SimHub's left menu, find **OpenDash** and open its **Matrix** page. The line beside the title
names the profile, `OpenDash Flag box`, with the version SimHub holds or *Not installed*. Press
**Install**.

That adds OpenDash's profile to SimHub's matrix profiles. Then pick it on your matrix device, the
same way you would pick any profile. It never touches a profile you made yourself: OpenDash only
recognises its own, by the id it stamps into it.

The press says what it will do before you press it — *Install*, *Update*, or *Reinstall* — and the
dot and the line beside it say what SimHub holds now. **OpenDash never installs it on its own.** A
profile paints hardware you own, and that is a thing to be asked about rather than assumed; the
reasoning is in [ADR 0013](decisions/0013-lighting-hardware.md).

When OpenDash carries a newer profile, the press reads **Update**, and its hover says what it costs:
*Replaces the copy in SimHub, including your changes to it.* OpenDash cannot tell an edited copy from
an untouched one, so if you have customised it in SimHub's LED editor, copy it under a new name
first. While your rig has a matrix, the profile is also a row of the **In SimHub** table on the
**Updates** page.

### If there is no press

The line offers no press when SimHub's matrix settings could not be reached — an older SimHub, or
the serial dash plugin not loaded. OpenDash writes the profile to a file instead:

```
SimHub\OpenDash\OpenDash Flag box.ledsprofile
```

The path is shown under the line, with **Copy to SimHub's import folder** beside it. Import that
file through SimHub's own profile import on your matrix device, and everything below works the same
way. That file is also what you copy to a second machine, and what to open if you want to read what
OpenDash is asking your hardware to do.

## 3. Say which box is which

SimHub composes up to **four matrix contents**, so you can run more than one box. On the **Matrix**
page, press **Add a matrix** for each box you own. The first is matrix 1, the next matrix 2, and so
on; the sheet says the steps left, which end in setting that number as the **RGB Matrix content**
on the device itself, which is where SimHub decides which content a box shows. A matrix arrives
doing everything, which is the right answer for one box.

**The name you give a matrix is OpenDash's own.** It labels that matrix's card and settings here and
is not shown in SimHub's matrix profile list. There is one profile — `OpenDash Flag box` — and it
paints all four contents, so adding a matrix puts nothing new in that list: it gained its one row in
step 2 and gains nothing after.

Select a matrix's card to set it. Its settings are a **Priority** list: what may take the matrix
over, first first, each with its switch, and under it the **Idle display**, what the matrix shows
when nothing has. The order is fixed for now; #505 makes it yours to drag. A live preview beside the
list draws the selected matrix, and its **Preview** chips play a flag, the spotter, the pit lane, a
warning or the revs on it.

| Per matrix | |
|---|---|
| **1 Flags** | Let the flag catalogue take this matrix. |
| **Critical flags only** | Quiet until something matters. Drops the chequer, the white, the green and the start gantry; keeps everything that means slow down or is addressed to you. |
| **2 Pit lane** | Let the limiter, the lane and speeding take this matrix. |
| **3 Spotter** | Let a car alongside take this matrix. |
| **Mounting side** | `Left`, `Right` or `Both sides`. |
| **4 Warnings** | Let low fuel, oil temperature and water temperature take this matrix. **Triggers** beside it opens the Alerts on the Settings page, where the three are set for the whole rig. |
| **Idle display** | `Gear` or `Dark`: what this matrix shows when nothing has taken it over. |
| **Shift colours** | With the gear shown: on, the gear changes colour as the revs rise, the same three bands the rev bar climbs. Off, it stays one colour at any engine speed, which is what to reach for if you have a rev bar in front of you and want the box to say the gear and nothing else. |
| **Car-specific shift points** | With Shift colours on: the colours change where this car's own shift lights do, from **Car Data**, which you download on the **LEDs** page under **Every strip**. A car with no table, or a rig that has never fetched them, falls back to the ladder the sim publishes. |
| **Redline flash** | With Shift colours on: the digit flashes while you are over-revving. Off leaves it steady and red, so nothing is lost but the strobe. |

**Mounting side is the one to get right.** It is where the box physically is, not what you want it to
show. A box on the left of your wheel that lights for a car on your *right* is worse than no box at
all. With one box, leave it on `Both sides` and it lights the correct edge of the single matrix.

A two-box setup people build on day one: one in each corner of the monitor stand, matrix 1 on
`Flags` and `Mounting side: Left`, matrix 2 on `Spotter` with `Idle display: Gear` and
`Mounting side: Right`.

## 4. What every matrix shares

| | |
|---|---|
| **Spotter bar animation** | Under Spotter on the Matrix page, and the same for every matrix. Off by default. On, the bar grows inwards from the edge instead of simply being there. |
| **Brightness** / **Night brightness** / **Night mode** | Under **Lighting** on the **Settings** page. These are for the whole rig, not just this box. Sixty-four LEDs at full output beside a wheel in a dark room is genuinely too bright; night mode is a switch you flip, not a time of day OpenDash guesses at. The **Shortcuts** page binds buttons to night mode and to brightness up and down. |
| **Low fuel** | Under **Alerts** on the **Settings** page. Laps left in the tank, not litres — litres mean nothing without knowing the car. One number for the whole rig: the box, the screens and the strips all use it. |
| **Oil temperature** / **Water temperature** | Under **Alerts** on the **Settings** page, in **SimHub's unit**. Leave them empty and OpenDash uses the right default for whichever unit SimHub is set to: 120 °C or 248 °F for oil, 110 °C or 230 °F for water. |

**Critical flags only**, the gear and the two temperatures once had one value for every matrix.
Critical flags only and the gear belong to a matrix and are in its own settings above; a setting you
had already chosen was carried into all four matrices the first time a later version read your
settings.

**Show the gear** is gone as well, since **Idle display** was asking the same question: its `Dark`
is what that switch called off. A matrix whose switch you had turned off comes back set to `Dark`,
so the box goes on showing what it showed before.

## What it shows, and in what order

One picture at a time. Everything higher in this table takes the matrix from everything lower, so
you can learn the box away from the car.

| | | Critical |
|---|---|---|
| 1 | Red — the whole box red | ● |
| 2 | Disqualified — a cross, blinking | ● |
| 3 | Black — an outline | ● |
| 4 | Black — a bar | ● |
| 5 | Meatball — an orange disc | ● |
| 6 | Chequered — a checkerboard | |
| 7 | Full course yellow — SC in black on yellow, blinking | ● |
| 8 | Yellow — blinking | ● |
| 9 | Yellow — solid, steady | ● |
| 10 | Debris — yellow with danger stripes | ● |
| 11 | Blue — an arrow that moves | ● |
| 12 | White — the last lap | |
| 13 | Green | |
| 14 | Set — two bars of the start gantry | |
| 15 | Ready — one bar | |
| | **Pit**: speeding, then limiter out of the lane, then limiter in the lane | |
| | **Warnings**: oil, then water, then low fuel | |
| | **The gear**, coloured by the shift lights, when nothing else is out | |

**The spotter is not in that table**, and that is deliberate. A bar down the edge a car is on is
drawn *over* whatever else the matrix is showing rather than instead of it, so a car alongside is
never hidden by a yellow and never hides the gear. With one box you will see the gear in the middle
of the matrix with a bar down one edge, which is two facts at once and is what you want at that
moment.

A black flag is an **outline** because black is the absence of light: a black panel is a box that
is off. A yellow being waved is the yellow flag **blinking**, which is how you tell it from a
standing one without a second colour or a second name. A full course yellow blinks as well, with
**SC** cut out of the yellow, so the letters are how you tell it from a yellow being waved. The pit
limiter is a **frame** in the lane and an **exclamation mark** out of it, so you never have to judge
by colour alone.

## What it does not do

**iRacing only.** Other sims may work and are welcome to; nothing here has been driven in one, and
flags are more tempting to over-claim than a dashboard because they look universal. They are not.

**These are not drawn, because iRacing does not publish them.** Each is a thing somebody will ask
for; the honest answer is that the data is not there, not that it was forgotten.

| | |
|---|---|
| Yellow per sector | Not in iRacing's flag data at all. Eight pixels across could not say *which* sector anyway. |
| Virtual safety car | iRacing has no VSC. The full course yellow is drawn and is a different thing. |
| White for a slow car | iRacing's white flag is the last lap and nothing else. |
| Incident, penalty, drive through, stop and go | None is a flag in the data. iRacing says them with the black flag and with text; the box shows the black flag. |
| A countdown to your pit box | The most loved thing on any flag box, and iRacing publishes no distance to your own stall. Working one out from track position is a calculation OpenDash refuses to do until a decision record says otherwise. |
| Ten to go, five to go, one lap to green | Published, but session information rather than flags. The screen has the room to say them in words; drawing a numeral here would fight the gear. |
| Two cars on one side | iRacing distinguishes it; SimHub folds it away before OpenDash sees it. Three spotter states ship rather than a fourth faked. |

**No acknowledgement of a warning.** A low fuel light stays until you have fuel. That is
deliberate: it sits *below* the flags and cannot hide one, which is the version of this feature
that matters.

**No themes, no presets, no custom idle picture.** OpenDash ships one opinionated look; the
reasoning is in [scope.md](scope.md).

## If it looks wrong

| | |
|---|---|
| Nothing at all, ever | The profile is not installed, or not selected on the device. Step 2. |
| SimHub's matrix list has no profile named after my matrix | There never is one. `OpenDash Flag box` paints all four contents, and a matrix's name is OpenDash's own label. Step 3. |
| A single dim dot in the middle | The box is working and the car's ignition is off. That mark exists so this is not confused with a broken profile. |
| Everything sideways, mirrored or shredded | Rotation or serpentine on the *device*. Step 1, not the OpenDash panel. |
| The gear is dark but flags work | That matrix's **Idle display** is `Dark`. |
| A car alongside lights the wrong box | **Mounting side** is set to the side you want rather than the side the box is on. |
| The chequered flag never shows | That matrix's **Critical flags only** is on. It is not a critical flag. |
| Far too bright at night | **Night brightness**, and turn **Night mode** on. |

## Honesty about what has been checked

No 8x8 panel is plugged into OpenDash's test machine, and CI owns no hardware.
[scope.md](scope.md)'s definition of done states that exception rather than leaving it implied.
Every picture on this page is checked by tests and rendered into `build/flag-box.svg`, and the
whole catalogue can be driven in the emulator — but **the profile has not yet been watched running
on a real matrix.** If you own one, saying what it actually does is the most useful thing you
could contribute.
