# ADR 0003: Plugin settings reach the dashboard through SimHub properties

**Date:** 2026-09-10
**Status:** Accepted

## Context

The MVP plugin lets the user decide which card sits in which slot, and choose a few data modes
(position overall or in class, delta reference, session progress). Something therefore has to
change what a running dashboard shows, and there are two ways to do it.

The first is to regenerate the `.djson` from the plugin whenever a setting changes. That would
require either porting the generator to C# or running a JavaScript runtime on the user's
machine, and it would still leave the problem that SimHub offers no API to reload a dashboard
that is being displayed.

The second is to make the dashboard itself react. A SimHub plugin can attach named properties,
and any binding in a dashboard can read `[PluginClass.PropertyName]` exactly as it reads game
telemetry. Every existing package that offers configuration, DahlDesign, Lovely and Daniel
Newman Racing, works this way; the Daniel Newman dashboards, in particular, drive their
"adaptive display zones" by binding a widget's `InitialScreenIndex` to a plugin property.

## Decision

Settings are properties. The plugin attaches one property per setting under the `OpenDash`
prefix, with a documented name, type and default, and the generator emits bindings that read
those properties with a null fallback. Slots are `WidgetItem` instances pointing at a shared
cards file with one screen per card, and the slot property selects the screen.

Nothing is regenerated at runtime, and SimHub remains the only renderer, so
[ADR 0001](0001-simhub-native-rendering.md) and
[ADR 0002](0002-djson-generated-from-source.md) stand unchanged.

## Consequences

The dashboard must work without the plugin. Every expression that reads an `OpenDash` property
falls back to the default, and the standalone `.simhubdash` is a complete product with the
default layout.

The settings contract is a public interface. Property names and value sets are part of the
scope document, a validator checks that every property read by a binding is declared, and
renaming one is a breaking change for anyone who built on the dashboard.

Cards must share one size, since any card can land in any slot; a card that needs more room is
two cards.

The plugin has no rendering or telemetry responsibility, which keeps it small and keeps it out
of the 60 Hz data path.

The runtime screen switch on `InitialScreenIndex` is unverified and is item 5 of the spike. The
fallback, emitting every card in every slot with `Visible` bindings, is verified by existing
dashboards and only costs item count.
