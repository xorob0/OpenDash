# ADR 0021: A dashboard never names a game

**Date:** 2026-10-14
**Status:** Accepted, on the author's ruling of 2026-10-14. Applies [ADR 0009](0009-does-the-plugin-compute.md)'s
rule to a new kind of value and keeps [ADR 0003](0003-plugin-settings-through-properties.md)'s
promise that a package without the plugin draws what it always drew. Takes up the question
[#788](https://github.com/xorob0/OpenDash/issues/788) asked and was closed on before a second sim was claimed.

## Context

1.0 claims Assetto Corsa, Assetto Corsa Competizione and Le Mans Ultimate beside iRacing
([#479](https://github.com/xorob0/OpenDash/issues/479)), and every binding OpenDash ships was written against
iRacing. The research for [#1100](https://github.com/xorob0/OpenDash/issues/1100), [docs/research/sims.md](../research/sims.md),
read SimHub 9.12.6's four readers and found that most of what the dashboards read means the same on every
game, because SimHub normalises it into `GameData`, and that the rest falls into two kinds.

The first kind is iRacing's raw telemetry, `DataCorePlugin.GameRawData.Telemetry.*`, which simply does
not exist on the other three. A binding that reads it already has an answer for that: the property is
null, and an `isnull()` either falls back to SimHub's normalised member or draws the not-available
state. Incidents, the pit service, the delta to the last lap, the steering angle and the flag bits are
this kind, and none of them needs to know which game is running to stop drawing a value it does not
have.

The second kind is a question whose answer differs by game in a way no expression can see from the
properties it already reads:

- **Whether the car has a pit limiter.** SimHub's `PitLimiterOn` reads 0 on a car without one, exactly as
  on a limiter switched off. iRacing's answer is the car's own `dcPitSpeedLimiterToggle` control being
  there; Assetto Corsa has no such control and every car has a limiter (#558).
- **Whether the limiter is on.** On Assetto Corsa SimHub fills `PitLimiterOn` with the pit lane
  (`ACManager.GD_PitLimiterOn` returns `GD_IsInPitLane`), so the member is non-null and wrong.
- **Whether the sim reports damage.** iRacing fills `CarDamage1..5` with zeros, which cannot be told from
  an undamaged car; Assetto Corsa and ACC fill them (#582).
- **Whether the driver is in the car.** iRacing's `IsOnTrack` is absent elsewhere, and each other sim says
  the same thing under another name and in another shape, Assetto Corsa's as a boxed enum (#561).

Each could be answered in the expression by asking which game is running,
`[DataCorePlugin.CurrentGame] = 'AssettoCorsa'`, and the author turned that down.

## Decision

**A dashboard never names a game.** No expression OpenDash generates reads `DataCorePlugin.CurrentGame`
or `GameData.GameName`, or compares anything with a game's code. A dashboard reads, in this order of
preference:

1. **SimHub's normalised member**, where it carries the same meaning on every claimed game.
2. **iRacing's raw property behind an `isnull()`**, where the property's absence on another sim is itself
   the answer, falling back to SimHub's member or to the not-available state.
3. **A capability the plugin publishes**, where neither can tell "not reported" from a value.

A capability is one `OpenDash.*` property per question, **named for what the dashboard asks** and not for
a game: `CarHasPitLimiter`, not `IsAssettoCorsa`. The plugin answers each from a table with one row per
game, `plugin/OpenDash/SimCapabilities.cs`, filled from the research page, and publishes the answer as
`true` or `false`, or null where it has none. The four this record starts with:

| Capability | iRacing | Assetto Corsa | ACC | Le Mans Ultimate | Without the plugin |
|---|---|---|---|---|---|
| `CarHasPitLimiter` | the car's `dcPitSpeedLimiterToggle` is there | every car | every car | `PlayerNativeTelemetry.mSpeedLimiterAvailable` | the toggle is there |
| `PitLimiterOn` | `GameData.PitLimiterOn` | raw `Physics.PitLimiterOn` | `GameData.PitLimiterOn` | `GameData.PitLimiterOn` | `GameData.PitLimiterOn` |
| `DamageReported` | no | yes | yes | no | no |
| `DriverInCar` | raw `Telemetry.IsOnTrack` | `Graphics.Status` is `AC_LIVE` | `Graphics.Status` is `AC_LIVE` | `scoringInfo.mInRealtime` | `IsOnTrack`, then the caller's answer |

**The default is iRacing's answer.** Every dashboard reads a capability through `setting.reported` in
`contract.ts`, from a helper in `second/values.ts` (`sim.carHasPitLimiter()` and its three siblings), with
the reading OpenDash made before the capability existed as its `isnull()` fallback. The plugin publishes
null with no game running and for a game its table has no row for. So a package without the plugin, a rig
between sessions and a fifth sim nobody has added all draw exactly what they drew before this record, and
on iRacing the plugin's answer and the fallback are the same reading.

**A game is added by a row and nothing else.** Its column goes into the research page first, read from
the reader's source or from a drive, then a row into the table answering every question, with a unit test
per question in `SimCapabilitiesTests.cs`, which also holds the table to the claimed sims. No dashboard
changes: a dashboard that never named iRacing has nothing to learn about the new game.

**A capability is added only for the third kind.** A question an `isnull()` over the properties already
read can answer does not get one. When one is needed, it is named for the question, answered by every row
of the table, declared on both sides of the contract and in `declared-properties.txt`, given its helper in
`values.ts` with iRacing's reading as the fallback, and added to the table above.

ADR 0009's three conditions hold. A feature needs each answer: the four tickets cited above draw a wrong
value without one. Nothing else provides it: SimHub publishes the running game's code and nothing that
says what a game can report, and its own list of what each reader cannot answer, the `[NotAvailable]`
attribute behind `DataCorePlugin.GetPropertySupportStatus`, is incomplete and in places wrong: it does not
mark Assetto Corsa's `PitLimiterOn`, which is answered with the pit lane, and it marks ACC's `ABSLevel`,
which is filled. And the package stays complete, because every read falls back on what it read before.

## Alternatives considered

**The game's name in the expression.** The smallest change, and the one turned down. Every binding that
differs would carry a nested `if` per game, a fifth sim would be an edit to every one of them, and a
dashboard would be a list of the games somebody remembered when it was written. It also hides the
question: `CurrentGame = 'AssettoCorsa'` says nothing about why a reminder shows, and `CarHasPitLimiter`
does.

**A JavaScript binding.** It moves the same `if` per game into a function, evaluated again in every item
that asks, and still names the game.

**Publishing the game itself as an OpenDash property.** The name changes and nothing else: a dashboard
comparing `OpenDash.Game` with a code names a game exactly as one comparing `CurrentGame` does.

**SimHub's `GetPropertySupportStatus` as the table.** It is a real per-game list, and it can seed a row,
but it cannot decide one, for the two reasons above. The table cites it where it agrees.

**Normalising every reading in the plugin.** Republishing SimHub's `GameData` under OpenDash's names,
corrected per game, would answer every question at once and make the plugin a second reader of every
value a dashboard draws, which ADR 0009 refuses. A capability is published only where neither SimHub's
member nor an `isnull()` can be right.

## Consequences

### Good

No dashboard names a game, and the per-game knowledge lives in one table with a test per game. The A3
tickets that wait on this (#558, #582, #561) become a change to the binding alone, and #558's "has a
limiter" is the first: the pit family asks `sim.carHasPitLimiter()`, so on ACC and Le Mans Ultimate, where
SimHub's `PitLimiterOn` is the car's limiter, the reminder to engage it can show, and on iRacing it reads
what it always read. On Assetto Corsa it cannot show yet. The family still reads SimHub's `PitLimiterOn`,
which there is the pit lane, so "in the lane with the limiter off" is never true, and every limiter
reading lights in the lane whether the limiter is on or not. That changes when the family's limiter reads
move onto `sim.pitLimiterOn()`, which is #558's.

### Bad

The plugin reads a handful of SimHub's properties by name on every frame, which is the cost of a reader
that does not know the game's types. A capability is the frame SimHub last finished, so it lags the
dashboard's own reads by one frame; nothing it answers moves faster than that matters. And a fifth sim
reads iRacing's answers until it has a row, which is what it read before this record and is no worse.

A trace carries a capability only once a package reads it. The traces carry `CarHasPitLimiter`, the one
read so far, typed as the constant `true` the plugin publishes for the emulator's car until the next
recording observes it, as `traces/README.md` records. The other three join the traces with the bindings
that read them.

### Unresolved

- **The steering angle** (#567) is not a capability yet. iRacing's angle is raw and absent elsewhere, so the
  dial can hide on another sim without one; following the wheel on Assetto Corsa needs AC's unit and sign,
  which is the author's test on a real wheel. It joins the table as a reading once that is known.
- **The flags** (#551) read iRacing's bits and fall back on SimHub's `Flag_*`, which needs no capability on
  Assetto Corsa and ACC. Le Mans Ultimate's `Flag_Black` is a retirement or a disqualification rather than a
  black flag shown on track; if #551 needs to tell that apart, it is a capability of this kind.
- **Every Assetto Corsa, ACC and LMU answer was read from SimHub's source, not from a drive.** The shapes in
  which `Graphics.Status` and `mSpeedLimiterAvailable` reach the plugin are handled in each form SimHub could
  hand them on in, and the AC shared-memory emulator (#579) is where they are first seen.
