# The settings panel

What the plugin draws in SimHub's left menu.

**A sidebar of pages, one per thing on the rig, and a screen is the unit.** Since
[#503](https://github.com/xorob0/OpenDash/issues/503) the panel is a sidebar -- Home, Rig, Screens, LEDs,
Matrix, Shortcuts, Settings, and Updates pinned at its foot -- and the page it opens beside it. The four
tabs grouped settings by their kind, which was honest about the settings model and wrong about the
driver: somebody who came to change their wheel found its rows on three tabs. A rig is still a set of
screens the user added, each owning the zones it shows and the buttons that cycle them, so two faces and
a pit wall are configured apart ([ADR 0017](../decisions/0017-a-screen-is-an-instance.md)).

| page | what is on it |
|---|---|
| **Home** | what needs fixing, what each device is showing, brightness and night mode |
| **Rig** | every screen, strip and matrix as a tile, painted with a flag, a car alongside, the pit lane, a warning or the revs |
| **Screens** | the screen cards, the selected screen's own pane, and the Add, Edit, Remove and Duplicate sheets |
| **LEDs** | one group per strip, the Add LEDs sheet, the rev light width and the car tables |
| **Matrix** | the flag box profile in the header, one group per matrix panel |
| **Shortcuts** | every wheel button and key: each screen's zones and quick glance, and the rig's night mode and brightness |
| **Settings** | what is the same everywhere: race data, flags, alerts and lighting |
| **Updates** | the plugin's version and the update check, what OpenDash has written into SimHub, Reinstall, Put mine back, the links |

### Where everything moved

| on the four tabs | now |
|---|---|
| Rig: the screen cards and each screen's pane | Screens |
| Rig: a face's Wheel buttons, the three quick-glance binders | Shortcuts (the glance's page stays on Screens, with a chip that opens its binding) |
| Data | Settings, Race data |
| Lights: the strips, the car tables, the rev light width | LEDs |
| Lights: the matrix panels | Matrix |
| Lights: brightness, night mode, the alert thresholds | Settings, and Home's quick controls |
| Install: the flag box row | Matrix's header (and still listed on Updates) |
| Install: everything else | Updates |

### Where the build departs from the #503 artboards

Recorded here because [voice.md](voice.md) says every divergence is, and the canvas is the author's:

- **The mark is Ui.Mark(), `media/logo.svg`**, not the artboard's three rising bars, which
  [brand.md](brand.md) rejected.
- **Eyebrows, tags and nav labels are sentence case**, because [brand.md](brand.md) says a label is in
  sentence case. The artboards type NEW and SOON in capitals, with no transform on `.new` or `.soontag`, so
  the build departs from their text there, not only from a transform.
- **The live card's eyebrow is one trimming line without tracking.** WPF has no letter spacing, and the
  tracked eyebrow is a block per glyph, which cannot trim; "Live · Assetto Corsa Competizione" is wider
  than the card's 151 px.
- **The live card is a fixed 85 px** against the artboard's 83.3, so its lines sit on whole pixels and the
  items below it never move when a session starts.
- **On the rail, night mode is an icon toggle**, a crescent (`PanelIcons.Night`, the panel's own glyph, since
  the icon sheet has no moon) that lights in the accent on the zone ground when on, a nav item high and the
  rail's 39 px inside wide. Its label is the tooltip where the label is not drawn, and a rail item's tooltip
  ends "Needs attention" when it wears the amber dot. The artboards draw no rail at all.
- **The main column fills the window.** The artboards are drawn 1200 wide as a frame, not as a maximum:
  past 1200 the column keeps growing beside the sidebar with no ceiling and nothing centred, rows and card
  grids stretch to its right edge, and two blocks sit side by side wherever the full sidebar leaves 760 px
  of content. Only prose keeps a measure, a caption or paragraph wrapping at .cap's 620
  (`PanelShell.ProseMaxWidth`), and a picture drawn at a fixed size shrinks to a narrower column rather
  than being clipped by it.
- **The version and the nav counts are the display family's SemiBold**; the artboard's 500 is a face the
  plugin does not bundle.
- **Voice replacements**: greyed rows are noun phrases ("Rig test", "Alert dismissal", "RPM colour for
  everything", "Alert display"); the Matrix row is "Car-specific shift points", not thresholds, in the name
  voice.md gives the car's own tables, which is also Lovely Sim Racing's name for them; a page is named as
  "the Screens page" wherever copy sends a driver to one. The LEDs page's #369 switch still reads "Car's own
  rev lights", which the review's ruling kept from the artboard's "Use the car's own rev lights"; whether
  it too becomes "Car-specific" is the author's to settle.
- **Two groups carry two words each, as the artboards do**, and are left for the author to settle since
  the canvas is theirs: the rig's night mode and brightness are "Lights" on Shortcuts (its rig group) and
  "Lighting" on Settings (its section), and Updates says "Lights" for the LED profiles; low fuel, oil and
  water are "Warnings" on Rig (its scenario group), "Alerts" on Settings and "Car warnings" on a Matrix
  panel. voice.md's one-word-per-thing rule would pick one noun per group.
- **The deliberate voice departures the pages draw today**, so that nobody comparing the two changes one
  back. This is not every string that differs: the pages still to be rebuilt draw inherited words the
  artboards replace (on LEDs "Fill the strip | True size" for "Stretch to fit | Actual size" and "LED
  device" for "SimHub device", on Matrix "Add a matrix panel" for "Add a matrix", on Screens "Revbar" for
  "Rev bar" and "Rev ring" and "Band D" for "In band D", on Updates "Reinstall" for "Repair everything"),
  and those are the rebuild's to change rather than departures to keep. Each page adds its own rows as it
  lands:

  | artboard | build | why |
  |---|---|---|
  | Main: "Try a flag or the spotter" | "Flags and spotter" | a label is a noun phrase, not an instruction |
  | Main: "Dash brow isn't showing openDash" | "Dash brow's profile is not selected" | no contractions, and the wordmark is not spelled in a sentence |
  | Main: "Its profile is installed but not selected on the device." | "Installed, but not selected in SimHub." | the title already names the profile; the caption says where |
  | Main: "Pick openDash Dash brow" | Select "Dash brow" | SimHub's own verb, and the name the profile is listed under |
  | Rig: "Limiter on" | "Pit limiter" | the word the rest of the panel uses for it |
  | Rig: "Oil hot", "Water hot" | "Oil temperature", "Water temperature" | the Settings rows' names for the same alerts |
  | Rig: "Light the real hardware" | "Real hardware" | a greyed title is a noun phrase |
  | Rig: a face tile's band reads its content | the band is drawn empty at rest | "Band D" is the panel talking to itself |
  | Sidebar: the search's label "Search settings" | "Search" as its placeholder and name, "Searches every setting." on the rail | the artboard's placeholder is "Search"; the rail, which has no placeholder, says the rest in its tooltip |
  | Screens: "Flags" | "Flag display" | voice.md settles the label |
  | Screens: "Show the last lap after the line" | "Lap review" | a label is a noun phrase, not a sentence |
  | Screens: the round block's cards | headed "Cards", the artboard's noun | never the settings model's "Slots" |
  | LEDs: "Use the car's own rev lights" | "Car's own rev lights" | a switch names the thing (#369) |
  | LEDs: "Width", under "Every strip" | "Rev light width" | one noun for the car's lights across the row and its caption, and a search result that says which width |
  | Matrix: "The car's own shift points" | "Car-specific shift points" | voice.md's name for the car's tables |
  | LEDs: "Centre shows" | "Centre display" | voice.md's own example |
  | LEDs: "Flags animated" | "Flag animation" | a label is a noun phrase |
  | LEDs: "Spotter uses the whole strip" | "Full-strip spotter" | voice.md's example of a switch labelled as a sentence |
  | Matrix: "At rest" | "Idle display" | "at rest" is the panel talking to itself |
  | Matrix: "Cars on" | "Mounting side" | decided |
  | Matrix: "Slide in" | "Spotter bar animation" | a switch names the thing |
  | Matrix: "Flash at redline" | "Redline flash" | a label is a noun phrase |
  | Shortcuts: "A wheel button, a button box, a key or a touch. SimHub saves them." | "A wheel button, a button box or a key." | a touch is not bound here, and the panel does not describe mechanism |
  | Shortcuts: "Previous page, zone in focus" | "Band D · previous page", one row per zone | the zone in focus is a concept the panel has nowhere else |
  | Settings: "Delta against" | "Delta reference" | a label is a noun phrase, and it is pinned |
  | Settings: "Delta decimals · 0.00 \| 0.000" | "Delta precision · Hundredths \| Thousandths" | the labels are words, with no digits |
  | Settings: "Show team names" | "Team names" | a switch names the thing |
  | Settings: "Next to a blue flag" | "Blue flag detail" | a noun phrase, not a prepositional fragment |
  | Settings: the oil and water captions | "In SimHub's unit; 0 uses 120 °C (248 °F)." and 110 °C (230 °F) | the row says which unit to type, and names its own default |
  | Updates: "Something wrong?" | "Support" | a heading is never a question |

### The geometry is not in the token file

Every colour the panel draws is a `Theme` constant, and `ThemeTests` holds each one against the token it
mirrors in `design/tokens.json`. The geometry is not tokens: the frame's numbers are `PanelShell`'s and
the shared controls' are `PanelKit`'s, each read off the #503 artboards and pinned there by
`PanelShellTests` and `PanelKitTests`. `design/tokens.json` is the author's rather than something a build
writes into, so they belong in it when somebody adds them there, and this paragraph is the record that
they are missing rather than forgotten.

## Before #503: the four tabs

What follows is the record of the panel as four tabs -- Rig, Data, Lights and Install -- kept because the
reasoning under each row still holds and the page agents rebuild from it. It describes what the plugin
drew before [#503](https://github.com/xorob0/OpenDash/issues/503), not what it draws: the tab bar is gone,
and each tab's rows are on the pages [Where everything moved](#where-everything-moved) names. A tab here
is that tab, not a page; each section is rewritten for its page as that page lands.

### Three tokens were owed

[#176](https://github.com/xorob0/OpenDash/issues/176) named `panel.tabs`, `control.tab` and
`control.screenCard` as tokens. They never reached `design/tokens.json`, so the tab bar and the screen
card were composed from the tokens that did exist, and their geometry -- the tab height, the underline
weight, the card's width and height -- was literals in `Widgets.cs`. Both went with #503, the tab bar for
the sidebar and the card for the kit's DeviceCard, and their geometry with them.

### The tab bar

*Gone with #503, for the sidebar above. This section and the four below it describe the tabs as they
were; each moves to its page, in the words of the table above, as the page agent rebuilds that page.*

Across the top under the header, `control.tab`. The selected tab carries the accent underline; the
rest are `text.secondary`. Four tabs never need to scroll, so there is no overflow behaviour.

The tab is remembered for the session and not persisted. A user who came to change a zone should
land where they left off within one sitting; a user coming back next week should land on Rig, which
is the answer to "what is this".

### Rig

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

#### Adding one

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

**What the sizes list should become.** [#85](https://github.com/xorob0/OpenDash/issues/85) wants the sizes
SimHub reports it is driving offered first, with the rest behind "something else", and it is right
that a flat list of fourteen resolutions is the thing a new user gives up on. It is not built here,
because nothing in the SDK research says whether SimHub exposes the displays to a plugin. That is
the first thing #85 has to check.

#### Removing one

Removes the screen, its settings, and the folder it owns — and says so first, in those terms,
naming the folder. It also says the thing that is easy to miss: **a wheel button bound to this
screen's actions will stop doing anything**, because the action is no longer registered. A driver
who removes a screen and finds a dead button three laps into a race is a bug report we can prevent
with one sentence.

A screen whose folder is gone — deleted in SimHub, renamed by hand, or never written because the
add failed — **keeps its card**, marked, with a button to write the package back. Dropping it would
destroy the zone setup behind it and hide the thing that needs fixing, which is the same reasoning
[#176](https://github.com/xorob0/OpenDash/issues/176) applies to a failed install.

#### The pane of a face

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

#### The pane of a pit wall

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

#### The pane of a companion

A companion shows one module at a time and the header says which, so its pane is the rotation: the
twenty-one modules as toggles, and one binder for "next module". A module that is off is skipped
when you page.

Energy, Damage and Track rivals are off by default because iRacing publishes none of their data.
The pane says so rather than letting a user switch one on and wonder why it is blank.

#### The pane of a slots face

The twelve-slot picture, unchanged, for anyone running an `openDash slots <size>` package. It is on
its own screen card rather than in a section of its own, which is the whole reason the card model
survives the redesign without cluttering it: you see it only if you installed one. It leaves with
the cards in [#146](https://github.com/xorob0/OpenDash/issues/146).

### Data

The settings that are not per screen, because a lap time means the same thing on the rim as it does
on the pit wall, and so does a name.

```
These apply to every screen

  The rev bar                          [ Shift lights | RPM bar | Off ]
  Shift lights, a plain RPM bar, or off entirely if your DDU has
  LEDs of its own. Off gives its room back to the zones.

  Position                             [ Overall | Class ]
  Overall, or within your class.

  Delta reference           [ Session best | All-time best | Last lap ]
  Which lap the delta compares against.

  Delta precision                      [ Hundredths | Thousandths ]
  Thousandths for a hotlap, hundredths to read at a glance.

  Session progress                     [ Auto | Laps | Time ]
  Auto shows laps when the session declares a lap count, time otherwise.

  Blue flag detail                     [ Nothing | Class | Position and class ]
  What shows next to a blue flag.

  Driver names        [ Liam Byrne | L. Byrne | B. Liam | Byrne Liam ]

  Team names                                              [ on/off ]
  Names the team instead of the driver, and keeps the driver where
  the sim has no team.

  Clock                                          [ 14:32 | 2:32 PM ]
  The sim's time of day follows it too.
```

**The four name formats are shown as what they make of one name rather than described.** A value in a
chooser is read without its label and alongside the values beside it
([voice.md](voice.md)), and "Initial and surname" next to "Surname and initial" is two fragments a
reader has to decode where `L. Byrne` next to `B. Liam` is the answer itself. The row therefore
carries no caption: the control has already said everything.

The examples stay mixed-case although a list draws a name upper-cased at every size it draws one at,
which [zones.md](zones.md) records and `MIXED_CASE_NAME_SIZE` enforces. The question the control asks
is which word order, and four shouted examples would read as a fifth choice about case that the panel
does not offer.

Naming a driver is here and not on a screen's pane because a name is read by a person, and the person
does not change between the wheel and the pit wall ([#385](https://github.com/xorob0/OpenDash/issues/385)).

**The clock is two worked examples of one time for the same reason**, `14:32` against `2:32 PM`. Its
caption says the one thing the control cannot: that the sim's time of day follows the setting as well
as the wall clock, which a row labelled "Clock" does not say on its own
([#324](https://github.com/xorob0/OpenDash/issues/324)).

**The delta reference has a third segment the canvas does not draw.** The Plugin artboard offers the
session best and the all-time best; Last lap was added by
[#322](https://github.com/xorob0/OpenDash/issues/322) and is iRacing's own live delta to the lap
before this one, which SimHub does not publish. The label is the canvas's own name for that lap, the
one the Last lap card and the Lap times page already draw, and the row's caption holds for all three,
since each of them is a lap. Three values is still a segmented control by the component sheet's rule.
`PanelDataTab.DeltaLabels` holds the words, with a test that counts them against the contract.

**The delta precision row is not on the canvas at all.** The canvas draws every live delta to two
places, and [#322](https://github.com/xorob0/OpenDash/issues/322) lets a driver ask for three, which
is what a hotlap is decided by. It sits directly under the reference because it qualifies the same
number, and it is rig-wide for the reason the reference is: a delta read to the thousandth on the
rim and to the hundredth on the pit wall is two answers to one question. The two values are words,
although the driver names and the clock answer their questions with worked examples. A name is not a
numeral, and a delta is nothing else: `0.21` and `0.214` would be set in the panel's own face,
Barlow, where the canvas's fourth rule keeps numerals, version numbers in the plugin included, to
Barlow Condensed. The clock's `14:32` and `2:32 PM` are drawn in that Barlow too. That is a
disagreement between the build and the canvas for the canvas's owner to settle, not a precedent this
row follows. The caption says what each is for, which the two words cannot. Neither answer resizes
or rearranges a box, since every box that draws the delta is cut for three places whichever is
chosen: what changes is the digits, and the delta page's caption beside the number, which follows
the figure it draws and so moves one cell along when a third place is drawn;
[ADR 0011](../decisions/0011-personalisation.md) is why that is the condition of the setting
existing at all.

The rev bar is three states in one control rather than a toggle and a second toggle under it: what
the top of the face carries is one decision, and a driver whose wheel already has LEDs across it
wants the third of them (#189).

### Lights

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

#### Why the page is shaped this way

**Brightness is at the top and is named for the rig.** `LightsBrightness`, `LightsNightBrightness`
and `LightsNightMode` are not flag-box settings: a driver who owns a flag box probably owns other
lights, and a second profile would read the same three. Everything below them is this box's.

**The profile row is a button, and it never presses itself.** openDash hands SimHub a profile object
through SimHub's own public API and SimHub writes its own settings file, so there is nothing unsafe
about the act — what is left is consent, and a profile paints hardware the user owns. The button
carries the verb (*Install*, *Update*, *Reinstall*) and the line above it says what SimHub holds
now, so pressing it is never a guess. The path stays under it as the fallback for when SimHub's
matrix settings cannot be reached. See the amendment to ADR 0013.

**One group per matrix**, prefixed, the way [#175](https://github.com/xorob0/OpenDash/issues/175)
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

### Install

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
[#85](https://github.com/xorob0/OpenDash/issues/85)'s whole design, and the reason it is one fewer
surface to build.
