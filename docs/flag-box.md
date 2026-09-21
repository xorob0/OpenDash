# The flag box

An 8x8 LED matrix beside your screen, showing the flag that is out, the gear, the pit state, a car
alongside, and the warnings you would otherwise miss. This page takes you from a matrix in an
anti-static bag to a box showing flags.

[design/flag-box.md](design/flag-box.md) is what every picture means and why.
[ADR 0013](decisions/0013-lighting-hardware.md) is why openDash ships one at all.

## What you need

An **8x8 WS2812b matrix** on an Arduino — 64 addressable LEDs on one data pin, wired to **D6**.
These are sold ready made by several small makers and printed by plenty of people from the free
models. SimHub drives them natively; openDash only supplies what they show.

You also need SimHub itself, and openDash's plugin if you want to change any of the settings from
the panel. The profile works without the plugin: every setting it reads has a default built in.

## 1. Add the device in SimHub, and set its position first

**Do this before anything else.** It is the step everybody gets wrong, and the symptom is that the
profile looks broken.

In SimHub, open **Arduino / RGB LEDs** and add your matrix. Then set, on the device:

| Setting | What goes wrong if it is not right |
|---|---|
| **Rotation** | Everything is sideways or upside down. The gear is the giveaway: a `3` that reads as an `E` is a rotation, not a bug. |
| **Serpentine** | Alternate rows run backwards. A chequered flag comes out as diagonal stripes and the gear looks shredded. |

Which values are right depends on **the corner your data cable enters**, which is a fact about
your box and not about the profile. That is why openDash does not have rotation or serpentine on
its own settings page: two places to set them would be two places to disagree, and SimHub's are the
ones the hardware actually obeys.

Get a solid colour showing on the panel through SimHub's own test before going further. If that is
wrong, nothing below will be right.

## 2. Install the profile

Open SimHub's left menu, find **OpenDash**, scroll to **Lights**, and press **Install into SimHub**.

That adds openDash's profile to SimHub's matrix profiles. Then pick it on your matrix device, the
same way you would pick any profile. It never touches a profile you made yourself: openDash only
recognises its own, by the id it stamps into it.

The button says what it will do before you press it — *Install*, *Update*, or *Reinstall* — and the
line beside it says what SimHub holds now. **openDash never installs it on its own.** A profile
paints hardware you own, and that is a thing to be asked about rather than assumed; the reasoning is
in [ADR 0013](decisions/0013-lighting-hardware.md).

When openDash updates, the button offers **Update in SimHub**. Updating replaces the copy in SimHub,
**including any changes you made to it there** — openDash cannot tell an edited copy from an
untouched one, so if you have customised it in SimHub's LED editor, copy it under a new name first.

### If the button is greyed out

It says why beside it. The usual cause is that SimHub's matrix settings could not be reached — an
older SimHub, or the serial dash plugin not loaded. openDash also writes the profile to a file:

```
SimHub\OpenDash\openDash Flag box.ledsprofile
```

The path is shown under the button. Import that file through SimHub's own profile import on your
matrix device, and everything below works the same way. That file is also what you copy to a second
machine, and what to open if you want to read what openDash is asking your hardware to do.

## 3. Say which box is which

SimHub composes up to **four matrix contents**, so you can run more than one box. openDash gives
each one its own settings on the **Lights** page.

Out of the box, matrix 1 does everything and 2 to 4 are off, which is the right answer for one box.

| Per matrix | |
|---|---|
| **At rest** | `Gear` or `Dark`: what this panel shows when nothing has taken it over. |
| **Flags** | Let the flag catalogue take this panel. |
| **Pit** | Let the limiter, the lane and speeding take this panel. |
| **Spotter** | Let a car alongside take this panel. |
| **Warnings** | Let low fuel, oil and water take this panel. |
| **Mounted** | `Both`, `Left` or `Right`. |
| **Critical flags only** | Quiet until something matters. Drops the chequer, the white, the green and the start gantry; keeps everything that means slow down or is addressed to you. |
| **Oil / Water temperature** | In **your own unit**. Leave them at 0 and openDash uses the right default for whichever unit SimHub is set to: 120 °C or 248 °F for oil, 110 °C or 230 °F for water. |

**Mounted is the one to get right.** It is where the box physically is, not what you want it to
show. A box on the left of your wheel that lights for a car on your *right* is worse than no box at
all. With one box, leave it on `Both` and it lights the correct edge of the single panel.

A two-box setup people build on day one: one in each corner of the monitor stand, matrix 1 on
`Flags` and `Mounted: Left`, matrix 2 on `Spotter` with `At rest: Gear` and `Mounted: Right`.

## 4. The rest of the Lights page

| | |
|---|---|
| **Brightness** / **Night brightness** / **Night mode** | These are for the whole rig, not just this box. Sixty-four LEDs at full output beside a wheel in a dark room is genuinely too bright; night mode is a switch you flip, not a time of day openDash guesses at. |
| **Low fuel, laps** | Laps left in the tank, not litres — litres mean nothing without knowing the car. One number for the whole rig: the box, the screens' fuel telltale and the pop-up all use it. |
| **Spotter bar grows** | Off by default. On, the bar grows inwards from the edge instead of simply being there. |

**Critical flags only**, the gear and the two temperatures used to live here, one value for
every panel. They belong to a panel and are now in each matrix's own group above; a setting you had
already chosen is carried into all four panels the first time this version reads your settings.

**Show the gear** is gone from the matrix group as well, since **At rest** was asking the same
question: its `Dark` is what that switch called off. A panel whose switch you had turned off comes
back set to `Dark`, so the box goes on showing what it showed before.

## What it shows, and in what order

One picture at a time. Everything higher in this table takes the panel from everything lower, so
you can learn the box away from the car.

| | | Critical |
|---|---|---|
| 1 | Red — the whole box red | ● |
| 2 | Disqualified — a cross, blinking | ● |
| 3 | Black — an outline | ● |
| 4 | Black furled — a bar | ● |
| 5 | Meatball — an orange disc | ● |
| 6 | Chequered — a checkerboard | |
| 7 | Full-course caution — yellow in bands | ● |
| 8 | Waved yellow — yellow, blinking | ● |
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
drawn *over* whatever else the panel is showing rather than instead of it, so a car alongside is
never hidden by a yellow and never hides the gear. With one box you will see the gear in the middle
of the panel with a bar down one edge, which is two facts at once and is what you want at that
moment.

A black flag is an **outline** because black is the absence of light: a black panel is a box that
is off. A waved yellow is the yellow flag **blinking**, which is how you tell it from a standing
one without a second colour. The pit limiter is a **frame** in the lane and an **exclamation mark**
out of it, so you never have to judge by colour alone.

## What it does not do

**iRacing only.** Other sims may work and are welcome to; nothing here has been driven in one, and
flags are more tempting to over-claim than a dashboard because they look universal. They are not.

**These are not drawn, because iRacing does not publish them.** Each is a thing somebody will ask
for; the honest answer is that the data is not there, not that it was forgotten.

| | |
|---|---|
| Yellow per sector | Not in iRacing's flag data at all. Eight pixels across could not say *which* sector anyway. |
| Virtual safety car | iRacing has no VSC. The full-course caution is drawn and is a different thing. |
| White for a slow car | iRacing's white flag is the last lap and nothing else. |
| Incident, penalty, drive through, stop and go | None is a flag in the data. iRacing says them with the black flag and with text; the box shows the black flag. |
| A countdown to your pit box | The most loved thing on any flag box, and iRacing publishes no distance to your own stall. Working one out from track position is a calculation openDash refuses to do until a decision record says otherwise. |
| Ten to go, five to go, one lap to green | Published, but session information rather than flags. The screen has the room to say them in words; drawing a numeral here would fight the gear. |
| Two cars on one side | iRacing distinguishes it; SimHub folds it away before openDash sees it. Three spotter states ship rather than a fourth faked. |

**No acknowledgement of a warning.** A low fuel light stays until you have fuel. That is
deliberate: it sits *below* the flags and cannot hide one, which is the version of this feature
that matters.

**No themes, no presets, no custom idle picture.** openDash ships one opinionated look; the
reasoning is in [scope.md](scope.md).

## If it looks wrong

| | |
|---|---|
| Nothing at all, ever | The profile is not installed, or not selected on the device. Step 2. |
| A single dim dot in the middle | The box is working and the car's ignition is off. That mark exists so this is not confused with a broken profile. |
| Everything sideways, mirrored or shredded | Rotation or serpentine on the *device*. Step 1, not the openDash panel. |
| The gear is dark but flags work | That matrix's **At rest** is `Dark`. |
| A car alongside lights the wrong box | **Mounted** is set to the side you want rather than the side the box is on. |
| The chequered flag never shows | That matrix's **Critical flags only** is on. It is not a critical flag. |
| Far too bright at night | **Night brightness**, and turn **Night mode** on. |

## Honesty about what has been checked

No 8x8 panel is plugged into openDash's test machine, and CI owns no hardware.
[scope.md](scope.md)'s definition of done states that exception rather than leaving it implied.
Every picture on this page is checked by tests and rendered into `build/flag-box.svg`, and the
whole catalogue can be driven in the emulator — but **the profile has not yet been watched running
on a real matrix.** If you own one, saying what it actually does is the most useful thing you
could contribute.
