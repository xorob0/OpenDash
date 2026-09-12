# Icons and telltales

**Last updated:** 2026-09-11
**Status:** library chosen, not yet wired into the build.

openDash needs two different kinds of small graphic, and conflating them is the mistake to avoid.

A **user-interface icon** belongs to the plugin panel. It is stroked, 1.5 px, square caps, mitre
joins, and it sits beside a control. There are eight of them and they are drawn by hand in
`design/canvas`, because eight is not worth a dependency.

A **telltale** belongs to the dash face. It is a filled pictogram, it is the thing every road car
and every race car puts in front of a driver, and it follows ISO 2575, which fixes both the shape
and the colour: blue for the high beam, green for a system operating as intended, amber for a
caution, red for a danger. There are several dozen of them, they are conventional rather than
inventable, and drawing them by hand would be both slow and wrong.

## The library

**Material Design Icons**, by Pictogrammers, under the Apache Licence 2.0.

- 7,447 icons, of which the `car-*` family alone covers what a sim dashboard needs:
  `car-light-high`, `car-light-dimmed`, `car-light-fog`, `car-parking-lights`, `car-brake-abs`,
  `car-brake-alert`, `car-brake-parking`, `car-brake-temperature`, `car-brake-fluid-level`,
  `car-traction-control`, `car-esp`, `car-cruise-control`, `car-speed-limiter`, `car-tire-alert`,
  `car-battery`, `car-electric`, `car-turbocharger`, `car-coolant-level`, `car-engine-start`,
  `car-shift-pattern`, `car-clutch`, `car-door`, `car-door-lock`, `car-defrost-front`,
  `car-defrost-rear`, `car-windshield`.
- Outside that family it also has `engine`, `fuel`, `fuel-pump`, `oil-temperature`, `wiper`,
  `wiper-wash`, `steering`, `flag-checkered`, `gauge`, `thermometer`, and the `weather-*` set that
  a rain forecast needs.
- Source: <https://pictogrammers.com/library/mdi/>, repository
  <https://github.com/Templarian/MaterialDesign>.

### Why this one

The alternatives were weighed on coverage first, because a set that is missing the traction
control pictogram is of no use however elegant the rest of it is. Bootstrap Icons, Lucide,
Phosphor and Iconoir each carry a car or two and nothing resembling a telltale. Tabler has a
handful. Font Awesome's free tier is thin here and its licence, CC BY 4.0, is more awkward to
satisfy inside a redistributed binary than a permissive software licence. Only Material Design
Icons has the whole vocabulary, and it has it in one consistent drawing style at one grid size.

### The licence, which is not incidental

Apache 2.0 is permissive and compatible with this project's MIT licence, in the same way the SIL
Open Font Licence is for Barlow. It does, however, require that the licence text and a notice
travel with any redistribution, and a `.simhubdash` package **is** a redistribution, exactly as it
is for the fonts in `_SHFonts/`. Shipping therefore means:

1. `third-party/mdi/LICENSE` committed in the repository, and
2. a `NOTICE` line naming Pictogrammers and the Apache 2.0 licence, included in the release
   artifact alongside the dashboard.

No icon is modified. The 24 by 24 paths are used as published, which keeps the attribution simple
and honest.

## How they reach the dashboard

SimHub cannot draw an SVG. `.djson` has an `ImageItem` which references an entry in the top-level
`Images` object, and that entry is base64. The build therefore rasterises the chosen subset from
SVG to PNG at the sizes the layouts ask for, and inlines them, exactly as it already does for the
assets under `assets/`.

Two consequences follow from
[research/simhub-dash-format.md](simhub-dash-format.md), where base64 images were 87.6 per cent of
a real dashboard file:

- **Take the subset, never the set.** Twelve to twenty telltales, not seven thousand.
- **Rasterise small and once per size.** A telltale is 26 px on the face, so a 32 px and a 48 px
  PNG cover every layout including the 1280 by 720. Twenty icons at two sizes is a few tens of
  kilobytes, which is nothing beside a single photograph.

The SVG sources stay in the repository and the PNGs are build output, which is the same rule the
`.djson` itself follows.

## What is drawn by hand and stays drawn

The rev bar segments, the tyre, the brake disc, the car top view, the steering angle, the track
map, the radar, the flags and the rank triangles. Those are not icons: they are instruments whose
shape carries a reading, and they are specified in `design/tokens.json` under `illustration`.
