# ADR 0001: Render natively in SimHub, not in our own React renderer

**Date:** 2026-09-10
**Status:** Accepted

## Context

Two architectures were viable for an OpenDash MVP.

Path A, SimHub native: ship a `.simhubdash` rendered by SimHub's DashStudio engine, which is
what every existing dashboard does, including both competitors.

Path B, our own renderer: a SimHub plugin in C# reads telemetry at 60 Hz, hosts a local
WebSocket and static HTTP server, and a React application renders the dash in a WebView2 window
placed on the DDU, with SimHub reduced to a telemetry source.

Path B was genuinely attractive: React, a real component model, testability, and a design
system that maps one to one from the design tool to code.

## Decision

Path A. SimHub renders, and we do not build a renderer.

## Rationale

The deciding factor was display support, which is a hard product requirement.

| | Path A | Path B |
|---|---|---|
| HDMI DDU | yes | yes |
| Vocore and USBD480 USB screens | yes | no |
| Phone and tablet | yes, through SimHub's web display | yes, it is a web application |
| Round DDUs | yes | needs work |
| Memory-mapped rendering for third-party tools | yes | no |

SimHub drives proprietary USB screens by writing pixels over USB, which a WebView2 window
cannot do. Path B would permanently exclude a real segment of DDU owners, and reaching parity
would mean re-implementing SimHub's entire display pipeline.

Path A also keeps us inside the ecosystem users already understand, since SimHub's dash
gallery, dash switching, wheel-bound navigation and screen management all keep working, whereas
Path B would put us outside all of it and require replacing each piece.

## What we gave up

React, and with it a mature component ecosystem, CSS and SVG rendering quality, and ordinary
front-end testing. Layout in DashStudio is absolute-positioned, and we accept SimHub's
rendering performance and update cadence as external dependencies.

## Why this is not as costly as it looks

The usual argument for Path B is that a `.simhubdash` is an opaque binary, so there is no
design system, no code review and no outside contribution. That turns out to be avoidable: the
package is a zip of JSON, and the JSON is generator-friendly, so source can live in git and the
blob can be compiled. That is [ADR 0002](0002-djson-generated-from-source.md), and it is what
makes this decision affordable. The two records must be read together, because Path A without a
generator would have been the wrong call: it would have killed the contribution model this
project depends on.

## Consequences

Design must work within DashStudio's node types. Positioning is absolute, and all layout logic
resolves to pixels in the generator. A SimHub version is pinned and the output re-tested on
updates. The MVP plugin renders nothing; it installs the dashboard and exposes its settings, as
[ADR 0003](0003-plugin-settings-through-properties.md) describes.
