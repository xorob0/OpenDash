# The SimHub dashboard format, reverse-engineered

**Last updated:** 2026-09-10
**Confidence:** structure verified against a real dash; bindings NOT yet verified.

SimHub's dashboard format is undocumented. This is what we established by inspecting real
dashboards and community build pipelines. Everything below is split into **verified** (we
looked at it) and **unverified** (we inferred it, and it needs proving).

Sample analysed: [`Pirito10/ETS2-Dashboard`](https://github.com/Pirito10/ETS2-Dashboard)
(`ETS2 Dashboard.djson`, 1280×720, 2,463,902 bytes, 7,651 lines). Not vendored here — it is
someone else's work; fetch it yourself if you need to re-check.

---

## Verified

### `.simhubdash` is a zip of a folder

This is the entire packaging story. From
[`Blumlaut/simhub-dashes`](https://github.com/Blumlaut/simhub-dashes) `.github/workflows/release.yml`:

```yaml
- name: Zip Folders
  run: |
      zip -r IC-7.simhubdash IC-7/
      zip -r ADU5.simhubdash ADU5/
```

Double-clicking the resulting file imports it into SimHub, which unpacks it and installs any
bundled fonts and images. Installed dashes live under `SimHub/DashTemplates/`.

**Our "compile" step is one `zip` command.** This is the single fact the whole project
strategy rests on.

### A dash folder contains

```
MyDash/
  MyDash.djson              the scene graph
  MyDash.djson.metadata     sidecar
  MyDash.djson.ressources   sidecar (note: misspelled in the format itself, not a typo here)
  MyDash.djson.carclasses   sidecar, optional
  MyDash.djson.png          preview image
  MyDash.djson.00.png       per-screen previews, 00..NN
  _SHFonts/                 bundled .ttf files
```

Additional `.djson` files in the same folder appear to be sub-dashes or widgets
(Blumlaut ships `blumalerts.djson`, `HeroWidgets.djson` alongside the main dash).

### `.djson` is Json.NET-serialised JSON

Top-level keys:

```
$id, Version, Id, BaseHeight, BaseWidth, BackgroundColor, Screens, SnapToGrid,
HideLabels, ShowBoundingRectangles, GridSize, SelectedScreen, Images, Metadata,
ShowOnScreenControls, ShowInTaskBar, IsOverlay, EnableClickThroughOverlay,
EnableOnDashboardMessaging
```

`BaseWidth` / `BaseHeight` define the target resolution — **this is the lever that makes
generated multi-size support work.**

It uses Json.NET reference tracking (`$id` / `$ref`) and type discriminators:

```
"$type": "SimHub.Plugins.OutputPlugins.GraphicalDash.Models.TextItem, SimHub.Plugins"
```

### Structure

`Screens` → `Layer` → typed items. Node types observed in the sample:

| Type | Count |
|---|---|
| `ImageItem` | 38 |
| `TextItem` | 21 |
| `RectangleItem` | 15 |
| `Layer` | 12 |
| `DialGaugeItem` | 10 |
| `GearText` | 1 |
| `SpeedText` | 1 |

`GearText` and `SpeedText` are purpose-built types — SimHub has first-class nodes for common
dash fields, not just generic text.

### A `TextItem` in full

Every property is a literal. This is what makes the format generator-friendly:

```json
{
  "$id": "91",
  "$type": "SimHub.Plugins.OutputPlugins.GraphicalDash.Models.TextItem, SimHub.Plugins",
  "IsTextItem": true,
  "Font": "LCDMono", "FontWeight": "Normal", "FontStyle": "Normal", "FontSize": 40.0,
  "Text": "---", "TextColor": "#FFF2CD11", "TextWrapping": 1,
  "HorizontalAlignment": 1, "VerticalAlignment": 1,
  "Left": 516.0, "Top": 478.0, "Width": 77.0, "Height": 37.0,
  "Rotation": 0.0, "UseRotation": false,
  "BackgroundColor": "#00FFFFFF", "Opacity": 100.0, "Visible": true,
  "ShadowDepth": 0, "ShadowBlur": 0, "ShadowDirection": 315, "ShadowColor": "#FF808080",
  "BlurRadius": 0.0, "EnableBlur": false,
  "BlinkDelay": 250.0, "BlinkEnabled": false,
  "BorderTop": 0, "BorderRight": 0, "BorderBottom": 0, "BorderLeft": 0,
  "BorderColor": "#FFFFFFFF",
  "Id": "57f2c886-c879-4ac3-9ff3-68a61eeaf6ce",
  "Name": "CruiseControlSpeed",
  "CanResize": true, "IsFreezed": true, "IsSelected": false,
  "RenderingSkip": 0, "Sid": 0
}
```

Notes for the generator:

- **Colours are `#AARRGGBB`** — alpha *first*. `#00FFFFFF` is transparent white. Getting this
  backwards is the most likely early bug.
- Layout is absolute: `Left`, `Top`, `Width`, `Height`, as floats.
- `Id` is a GUID per item. Must be stable across builds or SimHub may treat items as new —
  derive it deterministically from the component path rather than randomly.
- Editor-only fields (`IsSelected`, `IsFreezed`, `CanResize`, `SnapToGrid`, `HideLabels`,
  `ShowBoundingRectangles`) can be emitted as constants.

### Images are 87.6% of the file

`Images` holds base64 payloads as `{"$id": ..., "$values": [...]}`. In the sample that was
2,158,070 of 2,463,902 bytes.

**Implication:** keep PNGs as real files in `assets/` and inline them at build time. This
alone makes our diffs readable where other dash repos' are not. It also means image-heavy
design is expensive — prefer vector-ish primitives (`RectangleItem`) where possible.

### Community precedent for source-in-git

- **Blumlaut** commits raw `.djson` and zips in CI (above).
- **DahlDesign** runs Prettier over `**/*.djson` on every PR purely for diff readability.

Both stop short of generating the JSON. That is where openDash goes further.

---

## Unverified

**These are the risks. The MVP gate spike exists to close them.**

### Telemetry bindings — the big one

We have not confirmed how a property binds to live telemetry. The sample's `Name` field
(`"CruiseControlSpeed"`) is suggestive but is probably just an editor label. Bindings are
likely sibling objects on each item that we did not dump.

SimHub has **two** expression engines, both documented in its wiki:

- [NCalc scripting](https://github.com/zegreatclan/SimHub/wiki/NCalc-scripting---Introduction)
- [JavaScript Formula Engine](https://github.com/zegreatclan/SimHub/wiki/Javascript-Formula-Engine)

The generator must model whichever representation the `.djson` uses.

**To resolve:** build a two-element dash in DashStudio by hand, bind one to RPM, save, and
diff the JSON against an unbound version.

### Also unconfirmed

- Whether `$id` values must be contiguous, or merely internally consistent
- The structure of `DialGaugeItem`, `GearText`, `SpeedText`
- What `.metadata`, `.ressources`, and `.carclasses` actually contain
- Whether `Version` gates compatibility, and how SimHub reacts to an unknown value
- The phone/tablet web display protocol (community sources say port 8888; not verified by us)

## Sources

- [Blumlaut/simhub-dashes](https://github.com/Blumlaut/simhub-dashes)
- [andreasdahl1987/DahlDesignDash](https://github.com/andreasdahl1987/DahlDesignDash)
- [Pirito10/ETS2-Dashboard](https://github.com/Pirito10/ETS2-Dashboard)
- [SimHub wiki](https://github.com/SHWotever/SimHub/wiki)
