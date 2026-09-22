# Resources

The plugin embeds every `*.simhubdash` found in this folder and installs it into SimHub's
`DashTemplates/` on startup. The packages are build output of `packages/dash`, so they are not
committed (`plugin/OpenDash/Resources/*.simhubdash` is gitignored) and CI copies them here from
the dash job before building the plugin.

`*.ledsprofile` is embedded the same way but is **not installed**: the plugin writes the flag box
profile into `SimHub/OpenDash/` and stops there, and the user imports it themselves. SimHub keeps
matrix profiles inside a settings file it rewrites on every change, so merging into that file would
lose the user's other profiles, and painting hardware somebody owns is not something a dashboard
should do unasked. [ADR 0013](../../../docs/decisions/0013-lighting-hardware.md) is the reasoning;
`FlagBoxProfile.cs` is the code.

The extension is shared by SimHub's two lighting families, so most of the profiles here are not the
flag box: one is the 8x8 matrix and the rest are the RPM strips, one per hardware shape. The flag
box is picked out by its file name, `OpenDash Flag box.ledsprofile`, which is `FlagBoxProfile.FileName`
in the plugin and `FLAG_BOX_PROFILE_NAME` in `packages/dash/src/leds/profile.ts`. Nothing may pick it
out by sort order: `OpenDash 0-10-0.ledsprofile` sorts first, and taking that one would hand SimHub's
matrix driver a ten-LED strip.

`fonts/` is build output too, and for the same reason. The settings panel draws in the faces the
dash face draws in, and the condensed ones are renamed as they leave the dash build, so embedding
them straight from `packages/dash/fonts` would embed the wrong family; see
`packages/dash/src/design/fontFiles.ts`.

To build a plugin locally that carries the dashboard:

```
bun run build
cp build/*.simhubdash plugin/OpenDash/Resources/
cp build/*.ledsprofile plugin/OpenDash/Resources/
cp -R build/fonts plugin/OpenDash/Resources/fonts
dotnet build plugin/OpenDash -c Release
```

A plugin built without a package still loads, exposes its properties and shows its settings
panel; the Dashboard section reports "Not installed" and the log says that no package was
embedded. The same holds for the profile: the lights page says no profile is in the build.
