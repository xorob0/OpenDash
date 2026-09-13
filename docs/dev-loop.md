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
bun run dev 'openDash Pit wall'               # another package
bun run dev --scenario notc                   # another scenario
bun run dev --no-build                        # when only the scenario changed
bun run dev --keep                            # leave the emulator running and the VM claimed
```

## The pieces underneath

`dev` is only the order. Each part is its own command and is worth knowing separately.

| | |
|---|---|
| [`bun run vm`](../scripts/vm.ts) | the VM and SimHub: `status`, `up`, `down`, `wait`, `install`, `plugin`, `logs`, `shot`, `claim`, `release` |
| [`bun run emulator`](../scripts/emulator.ts) | the telemetry: `start <scenario> [--follow]`, `stop`, `status`, `tail` |
| [`scripts/gui.ts`](../scripts/gui.ts) | the clicking, which is how a dashboard gets opened |

[testing-vm.md](testing-vm.md) describes the VM itself and is what to read when something in it
breaks.

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
column, the window is put where they expect it first, every other window that could take a click is
minimised, and the result is checked by asking Windows which dash windows exist. It retries once
and then tells you to open it by hand, which is enough, since everything else will already be in
place.

## What to know before changing any of this

**A command over SSH lands in session 0**, which has no desktop, so anything with a window has to
go through a scheduled task. An emulator started over SSH is invisible to SimHub and the dash
simply shows its defaults, with nothing in any log.

**SimHub reads its template list once**, at startup, so a package has to be installed before SimHub
starts rather than after.

**Bun does not deliver signals to a handler.** On 1.3.3, `process.on('SIGINT', ...)` registers a
handler that is never called, and registering it suppresses the default action, so a long running
Bun script that arms one cannot be interrupted at all. Where a clean stop matters, the trap lives
in a shell wrapper: `scripts/emulator.sh` is the example.

**GDI+ will not write to the share.** `Bitmap.Save` to `\\host.lan\Data\...` reports success and
leaves nothing behind, so a capture is saved to the guest's own disk and copied afterwards.

**A window has to be fully on screen to be photographed.** `PrintWindow` asks the window to draw
itself, which is how a dash behind another window is still captured, but the part hanging off the
screen comes back cut and looks exactly like a clipped glyph.
