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
re-record.

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
exist. It retries once and then tells you to open it by hand, which is enough, since everything
else will already be in place.

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

**Bun does not deliver signals to a handler.** On 1.3.3, `process.on('SIGINT', ...)` registers a
handler that is never called, and registering it suppresses the default action, so a long running
Bun script that arms one cannot be interrupted at all. Where a clean stop matters, the trap lives
in a shell wrapper: `scripts/emulator.sh` is the example.

**GDI+ will not write to the share.** `Bitmap.Save` to `\\host.lan\Data\...` reports success and
leaves nothing behind, so a capture is saved to the guest's own disk and copied afterwards.

**A window has to be fully on screen to be photographed.** `PrintWindow` asks the window to draw
itself, which is how a dash behind another window is still captured, but the part hanging off the
screen comes back cut and looks exactly like a clipped glyph.
