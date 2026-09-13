# Changelog

Every release is the tag `v<VERSION>`, where `VERSION` is the one version string of the project.
The release workflow refuses a tag that does not match it. A version carrying a suffix such as
`-rc.2` publishes as a pre-release, so a candidate is never the download a first-time user is
offered.

Each release carries `OpenDash-plugin.zip`, which embeds and installs the dashboards that are ready
to install, and one `.simhubdash` per package for anyone who wants a dashboard without the plugin,
including any that the plugin does not install.

## 0.1.0-rc.4 (2026-09-13)

The candidate that can tell you it is out of date, and then fix that itself. openDash now asks
GitHub once a day whether a newer release exists, says so in the plugin's Dashboard section, and
replaces your installed dashboards with one click when you ask it to. Nothing about you is sent,
it can be switched off, and switching it off means nothing is fetched at all rather than fetched
and discarded.

The face itself is unchanged for anyone who has one installed. The zone faces are still built for
review and still not installed, as in rc.3; they take the shipped names in 0.2.0.

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
  taken then is kept under a name no later update reclaims. A dashboard openDash has never seen
  before is adopted as it is, because an edit made before openDash started watching cannot be told
  from an untouched folder.
- **A Restore button**, for putting back the copy an update kept.

### Changed

- Release notes are this file rather than a list of pull request titles, and a tag with no entry
  here fails the release rather than publishing empty notes.
- The settings panel says which screen it is configuring and draws that screen. Each screen keeps
  its own zones, bar and glance, so a rig with a face on the wheel and another beside it is set up
  apart; they used to move together.
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
  0.2.0, and updating to that release replaces your face with a different design; it will say so.
- `plugin/INSTALL.md` and `README.md` describe the twelve-slot face, which is still what this
  release installs, so they are accurate for this one and not for the next.

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
