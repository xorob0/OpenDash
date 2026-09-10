# Competitor analysis

**Last updated:** 2026-09-10

## Correction worth recording

An early assumption in this project was that Lovely SimRacing is a standalone application
with its own telemetry ingestion, and therefore a fundamentally different architecture.

**That was wrong.** Lovely is a SimHub dashboard. So is Daniel Newman Racing. Both competitors
sit on the same substrate we do, which means the competition is on design, breadth, and
distribution — not on telemetry plumbing.

## Lovely SimRacing

[github.com/Lovely-Sim-Racing/lovely-dashboard](https://github.com/Lovely-Sim-Racing/lovely-dashboard)

A SimHub DDU dashboard plus a matching stream overlay.

**Distribution model — worth copying.** A "Lovely Plugin (DLL & Installer)" contains a
**Dashboard Manager** that lets users browse, install, and update dashes from inside SimHub.
Install flow: get SimHub → run the Lovely installer → pick dashes in Dashboard Manager →
bind wheel/button-box actions for navigation.

**Sizes shipped:** 850×480 (5in), 1280×400 (7.8in), 1280×480 (10in), 1920×480 (ultrawide),
round DDUs, plus portrait / square / XL / nano / tower variants.

**Features:** 17+ sims, TeamLINQ (real-time teammate telemetry), "True Dark Mode" with blue
light reduction, vendor integrations (e.g. Ascher Racing).

### ⚠️ Licence restriction — read this

Lovely's licence explicitly covers its **user interface design**:

> "usage of its user interface design (UI) is also subject to the license it is distributed
> under. Any use in commercial or marketing material […] is forbidden unless direct consent
> is given."

**Therefore:** openDash's design must be independently derived. Do not copy Lovely layouts or
visual language. Do not put Lovely screenshots in openDash marketing, docs, or comparison
material. This is why the design direction is Porsche GT3 R / 963 race dash rather than
"Lovely-like" — see [../design/brand.md](../design/brand.md).

## Daniel Newman Racing

Paid SimHub dashboard packs, strong presence in iRacing / ACC / LMU. Same substrate, same
distribution shape.

## Where openDash can actually win

Both competitors hand-maintain every screen size as a separate `.simhubdash`. Every feature
change is re-done by hand in 8+ variants. Neither can accept outside contributions in any
meaningful way, because the artifact is an opaque binary.

openDash's generator turns that hand-work into a build step. The advantages compound:

| | Competitors | openDash |
|---|---|---|
| New screen size | Hand-rebuild | Re-run generator |
| Feature change | Repeat in every variant | Once, in source |
| Outside contributions | Effectively impossible | Normal PR |
| Price | Paid | Free, MIT |

The risk is symmetrical and should be stated plainly: they have shipped, and we have not.
Their design maturity, sim coverage, and community are real and substantial. Our advantage
is structural and only pays off if the generator actually works — which is why the MVP has a
[spike gate](../scope-mvp.md#gate-before-build-starts).

## Naming caution

Blumlaut ships dashes named "Porsche 992 GT3" and "Porsche 992 GT3 Cup", so there is
precedent for trading on the association. Do not follow it. Design *inspired by* GT3 R visual
language is fine; using Porsche marks, logos, or model names in openDash branding is not.
