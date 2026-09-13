# trace-recorder

A SimHub plugin that writes down what SimHub sees, one frame at a time, so that a scenario can be
replayed anywhere afterwards. It is a development tool and is never shipped: `bun run record` builds
it, installs it into SimHub on the Windows test VM for the length of one recording and takes it off
again.

## Why it has to be a plugin

The dashboards read `DataCorePlugin.GameData.*`, `DataCorePlugin.Computed.*` and the properties the
other plugins publish. All of those are SimHub's normalised view of the sim rather than the irsdk
variables `tools/irsdk-emulator` writes into the shared memory, and the mapping between the two is
SimHub's own code. Reimplementing enough of it to produce a trace off the VM would be a second
implementation of somebody else's undocumented one, and it would be wrong in ways no test here could
see. Asking a running SimHub is therefore the only truthful answer, and a plugin is how a program
asks SimHub anything.

## What it does

`Init` reads `opendash-trace-request.json` from SimHub's own directory, where the caller leaves it.
No request means the plugin loads and does nothing at all, which is what makes it safe to leave
installed between recordings. A request names the properties to sample, how many frames to take and
from which emulator tick to start:

```json
{
  "scenario": "green",
  "hz": 10,
  "frames": 200,
  "step": 6,
  "warmUpTicks": 7200,
  "out": "C:\\Temp\\opendash-trace\\trace.ndjson",
  "properties": ["DataCorePlugin.GameData.Rpms", "..."]
}
```

`DataUpdate` then samples on `DataCorePlugin.GameRawData.Telemetry.SessionTick` rather than on the
clock. The emulator is a function of its tick, so a frame keyed to a given tick holds the same
telemetry on every run, and two recordings of one unchanged scenario cover the same span of it. What
still moves between them is what SimHub keeps its own history of, which is the lap deltas and the
fuel averages, together with the wall clock it publishes.

`warmUpTicks` is counted from the first tick the recorder sees with the game running, not from tick
zero. The emulator's `SessionTick` starts at the scenario's own session time, above seventy thousand
in a race half run, so a request naming an absolute tick would fire on the first sample of every
scenario and warm nothing up.

The file it leaves is a header, one line per frame, and a trailer naming the properties whose .NET
type a JSON scalar would not carry. It is deliberately not the committed format: transposing it into
the columnar trace happens in `scripts/record.ts`, where it is tested. Once the last frame is
written the plugin writes `<out>.done`, which is what the caller waits on; a run that ends without
that marker was interrupted and is reported rather than committed.

## Build

```bash
export PATH="$HOME/.dotnet:$PATH"; export DOTNET_ROOT="$HOME/.dotnet"
dotnet build tools/trace-recorder -c Release
```

net48 with no XAML, against the SimHub assemblies in `plugin/lib`, so it cross-builds from Linux and
macOS exactly as the shipped plugin does. `bun run record` builds it for you; this is for when the
build itself is what has broken.

## Installing it by hand

`bun run record` does all of the below, and doing it by hand is only worth it when that command is
what you are debugging. The DLL goes beside `SimHubWPF.exe` in `C:\Program Files (x86)\SimHub`, it
has to be unblocked because Windows marks a file that arrived over a share, and it has to be named
in `PluginsData\PluginsActivation.json` before SimHub starts. Without that last step SimHub shows a
modal "new plugin found" prompt that blocks the desktop until somebody clicks it.

That file is a flat JSON array of `{ClassName, IsEnabled, ShowInMainMenu, ShowInMainMenuPosition}`,
keyed by the plugin type's full name, which here is `OpenDashTraceRecorder.TraceRecorderPlugin`. Do
not edit it with PowerShell's `ConvertTo-Json`: handed an array, PowerShell 5.1 wraps it in
`{"value": [...], "Count": n}`, and SimHub then reads one plugin with no class name and dies at
startup with a NullReferenceException before it draws anything. `activatePlugin` in `scripts/vm.ts`
edits it through a real JSON parser instead. Should it happen anyway, SimHub keeps copies under
`PluginsData\_Backups`.
