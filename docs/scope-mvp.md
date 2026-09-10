# MVP scope

**Status:** signed off 2026-09-10.
Changes to this document require a new sign-off — it is the contract the tickets are cut from.

## Product

An open-source SimHub dashboard for iRacing, targeting a 1920×480 DDU, plus a minimal SimHub
plugin that installs and launches it. Free, MIT, with bounties and donations to follow later.

## Architecture summary

Source (TypeScript + design tokens) → generator → `.djson` → zip → `.simhubdash` → SimHub
renders it natively. See [architecture.md](architecture.md) for detail and
[decisions/](decisions/) for why.

| | |
|---|---|
| Renderer | SimHub native (DashStudio) |
| Telemetry | SimHub (user supplies their own install) |
| Target sim | iRacing |
| Target screen | 1920×480 |
| Licence | MIT |

Other sims will *probably* work, because SimHub normalises common fields into
`StatusDataBase`. They are untested and unsupported in the MVP. Do not advertise them.

## In scope

### The dash — 15 fields, all confirmed as fitting at 1920×480

| Group | Fields |
|---|---|
| Drivetrain | gear, speed, RPM bar with shift lights |
| Timing | current lap, last lap, best lap, delta to best |
| Session | position / total, lap X of Y (or time remaining) |
| Fuel | fuel remaining, laps remaining on fuel |
| Tyres | pressures, temperatures |
| Assists | TC level, ABS level |
| Alerts | flag indicator, pit limiter |

### The plugin — deliberately minimal

- Install and update the dash into SimHub
- Launch it / select display
- Nothing else

### Build infrastructure

- The `.djson` generator
- GitHub Actions: build and attach `.simhubdash` to releases on tag

## Out of scope

Explicitly deferred. Do not let these creep in:

multiple dashes · multiple screen sizes · theming and customisation · settings panel ·
stream overlay · computed telemetry (fuel prediction, stint estimates) · phone/tablet
layouts · round DDUs · licensing/activation · auto-update · any sim other than iRacing

## Gate before build starts

**Spike: prove a generated `.djson` loads in SimHub with working telemetry bindings.**
Estimated 1–2 days. Build a trivial two-element dash by hand, generate it, load it, confirm
bindings resolve.

This exists because bindings are the least-documented part of the format
(see [research/simhub-dash-format.md](research/simhub-dash-format.md#unverified)). If the
spike fails, the generator approach needs rethinking before anyone writes production code.

**No production work starts until this passes.**

## Sizes after MVP

In priority order. `1280×480` is first because it shares the MVP's height, so it is a pure
reflow and the cheapest possible test of whether the generator's size story actually holds.

1. 1280×480 (10in)
2. 1280×400 (7.8in)
3. 850×480 (5in)
4. Round DDUs
