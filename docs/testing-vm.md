# Testing against real SimHub: the `winvm` Windows VM

A Windows 10 VM with SimHub 9.12.6 runs on this host (the VPS) so agents and humans can load
generated dashes into the *real* renderer instead of guessing at the format. It is reachable
from any Claude Code session on this machine through the **`winvm` MCP server**.

How it was built and how to fix it when it breaks: [testing-vm-setup.md](testing-vm-setup.md).

## The script

Everything below is available as `bun run vm`, which is the supported way in and the one that is
checked by being run:

```bash
bun run vm status                 # container, guest SSH and VNC
bun run vm up                     # start it; `wait` blocks until the guest answers
bun run vm install 'OpenDash'     # expand built packages into DashTemplates, restart SimHub
bun run vm plugin                 # package, install OpenDash.dll, restart SimHub
bun run vm logs 80                # tail the log SimHub is writing now
bun run vm shot build/vm.png      # screenshot the display through QEMU's VNC
bun run vm bind RimCycleZoneC F7  # bind a key to an OpenDash action, restart SimHub
bun run vm unbind                 # drop every key binding bind made, restart SimHub
bun run vm claim "what for"       # there is one VM; say who has it
bun run vm release
```

Telemetry is `bun run emulator`, described in
[tools/irsdk-emulator/README.md](../tools/irsdk-emulator/README.md).

The settings panel has two commands of its own, described under *Photographing the settings panel*
and *Pressing a wheel button* below:

```bash
bun scripts/rig.ts panel          # the rig the panel is photographed on; Rim's folder deleted
bun scripts/rig.ts empty          # a first run: no settings, no copies, no OpenDash folders
bun scripts/rig.ts clear          # no screens, every other setting kept
bun run panel-shots --menu-y <y> --rig panel   # every page at every width, into build/panel/
```

Both find the VM by themselves: this machine when `/opt/winvm` is present, otherwise the SSH host
in `OPENDASH_VM_HOST`, which defaults to the host the project uses. `scripts/vm.ts` is also a library,
so a longer script can import `powershell`, `inDesktop`, `install` and the rest rather than
shelling out to the command.

The prose below explains what the script does and is what to read when it breaks.

## TL;DR for an agent

**Claim the VM before anything below touches it.** There is one guest, and the MCP tools do not
consult the lock: `click`, `type_text`, `run_in_desktop`, `simhub_start` and the rest act
immediately, whoever else is working in there. Two sessions driving the same desktop send each
other's clicks astray, and a dashboard photographed while somebody else is clicking comes back
showing the wrong thing, which is #218.

```bash
bun run vm who                    # who holds it, or nobody
bun run vm claim "what for"       # take it; refused when somebody else holds a fresh claim
bun run vm release                # give it back, which is owed even when a run failed
```

1. Call `vm_status`. If `ssh_reachable` is false, call `vm_start` then `vm_wait_ready`.
2. Put your build output where Windows can see it:
   `cp build/OpenDash.simhubdash /opt/winvm/shared/` (that folder is `Z:\` in Windows),
   or `upload_file(local_path, "C:\\Temp\\OpenDash.simhubdash")`.
3. Install it into SimHub (see *Loading a dash* below), `simhub_start`, then `screenshot`.
4. Read `simhub_logs` for binding / parse errors. Iterate.
5. `simhub_stop` when done; `vm_stop` if nobody else needs the VM (it holds ~4.5 GB RAM
   and 2 cores while running).
6. `bun run vm release`, so that the next session is not left waiting on a claim nobody is using.

The MCP server is registered user-wide (`~/.claude.json`) **and** in this repo's
[.mcp.json](../.mcp.json), so a fresh session in this directory has the tools without setup.

## What is in the VM

| | |
|---|---|
| OS | Windows 10 Pro (unactivated, en-US), user `Docker` (local admin) |
| Size | 2 vCPU, 4 GB RAM (ballooned back to host when idle), 40 GB sparse disk, 3840×2160 display |
| SimHub | 9.12.6, `C:\Program Files (x86)\SimHub\`, free (unlicensed) edition |
| SimHub state | first-run wizard dismissed, defaults kept (km/h, °C, psi, litres). No sim installed. |
| Extras | OpenSSH server, SimHub "web dash server" on guest port 8888 |
| Shared folder | host `/opt/winvm/shared`  ⇄  guest `Z:\` and the "Shared" desktop icon |

There is no iRacing in the VM. Bindings can be checked for *resolution* (does SimHub accept
the `.djson`, does the property exist, does the element render with its default/zero value),
and with SimHub's **Replay** feature if a telemetry recording is provided. Live-telemetry
checks still need a real rig.

## MCP tools

| Group | Tools | Notes |
|---|---|---|
| Lifecycle | `vm_status` `vm_start` `vm_stop` `vm_restart` `vm_wait_ready` `vm_logs` | `vm_stop` shuts Windows down cleanly and frees the RAM. |
| Commands | `run_powershell(script, timeout_seconds)` `run_cmd(command)` | Runs over SSH as the admin user in **session 0**. Anything with a window started here is invisible; use `run_in_desktop`. |
| Desktop | `run_in_desktop(command, arguments, working_dir)` | Launches into the logged-in desktop via a scheduled task. Returns immediately. |
| Files | `upload_file` `download_file` `read_file(remote_path, tail_lines)` `write_file` | SCP under the hood. Windows paths, backslashes escaped in JSON. |
| GUI | `screenshot(max_width)` `screen_size` `click(x,y,button,double)` `mouse_move` `drag` `type_text` `press_keys` | Coordinates are full-resolution (3840×2160, what `screen_size` reports). `screenshot` defaults to 1024 px wide; scale your click coordinates by 3840/1024 = 3.75 or ask for `max_width=0`. A guest at 1280×800 has lost its display mode in a container restart, and [dev-loop.md](dev-loop.md) says how `setres.ps1` puts it back. Works even while Windows is installing. |
| SimHub | `simhub_status` `simhub_start` `simhub_stop(force)` `simhub_logs(lines)` `simhub_install_plugin(local_dll_path)` | `simhub_install_plugin` copies a DLL into the SimHub folder, unblocks it and restarts SimHub. |

`press_keys` uses vncdotool names: `enter`, `tab`, `esc`, `ctrl-c`, `alt-f4`, `super`, `f5`,
`down`, `ctrl-alt-del`. Several can be sent space-separated.

## Loading a dash

A `.simhubdash` is a zip of a folder `<Name>/` containing `<Name>.djson` plus sidecars
(`.metadata`, `.ressources`, `_SHFonts/`). SimHub installs it by extracting that folder into
`C:\Program Files (x86)\SimHub\DashTemplates\<Name>\`.

Fast path, no GUI involved:

```powershell
# run_powershell
$src = 'Z:\OpenDash.simhubdash'
$dst = 'C:\Program Files (x86)\SimHub\DashTemplates'
Remove-Item "$dst\OpenDash" -Recurse -Force -ErrorAction SilentlyContinue
Expand-Archive -LiteralPath $src -DestinationPath $dst -Force
Get-ChildItem "$dst\OpenDash"
```

Then `simhub_start` (or `simhub_stop` + `simhub_start` if it was already running so the
template list refreshes), `screenshot`, navigate **Dash Studio** in the left menu, and open the
dash. Parse errors show up in `simhub_logs` immediately after the template list is scanned.

GUI path (what a user does): `run_in_desktop('C:\Temp\OpenDash.simhubdash')` triggers
SimHub's importer through the file association, then click through the prompt.

### Web renderer

SimHub serves dashboards over HTTP on guest port 8888, forwarded to the host as
`http://127.0.0.1:8888`. Once a dash is installed it can be rendered in a browser at
`http://127.0.0.1:8888/dashboard/<Name>` — useful for quick visual diffs with a headless
browser, with the caveat that the HTML renderer is not the native one the DDU uses.

### Running the plugin

Build the C# plugin, then `simhub_install_plugin('/path/to/OpenDash.dll')`. SimHub will show
the "new plugin found" activation prompt on the desktop the first time; `screenshot` and
`click` through it, or pre-activate by editing
`C:\Program Files (x86)\SimHub\PluginsData\PluginsActivation.json`.

### Reading a property value, which is how a plugin is checked without a dashboard

The left menu's **Available properties** page lists every property with its live value, and its
**Ncalc tester** button evaluates any expression against the running sim. Between them they answer
"is the plugin publishing what I think, and does the expression a profile carries actually work",
with no dashboard to open and no hardware to own — which is how the LED mirror was checked
(docs/research/iracing-led-patterns.md, "Seen working").

Two things make it usable from here:

- **`type_text` does not work on this VM.** Put the text on the desktop session's clipboard instead
  and paste it: `write_file` it to `C:\Windows\Temp\expr.txt`, then
  `run_in_desktop('powershell.exe', '-NoProfile -Command "Set-Clipboard -Value ((Get-Content -Raw
  ''C:\Windows\Temp\expr.txt'').Trim())"')`, then click the box and `press_keys('ctrl-a ctrl-v')`.
  A `run_powershell` clipboard write does not reach it: session 0 has its own.
- The result field is one line and clips a long value, so ask for the part you want rather than the
  whole of it — `left(value, 18, 9)` rather than `value`.

### Photographing the settings panel

`OPENDASH_VM_WHO=<you> bun run panel-shots --menu-y <y> --rig panel` claims the VM, installs the
plugin with its left-menu entry, seeds a rig of every kind of screen, strip and matrix
(`bun scripts/rig.ts panel`; `empty` is a genuine first run), and photographs every page at SimHub
widths of 700, 1000 and 1600 px and maximised into `build/panel/`, cropped to the panel. `--menu-y` is
where OpenDash sits in SimHub's left menu, read off a `bun run vm shot`, and has no default. The
panel's own width is measured by UI Automation and decides where each sidebar item is clicked, from a
mirror of `PanelShell` that `scripts/panel-shots.test.ts` holds to the C#; `bun run panel-shots --help`
has the rest.

The pages are the sidebar's eight: Home, Rig, Screens, LEDs, Matrix, Shortcuts, Settings and Updates.
`--pages` takes a few of them and `--scenario race` gives the live card a session. A page taller than
the panel comes back in parts, `<page>-<width>-1of2.png` and on. `build/panel/` is scratch; a capture a
document cites is copied to `media/503/`. The rig on its own, without the photographs, is
`bun scripts/rig.ts panel`, `empty` or `clear`; each restarts SimHub and expects the VM claimed
already (`bun run vm claim "panel" && bun scripts/rig.ts panel`). What to check on each
page, row by row, is [testing-panel.md](testing-panel.md).

## Pressing a wheel button

OpenDash's actions are bound to wheel buttons by a driver, on the panel's Shortcuts page, and a test
has to be able to press them. `bun run vm bind` writes SimHub's own input mappings and turns on the
keyboard reader, which ships disabled, so a binding needs no clicks in the Shortcuts page's binder:

```bash
bun run vm bind RimCycleZoneB F7 RimHoldQuickGlance F8
bun run vm unbind          # drop every OpenDash key binding again
```

An action is named after its screen's namespace: `RimCycleZoneB` is zone B on the screen called Rim
in `bun scripts/rig.ts panel`, and a bare name is OpenDash's, so it is `OpenDash.RimCycleZoneB` to
SimHub.

SimHub is restarted by both, because it reads `PluginsData/PluginManagerSettings.json` at startup.
A binding survives until it is unbound, so a capture run binds once.

Then press the key: `press(host, 'f7')` for an action that only has a press, and
`captureWhileHeld(host, 'f8', file)` for one that is held — see the gotcha about VNC releasing
held keys.

**A held action must be bound with press type `During`.** SimHub calls an action's start on press
and its end on release only for that type; every other type goes through `TriggerAction`, which
fires start and end back to back, so the page appears and vanishes in one frame. `bun run vm bind`
chooses it only for an action whose name begins with `Hold`, and a glance's name begins with its
screen's namespace, so `RimHoldQuickGlance` is bound as a press. The Shortcuts page corrects a glance
binding to `During` while it shows it: open the page once after binding, before holding the key.

## Manual access (humans)

All ports are bound to `127.0.0.1` on the VPS only; tunnel them:

```bash
ssh -L 8006:127.0.0.1:8006 -L 3389:127.0.0.1:3389 -L 8888:127.0.0.1:8888 root@<vps>
```

- Web viewer (noVNC, no auth): http://localhost:8006
- RDP: `localhost:3389`, user `Docker`, password in `/opt/winvm/.env` on the VPS
- SSH into Windows from the VPS: `ssh -i /opt/winvm/ssh/id_ed25519 -p 2222 Docker@127.0.0.1`

## Gotchas

- **Slow.** 2 cores. Give `run_powershell` generous `timeout_seconds`, wait a few seconds
  after `simhub_start` before screenshotting, and expect ~2 minutes from `vm_start` to SSH.
- **Session 0 vs desktop.** SSH commands cannot see or create windows. `Get-Process` still
  works from SSH, so status checks are fine; only *launching* GUI things needs `run_in_desktop`.
- **Dialogs block.** SimHub occasionally pops modal prompts (plugin activation, update
  notices, ShakeIt audio errors are log-only). If a screenshot shows one, click it away.
- **Don't author in DashStudio.** Anything edited there is overwritten by the next build
  (see [architecture.md](architecture.md)). Use it to inspect.
- **Guest clock is not UTC** and drifts after suspend; do not compare guest and host
  timestamps. It also drifts backwards across a restart, which makes the modification time of a
  file inside the guest useless for deciding which of two is newer. SimHub's logs are the case
  that bites: `SimHub.txt` is the one being written and `SimHub.N.txt` are rotations with N
  growing as they age, so `bun run vm logs` chooses on that rather than on a timestamp.
- **Bun does not deliver signals here.** On Bun 1.3.3 `process.on('SIGINT', ...)` registers a
  handler that is never called, and registering it suppresses the default action, so a long
  running Bun script that arms one cannot be stopped with Ctrl-C at all. Anything that has to
  clean up on an interrupt puts the trap in a shell wrapper, as `scripts/emulator.sh` does.
- **Pinned SimHub version.** 9.12.6. Do not let the VM auto-update; the format is
  undocumented and a newer SimHub is a different test target. Windows Update is disabled too.
- **A VNC client releases every held key when it disconnects.** So a key held in one call and
  photographed in the next photographs a key that is no longer down. `captureWhileHeld` in
  `scripts/gui.ts` holds, captures and releases inside one session, and is what proved the quick
  glance: without it the glance looked broken while working perfectly.
- **The track-layout offer.** Dash Studio shows a "prebuilt track layouts" band at the top of
  its page while `MapOnlineSuggestionDiscarded` in `PluginsData\Common\DashStudioSettings_2.json`
  and `UseOnlineMaps` in `PluginsData\GlobalSimhubSettings.json` are both false. "No thanks" sets
  the first in memory only, and SimHub saves it on a clean exit, which `Stop-Process -Force` never
  is. To see the offer on purpose: stop SimHub, set `"MapOnlineSuggestionDiscarded": false` in that
  file, and start SimHub. Never set `UseOnlineMaps` and never click "Enable it now": that opts the
  guest into sharing its laps.
- **Shared state.** There is one VM. If two agents test at once they will fight over SimHub.
  Check `simhub_status` / `vm_status` before assuming the desktop is yours.
