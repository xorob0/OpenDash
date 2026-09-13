# Changelog

Every release is the tag `v<VERSION>`, where `VERSION` is the one version string of the project.
The release workflow refuses a tag that does not match it. A version carrying a suffix such as
`-rc.2` publishes as a pre-release, so a candidate is never the download a first-time user is
offered.

Each release carries `OpenDash-plugin.zip`, which embeds and installs the dashboards that are ready
to install, and one `.simhubdash` per package for anyone who wants a dashboard without the plugin,
including any that the plugin does not install.

## Unreleased

### Added

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
