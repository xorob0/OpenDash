# How the `winvm` test VM was built, and how to fix it

Companion to [testing-vm.md](testing-vm.md) (which is about *using* it). This file is for
whoever has to troubleshoot or rebuild the VM. Everything lives in **`/opt/winvm`** on the VPS;
nothing about the VM is in this repo except the docs and `.mcp.json`.

Built 2026-09-10 by a Claude Code session. Host: Debian 13, i5-12500 (12 threads), 62 GB RAM,
KVM available, Docker 29 + Compose v5, public IP with **no host firewall** (nftables INPUT
policy accept, only Tailscale rules). Host memory was already ~41 GB used by other
containers, hence the small VM.

## Design

| Decision | Choice | Why |
|---|---|---|
| Hypervisor | `dockurr/windows` container (QEMU/KVM) | unattended install, ISO auto-download, virtio drivers, ballooning, one compose file; same pattern as the `android` emulator container already on the host |
| Windows | 10 Pro (`VERSION: "10"`) | lighter than 11, no eval expiry (LTSC/Enterprise images are 90-day evals), officially supported by SimHub |
| Resources | `CPU_CORES: 2`, `RAM_SIZE: 4G`, `DISK_SIZE: 40G`, `BALLOONING: Y`, `cpus: 3` on the container, **no `mem_limit`** | see failure #1 below for why no cgroup memory cap |
| Guest access | Win32-OpenSSH (bundled zip, not the Windows capability) + ed25519 key | works without Windows Update, deterministic |
| GUI access | QEMU's own VNC (port 5900) via `vncdotool` | screenshots/input work regardless of Windows session state, even during install |
| GUI launching | Task Scheduler with `-LogonType Interactive` | SSH sessions are session 0; scheduled tasks are the standard way to reach the interactive desktop |
| Exposure | every port published on `127.0.0.1` only | public IP, no firewall |
| MCP | Python, `mcp` 2.x SDK (`MCPServer`), stdio, registered user-wide in `~/.claude.json` | any Claude Code session as root on the host gets it |

## Files on the host

```
/opt/winvm/
├── compose.yml           container definition (ports, resources, mounts)
├── .env                  WIN_USERNAME / WIN_PASSWORD  (chmod 600)
├── README.md             short ops sheet
├── mcp.json.example      snippet for a project .mcp.json
├── storage/              data.img (sparse 40G), win10x64.iso (5.2G, cached), UEFI vars
├── shared/               ⇄ Z:\ in Windows
├── oem/                  copied to C:\OEM on first boot
│   ├── install.bat       dockur runs this at the end of setup → calls setup.ps1
│   ├── setup.ps1         power/update tweaks, OpenSSH install, SimHub silent install, 'SimHub' scheduled task
│   ├── authorized_keys   public half of ssh/id_ed25519
│   ├── OpenSSH-Win64.zip Win32-OpenSSH 10.0.0.0p2
│   └── SimHubSetup_9.12.6.exe  from github.com/SHWotever/SimHub/releases (Inno Setup)
├── ssh/id_ed25519        private key the MCP uses (root only)
└── mcp/
    ├── server.py         the MCP server
    └── .venv/            python 3.13 + mcp, vncdotool, pillow (created with uv)
```

Inside Windows: `C:\OEM\setup.log` (provisioning log), `C:\OEM\install.log`,
`C:\Program Files (x86)\SimHub\Logs\SimHub.txt`, `C:\ProgramData\ssh\` (sshd config +
`administrators_authorized_keys`).

## Build steps, in order

1. Checked prerequisites: `/dev/kvm` present, `/dev/net/tun` present, Docker running, free
   RAM/disk, no firewall (`nft list ruleset`), which ports were already taken (`ss -tlnp`).
2. `docker pull dockurr/windows`.
3. Downloaded SimHub 9.12.6 zip from GitHub, extracted the single `SimHubSetup_9.12.6.exe`
   into `oem/`. Confirmed it is Inno Setup (`grep -a -c 'Inno Setup'` > 0), so
   `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /NOCANCEL /SP-` is the silent invocation.
4. Downloaded `OpenSSH-Win64.zip` from PowerShell/Win32-OpenSSH releases into `oem/`.
5. `ssh-keygen -t ed25519` into `ssh/`, public key copied to `oem/authorized_keys`.
6. Wrote `oem/install.bat` + `oem/setup.ps1` (contents summarised in the table above; the
   script logs every step to `C:\OEM\setup.log`).
7. Wrote `compose.yml`, `docker compose up -d`. dockur downloads the ISO, builds the
   install media with an unattend file, boots QEMU, installs Windows, auto-logs-in the
   `Docker` user, runs `C:\OEM\install.bat`.
8. Wrote `mcp/server.py`, `uv venv` + `uv pip install mcp vncdotool pillow`.
9. Registered in `~/.claude.json` under `mcpServers.winvm` (backup kept as
   `~/.claude.json.bak-<timestamp>`), and in this repo's `.mcp.json`.
10. Verified end-to-end with a scripted MCP client: tool list, `vm_status`, `read_file` of
    `setup.log`, `simhub_start`, `screenshot` showing SimHub's window, `click` to dismiss the
    first-run wizard (units left at defaults, all game shortcuts left enabled).
11. Added a Windows firewall rule for 8888 and the `127.0.0.1:8888:8888` mapping so
    SimHub's web dash server is reachable from the host.

Timeline for reference: ISO download ~4 min, Windows install + first logon ~7 min, SimHub
silent install ~4 min (it also runs an msiexec step for a bundled redistributable).
Whole thing ≈ 20 minutes when nothing goes wrong.

## Failures hit during the build (and the fixes now baked in)

1. **`ERROR: Windows 10 requires at least 2.0 GB of RAM, but only 303 MB can be allocated`**
   right after the ISO was built, then the container restarted and *deleted the download*.
   Cause: the compose file had `mem_limit: 6g`; dockur's `RAM_CHECK` reads free memory from
   the cgroup, and the 5 GB ISO it had just written was sitting in page cache counted against
   that limit. Fix: no `mem_limit` (the VM cannot exceed `RAM_SIZE` + QEMU overhead anyway).
   Alternative if a cap is ever needed: `RAM_CHECK: "N"`.
2. **`SSL certificate problem: unable to get local issuer certificate`** from the container
   when asking Microsoft's download API. Harmless: dockur falls back to the ESD catalogue,
   which worked. The ISO is now cached in `storage/win10x64.iso`, so this only matters on a
   from-scratch rebuild. If the fallback ever breaks, put any Windows 10 ISO at
   `storage/` or bind-mount one as `/custom.iso`.
3. **Readiness false positives.** `docker-proxy` accepts TCP on every published port even when
   nothing is listening behind it, so "port 2222 open" meant nothing. The MCP now checks for
   the `SSH-` and `RFB ` protocol banners instead of a bare connect.
4. **`mcp` 2.x renamed the API.** `from mcp.server.fastmcp import FastMCP` is gone; it is
   `from mcp.server.mcpserver import MCPServer, Image` and errors must be raised as
   `mcp.server.mcpserver.exceptions.ToolError` or the client only sees
   "Error executing tool".
5. System Python has no `pip`/`ensurepip`; `uv` was installed to `~/.local/bin/uv` and used
   to build the venv.

## Troubleshooting

| Symptom | Check | Fix |
|---|---|---|
| `vm_status` → container not running | `docker ps -a \| grep winvm`, `docker logs --tail 50 winvm` | `cd /opt/winvm && docker compose up -d` |
| container running, `vnc_reachable` false | `docker logs winvm` for QEMU errors, `ls -l /dev/kvm` | usually still booting; KVM permission issues show in the log |
| `vnc_reachable` true, `ssh_reachable` false for > 3 min | `screenshot` — is Windows at login/OOBE/BSOD? | Windows booting takes ~90 s. If it is a fresh install, `setup.ps1` has not run yet. If sshd died: RDP in (`.env` password) and `Start-Service sshd` |
| SSH works but `Permission denied (publickey)` | key/ACL on `C:\ProgramData\ssh\administrators_authorized_keys` | re-run the OpenSSH block of `oem/setup.ps1` over RDP, or `icacls ... /inheritance:r /grant Administrators:F /grant SYSTEM:F` |
| `simhub_start` says task started but no process | `simhub_logs`, `screenshot` | SimHub may be showing a crash dialog; `run_in_desktop('C:\\Program Files (x86)\\SimHub\\SimHubWPF.exe')` as a second path; check the `SimHub` task exists: `Get-ScheduledTask SimHub` |
| GUI app launched via `run_in_desktop` not visible | `Get-Process` shows it? | Session confusion: verify the task principal is `Interactive`; `Get-ScheduledTask mcp_desktop \| Select -Expand Principal` |
| screenshot is black / lock screen | — | lock screen is disabled by policy, but if Windows locked anyway: `press_keys('ctrl-alt-del')` then `type_text(password)` `press_keys('enter')` |
| Host RAM pressure | `docker stats winvm`, `free -h` | ballooning is on; if still tight, `vm_stop` or lower `RAM_SIZE` (3G is the floor for SimHub + Win10) |
| MCP server fails to start in a session | `/opt/winvm/mcp/.venv/bin/python /opt/winvm/mcp/server.py` by hand and read stderr | reinstall deps: `~/.local/bin/uv pip install --python /opt/winvm/mcp/.venv/bin/python mcp vncdotool pillow` |
| Want to see it | tunnel `8006` and open http://localhost:8006 | noVNC has no auth; never publish it beyond 127.0.0.1 |

Quick MCP smoke test without a Claude session:

```bash
cd /opt/winvm/mcp && .venv/bin/python - <<'PY'
import asyncio
from mcp import ClientSession, StdioServerParameters
from mcp.client.stdio import stdio_client
async def main():
    async with stdio_client(StdioServerParameters(command=".venv/bin/python", args=["server.py"])) as (r, w):
        async with ClientSession(r, w) as s:
            await s.initialize()
            print((await s.call_tool("vm_status", {})).content[0].text)
asyncio.run(main())
PY
```

## Changing things

- **Ports / resources**: edit `compose.yml`, then `docker compose up -d` (recreates the
  container, which reboots Windows; the disk is untouched).
- **MCP tools**: edit `mcp/server.py`; every Claude session spawns its own server process,
  so changes apply to the next session. Syntax-check with `.venv/bin/python -c 'import ast; ast.parse(open("server.py").read())'`.
- **Guest provisioning**: `oem/setup.ps1` only runs on first boot. To apply changes to the
  existing VM, run the relevant block through `run_powershell`. To re-run everything:
  `run_powershell('C:\\OEM\\install.bat')` (the folder is re-synced from `/oem` at container start).
- **Upgrade SimHub**: drop the new `SimHubSetup_x.y.z.exe` in `oem/`, run it via
  `run_powershell` with the same silent flags, update the pinned version in
  [testing-vm.md](testing-vm.md). Do this deliberately; the dash format is version-sensitive.
- **Rebuild from scratch**: `cd /opt/winvm && docker compose down && rm -rf storage/data.img storage/windows.* storage/setup.img`
  (keep `win10x64.iso` to skip the download), then `docker compose up -d` and wait ~15 min.
  Windows will get a new machine identity; the SSH key and everything in `oem/` still apply.
- **Snapshot before risky tests**: `docker compose stop && cp --sparse=always storage/data.img storage/data.img.good && docker compose start`.
