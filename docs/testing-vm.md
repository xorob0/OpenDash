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
bun run vm install 'openDash'     # expand built packages into DashTemplates, restart SimHub
bun run vm plugin                 # package, install OpenDash.dll, restart SimHub
bun run vm logs 80                # tail the log SimHub is writing now
bun run vm shot build/vm.png      # screenshot the display through QEMU's VNC
bun run vm claim "what for"       # there is one VM; say who has it
bun run vm release
```

Telemetry is `bun run emulator`, described in
[tools/irsdk-emulator/README.md](../tools/irsdk-emulator/README.md).

Both find the VM by themselves: this machine when `/opt/winvm` is present, otherwise the SSH host
in `OPENDASH_VM_HOST`, which defaults to the host the project uses. `scripts/vm.ts` is also a library,
so a longer script can import `powershell`, `inDesktop`, `install` and the rest rather than
shelling out to the command.

The prose below explains what the script does and is what to read when it breaks.

## TL;DR for an agent

1. Call `vm_status`. If `ssh_reachable` is false, call `vm_start` then `vm_wait_ready`.
2. Put your build output where Windows can see it:
   `cp build/openDash.simhubdash /opt/winvm/shared/` (that folder is `Z:\` in Windows),
   or `upload_file(local_path, "C:\\Temp\\openDash.simhubdash")`.
3. Install it into SimHub (see *Loading a dash* below), `simhub_start`, then `screenshot`.
4. Read `simhub_logs` for binding / parse errors. Iterate.
5. `simhub_stop` when done; `vm_stop` if nobody else needs the VM (it holds ~4.5 GB RAM
   and 2 cores while running).

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
| GUI | `screenshot(max_width)` `screen_size` `click(x,y,button,double)` `mouse_move` `drag` `type_text` `press_keys` | Coordinates are full-resolution (1280×800). `screenshot` defaults to 1024 px wide; scale your click coordinates by 1280/1024 = 1.25 or ask for `max_width=0`. Works even while Windows is installing. |
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
$src = 'Z:\openDash.simhubdash'
$dst = 'C:\Program Files (x86)\SimHub\DashTemplates'
Remove-Item "$dst\openDash" -Recurse -Force -ErrorAction SilentlyContinue
Expand-Archive -LiteralPath $src -DestinationPath $dst -Force
Get-ChildItem "$dst\openDash"
```

Then `simhub_start` (or `simhub_stop` + `simhub_start` if it was already running so the
template list refreshes), `screenshot`, navigate **Dash Studio** in the left menu, and open the
dash. Parse errors show up in `simhub_logs` immediately after the template list is scanned.

GUI path (what a user does): `run_in_desktop('C:\Temp\openDash.simhubdash')` triggers
SimHub's importer through the file association, then click through the prompt.

### Web renderer

SimHub serves dashboards over HTTP on guest port 8888, forwarded to the host as
`http://127.0.0.1:8888`. Once a dash is installed it can be rendered in a browser at
`http://127.0.0.1:8888/dashboard/<Name>` — useful for quick visual diffs with a headless
browser, with the caveat that the HTML renderer is not the native one the DDU uses.

### Running the plugin

Build the C# plugin, then `simhub_install_plugin('/path/to/openDash.dll')`. SimHub will show
the "new plugin found" activation prompt on the desktop the first time; `screenshot` and
`click` through it, or pre-activate by editing
`C:\Program Files (x86)\SimHub\PluginsData\PluginsActivation.json`.

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
- **Shared state.** There is one VM. If two agents test at once they will fight over SimHub.
  Check `simhub_status` / `vm_status` before assuming the desktop is yours.
