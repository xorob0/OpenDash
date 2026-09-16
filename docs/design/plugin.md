# The settings panel

What the plugin draws in SimHub's left menu.

**Four tabs, and a screen is the unit.** A rig is a set of screens the user added, each owning the
zones it shows and the wheel buttons that cycle them, so two faces and a pit wall are configured
apart rather than sharing one set of settings — and so are two faces of the *same size*, which
[ADR 0017](../decisions/0017-a-screen-is-an-instance.md) is the record of. What is genuinely the same
everywhere lives in Data. The packages live in Install. Everything that drives an LED lives in
Lights.

| tab | what is on it |
|---|---|
| **Rig** | the screen cards, and the selected screen's own pane |
| **Data** | the four settings that mean the same thing on every screen |
| **Lights** | the flag box, the matrices and the strips |
| **Install** | the packages, the plugin version, reinstall and the update check |

The canvas draws the first, second and fourth on the `Plugin` artboard, and the controls they need
on `PluginComponents`. It was drawn before the lights wave and shows three tabs named Screens, Data
and Install; this file is ahead of it on two points, said here rather than changed there because the
canvas is the author's:

- **Lights is a fourth tab, not a section.** [XOR-231](https://linear.app/xorob/issue/XOR-231) settles
  that a light device "is the same shape of thing as a screen", which argued for putting a flag box
  card in the same row as the screens. It is not done, because a screen and a box are the same shape
  to *the settings model* and nothing alike to a user: a screen is a rectangle with zones, a box is
  64 LEDs with a mounting side, and one row of cards mixing them would have to explain itself. Four
  tabs is the cheaper honesty.
- **The first tab is Rig, not Screens.** Because it is now a list of what you have rather than a list
  of what exists.

### Three tokens are owed

[XOR-125](https://linear.app/xorob/issue/XOR-125) names `panel.tabs`, `control.tab` and
`control.screenCard` as tokens. They are not in `design/tokens.json`, and that file is the author's
rather than something a build writes into, so the tab bar and the screen card are composed from the
tokens that do exist — `control.height`, `radius.sm`, `purpose.ui.*`, `color.surface.*`. Nothing
invents a colour; every value in `Widgets.cs` is a `Theme` constant and `ThemeTests` still holds
each one against the token it mirrors.

What is not expressed as a token is the geometry: the tab height, the underline weight, and the
card's width and height are literals in `Widgets.cs`. They belong in the token file when somebody
adds them there, and this paragraph is the record that they are missing rather than forgotten.

## The tab bar

Across the top under the header, `control.tab`. The selected tab carries the accent underline; the
rest are `text.secondary`. Four tabs never need to scroll, so there is no overflow behaviour.

The tab is remembered for the session and not persisted. A user who came to change a zone should
land where they left off within one sitting; a user coming back next week should land on Rig, which
is the answer to "what is this".

## Rig

```
Rig                                                        Data   Lights   Install
━━━━                                                        

┌──────────────┐ ┌──────────────┐ ┌──────────────┐ ┌──────────────┐ ┌───────────┐
│ Main dash    │ │ Rim          │ │ Pit wall     │ │ Phone        │ │     +     │
│ 1280 × 480   │ │ 1280 × 480   │ │ 1920 × 1080  │ │ 850 × 480    │ │ Add a     │
│ face         │ │ face         │ │ pit wall     │ │ companion    │ │ screen    │
└──────────────┘ └──────────────┘ └──────────────┘ └──────────────┘ └───────────┘
  ▔▔▔▔▔▔▔▔▔▔▔▔

Main dash                                          [ Rename ]  [ Remove this screen ]
face · 1280 × 480 · openDash Main dash · properties OpenDash.MainDash*
```

**The card row is the rig.** One card per screen, in the order they were added, then the add card.
A selected card carries the accent underline and its pane is drawn below. A card shows the name, the
size and the kind, and nothing else — a status dot only when there is something wrong.

**The line under the name is where the facts live.** Kind, size, the folder under `DashTemplates`,
and the namespace its properties carry. The namespace is there because
[ADR 0017](../decisions/0017-a-screen-is-an-instance.md) freezes it at creation and a rename does not
move it, so a screen called "Rim" whose properties say `MainDash` has to be able to say so. It is a
fact on the page rather than a thing to discover.

### Adding one

The add card opens a panel, not a dialog.

```
Add a screen

  Size        [ 1280 × 480                          v ]
              The sizes openDash ships. Pick the one your
              screen actually is; SimHub scales nothing.

  Name        [ Rim                                   ]
              Yours. It names the card here and the
              dashboard in SimHub's own list.

  This one is your second 1280 × 480, so it gets its own copy of the package
  and its own settings. The first keeps the stock one.

                                          [ Cancel ]  [ Add screen ]
```

Then, once: **"Added. Restart SimHub to see `openDash Rim` in its dashboard list, then assign it to
this display under Dash Studio."** SimHub reads its template list once at startup
([dev-loop.md](../dev-loop.md)), so this cannot be made to happen live, and the display assignment is
in another part of SimHub entirely. Both are said at the moment they become true rather than left to
be found.

The name is prefilled from the size, and from the size and a numeral when the rig already has one.
The sentence about the second copy appears only when it is true.

**What the sizes list should become.** [XOR-34](https://linear.app/xorob/issue/XOR-34) wants the sizes
SimHub reports it is driving offered first, with the rest behind "something else", and it is right
that a flat list of fourteen resolutions is the thing a new user gives up on. It is not built here,
because nothing in the SDK research says whether SimHub exposes the displays to a plugin. That is
the first thing XOR-34 has to check.

### Removing one

Removes the screen, its settings, and the folder it owns — and says so first, in those terms,
naming the folder. It also says the thing that is easy to miss: **a wheel button bound to this
screen's actions will stop doing anything**, because the action is no longer registered. A driver
who removes a screen and finds a dead button three laps into a race is a bug report we can prevent
with one sentence.

A screen whose folder is gone — deleted in SimHub, renamed by hand, or never written because the
add failed — **keeps its card**, marked, with a button to write the package back. Dropping it would
destroy the zone setup behind it and hide the thing that needs fixing, which is the same reasoning
[XOR-125](https://linear.app/xorob/issue/XOR-125) applies to a failed install.

### The pane of a face

A face is configured **on a picture of itself**, which is the part of the current panel worth
keeping: "zone C" means nothing until you see where zone C is.

```
What each zone shows
Pick the page a zone opens on, and how many pages its button cycles through.
A zone with one page enabled never changes.

┌─────────────────────────────────────────────────────────────────────┐
│ Rev bar                                                             │
├──────────────────┬───────────────────────────────┬──────────────────┤
│ Race · lap       │        Car settings           │ Position · class │
├──────────────────┼───────────────────────────────┼──────────────────┤
│ Zone B           │ Zone A                        │ Zone C           │
│ [ Lap times   v] │ [ Gear                     v] │ [ Relative    v] │
│ [ 6 of 21     v] │ [ 2 of 4                   v] │ [ 4 of 21     v] │
│ ☐ My class only  │                               │ ☑ My class only  │
├──────────────────┴───────────────────────────────┴──────────────────┤
│ Band D    [ Fuel          v ]  [ 3 of 8    v ]                      │
└─────────────────────────────────────────────────────────────────────┘
```

Drawn to **that face's** proportions: the portrait reads as a column, and the nano at 800 x 286
shows no bar because it has none. The shape comes from the contract, which carries just enough of it
for a plan, since the plugin cannot read a layout file.

Two controls per zone, and they are the two decisions: the page it **opens on**, and **how many** it
cycles. The second is the most consequential control on the panel — it decides how long a driver's
thumb-press cycle is — and it is a count that opens a panel of checkboxes rather than a list,
because twenty-one checkboxes do not fit in a combo box.

The class filter sits in the zone's own cell because it is a property of the zone and not of the
face: the point of it is zone B listing the race while zone C lists the class you are racing in.

Under the picture, the wheel buttons, **bound per screen**:

```
Wheel buttons on this screen
Bound per screen, so a second face can stay still while the one in front of you cycles.

  Zone B    [ Wheel · 7 ]        Zone A    [ Not bound ]
  Zone C    [ Wheel · 8 ]        Band D    [ Not bound ]

  Quick glance                                        [ Zone C · Track  v ]
  Hold to show one page, release to return.           [ Wheel · 9 ]
  Usually the relative or the track.

  ⚠ Zone C and the quick glance both show the track.
```

The glance binder forces its mapping to `During`, because SimHub only calls an action's start on
press and its end on release for that press type, and the binding dialog offers
`ShortAndLongPress` by default. A glance is only meaningful as a hold, so a binding to it is
corrected rather than second-guessed.

The clash line says what is doubled up and does not prevent it. Two zones on the same page is a
thing people do on purpose.

### The pane of a pit wall

The letters need a picture, so the pit wall gets one of its three pages rather than a plan of one
screen:

```
Where the zones are

  Race              Tower                 Telemetry
  ┌─────┬─────┐     ┌───────────────┐     ┌───────────┐
  │     │  A  │     │     Wide      │     │     A     │
  │Board├─────┤     ├───────┬───────┤     ├───────────┤
  │     │  B  │     │   C   │   D   │     │     B     │
  └─────┴─────┘     └───────┴───────┘     ├───────────┤
                                          │     C     │
                                          └───────────┘
```

Then a row per zone naming where it is, the wide zone, and the web view address.

### The pane of a companion

A companion shows one module at a time and the header says which, so its pane is the rotation: the
twenty-one modules as toggles, and one binder for "next module". A module that is off is skipped
when you page.

Energy, Damage and Track rivals are off by default because iRacing publishes none of their data.
The pane says so rather than letting a user switch one on and wonder why it is blank.

### The pane of a slots face

The twelve-slot picture, unchanged, for anyone running an `openDash slots <size>` package. It is on
its own screen card rather than in a section of its own, which is the whole reason the card model
survives the redesign without cluttering it: you see it only if you installed one. It leaves with
the cards in [XOR-95](https://linear.app/xorob/issue/XOR-95).

## Data

The four settings that are not per screen, because a lap time means the same thing on the rim as it
does on the pit wall.

```
These apply to every screen

  The rev bar                          [ Shift lights | RPM bar | Off ]
  Shift lights, a plain RPM bar, or off entirely if your DDU has
  LEDs of its own. Off gives its room back to the zones.

  Position                             [ Overall | Class ]
  Overall, or within your class.

  Delta reference                      [ Session best | All-time best ]
  Which lap the delta compares against.

  Session progress                     [ Auto | Laps | Time ]
  Auto shows laps when the session declares a lap count, time otherwise.
```

The rev bar is three states in one control rather than a toggle and a second toggle under it: what
the top of the face carries is one decision, and a driver whose wheel already has LEDs across it
wants the third of them (XOR-138).

## Lights

Everything about the 8x8 flag box, and about any light openDash drives later.
[ADR 0013](../decisions/0013-lighting-hardware.md) is why the page exists;
[flag-box.md](flag-box.md) is what the box draws.

```
Lights
  An 8x8 LED matrix beside the screen. openDash builds the profile and puts it where you can
  find it, but does not install it: SimHub keeps matrix profiles in a file it rewrites itself.
  Import it once, and everything on this page reaches it while you drive.

  Flag box profile                                            [ Install into SimHub ]
    Not installed. Press the button to add it to      [ C:\…\OpenDash\openDash Flag… ]
    SimHub's matrix profiles; then pick it on your
    matrix device.

  Brightness                                                                          [  100 ]
    Percent, for every light openDash drives. SimHub's
    own device brightness applies on top.

  Night brightness                                                                    [   25 ]
    Used while night mode is on. 64 LEDs at full output
    beside a wheel in a dark room is too bright.

  Night mode                                                                          (  off )
    A switch you flip, not a time of day we guess at.

  Critical flags only                                                                 (  off )
    Quiet until something matters: drops the chequer,
    the white, the green and the start gantry.

  Show the gear                                                                       (   on )
    What the box shows when nothing else is on it.
    Off leaves it dark.

  Low fuel, laps                                                                      [    2 ]
  Oil temperature                                                                     [    0 ]
  Water temperature                                                                   [    0 ]
    In your own unit; 0 uses the default for it.

  SimHub composes up to four matrix contents. Matrix 1 does everything by default; switch on a
  second only if you own a second box.

  Matrix 1
      At rest                                                        [ Dark |  Gear  ]
      Flags                                                                     (  on )
      Pit                                                                       (  on )
      Spotter                                                                   (  on )
      Warnings                                                                  (  on )
      Mounted                                                 [ Both | Left | Right ]
  Matrix 2 … 4      the same, all off, at rest Dark

  An RGB LED strip across the wheel or the rim. Install the profile that matches your strip,
  then these two decide what it shows.

  Strip centre                                                        [ RPM          v ]
    What the middle of the strip shows. RPM keeps
    the brake on the sides; RPM only leaves them dark.

  Rev style                                    [ Left to right | Meet in middle |  F1  ]
    How the ladder fills. Meet in middle works
    inwards from both ends; F1 is a formula wheel's
    colours, and flashes whole.
```

**Matrix 2 to 4 are collapsed until they are switched on.** Four groups of six rows is twenty-four
rows of settings for hardware almost nobody owns, and the tab opens on the one matrix that does
everything by default. This is the "less often used, but kept" rule applied where it costs the most
scroll.

### Why the page is shaped this way

**Brightness is at the top and is named for the rig.** `LightsBrightness`, `LightsNightBrightness`
and `LightsNightMode` are not flag-box settings: a driver who owns a flag box probably owns other
lights, and a second profile would read the same three. Everything below them is this box's.

**The profile row is a button, and it never presses itself.** openDash hands SimHub a profile object
through SimHub's own public API and SimHub writes its own settings file, so there is nothing unsafe
about the act — what is left is consent, and a profile paints hardware the user owns. The button
carries the verb (*Install*, *Update*, *Reinstall*) and the line above it says what SimHub holds
now, so pressing it is never a guess. The path stays under it as the fallback for when SimHub's
matrix settings cannot be reached. See the amendment to ADR 0013.

**One group per matrix**, prefixed, the way [XOR-124](https://linear.app/xorob/issue/XOR-124)
settled that a screen owns its settings. A device is the same shape of thing. People do own two
boxes — one in each corner of a monitor stand, one on flags and one on the gear — and that setup
has to be configurable without either box guessing.

**"Mounted" is asked rather than inferred.** A box to the left of the wheel that lights for a car
on the right is worse than no box, so the side is a setting with no clever default: `Both` is the
single-box answer and shows both edges of the panel.

**The strips get a few rows and no group per device.** A matrix is a box somebody owns and so has a
group of its own; a strip is a length, and openDash generates one profile per strip shape rather
than per box. What a driver picks is therefore the profile, and `OpenDash.LedCentre` and
`OpenDash.LedRpmStyle` say what whichever profile they picked shows. They went a whole pull request
declared by the TypeScript and attached by nothing, which is a strip permanently on its defaults;
`packages/dash/test/declared-properties.txt` is the pin that now fails when the two halves of the
contract disagree.

Both are drop-downs: five centres and four styles are past the two or three `Segmented.cs` is drawn
for, and a `ComboBox` is the panel's control for a choice from a list.

**The car's own bar is a style rather than a switch**, and it is the default (ADR 0018). A driver who
wants one look in every car picks one of openDash's three; everybody else gets the lights of the car
they are in, and a car openDash has no table for falls back without them choosing anything. The fit
row beneath it means nothing under the other three, which is a cost of putting it on the same page
and is cheaper than a page of its own for one setting.

**Whose measurements they are is on the page.** The tables are fetched rather than shipped and are
CC BY-NC-SA 4.0, so the attribution is a caption under the strip rows, naming the project and the
licence. It is the one row on this page that is there for a reason other than configuring something.

**Rotation and serpentine are not on this page.** They are SimHub device settings, decided by the
corner the data cable enters, and duplicating them here would produce two places that disagree.
The guide says where they are instead.

**Presets are not on this page either.** DNR's control panel offers saved presets per device.
openDash has no store: a setting *is* a SimHub property, which is what makes it readable by
anything and changeable while driving. A preset is a set of values with a name — its own storage,
its own migration, and its own failure when a property is added. It is a real gap and worth a
ticket if somebody asks for it; it is not smuggled in here.

**A temperature of 0 means "not set".** The plugin then publishes nothing for it and the profile
applies its own default, which is chosen from SimHub's `TemperatureUnit`. A driver in Fahrenheit
who has never opened this page gets 248, not 120.

## Install

The packages, and the plugin itself.

```
Screens openDash can install
One package per size, installed into SimHub DashTemplates. Adding one here makes it
a screen; its settings are its own.

  Main dash      1280 × 480    Installed       [ Remove ]
  Rim            1280 × 480    Installed       [ Remove ]
  Pit wall       1920 × 1080   Installed       [ Remove ]
  Nano           800 × 286     Not installed   [ Add ]

This plugin
openDash 0.4.0                                 [ Reinstall ]
Reinstall restores every embedded copy. Your settings are kept.

Check for updates                              [ Check now ]  ( on )
Asks GitHub for the newest release once a day. Nothing else leaves your machine.
```

The Install tab and the Rig tab are two views of one list, and adding in either place does the same
thing. Rig is where you go to configure; Install is where you go to see what is on disk and what
version it is.

**An empty rig is the first run.** The Rig tab shows the add card alone over a line saying nothing is
installed yet. There is no wizard to dismiss and no "never show this again" flag, because the empty
state stops appearing exactly when it stops being true — which is
[XOR-34](https://linear.app/xorob/issue/XOR-34)'s whole design, and the reason it is one fewer
surface to build.
