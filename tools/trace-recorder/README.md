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
installed between recordings. A request names the properties to sample, the calls to evaluate, how
many frames to take and from which emulator tick to start:

```json
{
  "scenario": "green",
  "hz": 10,
  "frames": 200,
  "step": 6,
  "warmUpTicks": 7200,
  "cars": 24,
  "out": "C:\\Temp\\opendash-trace\\trace.ndjson",
  "properties": ["DataCorePlugin.GameData.Rpms", "..."],
  "calls": ["drivername(1)", "getopponentleaderboardposition_aheadbehind(-1)", "round(2.675, 2)", "..."]
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

## The calls

The opponent calls (`drivername(3)` and its family) answer from SimHub's in-memory leaderboard, not
from a property, so the plugin evaluates each text in `calls` on every frame and writes the answer
under that text, beside the properties. It does so with SimHub's own engine, the one a dashboard
binding goes through, so the answer is the dashboard's:

```csharp
// SimHub.Plugins.dll 9.12.6, read out of plugin/lib with ilspycmd
public class SimHub.Plugins.OutputPlugins.Dash.TemplatingCommon.NCalcEngineBase
    public NCalcEngineBase(bool enableParsingCache)
    public object ParseValue(ExpressionValue value, Func<string> contextInfo = null)
    public bool UseCache { get; set; }
    public bool AllowLogging { get; set; }
    public static GameData lastData;
// and SimHub.Plugins.OutputPlugins.Dash.GLCDTemplating.ExpressionValue, which converts from a string
```

`ParseValue` is an instance method, so the plugin makes one engine of its own in `Init`, and three
things about it are deliberate:

- **A fresh `ExpressionValue` per call per frame.** The value object is where the engine keeps what it
  learnt about an expression. Once `IsDeterminist` is true, `ParseValue` returns the first answer for
  ever; after one exception `Invalid` makes it answer null for thirty seconds without evaluating. No
  call recorded today is determinist (`ParameterExtractionVisitor` marks any function call or
  identifier as not), but a fresh value keeps a column from ever depending on the frame before it.
  The parse is not repeated, because the engine is built with its parsing cache, keyed by the text.
- **`UseCache = false`.** `ParseValue` otherwise keeps one answer per expression text in a static
  dictionary shared with every dashboard, cleared on each PreUpdate. The recorder has no reason to
  write into SimHub's cache or to read a dashboard's answer back out of it.
- **`AllowLogging = false`.** A call that throws, a null compared or a probe asking what NCalc does
  with a bad operand, answers null, which is what the dash draws; with logging on SimHub would write it
  to the log on every frame.

The calls are evaluated inside `DataUpdate`. The PluginManager sets the static
`NCalcEngineBase.lastData` to the update's `GameData`, raises PreUpdate, and only then calls the data
plugins, so the leaderboard a call reads is the same finished frame the properties come from.

## The file

The file it leaves is a header (`"recorder": 2`, with `cars` echoed from the request), one line per
frame carrying the properties under `v` and the calls under `c`, and a trailer naming the properties
and calls whose .NET type a JSON scalar would not carry. It is deliberately not the committed format: transposing it into
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
