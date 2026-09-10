# openDash

An open-source sim racing dashboard for [SimHub](https://www.simhubdash.com/), released under
the MIT licence.

> **Status: pre-alpha.** Nothing is built yet. The MVP scope is signed off and the design is in
> progress. See [docs/scope-mvp.md](docs/scope-mvp.md).

## What makes this different

Existing SimHub dashboards ship as `.simhubdash` binaries. One cannot diff them, one cannot
review a pull request against them, and every screen size is hand-maintained as a separate
copy.

openDash treats the dashboard as compiled output. The source of truth is TypeScript together
with a set of design tokens; a generator emits the `.djson` scene graph that SimHub renders and
packs it into a `.simhubdash`. A small SimHub plugin installs that package and exposes a
handful of settings, so that the user can choose which card sits in which slot without touching
the dashboard itself.

That buys three things. Contributors can actually contribute, because a pull request is a
TypeScript diff rather than an opaque blob. A feature change reaches every screen size at once,
because cards are shared components and a size is only a layout that arranges them, whereas the
competition repeats every change by hand in each of its variants. Finally, one set of design
tokens drives the dashboard and the plugin panel, so colours cannot drift between them.

Because rendering stays native to SimHub, everything SimHub already does well is kept: HDMI
DDUs, Vocore and USBD480 USB screens, phones and tablets, and the seventeen or so sims it
reads.

## Repository layout

```
design/
  tokens.json        Design tokens, source of truth for all colour, type and spacing
  canvas/            Design system canvas artboards (Claude Design), derived from the tokens
packages/
  generator/         TypeScript library that emits SimHub .djson scene graphs
  dash/              openDash itself: cards, layouts, fonts; builds the .simhubdash
plugin/              C# SimHub plugin: installs the dashboard and exposes its settings
docs/
  scope-mvp.md       What version 1 is, and what it is not
  architecture.md    How source becomes a .simhubdash, and how a setting reaches it
  decisions/         Architecture decision records
  research/          Reverse-engineered format notes, SDK notes, competitor analysis
  design/            Brand and visual direction
```

`packages/` and `plugin/` do not exist yet; the layout above is the one the MVP builds towards.

## MVP at a glance

| | |
|---|---|
| Target sim | iRacing |
| Target screen | 1920 by 480 (8.8 in ultrawide DDU), one screen |
| Renderer | SimHub native (DashStudio `.djson`) |
| Dashboard | a fixed hero zone and twelve slots holding twelve cards |
| Plugin | installs the dashboard; settings for shift lights, position, delta, session and slot layout |
| Licence | MIT |

Deferred to post-MVP: further screen sizes, theming, idle and pit screens, stream overlay,
computed telemetry, round DDUs, and sims beyond iRacing.

## Prior art and credit

This project exists because others documented the path first.

- [Blumlaut/simhub-dashes](https://github.com/Blumlaut/simhub-dashes) proved that a
  `.simhubdash` is a zip, and that CI can build releases from source in git.
- [DahlDesignDash](https://github.com/andreasdahl1987/DahlDesignDash) runs Prettier over
  `.djson` in CI to keep diffs readable, and shows how a plugin's properties drive a dashboard.
- [Lovely Dashboard](https://github.com/Lovely-Sim-Racing/lovely-dashboard) is the reference
  for what a mature SimHub dash ecosystem looks like.

Lovely's licence explicitly forbids reuse of its UI design. openDash's visual design is
independently derived: do not copy Lovely layouts, and do not use its screenshots in any
openDash material.

## Licence

MIT, see [LICENSE](LICENSE).
