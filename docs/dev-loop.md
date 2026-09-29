# The development loop

One command takes a clean machine to a dash rendering live telemetry:

```bash
bun run dev
```

It claims the VM, starts it if it is down, builds the packages, installs the one asked for,
restarts SimHub, starts the telemetry emulator, opens the dashboard and photographs it into
`build/dev.png`. Two and a half minutes from cold, most of which is the VM.

```bash
bun run dev                                   # OpenDash on the race scenario
bun run dev 'OpenDash Pit wall'               # another package
bun run dev --scenario notc                   # another scenario
bun run dev --no-build                        # when only the scenario changed
bun run dev --keep                            # leave the emulator running and the VM claimed
bun run dev --scenario flagbox                # walk every state the flag box draws
bun run dev --scenario nosession               # the game running with no session, for #406's notices
bun run dev --scenario untimed                 # a lap race with no clock: #387's laps form, #439's mark
```

## The pieces underneath

`dev` is only the order. Each part is its own command and is worth knowing separately.

| | |
|---|---|
| [`bun run vm`](../scripts/vm.ts) | the VM and SimHub: `status`, `up`, `down`, `wait`, `install`, `plugin`, `logs`, `shot`, `claim`, `release` |
| [`bun run emulator`](../scripts/emulator.ts) | the telemetry: `start <scenario> [--follow]`, `stop`, `status`, `tail` |
| [`scripts/gui.ts`](../scripts/gui.ts) | the clicking, which is how a dashboard gets opened |
| [`bun run record`](../scripts/record.ts) | the telemetry traces: one recording of a scenario, committed under `traces/` |
| [`bun run shots`](../scripts/shots.ts) | several packages photographed on one claim, into `build/shots/` |
| [`bun run previews`](../scripts/previews.ts) | the same captures, scaled and committed as the thumbnails SimHub's dashboard list draws |

[testing-vm.md](testing-vm.md) describes the VM itself and is what to read when something in it
breaks.

## Recording a trace is the one thing that has to happen here

Everything else in this loop is a convenience, since a dashboard can be read in the JSON and
measured by the tests. A telemetry trace cannot: the values a dashboard reads are SimHub's
normalised view of the sim rather than the variables the emulator writes, and only a running SimHub
knows the mapping. So `bun run record` runs each scenario past a recorder plugin once and commits
what SimHub saw, and the preview renderer, the per-pull-request video and the goldens replay that
file instead of claiming the VM. [traces/README.md](../traces/README.md) is the format and when to
re-record -- including the header's `asserted` list, which names any column of a trace that was
written by hand rather than observed, and which the next re-record of that scenario removes.

## Recording a clip is the same loop with a recorder in place of the camera

```bash
bun run clips                                  # the base face, the companion and the pit wall
bun run clips --packages 'OpenDash 1280x480'   # one more
bun run clips --encode-only                    # encode build/clips again without the VM
```

`bun run shots` photographs; `bun run clips` films. The loop is the same, one claim and one install,
and per package it opens the dashboard, places it, and runs a C# loop in the desktop session that
`PrintWindow`s the window on a fixed cadence, twenty times a second for a face and fifteen for the
pit wall. The frames go to the guest's disk as raw pixels, because a PNG per frame cannot keep that
rate, and come back to the host as one file. **ffmpeg on the host** (`apt-get install ffmpeg`) turns
them into a webm, an mp4 and a poster; the guest never encodes. Six seconds is the default because
it is one period of the emulator's rev sweep, so the loop seam is quiet. Every clip carries a
`record.json` with the version, commit, scenario and the rate actually achieved, and
`site/scripts/sync-clips.ts` moves the result into the site with that provenance.

## There is one VM

`dev` claims it and refuses when somebody else holds the claim, because two sessions driving one
SimHub produce confusing results rather than an error. A claim older than ninety minutes counts as
abandoned, since a session that dies never releases one. `bun run vm who` says who has it and
`bun run vm release` gives it back.

## Why opening the dashboard is clicked

SimHub offers no way to open a dashboard in a window except from its Dash Studio page: no command
line argument, no setting, and `SaveAndRestoreOppenedDashboards` does not bring a windowed dash
back after a restart, which was measured rather than assumed. So that one step drives the mouse
over VNC.

It is the fragile part of the loop and it is treated as such. The coordinates are a fixed offset
from the window's top-left for the menu and a fraction of the screen width for the centred content
column, the window is **waited for** and then put where they expect it, every other window that
could take a click is minimised, and the result is checked by asking Windows which dash windows
exist.

Since #308 it also **looks before it clicks**, at a few pixels read over the same VNC. After "No
thanks" it checks SimHub's track-layout offer went, clicks it again if not, and works 61 px lower
under one that will not go. Once the filter is in it reads where the list's cards are rather than
assuming where the first row starts, and it rests the pointer on the row and checks it lit before
pressing Start. When nothing opens, the first line says what the looks saw -- no row in the list, a
row that would not light, the offer, a dashboard opened from a list the filter never narrowed, or
SimHub gone, which is #303 -- and `build/vm-open-<dashboard>.png` is the screen as it gave up. Then
it tells you to open it by hand, which is enough, since everything else will already be in place.

## The flag box, which has no hardware

`bun run dev` ends in a screenshot of a dashboard. The 8x8 flag box has no equivalent and cannot
have the same one: **no matrix is plugged into the VM, and CI owns no hardware at all.**
[scope.md](scope.md)'s definition of done states that as an exception rather than leaving it
implied, and this is what stands in for it.

```bash
bun run build                                  # writes the profile and the contact sheet
bun run dev --scenario flagbox                 # drives every state the box can draw, on a loop
```

**The contact sheet is the check that needs nothing.** `build/flag-box.svg` renders every glyph
from the same functions the profile is built from, so a change to the chequered flag shows the
chequered flag in the pull request. `packages/dash/test/glyphFit.test.ts` is the matrix's
`textFit`: sixty-four cells, tokens rather than literals, nothing invisible at low brightness,
nothing a lamp at night, and no two pictures that differ only in hue.

**The `flagbox` scenario is the check that needs SimHub.** It walks the catalogue in priority
order — fifteen flags, the pit family, the spotter on each side, the three warnings, then a gear
sweep through the redline — six seconds apart, in a fixed order, and loops after 156 s, so two
runs are comparable and the whole thing can be watched twice without restarting. The emulator's
`--selfcheck` runs it in memory on Linux and is part of what CI checks:

```bash
export PATH="$HOME/.dotnet:$PATH"; export DOTNET_ROOT="$HOME/.dotnet"
cd tools/irsdk-emulator
dotnet devcheck/bin/Release/net8.0/IrsdkEmulator.dll --selfcheck flagbox
```

**What is still missing, and it is the important part.** Three of this loop's steps have not been
performed:

* **Nothing has opened SimHub's own matrix preview.** SimHub's LED profile editor previews a
  matrix on screen, and if that preview is faithful it is the whole answer for everyone without a
  box. Whether `bun run dev`'s scripted clicking can reach it has not been tried; the traps in
  this file apply unchanged, and the easier case -- `dev` opening a *dashboard* -- took #303 to
  get right.
* **The profile has not been loaded in real SimHub.** It is generated against the format read out
  of the decompiled 9.12.6 assemblies. Until somebody imports it, "it parses" is a claim about
  Json.NET rather than about SimHub.
* **Nobody has compared the sheet to a real panel.** Brightness and diffusion are exactly where a
  preview lies. When somebody who owns an iFlag checks it, that result belongs in
  [design/flag-box.md](design/flag-box.md) whichever way it comes out.

Until those three are done, every "seen on the VM" line on the `iflag` tickets is an aspiration,
and this section exists so that it is a recorded one.

## What to know before changing any of this

**A command over SSH lands in session 0**, which has no desktop, so anything with a window has to
go through a scheduled task. An emulator started over SSH is invisible to SimHub and the dash
simply shows its defaults, with nothing in any log.

**With no emulator running, what you are looking at is the idle screen**, not a face with no data in
it: since #113 every package carries one and SimHub switches to it whenever `GameRunning` is false.
The wordmark and a clock on screen therefore mean the telemetry never arrived, which is a clearer
symptom than the old one and a surprise if you were expecting the face.

That sentence was read off the decompiled 9.12.6 screen selection that
[research/simhub-dash-format.md](research/simhub-dash-format.md) records for as long as #113 was
open, and it has now been watched. On 2026-09-27, at 8d748ac, `OpenDash 850x480` and `OpenDash
Companion` both switched to the idle screen with SimHub running and no game, and both came back to
the racing face when the emulator started -- the way back matters as much as the way in, since a
display that shows the idle screen and stays on it is the same bug the other way round. The
wordmark's "open" draws correctly in Light, which was the one weight a package ships that WPF might
have had to synthesise and the one case the advances cannot predict.

**The neighbouring state is not this one.** A game running with no session named keeps the racing
face and fills the timing pages with `… · Go into a session` (#406); the wordmark and the clock mean
no game at all. `bun run dev --scenario nosession` is how to reach the first, and stopping the
emulator is how to reach the second.

**SimHub reads its template list once**, at startup, so a package has to be installed before SimHub
starts rather than after.

**SimHub's process exists long before its window does.** `bun run vm install` reports "started" as
soon as `SimHubWPF.exe` is running, which is about a second after launch; the window you can click
arrives twenty-five to fifty seconds later. The gap is not empty, which is the trap: for three
seconds the process has no main window at all, and then for twenty seconds its main window is the
splash, a 540x320 panel that answers "are you maximised" with yes and stretches to 3840x320 when
you maximise it. A script that maximised once and started clicking was aiming full-screen
coordinates at a 320 pixel strip. `maximiseSimHub` in [gui.ts](../scripts/gui.ts) is what waits,
and it accepts nothing but a rectangle covering the desktop's working area.

**The guest's display mode is part of the loop, and a container restart loses it.** `MENU` and
`LIST` in [gui.ts](../scripts/gui.ts) are absolute pixels measured at 3840x2160; only the centred
content column is a fraction of the width. On 2026-09-27 the VM came back at 1280x800 after a
container restart and `openDashboard` clicked into empty space twice, reporting nothing but "could
not be opened" — which sends the reader after coordinates that were right all along. The mode is now
read as soon as the guest answers and before anything is built: `dev`, `shots`, `clips` and `modules`
all ask `guiProblem` there, `openDashboard` asks again at the click, and both refuse, name the mode
the guest is in and the one they need, and point at `/opt/winvm/shared/setres.ps1`, which has to be
run in the interactive session because that is whose display it is. The coordinates themselves were left alone;
a fraction of the height would be a guess at a page nobody has measured at a second mode, and a guess
opens the wrong dashboard instead of saying so.

**SimHub's track-layout offer comes back after every restart.** Dash Studio offers prebuilt track
layouts in a band at the top of its page, and the band pushes the search box and every row 61 px
down. "No thanks" is only remembered: SimHub keeps it in memory as `MapOnlineSuggestionDiscarded`
and writes it to `PluginsData\Common\DashStudioSettings_2.json` when it exits cleanly, and `bun run
vm` restarts SimHub by killing it. So every install brings the offer back, and a script that clicks
it away blind cannot tell a click that took from one that missed. `openDashboard` looks (#308), and
[testing-vm.md](testing-vm.md) has how to bring the offer back on purpose to test against it.

**Bun does not deliver signals to a handler.** On 1.3.3, `process.on('SIGINT', ...)` registers a
handler that is never called, and registering it suppresses the default action, so a long running
Bun script that arms one cannot be interrupted at all. Where a clean stop matters, the trap lives
in a shell wrapper: `scripts/emulator.sh` is the example.

**GDI+ will not write to the share.** `Bitmap.Save` to `\\host.lan\Data\...` reports success and
leaves nothing behind, so a capture is saved to the guest's own disk and copied afterwards.

**A window has to be fully on screen to be photographed.** `PrintWindow` asks the window to draw
itself, which is how a dash behind another window is still captured, but the part hanging off the
screen comes back cut and looks exactly like a clipped glyph.

**The bar's race clock in a lap-counted race is fixed, and the `untimed` scenario is where to see
it.** It used to be the one clipped glyph on the VM that was ours: `zones/bar.ts` bound both
`raceTime` and `timeLeft` to `clock(sessionTimeLeft())` with no guard, where the session card, the
session module, the pit wall header and the pit wall's own time field all had one — and `raceTime` is
the default of the bar's first slot. iRacing publishes a week of time left for a session that has no
clock, so the field drew `168:00:00`, seven digit cells in a budget of six, and WPF took the last
glyph off it on all 22 packages. Fixed in #439: every surface now reads `sessionClock()` and draws
`∞` where the session has no clock, `-:--:--` only where the clock is at or below zero. So on
`untimed` the bar reads `RACE ∞` and nothing is clipped; a clipped clock there is a regression, and a
clipped anything else is most likely the off-screen window above. The two surfaces that follow
`SessionProgress`, the session module and the session card, draw the lap on `untimed` under the
default `auto` and show the mark only when the setting is forced to `time`; and no scenario publishes
a zero clock, so `-:--:--` is not a thing to look for on the VM at all.
