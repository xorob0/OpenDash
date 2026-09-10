# ADR 0002 — Generate the `.djson` from TypeScript source

**Date:** 2026-09-10
**Status:** Accepted

## Context

[ADR 0001](0001-simhub-native-rendering.md) commits us to shipping a `.simhubdash`. The
obvious way to author one is SimHub's DashStudio visual editor, which is what every existing
dashboard project does.

That is incompatible with the goals of this project:

- **MIT open source with bounties.** Nobody can review a PR against a binary blob, and nobody
  can claim a bounty on a change no one can read.
- **Multiple screen sizes.** Competitors hand-maintain 8+ variants and repeat every feature
  change in each.
- **A real design system.** A visual editor has no concept of a design token.

## Investigation

We took a real dashboard apart before deciding. Findings in full:
[research/simhub-dash-format.md](../research/simhub-dash-format.md).

The three facts that mattered:

1. **`.simhubdash` is a zip of a folder.** Blumlaut's entire release CI is `zip -r X.simhubdash X/`.
2. **`.djson` is plain JSON with literal values** — `"FontSize": 40.0`,
   `"TextColor": "#FFF2CD11"`, `"Left": 516.0`. Trivially emittable from tokens.
3. **`BaseWidth` / `BaseHeight` are top-level properties.** Target resolution is a parameter,
   not a structural property of the file.

Fact 3 is the one that turns multi-size from a maintenance burden into a build flag.

## Decision

The `.djson` is **build output**. Source of truth is TypeScript plus `design/tokens.json`.
The generator emits the scene graph; CI zips it. Generated `.djson` and `.simhubdash` are
gitignored.

## Consequences

### Good

- PRs are TypeScript diffs
- New screen size = new generator invocation
- One token set drives dash, Figma, and plugin UI
- Assets stay as real PNG files in source and are inlined at build time — in a real sample,
  base64 images were 87.6% of the file, so this is the difference between a readable diff and
  an unreadable one

### Costs, stated plainly

- **Upfront investment.** The emitter must exist before a single pixel ships. Mitigate by
  supporting only the node types the MVP needs.
- **One-way pipeline.** DashStudio edits do not flow back and will be destroyed on next build.
  DashStudio is for preview and inspection only. This must be loud in CONTRIBUTING.
- **Undocumented format.** `$type` strings are internal SimHub class names. Pin a version.
- **Bindings are unproven.** How a property binds to live telemetry is the least-understood
  part of the format.

### The gate

Because of that last point, the MVP has a hard gate before production work:
**prove a generated `.djson` loads in SimHub with working bindings.** 1–2 days.

If the spike fails, this ADR gets revisited — the fallback is hand-authoring in DashStudio
and accepting the loss of the contribution model, which would in turn make
[ADR 0001](0001-simhub-native-rendering.md) worth reopening.

## Prior art

Neither of these generates the JSON — both stop at storing and formatting it. That gap is
where openDash's advantage lives.

- [Blumlaut/simhub-dashes](https://github.com/Blumlaut/simhub-dashes) — raw `.djson` in git, zipped by CI
- [DahlDesignDash](https://github.com/andreasdahl1987/DahlDesignDash) — Prettier over `.djson` in CI for diff readability
