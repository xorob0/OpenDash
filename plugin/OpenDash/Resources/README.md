# Resources

The plugin embeds every `*.simhubdash` found in this folder and installs it into SimHub's
`DashTemplates/` on startup. The packages are build output of `packages/dash`, so they are not
committed (`plugin/OpenDash/Resources/*.simhubdash` is gitignored) and CI copies them here from
the dash job before building the plugin.

To build a plugin locally that carries the dashboard:

```
bun run build
cp build/*.simhubdash plugin/OpenDash/Resources/
dotnet build plugin/OpenDash -c Release
```

A plugin built without a package still loads, exposes its properties and shows its settings
panel; the Dashboard section reports "Not installed" and the log says that no package was
embedded.
