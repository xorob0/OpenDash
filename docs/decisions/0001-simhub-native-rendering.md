# ADR 0001 — Render natively in SimHub, not in our own React renderer

**Date:** 2026-09-10
**Status:** Accepted

## Context

Two viable architectures for an openDash MVP.

**Path A — SimHub native.** Ship a `.simhubdash` rendered by SimHub's DashStudio engine. This
is what every existing dashboard does, including both competitors.

**Path B — own renderer.** A SimHub plugin (C#/.NET) reads telemetry at 60 Hz, hosts a local
WebSocket and static HTTP server, and a React app renders the dash in a WebView2 window
placed on the DDU. SimHub becomes a telemetry source only.

Path B was genuinely attractive. React, a real component model, testability, and a design
system that maps 1:1 from Figma to code.

## Decision

**Path A.** SimHub renders. We do not build a renderer.

## Rationale

The deciding factor was **display support**, which is a hard product requirement:

| | Path A | Path B |
|---|---|---|
| HDMI DDU | ✅ | ✅ |
| Vocore / USBD480 USB screens | ✅ | ❌ |
| Phone / tablet | ✅ via SimHub web display | ✅ (it is a web app) |
| Round DDUs | ✅ | needs work |
| MMF rendering for third-party tools | ✅ | ❌ |

SimHub drives proprietary USB screens by writing pixels over USB. A WebView2 window cannot.
Path B would permanently exclude a real segment of DDU owners, and would require us to
re-implement SimHub's entire display pipeline to ever reach parity.

Secondary: Path A keeps us inside the ecosystem users already understand — SimHub's dash
gallery, dash-switching, wheel-bound navigation, and screen management all keep working. Path B
would put us outside all of it and require replacing each piece.

## What we gave up

React, and with it a mature component ecosystem, CSS/SVG rendering quality, and ordinary
frontend testing. Layout in DashStudio is absolute-positioned with no flow model.

We also accept SimHub's rendering performance characteristics and its update cadence as
external dependencies.

## Why this is not as costly as it looks

The usual argument *for* Path B — that `.simhubdash` is an opaque binary, so there is no
design system, no code review, and no outside contribution — turns out to be avoidable.
`.simhubdash` is a zip of JSON, and the JSON is generator-friendly. We can keep source in git
and compile the blob.

That is [ADR 0002](0002-djson-generated-from-source.md), and it is what makes this decision
affordable. **The two ADRs must be read together** — Path A without a generator would have
been the wrong call, because it would have killed the open-source contribution model this
project depends on.

## Consequences

- Design must work within DashStudio's node types
- Absolute positioning; all layout logic resolves to pixels in the generator
- Pin a SimHub version; re-test on updates
- The MVP plugin renders nothing — it installs and launches
