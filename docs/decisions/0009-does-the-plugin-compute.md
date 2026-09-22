# ADR 0009: Does the plugin compute?

**Date:** 2026-09-11
**Status:** Accepted. Refines [ADR 0003](0003-plugin-settings-through-properties.md) and moves the
"computed telemetry of our own" line in [scope.md](../scope.md).

## Context

[ADR 0003](0003-plugin-settings-through-properties.md) made the plugin deliberately inert: it
installs packages, attaches settings as properties, and does nothing else. It does not render, does
not read telemetry and does not compute. [scope.md](../scope.md) restated that as a standing
refusal.

The zone face forces the question, because the catalogue draws values the refusal appears to
forbid. `ZoneCatalogue.dc.html` shows `Per lap`, `Avg 5`, `Est. laps`, `Window L14–18`,
`Last stop`, `Runs out` and `SoF` as ordinary page content, not as aspirations. A scene graph
cannot keep state across laps, so on the face of it about a third of the catalogue needs somewhere
else to live.

Answered after the pages are built, that third ships as not-available states and is then rebuilt.
So it is answered first.

## Investigation

We went and looked rather than reasoning from the refusal. SimHub 9.12.6 was decompiled and its
attached properties enumerated.

**SimHub already computes most of it.** `DataCorePlugin.Computed` publishes the whole fuel family:
`Fuel_LitersPerLap`, `Fuel_RemainingLaps`, `Fuel_RemainingTime`, `Fuel_LastLapConsumption`,
`Fuel_CurrentLapConsumption`, `Fuel_Percent`. `PersistantTrackerPlugin` publishes the delta family
and the lap history: `LiveDeltaSeconds`, `SessionBestLiveDeltaSeconds`,
`AllTimeBestLiveDeltaSeconds`, `EstimatedLapTime` on three different bases, `SessionBest`,
`AllTimeBest`, and `PreviousLap_00` through `PreviousLap_NN` with their deltas.

**The lap history is the important find.** `PreviousLap_00` … `PreviousLap_04` are five ordinary
properties. A five-lap average is therefore *arithmetic over properties that already exist*, and
NCalc can do arithmetic. It needs no state, no plugin and no memory.

That generalises. Going through the catalogue field by field:

| Field | Where it comes from |
|---|---|
| Per lap | `Computed.Fuel_LitersPerLap` |
| Est. laps | `Computed.Fuel_RemainingLaps` |
| Runs out | `Computed.Fuel_RemainingTime` |
| Last stop | `driverpitlastduration(position)` |
| Avg 5 | arithmetic over `PreviousLap_00`…`_04` |
| Delta, estimate, session best, all-time best | `PersistantTrackerPlugin` |
| Window L14–18 | arithmetic: laps remaining, tank capacity, `Fuel_LitersPerLap` |
| Fuel to energy | Le Mans Ultimate only; iRacing publishes no virtual energy |
| SoF | not published by SimHub in any form we could find |

So the third of the catalogue that looked like it needed a computing plugin is, on inspection,
**two fields**: one that only exists in a sim OpenDash does not support, and one that no plugin of
ours could compute either, because the datum is not there to compute from.

## Decision

**The plugin does not compute. The refusal stands, and it is cheap to keep.**

Where a value needs deriving, it is derived **in the expression**, from properties SimHub already
publishes. The scene graph stays a pure function of properties, the package stays complete without
the plugin — a derived value falls back exactly as a read one does — and there is no second place
where a number can be wrong.

Two consequences follow for the catalogue:

- **Fuel to energy** is drawn and marked as Le Mans Ultimate only, exactly as `energy` already is
  in the module catalogue. A page that reads what iRacing does not publish says so rather than
  drawing a zero, which is the rule `second-screens.md` already applies.
- **Strength of field** leaves the bar's catalogue of end fields until somebody finds where it
  comes from. Ten fields, not eleven. Drawing a field that can never have a value is worse than
  not offering it.

### State that is SimHub's own

One could think that the refusal above forbids every value which depends on what happened a moment
ago. In reality it forbids only a place of our own in which such a value is kept. SimHub's
`changed(ms, value)` reports that a property has moved within a window, and it is dispatched by the
engine and declared in `ncalcFunctions.ts`; the window is state, but it is SimHub's state, kept and
aged by SimHub, and an expression that asks for it is reading a property exactly as
`Fuel_RemainingLaps` is read. Thus an alert which must stand for a few seconds after a count moves,
of which the incident notice is the first, is written with `changed()` rather than waiting on
#167, and the package remains complete on its own, since a dashboard installed without the
plugin evaluates the same window.

The allowance is for reading what the host already remembers and does not extend to `setvalue` and
`getvalue`, with which an expression would keep state of its own. Those would be a computing plugin
written in NCalc, which is the thing this record refuses, and they remain unused.

## What would reopen this

An expression is not a good place for arithmetic that is long, shared between many items, or
stateful, and the first two will happen before the third.

- **When the same derivation appears in more than about three items**, it belongs behind one name.
  The first move then is a JavaScript binding (`"Interpreter": 1`, which the second screens already
  use), not the plugin: it keeps the package complete on its own, which is the property worth
  defending.
- **When something genuinely needs memory between frames** — a value that shows for three seconds
  and then stops, a rolling figure over a window SimHub does not keep — the question is whether a
  JavaScript binding can hold state between frames at all. That is #167, and its answer is the
  strongest argument this record could ever be reopened with. If a binding cannot, the plugin is
  the only place such a thing can live, and four alert tickets change shape with it.

Until one of those is true, a plugin that computes buys nothing and costs the guarantee that a
`.simhubdash` on its own is a complete product.

## Consequences

### Good

Zones B and C can be built now, and every page of the catalogue has a real source. ADR 0003 holds
unchanged, so the standalone promise holds. No number has two implementations.

### Bad

Some expressions get longer — a five-lap average is five property reads and a division. That is a
readability cost in the NCalc helpers rather than a correctness one, and `ncalcFunctions.ts` now
checks that whatever is written actually dispatches.

Two catalogue entries shrink: the bar offers ten end fields rather than eleven, and the energy
family is honestly labelled rather than quietly empty.

### Unresolved

Whether a JavaScript binding can hold state between frames (#167). The answer decides nothing
in this record but sets the terms on which it could be revisited.
