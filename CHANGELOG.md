# Changelog

Every release is the tag `v<VERSION>`, where `VERSION` is the one version string of the project.
The release workflow refuses a tag that does not match it. A version carrying a suffix such as
`-rc.2` publishes as a pre-release, so a candidate is never the download a first-time user is
offered.

Each release carries `OpenDash-plugin.zip`, which embeds and installs the dashboards that are ready
to install, and one `.simhubdash` per package for anyone who wants a dashboard without the plugin,
including any that the plugin does not install.

From 0.2.0-rc.2 it also carries one `.ledsprofile` per LED device shape, which covers the RGB
strips, the brows and the flag box, together with a `manifest.json` listing everything published.

## 0.3.0-rc.2 (2026-09-18)

The candidate that puts the zones back on the screen. In 0.3.0-rc.1 a single corner radius was
computed as 3.5999999999999996 and written into a field SimHub reads as an integer, and because
SimHub refuses the whole file rather than that one number, every screen made of the module
sub-dashboard drew nothing at all: zone B, zone C, both companions and every zone of every pit wall.
Nothing reached the log, which is why the rest of the face drew normally around an empty middle. If
you installed rc.1, this is the release to replace it with.

Besides that, the rig model finishes the move the screens began. An RGB strip is something you add
and name, with the settings its own profile reads, rather than a shape the whole rig answers for at
once, and a flag box panel is an instance in the same way. Moreover, the rev bar became a per-screen
answer, so a rim whose wheel already carries LEDs across its top and a display on the desk beside it
are no longer switched off together. A rig therefore starts with no strips and no panels, while a
settings file written against rc.1 keeps whatever was doing something in it.

Two of the readings below were simply wrong before the sim had measured a lap, and both concern
fuel: on an idle screen the tank read as low in five places at once, and band D estimated zero laps
remaining rather than saying that it had no estimate.

### Added

- **An LED bar is a strip you added, with settings of its own.** A bar has a name, a shape and a
  namespace frozen at creation, the way a screen does ([ADR 0017](docs/decisions/0017-a-screen-is-an-instance.md)):
  adding one installs a profile of that name into SimHub, and that profile is the embedded one with
  the bar's namespace written through its three settings, so a wheel and a brow on one rig can
  finally be told to show different things in their middles, to fill their ladders differently and to
  animate a flag or to hold it. Removing the bar takes the profile back out. What is the rig's stays
  the rig's, which is brightness, night mode, the low-fuel threshold and the car's own shift pattern.
  A rig starts with no bars, because a profile paints hardware somebody owns and openDash does not
  guess at what that is ([ADR 0013](docs/decisions/0013-lighting-hardware.md)).
- **A matrix panel is an instance as well.** Four numbered groups of eleven settings, one of them
  switched on because it happened to be first, is a page for hardware most people own none of. Add
  one, give it the name you will recognise it by, and it arrives working. A settings file written
  before this keeps every slot that was doing something, which on the shipped defaults is matrix 1
  alone, so nobody's box goes dark because the model under it changed.
- **The rev bar is per screen.** `OpenDash.<Face>RevBar` falls back to the rig-wide `OpenDash.RevBar`,
  which still falls back to the deprecated `ShiftLights`, so a settings file written before this
  keeps drawing what its owner chose until they answer one face individually. The row moved from the
  Data tab onto each face's own pane, above the flag format; the card faces own no properties at all,
  so theirs keeps writing the rig-wide name and says so where it is drawn.
- **A screen can change size** without being removed and added again, which had been throwing away
  its zones and every wheel button bound to it. The namespace is frozen through a resize as it is
  through a rename, so the settings and the bindings survive and only the folder is rewritten.
- **A 4/8/4 wheel**, being sides of four around a centre of eight. The table carried 4/9/4 and 4/14/4
  either side of it and nothing at eight, so a wheel of that geometry had to borrow a profile that
  paints one LED it does not have. Twenty-two profiles are built now, being twenty-one shapes and the
  flag box.
- **The redline flash on the flag box is a switch per panel.** `FlagBoxMatrix<N>GearBlink` is on by
  default, and turning it off leaves the digit in its redline colour, so nothing is lost but the
  strobe. The flash is the box repeating what the rev bar and the strip have already said twice,
  which is worth answering for one box and not for another, for the reason every other flag box
  setting became per panel.

### Changed

- **Adding a screen asks what it is first.** It had been one drop-down of every package the build
  carries, reading "480 × 850 · companion", which is two questions at once in a vocabulary a driver
  has no reason to know. The question now is what kind of screen, then how big only where there is a
  choice, and for the companion and the pit wall that second question is which way round rather than
  a resolution. Every kind says in one line what it is, and the name box opens on something
  recognisable.
- **The gear fills the flag box panel.** The digit was a 5 by 7 glyph dropped into the corner of the
  8 by 8 grid, with one column of margin on the left, two on the right and the bottom row dark, so it
  read as small and off-centre beside a flag that filled the panel, and a driver counted the row and
  the column that never lit. The font is redrawn on the whole panel with two-pixel strokes, and 6, 8
  and 9 are one family with one stroke between them, which is the only difference that survives sixty
  pixels read in peripheral vision.
- **The Install tab is a census rather than a place to install.** Its strip rows say which shapes this
  build draws for, and whether anything of ours is in SimHub for them; its package rows lose their
  Add, a screen not being a package, and a row that knows only the package could offer neither a name
  nor a size. Installing happens where the screen or the bar is.
- **The shape list opens on a wheel** rather than on "0/10/0", which is a bare run nobody owns: it was
  sorted by id where the canvas's own row order opens on the wheels. The name box opens on a name of
  ours in the same way, so the row it installs into SimHub's LED profile list reads as openDash's
  rather than as a bare geometry among everybody else's profiles.

### Fixed

- **Every module page drew nothing.** `JsonTextReader.ReadAsInt32` throws on "3.5999999999999996"
  rather than truncating it, and the throw unwinds `EditorModel.LoadFromFile`, so one
  `BorderStyle.RadiusTopLeft` computed as 0.2 × 18 cost the whole file. In 0.3.0-rc.1 that file was
  the module sub-dashboard, which is what zone B, zone C, both companions and every pit wall zone are
  made of. The serialiser now rounds all eight of `BorderStyle`'s numbers, which are `int` in SimHub,
  and `intFields.ts` is the backstop for the fields nothing rounds: the public integer properties of
  every `GraphicalDash` class, read out of the decompiled 9.12.6 assembly, at which a document
  holding a fraction is refused outright and every composed package is checked, so this cannot reach
  a driver again.
- **A tank was low before the sim knew what a lap costs.** SimHub publishes `Fuel_RemainingLaps` as
  zero rather than null before a lap has been run, so "under the threshold" was true at every idle
  screen, and one sentence feeds five drawings: the band's fuel telltale, the fuel pop-up, the flag
  box's low-fuel warning, the strip's low-fuel state and the zone telltale, every one of them lit
  while nothing was running. `tankIsLow` now asks for a per-lap consumption first, which is the gate
  the fuel module already draws its own estimate behind.
- **Band D estimated zero laps** beside a tank the sim had not measured, which is a reading with
  nobody behind it stated with the confidence of a number. It says it has no estimate instead, behind
  that same consumption gate.
- **The class filter drew as a white disc** over most of zone B and zone C. SimHub's `SHToggleButton`
  declares no size of its own and grows to whatever it is measured against, and this one was docked
  to the bottom of a cell as tall as the face's body, where every other switch on the panel sits in a
  row of automatic height. It is measured inside a vertical stack now, which asks the same question
  the rows ask.
- **Two screens could own one DashTemplates folder**, so removing one deleted the other's dashboard.
- **The mirror was computed only when one setting asked for it**, so a second bar set to the car's own
  pattern would have read a run that nothing filled. Anything on the rig asking for it is sufficient
  now.
- The line after adding a screen named the folder, whereas SimHub lists a dashboard under its title,
  which is the name the driver has just chosen. The two remove links read "Remove", the longer words
  having been cut off at the panel's edge once a group was indented inside its section.

## 0.3.0-rc.1 (2026-09-18)

The candidate that finishes the zone face. The whole design canvas was measured against the build,
requirement by requirement, and the differences were closed: band D now cycles its eight pages,
zone A its four, and zones B and C the full catalogue of twenty-one, each page drawn at the size its
own artboard gives it rather than at one size for every face. A flag can now take the whole body of
the screen instead of the band alone, and whichever format you choose it names all fifteen
conditions the catalogue holds, where it previously knew six. Besides the face, the flag box gained
its own settings per panel, the strips gained a wiring order for Fanatec wheels driven through
Fanalab, and the companion pages itself rather than borrowing SimHub's own ring.

Several of the corrections below are readings that were simply wrong, and had been wrong for some
time. If you race in a multi-class field, if you drive an imperial rig, or if you rely on the
spotter, it would be worth reading the Fixed section before anything else.

The version moves to 0.3.0 rather than to a third 0.2 candidate because of how much of the settings
surface this adds: twelve further properties, a flag format and a lap review that a screen owns
individually, a filter and a page-on-demand that a pit wall owns, four settings that moved from the
Lights tab into each flag box panel's own group, and a twenty-first LED profile. A settings file
written against 0.2.0-rc.2 is migrated rather than discarded, so nothing is lost by upgrading, but
the surface is wide enough that it is not the same minor version.

### Added

- **A flag can take the whole screen.** `OpenDash.<Face>FlagFormat` chooses between the band and a
  full-screen block over zones B, A and C, and it is a per-screen setting offered on that screen's
  own pane, so a rim and a main dash in one rig may answer differently. The block is derived from
  the layout rather than tabulated, which is why it is drawn correctly at all eight sizes and in
  both rev-bar arrangements.
- **Fifteen flag conditions rather than six**, on band D and on the full-screen block alike, ranked
  in the catalogue's own order and reading the same expression in both places. A red flag or a
  full-course caution therefore draws something, where the full-screen format had previously left
  the body blank while the band named the condition.
- **A lap review** after each crossing, off by default. It is the largest of the boxes drawn over
  zone A and covers the lap-time pop-up while it is out, and it never covers band D, the rev bar,
  the bar of settled values or the pit limiter banner.
- **Change notifications**: a setting you turn on the wheel is announced for three seconds, with the
  direction it moved, for the seven values the sim actually publishes.
- **Five pit alerts** in the limiter's own rectangle, being engage, disengage, the limiter on in the
  lane, the ignition off and the engine off. They are ranked among themselves rather than against
  the flag, so a caution does not blank the limiter band at the moment the pit lane is busiest.
- **Blue flag detail**: the band may name the class of the car behind, or that car's position and
  class, as a rig setting on the Data tab.
- **The flag box settles per panel.** The critical-flags switch, the gear, and the oil and water
  temperatures moved from the tab header into each matrix's own group, so somebody running one box
  on flags and another on the gear answers each separately. A settings file written before the move
  is migrated into all four panels once.
- **The spotter is an overlay** on the flag box rather than a state that displaces everything under
  it, so a car alongside no longer blanks the warnings or the gear, and its bar can be made to grow
  in three steps as the car closes.
- **A Fanalab wiring order** for the 3/9/3 wheel, shipped as a profile of its own so that the plain
  3/9/3 keeps working for the Simucube, Cammus and Moza wheels of the same geometry. Twenty-one
  profiles are now built and installable from the Install tab.
- **The companion pages itself.** Each of its twenty-one screens is enabled by the module switch and
  the page property together, which gives it a module to open on, a module a held button shows, and
  a next-module binding that skips what you have turned off.
- **A pit wall lists by class** on its own setting, and lends a zone a page on demand while a button
  is held, giving that zone its own page back on release.

### Changed

- **Labels are 15 px on a face and 13 px on the pit wall**, which is what every sheet draws. The row
  they sit in stays 13 px in both cases, so nothing else moved and no page sheds a field for it.
- **Zone A's cluster is cut from its column** rather than capped, the speed reads the unit the driver
  has chosen rather than always metric, and an eight-speed car no longer ghosts a ninth gear.
- **The oil and fuel warnings on the flag box are the telltales ISO 2575 registers**, an oil can and
  a fuel pump, in place of a disc that shared the meatball's silhouette and a tank outline that
  shared the limiter frame's. Nothing below the flags blinks any more, blinking being the flag
  layer's own vocabulary for a waved yellow.
- **The round faces draw a chequered ring at the density their artboards state**, which is 46 checks
  at 480 and 78 at 800, in place of a constant 24.

### Fixed

- **The class position read the size of the class.** In a multi-class field the bar showed how many
  cars were in your class rather than where you were in it.
- **The spotter lamp was lit permanently**, because the two sides were combined in a way that was
  true whenever either property was present rather than when either reported a car.
- **The five-lap average left out the newest lap**, so it was always one lap behind.
- **Zone A's speed and its unit disagreed on an imperial rig**: the value was always metric while the
  label beside it followed the driver's own setting.
- **A four-digit car number was drawn underneath the class chip** on the companion, the row having
  advanced by a fixed cell narrower than the number itself.
- **A bound unit was measured on its design-time text**, so a driver on a gallons profile lost the
  last glyph of GAL. A bound unit that declares no widest string is now refused outright.
- **The companion drew its flag band at 32 px where all three of its sheets draw 12**, which had been
  quietly shedding the three sector readings from the landscape lap times page.
- **The flag box showed nothing at all** on a sim that publishes no ignition state, neither of its
  two branches being satisfied.
- **The narrow delta page lost its bar**, which every one of the catalogue's four drawings carries.

### Development

- `global.json` pins the .NET SDK and `LangVersion` is a version rather than `latest`, so a local
  build and the runner read the same source the same way. The two together close a defect that had
  been fixed three times: on a newer compiler `array.Reverse()` binds to the in-place span overload
  rather than to LINQ, which compiles on one machine and not on the other. A test now holds the C#
  surface to the unambiguous spelling.
- The pre-commit hook moved to `.githooks/` so that it is reviewed like anything else, and it now
  refuses one of the author's drafts only while a draft of it is genuinely open somewhere, rather
  than refusing four paths for ever. Adopt it with `git config core.hooksPath .githooks`.

## 0.2.0-rc.2 (2026-09-13)

The candidate that lights the hardware around the screen, and that asks the car itself where its
shift point is rather than deriving one from the redline. Nineteen profiles for RGB strips and
brows are built beside the flag box's and published with this release, the rev bar can be switched
off entirely on a rig whose wheel already carries LEDs, and the bar, the arc and the companion's
speedo light at the four RPMs your sim publishes for the car you are driving. Besides the lights,
the pages inside the zones grow to fill the box they are given, so a narrow face no longer draws a
small table under a large empty space.

If you are on 0.1.0-rc.3, this is the first release the Update button has to offer you: 0.1.0-rc.4
and 0.2.0-rc.1 were prepared but neither was ever published, so the two sections beneath this one
describe changes that reach you here as well. The 0.2.0-rc.1 section is the one to read first,
because it replaces your dashboard with a different design rather than with a newer version of the
same one, and it says what happens to the face you have installed.

### Added

- **An RGB strip mirrors the shift lights of the car you are driving** — its LEDs, its colours, the
  order they light in, and how fast it flashes, in the gear you are in. A Porsche Cup fills from
  both ends inwards, a Next Gen stock car runs green to amber to red, a W13 finishes on a block of
  five blue, and none of that is a setting: it is the car. `Rev style` is a drop-down now, with
  `The car's own` as the default and openDash's three looks — left to right, meet in middle, F1 —
  beside it for anyone who would rather have one look in every car.
  - iRacing publishes no part of this. It publishes four RPMs per car and nothing about colour,
    order, LED count or the gear, so the pattern comes from a measured table: the open
    [Lovely Car Data](https://github.com/Lovely-Sim-Racing/lovely-car-data) project, by Lovely Sim
    Racing, ATSR and Gomez Sim Industries, under CC BY-NC-SA 4.0. openDash ships none of it and
    **fetches it once**, as one archive of every car rather than one car at a time, so nothing about
    which car you are driving leaves your machine. Everything after that works offline. Turning
    update checks off turns this off too.
  - A car with no table, a rig with no plugin, or a driver who prefers one of the three looks all
    get exactly what they got before: the ladder iRacing publishes for the car, or SimHub's bands
    for a car that publishes none. Nothing is lost by the mirror being unavailable.
  - `Car bar size` decides what happens when the car's bar and your strip are different lengths:
    fill the strip, or draw the bar at its own length in the middle of it. A bar that fills from
    both ends still does at any strip length.
  - [ADR 0018](docs/decisions/0018-the-cars-own-lights.md) records the four standing refusals this
    moved, including the one that said the plugin does not compute.
- The rev bar can be turned **off entirely**, for a wheel or DDU that already has LEDs across its
  top. `OpenDash.RevBar` carries the three states — `shift`, `rpm`, `off` — and the General row in
  the plugin panel is now a three-way choice rather than a toggle. `OpenDash.ShiftLights` stays
  attached as its deprecated alias for a release, so a settings file and a dashboard written before
  the mode existed both still say what their owner meant.
- With the rev bar off, a rectangular zone face draws a **second arrangement** rather than a hole
  where the well was: the bar rises to the top margin and the zones take the room back, which is 44
  rows of 480 on the reference face and a ninth of the nano. Both arrangements are built into the
  same package as two screens, so the setting changes the face in front of the driver with no
  reinstall. Their rectangles are derived rather than drawn and
  [docs/design/zones.md](docs/design/zones.md) §10 records what the canvas owes.
- **The shift lights are the car's own.** The bar, the arc and the companion's speedo now light at
  the four RPMs your sim publishes for the car you are driving, rather than at SimHub's idea of
  them. `Shift` reaches the bar for the first time: the point at which the sim is actually telling
  you to shift had no equivalent in the old model, whose two bands were both derived from the
  redline. A car released this morning is right with nothing to set up and no table to wait for.
  For a car that publishes no ladder, and for any sim but iRacing, the bar falls back to the bands
  you tune on SimHub's Car Settings page and looks exactly as it did. Nothing is configured either
  way: the bar asks the car, every frame. **In the last gear the top band stays lit but stops
  flashing**, because a flash asking for a shift there is an instruction that cannot be followed.
  The gear on the flag box is coloured *and flashed* by the same model, down to that last-gear
  exception, so a digit, a rev segment and an LED on a strip change colour and begin flashing on the
  same frame for the same reason.
  [ADR 0014](docs/decisions/0014-the-shift-model.md) is the whole of the reasoning, including why
  openDash mirrors thresholds and never colour.
- **Which ladder your car is on is visible.** The shift state is drawn as two layers,
  `revBar.shiftLights` and `revBar.shiftLightsSimHub`, and whichever is visible in Dash Studio is
  the one in use, so a car that lights oddly can be diagnosed without reading an expression. The
  plain RPM bar is the third layer and is unchanged; `off` is still not a layer. Nothing moved on
  the face and no setting gained a value.
- **openDash lights RGB strips as well as the flag box.** The build now writes a `.ledsprofile` for
  nineteen strip shapes beside the flag box's — wheel rims from 3/9/3 to 5/10/5, bare runs of eight
  to sixteen, and brows of nine to twenty-five — and every release carries them. Import one through
  SimHub's own LED profile import and pick it on your device; a strip wired from the far end has a
  profile of its own rather than needing rewiring. The rev ladder on a strip is built from the
  *same expressions* the rev bar is, so a strip and a screen in one rig cannot disagree about when
  a light comes on. The sides carry brake, and the run carries the flags, the pit states, a car
  alongside, ABS and traction control, DRS, low fuel and the temperature warnings, ranked the way
  the face ranks them.
- Two settings for the strips, in the plugin panel's **Lights** section. **Strip centre** chooses
  what the middle of the run shows: revs, revs with the sides left dark, brake, throttle and brake
  from the middle out, or fuel. **Rev style** chooses how the ladder fills: left to right, meeting
  in the middle, or the F1 look that flashes the whole bar. A style never changes *when* a light
  comes on, only which LED takes which rung and what colour it is, so your car's own shift points
  survive whichever look you pick.
- **A car whose shift point moves with the gear can be given a table.** `data/shift-points.json`
  overrides the published ladder for a named car, gear by gear, and a gear left out of an entry
  falls back to what the sim publishes for the car as a whole. It ships **empty**, which is
  deliberate rather than unfinished: openDash does not carry measurements it has not made, and an
  invented number puts a shift light in the wrong place with total confidence. What ships is the
  mechanism, its validator and the rules for contributing an entry, so a car somebody has actually
  measured can arrive as a reviewable pull request. A car that is not in the table gets the ladder
  iRacing publishes for it, which is right for most cars.

### Changed

- **A page fills the zone it is given.** A page's type grows one step of its density ramp at a time
  until it meets the height of its box, the width of its box or the top of the ramp, and it is
  refused a step that would leave it in a more ragged wrap than it started in. Zone B of the
  850 x 480 face drew two 34 px lap times across the top of it with most of the zone empty
  underneath, which the README capture showed plainly; that page now stacks and fills. Three pages
  moved and eighteen stayed where they were, and
  [docs/design/readability-pass.md](docs/design/readability-pass.md) records the reason for each
  one that stayed.
- **The car settings strip is drawn at the size of the bar's other values.** Slip, TC, bias and ABS
  were measured and drawn at the size of their own uppercase labels, and dimmed besides, which left
  them small and faint between RACE, LAP, POSITION and CLASS at 34 px. The canvas had said 34 px on
  the 1280 and 28 px on the 850 from the start, and the build now agrees with it. One consequence
  is visible on the two narrowest faces: brake bias needs 58 px where it needed 30, so the
  850 x 480 keeps TC and bias where it used to keep ABS as well, and the 800 x 480 keeps bias
  alone.
- Every release carries `manifest.json`, which lists the packages and the LED profiles it published
  with the size, the slot count and the rung of each. It carries a `schemaVersion` of its own, so a
  reader meeting one out in the world can tell which shape it is reading.

### Known

- **No profile openDash generates has yet been imported into a real SimHub**, and no strip, brow or
  matrix has been lit by one. They are generated against the format read out of the decompiled
  9.12.6 assemblies, their pictures are checked by tests and the whole catalogue can be driven in
  the emulator, so for the moment "it parses" is a claim about Json.NET rather than about SimHub.
  If you own any of this hardware, saying what it actually does is the most useful thing you could
  report.

## 0.2.0-rc.1 (2026-09-13)

The candidate that replaces your dashboard with a different one rather than a newer version.
The twelve-slot face becomes the zone face rc.3 published for review: a rev bar, a bar of settled
values, three zones across the body and a band along the foot, each zone holding one page at a time
out of its own catalogue and advanced with a wheel button. If you are on 0.1.0-rc.4 this is the
first release its Update button has to offer you, and pressing it changes what the dashboard is
rather than what it says.

**Your existing dashboard is copied before it is replaced.** A face OpenDash wrote and you have not
edited is zipped to `DashTemplates/<name>_backup.zip`, which the next install of the same folder
reclaims, so move it elsewhere if you want to keep it. A face you have edited in Dash Studio is
recognised and held back instead, and pressing Reinstall a second time is what replaces it; the
copy taken then is kept under a name no later update reclaims. Either way, Restore puts one back. A
folder installed by rc.3 or earlier carries no record of what OpenDash wrote, so an edit to it
cannot be told from an untouched copy and nothing can ask you; there the zipped copy is the whole
of the protection.

Nothing you have set is lost. A face's zones, bar and glance are stored against its screen size
rather than against the package that carried the name, so a zone face configured in 0.1.0-rc.4
comes up configured, and what rc.3 carried comes up on the 1920 x 480 face. The twelve-slot faces
are still built and published as `openDash slots <size>.simhubdash`, and installing one by hand
puts the old face back under a name of its own, beside the new one rather than over it.

### Changed

- **The zone faces take the shipped names.** `openDash zones 1920x480` is now `openDash` and its
  seven rectangular siblings follow, so the face the plugin installs at each rectangular size is
  the zone face. The twelve-slot faces they replace are renamed `openDash slots <size>`. They are
  still built and still published, so that the two can be compared on a rig, but the plugin no
  longer embeds them and so no longer installs them. The two round faces are untouched and keep
  their names.
- The documents a user reads describe the zone face. `README.md` and `plugin/INSTALL.md` were
  written around twelve slots and are now written around the five parts, each zone's catalogue and
  the wheel button that cycles it, which is what this release installs, and `docs/second-screens.md`
  points at that face rather than at twelve slots. `docs/architecture.md` still describes the card
  path, because that path still builds; it carries a banner saying which of the two it is about,
  and is rewritten when the path is retired.

### Added

- **The flag box: openDash now lights an 8x8 LED matrix.** A profile for the printed WS2812b box a
  lot of people have beside the screen, built by the same build as the dashboards and embedded in
  the same plugin. It shows the flag that is out, the pit state, a car alongside, low fuel, oil and
  water, and the gear underneath all of it — one picture at a time, ranked the way the face ranks
  the same conditions, coloured from the same tokens.

  **Installing it is one button.** Open the OpenDash page, scroll to **Lights**, press
  **Install into SimHub**, then pick the profile on your matrix device. openDash adds it through
  SimHub's own matrix-profile API, so SimHub writes its own settings and nothing of yours is
  touched — openDash only ever recognises its own profile. It **never installs on its own**: a
  profile paints hardware you own, so it is asked about once rather than assumed.

  When openDash updates, the button offers **Update in SimHub**. Updating replaces the copy in
  SimHub, including any changes you made to it there, so copy it under a new name first if you have
  customised it. The profile is also written to `SimHub\OpenDash\openDash Flag box.ledsprofile`,
  shown under the button, as the fallback when SimHub's matrix settings cannot be reached and as the
  thing you copy to a second machine.

  Set the matrix's **rotation and serpentine on the device in SimHub first.** They belong to SimHub
  because the right values depend on which corner your data cable enters, and if they are wrong the
  picture comes out sideways and the profile looks broken. [docs/flag-box.md](docs/flag-box.md) is
  the guide; [ADR 0013](docs/decisions/0013-lighting-hardware.md) is the reasoning.

- **The Lights page**, with brightness, a separate night brightness and a night switch for the
  whole rig, a critical-flags-only switch for drivers who want the box quiet until something
  matters, thresholds for the three warnings, and a settings group for each of SimHub's four matrix
  contents so that two boxes can do different jobs.

### Known

- The round faces are still the twelve-slot design, because what a round face does with zones is
  not decided. They are the only face at their size, so a user who has one keeps getting one.
- **No 8x8 matrix has ever been plugged into openDash's test machine.** The flag box profile is
  generated against the format read out of SimHub's own assemblies, its pictures are checked by
  tests and rendered into `build/flag-box.svg`, and the whole catalogue can be driven in the
  emulator — but nobody has yet watched it run on a real panel. If you own one, saying what it
  actually does is the most useful thing you could report.
- The flag box draws iRacing only, and several things a comparable box draws are deliberately
  absent because iRacing does not publish them: sector yellows, a virtual safety car, a countdown
  to your pit box, and the fourth spotter state. Each is listed with its reason in the guide.

## 0.1.0-rc.4 (2026-09-13)

The candidate that can tell you it is out of date, and then fix that itself. OpenDash now asks
GitHub once a day whether a newer release exists, says so in the plugin's Dashboard section, and
replaces your installed dashboards with one click when you ask it to. Nothing about you is sent,
it can be switched off, and switching it off means nothing is fetched at all rather than fetched
and discarded.

The face itself is unchanged for anyone who has one installed. The zone faces are still built for
review and still not installed, as in rc.3; they take the shipped names in 0.2.0-rc.1, which is the
section above.

### Added

- **An update check.** Once a day, the plugin asks GitHub what the newest release is and tells you
  in the Dashboard section. What is sent is a request carrying your IP address, which reaches
  GitHub and not us, and a `User-Agent` naming the product; that is the whole of it, and
  `docs/decisions/0012-update-checks.md` states it in full. There is a switch beside it.
- **One click applies it.** The Update button downloads what the release carries for the dashboards
  you actually have, checks each against the digest GitHub published, and installs them. Everything
  is in hand before anything on disk is touched, so a download that fails half way through leaves
  the machine as it was rather than half updated.
- **Your work is not overwritten.** A dashboard you have edited in Dash Studio is recognised and
  left alone, with a sentence saying so; pressing Reinstall a second time replaces it, and the copy
  taken then is kept under a name no later update reclaims. A dashboard OpenDash has never seen
  before is adopted as it is, because an edit made before OpenDash started watching cannot be told
  from an untouched folder.
- **A Restore button**, for putting back the copy an update kept.
- The generator can draw an image, which is what the telltales and the nationality flags will need.
  Nothing on a face draws one yet.

### Changed

- Release notes are this file rather than a list of pull request titles, and a tag with no entry
  here fails the release rather than publishing empty notes.
- The settings panel says which screen it is configuring and draws that screen: the portrait face
  reads as a column, and the 800 x 286 no longer offers fields for a bar it does not have.
- Every screen keeps its own zones, bar and glance, and its own wheel actions. A rig with a face on
  the wheel and another beside it used to configure them together, so cycling zone C on one moved
  zone C on the other; each face now has its own settings, named for its size. What you set in rc.3
  is carried over to the 1920 x 480 face.
- The car settings strip closes over a setting your car does not have, instead of leaving a hole
  where it would have been.
- The black flag covers band D, as the other five flags do. It used to be an outline you could read
  the page through.

### Fixed

- **Every package now carries `OFL.txt`**, the licence of the Barlow typefaces it ships. Earlier
  releases shipped the fonts without it, which the licence does not permit. The plugin zip carries
  it too, and the release publishes it.
- The tenth release candidate is no longer reported as older than the second, which would have
  offered a user a downgrade.
- The Update button goes when the update is done, rather than staying on screen and doing nothing
  when pressed.
- Closing SimHub during an update no longer leaves a dashboard folder half replaced.
- On the 1280 faces, page D6 of band D drew its last field over the DRS lamp.

### Known

- The eight zone faces are still for review and are not installed. They take the shipped names in
  0.2.0-rc.1, and updating to that release replaces your face with a different design; it will say
  so beside the Update button, and the section above says what happens to the dashboard you have.
- `README.md` and `plugin/INSTALL.md` now describe the zone face, having been rewritten for
  0.2.0-rc.1. On this release they are right about installing the plugin and wrong about the face
  it installs; the section above describes that face.

## 0.1.0-rc.3 (2026-09-12)

The candidate that fixes the font. Every numeral openDash draws has been drawn in the wrong face
since the first build, and on this one it is right, so the whole dashboard looks narrower and
better spaced than it did on rc.2. Alongside that, the plugin panel is rebuilt around the face
rather than around a list, a wheel button can be bound without leaving the page, and eight zone
faces are published for review.

Nothing a user has set is lost. The slot settings from rc.1 and rc.2 are still read, the zone
settings are added beside them, and the plugin reinstalls the dashboards over an rc.2 copy as it
did before.

### Fixed

- **The dashboard was never drawn in Barlow Condensed.** WPF reads the width word out of a family
  name and files the condensed faces under "Barlow" as a stretch, so a request for
  "Barlow Condensed" reached a face about a fifth wider than the design, on the dash face, on both
  second screens and in the plugin's own settings panel. openDash now ships that face under a name
  carrying no width word, so nothing is folded. Every value is the width the layouts were measured
  for, and the gear on the 480 round face is back to the 260 the design asks for after having been
  cut to 228 to survive the wrong font.
- At 850 px wide the car settings strip was drawn over the bar's right-hand fields.
- A driver column sized to fit the name "Toma" was being called wide enough for a name.
- Zone C drew zone B's letter, the speed was centred within its own glyphs rather than its column,
  and the upper ghost gear was a "3" where it should not have been.

### Added

- **Every rectangular face is the same five parts**: a rev bar, a bar of settled values, three
  zones across the body and a band along the foot. Each zone holds one page at a time out of its
  own catalogue, so a face is configured by choosing pages rather than by filling twelve slots.
  Eight of these are published here as `openDash zones <size>.simhubdash` for anyone who wants to
  look at one. They are **not** installed by the plugin and do not replace anything: the face you
  have stays the face you have until they take the shipped names.
- **A wheel button can be bound from the panel.** Five actions: one that advances each zone to its
  next enabled page, and one held for a glance, which shows a chosen page while the button is down
  and returns to the previous one when it is released.
- The settings panel draws a plan of the face, with each zone in its place, rather than a list of
  zone names.

### Changed

- The panel's Dashboard section, its fonts and its wordmark are drawn in the corrected face, as the
  dashboards are.
- `docs/decisions/0012-update-checks.md` records what an update check would send and how it is
  switched off, and `docs/scope.md` moves the refusal it supersedes. Nothing fetches anything yet;
  the record is the gate the code has to pass through.

### Known

- The eight zone faces are for review. A plugin that installed one would put a half-finished face
  into a dashboard list nobody asked to change, so the plugin embeds the fourteen card faces only.

## 0.1.0-rc.2 (2026-09-11)

No dashboard and no plugin logic changed since rc.1. The packages are the same drawings at a new
version string, and this candidate is the development loop and the install instructions around
them. The version is embedded in each package, so the plugin will still reinstall the dashboards
over an rc.1 copy.

### Added

- `bun run emulator` starts the synthetic iRacing telemetry emulator against the test VM, so
  cards can be seen with moving values without a copy of iRacing.
- `bun run dev` brings the whole rig up in one command: it builds the packages, deploys them to
  the VM, starts SimHub and the emulator, and leaves a screenshot in `build/dev.png`.
  [docs/dev-loop.md](docs/dev-loop.md) describes the loop and what to do when a step stalls.
- `media/` holds the screenshots that pull requests point at, with a README saying what belongs
  there.

### Changed

- The README's install section now says that `OpenDash-plugin.zip` is the only file most people
  need, since all fourteen dashboards are embedded in it. It also says which folder `OpenDash.dll`
  goes in, and why unblocking the file matters: Windows marks a downloaded DLL, .NET then refuses
  to load it, and SimHub either reports a loading error or never mentions the plugin at all.
- [docs/testing-vm.md](docs/testing-vm.md) and `CLAUDE.md` describe the new scripts rather than
  the manual steps they replace.

## 0.1.0-rc.1 (2026-09-11)

The first pre-release, and the first build that installs itself.

### Added

- Fourteen packages built from TypeScript and `design/tokens.json`: ten dash sizes from 1920x480
  down to two round faces, two companion screens and two pit wall screens, in landscape and
  portrait. Every card is a function from a slot rectangle to items, so one module description
  serves every size.
- Twenty-one companion modules and four pit wall zones, each of which shrinks or drops a field
  rather than drawing outside the box it was given.
- The SimHub plugin, which extracts the embedded dashboards on load, adds an "openDash" page to
  SimHub's left menu, and exposes each slot and mode as an `OpenDash.*` property. It compares the
  embedded version against the installed one to decide whether to reinstall.
- `packages/generator`, a typed model of SimHub's scene graph with the NCalc helpers, the
  serialiser, the validator and the package writer. It knows nothing about openDash.
- Text fitting as a test rather than a hope. SimHub hands every text box to WPF, which silently
  clips whatever does not fit, so every text of every package is measured against its box using
  advances read from the bundled fonts.
- CI that builds and tests the dashboards and the plugin on every push, and a release workflow
  that publishes both on a tag.
- `bun run vm` drives the test VM and its SimHub, and `tools/irsdk-emulator` feeds it scripted
  iRacing telemetry.
