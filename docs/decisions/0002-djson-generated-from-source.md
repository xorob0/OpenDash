# ADR 0002: Generate the `.djson` from TypeScript source

**Date:** 2026-09-10
**Status:** Accepted

## Context

[ADR 0001](0001-simhub-native-rendering.md) commits us to shipping a `.simhubdash`. The obvious
way to author one is SimHub's DashStudio visual editor, which is what every existing dashboard
project does, and that is incompatible with the goals of this project. An MIT project with
bounties needs pull requests that can be reviewed, and nobody can review a binary blob or claim
a bounty on a change nobody can read. Several screen sizes are planned, whereas competitors
hand-maintain eight or more variants and repeat every feature change in each. Finally, a real
design system needs a notion of token, which a visual editor does not have.

## Investigation

We took real dashboards apart before deciding; the findings are in
[research/simhub-dash-format.md](../research/simhub-dash-format.md). Three facts mattered. A
`.simhubdash` is a zip of a folder, and Blumlaut's entire release CI is
`zip -r X.simhubdash X/`. The `.djson` is plain JSON with literal values, `"FontSize": 40.0`,
`"TextColor": "#FFF2CD11"`, `"Left": 516.0`, which are trivially emitted from tokens, and
modern exports carry no Json.NET reference tracking at all. `BaseWidth` and `BaseHeight` are
top-level properties, so the target resolution is a parameter rather than a structural property
of the file.

## Decision

The `.djson` is build output. The source of truth is TypeScript plus `design/tokens.json`; the
generator emits the scene graph and CI zips it. Generated `.djson` and `.simhubdash` files are
gitignored.

## Consequences

### Good

Pull requests are TypeScript diffs. A new screen size is a new layout function over the same
cards. One token set drives the dashboard and the plugin panel. Images, should the design ever
need any, stay as real files in source and are packed into the `.ressources` zip at build time,
which keeps diffs readable; in an older sample that inlined them as base64, images were 87.6
percent of the file.

### Costs, stated plainly

The emitter must exist before a single pixel ships, which is mitigated by supporting only the
node types the MVP needs. The pipeline is one-way: DashStudio edits do not flow back and are
destroyed on the next build, so DashStudio is for preview and inspection only, and this must be
loud in CONTRIBUTING. The format is undocumented, `$type` strings are internal SimHub class
names, and a version has to be pinned. Bindings were the least understood part of the format
when this record was first written; they have since been verified against real exports, and the
remaining unknowns are listed in the research note and covered by the spike.

### The gate

The MVP has a hard gate before production work: prove that a generated `.djson` loads in SimHub
with working bindings, a plugin property and a switching slot, in two to three days. If the
spike fails on loading or on bindings, this record is revisited; the fallback is hand-authoring
in DashStudio and accepting the loss of the contribution model, which would in turn make
[ADR 0001](0001-simhub-native-rendering.md) worth reopening.

## Prior art

Neither [Blumlaut/simhub-dashes](https://github.com/Blumlaut/simhub-dashes), which keeps raw
`.djson` in git and zips it in CI, nor
[DahlDesignDash](https://github.com/andreasdahl1987/DahlDesignDash), which runs Prettier over
`.djson` in CI for diff readability, generates the JSON. Both stop at storing and formatting
it, and that gap is where openDash's advantage lives.
