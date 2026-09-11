# Resources

The plugin embeds every `*.simhubdash` found in this folder and installs it into SimHub's
`DashTemplates/` on startup. The packages are build output of `packages/dash`, so they are not
committed (`plugin/OpenDash/Resources/*.simhubdash` is gitignored) and CI copies them here from
the dash job before building the plugin.

`fonts/` is build output too, and for the same reason. The settings panel draws in the faces the
dash face draws in, and the condensed ones are renamed as they leave the dash build, so embedding
them straight from `packages/dash/fonts` would embed the wrong family; see
`packages/dash/src/design/fontFiles.ts`.

To build a plugin locally that carries the dashboard:

```
bun run build
cp build/*.simhubdash plugin/OpenDash/Resources/
cp -R build/fonts plugin/OpenDash/Resources/fonts
dotnet build plugin/OpenDash -c Release
```

A plugin built without a package still loads, exposes its properties and shows its settings
panel; the Dashboard section reports "Not installed" and the log says that no package was
embedded.
