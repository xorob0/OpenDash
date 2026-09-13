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

  Profile                                              [ C:\…\SimHub\OpenDash\openDash Flag… ]
    Import this file in SimHub's matrix device
    settings. openDash does not install it.

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
      Spotter                                                                   (  on )
      Warnings                                                                  (  on )
      Mounted                                                 [ Both | Left | Right ]
  Matrix 2 … 4      the same, all off, at rest Dark
```

### Why the page is shaped this way

**Brightness is at the top and is named for the rig.** `LightsBrightness`, `LightsNightBrightness`
and `LightsNightMode` are not flag-box settings: a driver who owns a flag box probably owns other
lights, and a second profile would read the same three. Everything below them is this box's.

**The profile row is a path, not a button.** The plugin writes the file and stops; there is no
"install" to press. Saying where the file is and that SimHub's own import is the next step is the
whole of the honesty ADR 0013 bought, and a button would imply otherwise.

**One group per matrix**, prefixed, the way [XOR-124](https://linear.app/xorob/issue/XOR-124)
settled that a screen owns its settings. A device is the same shape of thing. People do own two
boxes — one in each corner of a monitor stand, one on flags and one on the gear — and that setup
has to be configurable without either box guessing.

**"Mounted" is asked rather than inferred.** A box to the left of the wheel that lights for a car
on the right is worse than no box, so the side is a setting with no clever default: `Both` is the
single-box answer and shows both edges of the panel.

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
