# The settings panel

What the plugin draws in SimHub's left menu. Sections stack down one page in this order: General,
Data, Zones, Buttons, Layout, Companion, Pit wall, **Lights**, Dashboard.

Only the Lights page is drawn here so far. The rest was built before this file existed and is
described by `plugin/OpenDash/SettingsControl.cs`; a section added from now on belongs here first,
which is the habit [XOR-231](https://linear.app/xorob/issue/XOR-231) asked for and the reason the
file exists at all.

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

**The strips get two rows and no group per device.** A matrix is a box somebody owns and so has a
group of its own; a strip is a length, and openDash generates one profile per strip shape rather
than per box. What a driver picks is therefore the profile, and `OpenDash.LedCentre` and
`OpenDash.LedRpmStyle` say what whichever profile they picked shows. They are the only two
properties a generated `.ledsprofile` reads that are not the flag box's, and they went a whole pull
request declared by the TypeScript and attached by nothing, which is a strip permanently on its
defaults; `packages/dash/test/declared-properties.txt` is the pin that now fails when the two
halves of the contract disagree.

The centre is a drop-down and the style a segmented bar, because five options is past the two or
three `Segmented.cs` is drawn for and a `ComboBox` is the panel's control for a choice from a list.

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
