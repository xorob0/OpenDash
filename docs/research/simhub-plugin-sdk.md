# SimHub plugin SDK

**Last updated:** 2026-09-10
**Confidence:** interfaces and lifecycle verified from community SDK docs; not yet built against.

## Shape of a plugin

A SimHub plugin is a .NET DLL dropped into the SimHub install directory. It implements:

| Interface | Purpose |
|---|---|
| `IPlugin` | Base lifecycle — `Init()`, `End()` |
| `IDataPlugin` | Receives telemetry — `DataUpdate()` |
| `IWPFSettingsV2` | Provides a settings panel inside SimHub's UI |

Decorated with `[PluginName]`, `[PluginAuthor]`, `[PluginDescription]`.

```csharp
using GameReaderCommon;
using SimHub.Plugins;

public void DataUpdate(PluginManager pluginManager, ref GameData data)
{
    if (!data.GameRunning || data.NewData == null) return;
    // data.NewData is GameReaderCommon.StatusDataBase
    // e.g. data.NewData.Rpms, data.NewData.MaxRpm
}
```

## Lifecycle

- `PluginManager` is injected before `Init()`
- `Init()` runs once at startup
- **`DataUpdate()` runs every frame at 60 Hz** — keep it fast, avoid loops and allocation
- `End()` runs at shutdown

Always guard on `data.GameRunning` and `data.NewData != null`.

## Why this matters to openDash

`StatusDataBase` is SimHub's **normalised** telemetry model. Common fields are mapped across
~17 sims, which is why targeting iRacing gets partial support for other sims essentially free
— and equally why that support is untested and must not be advertised (see
[scope-mvp.md](../scope-mvp.md)).

## MVP plugin responsibilities

Deliberately minimal:

- Install and update the dash into SimHub
- Launch it, select display

The plugin renders nothing. SimHub renders the dash. If the MVP plugin ends up needing
`DataUpdate()` at all, that is a signal scope has crept.

`IDataPlugin` becomes relevant post-MVP, when computed telemetry (fuel prediction, stint
estimates) arrives — that is where a plugin genuinely earns its place, by exposing derived
properties the dash can bind to.

## Precedent

Lovely ships exactly this shape: a plugin DLL plus installer, containing a **Dashboard
Manager** that browses, installs, and updates dashes from inside SimHub. It is the pattern to
follow for MVP plugin UX.

## Sources

- [Using the SimHub SDK](https://www.simhubdash.com/community-2/projects/using-the-simhub-sdk/)
- [blekenbleu/SimHubPluginSdk](https://github.com/blekenbleu/SimHubPluginSdk) — portable SDK demo
- [SimHub wiki](https://github.com/SHWotever/SimHub/wiki)
