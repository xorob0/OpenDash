# Architecture

## The pipeline

```
design/tokens.json ──┬──> tokens.css ──> design renders (review only)
                     ├──> Figma variables
                     └──> generator
                                │
src/components/*.ts  ───────────┤   gauge() readout() bar() indicator()
src/layouts/*.ts     ───────────┤   compose at a given BaseWidth/BaseHeight
assets/*.png         ───────────┤   inlined as base64 at build time
                                ▼
                        build/openDash.djson
                                │  + sidecar files, + _SHFonts/
                                ▼
                     zip -r openDash.simhubdash openDash/
                                │
                                ▼
                          SimHub renders it
                     HDMI DDU · USB screens · phone/tablet
```

Contributors write and review TypeScript. The `.djson` is build output and is gitignored.

## Why this shape

Three properties fall out of it, and all three are things competitors cannot easily match:

**Diffable.** A PR shows a TypeScript change, not a 2.4 MB JSON blob.

**Multi-size nearly free.** Layouts are functions of the target dimensions. Adding a size is
re-running the generator, not rebuilding a dash by hand. Competitors maintain 8+ variants
manually and re-do every feature change in each.

**One source of truth for design.** `tokens.json` feeds the dash, Figma, and the plugin UI.
Colours cannot drift between them because there is only one place they exist.

## Components

### Generator (`src/`, TypeScript)

Emits the SimHub `.djson` scene graph. Responsibilities:

- Compose component functions into an absolutely-positioned scene graph
- Resolve design tokens to literal values (`#AARRGGBB`, font names, pixel sizes)
- Inline `assets/*.png` as base64 at build time — **kept as real files in source**, which is
  what makes our diffs readable. In a real sample dash, base64 images were 87.6% of the file.
- Assign Json.NET `$id` values consistently
- Emit sidecar files (`.metadata`, `.ressources`) and bundle `_SHFonts/`

Format details: [research/simhub-dash-format.md](research/simhub-dash-format.md).

### Plugin (`plugin/`, C# / .NET)

A standard SimHub plugin implementing `IPlugin`, `IDataPlugin`, `IWPFSettingsV2`.

MVP responsibility is only: install/update the dash, launch it, select display.

The plugin does **not** render anything — SimHub does. Details:
[research/simhub-plugin-sdk.md](research/simhub-plugin-sdk.md).

### Design (`design/`)

`tokens.json` is the source of truth. Everything else is generated from it.

## Constraints this imposes

**The pipeline is one-way.** Edits made in SimHub's DashStudio do not flow back to source and
**will be destroyed** on the next build. DashStudio is for previewing and inspecting only,
never for authoring. This needs to be stated loudly in CONTRIBUTING when that exists.

**Layout is absolute.** The `.djson` scene graph positions everything by `Left`/`Top`/
`Width`/`Height`. There is no flow layout in SimHub. Any layout logic — stacking, alignment,
distribution — lives in the generator and is resolved to absolute pixels before emit.

**Fonts are redistributed.** SimHub bundles fonts into `_SHFonts/` inside the dash package,
so shipping a dash means shipping the font file. Every font must be OFL, MIT, or similar.
This is why the tokens specify Barlow / Barlow Condensed (SIL OFL 1.1).

**The format is undocumented.** `$type` strings are internal SimHub class names. Pin a SimHub
version, and re-test on SimHub updates.
