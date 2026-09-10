# openDash

An open-source sim racing dashboard for [SimHub](https://www.simhubdash.com/). MIT licensed.

> **Status: pre-alpha.** Nothing is built yet. Scope is signed off, design is in progress.
> See [docs/scope-mvp.md](docs/scope-mvp.md).

## What makes this different

Existing SimHub dashboards ship as `.simhubdash` binaries. You can't diff them, you can't
review a pull request against them, and every screen size is hand-maintained as a separate
copy.

openDash treats the dashboard as **compiled output**. The source of truth is TypeScript plus
design tokens; a generator emits the `.djson` scene graph and packs it into a `.simhubdash`.

That buys three things:

1. **Contributors can actually contribute.** PRs are TypeScript diffs, not opaque blobs.
2. **Screen sizes are generated, not hand-built.** Competitors maintain 8+ variants by hand
   and re-do every feature change in each one. We re-run the generator at a different
   `BaseWidth`/`BaseHeight`.
3. **One set of design tokens** drives the dash, the Figma file, and the plugin UI. No drift.

Because rendering stays native to SimHub, we keep everything SimHub already does well —
HDMI DDUs, Vocore/USBD480 USB screens, phone and tablet, ~17 sims.

## Repository layout

```
design/            Design tokens (source of truth for all colour/type/spacing)
docs/
  scope-mvp.md     What v1 is and — importantly — is not
  architecture.md  How source becomes a .simhubdash
  decisions/       ADRs. Why we chose what we chose.
  research/        Reverse-engineered format notes, SDK notes, competitor analysis
  design/          Brand and visual direction
```

## MVP at a glance

| | |
|---|---|
| Target sim | iRacing |
| Target screen | 1920×480 (8.8in ultrawide DDU) |
| Renderer | SimHub native (DashStudio `.djson`) |
| Plugin | Minimum viable: install/update the dash, launch it |
| Licence | MIT |

Deferred to post-MVP: multiple dashes, multiple sizes, theming, stream overlay, computed
telemetry, round DDUs, sims beyond iRacing.

## Prior art and credit

This project exists because others documented the path first:

- **[Blumlaut/simhub-dashes](https://github.com/Blumlaut/simhub-dashes)** — proved
  `.simhubdash` is just a zip, and that CI can build releases from source in git.
- **[DahlDesignDash](https://github.com/andreasdahl1987/DahlDesignDash)** — runs Prettier
  over `.djson` in CI to keep diffs readable.
- **[Lovely Dashboard](https://github.com/Lovely-Sim-Racing/lovely-dashboard)** — the
  reference for what a mature SimHub dash ecosystem looks like.

> **Note on Lovely:** its licence explicitly forbids reuse of its UI design. openDash's
> visual design is independently derived. Do not copy Lovely layouts, and do not use its
> screenshots in any openDash material.

## Licence

MIT — see [LICENSE](LICENSE).
