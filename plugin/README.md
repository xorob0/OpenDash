# plugin

The SimHub plugin: `OpenDash/` is the net48 plugin, `OpenDash.Tests/` the net8.0 tests of its
pure parts, `lib/` the SimHub reference assemblies, `scripts/` the packaging script. `INSTALL.md`
is the user-facing guide that ships in the plugin zip.

## Build

The plugin embeds the dashboard package that `bun run build` writes, so build the dash first and
copy it into `OpenDash/Resources/` (gitignored; see `OpenDash/Resources/README.md`):

```
bun run build
cp build/*.simhubdash plugin/OpenDash/Resources/
dotnet build plugin/OpenDash -c Release
```

The output is `plugin/OpenDash/bin/Release/net48/OpenDash.dll`. It builds on Linux against the
reference assemblies; it only runs inside SimHub on Windows. The assembly version comes from
`/VERSION` through `Directory.Build.props`. MSBuild does not notice a removed resource: after
deleting a package from `Resources/`, build with `--no-incremental` or the old DLL, package
included, stays in place.

## Test

```
dotnet test plugin/OpenDash.Tests
```

The files without SimHub or WPF dependencies (`Contract.cs`, `Cards.cs`, `OpenDashSettings.cs`,
`Theme.cs`, `Versioning.cs`, `Installation.cs`, `PackageExtractor.cs`,
`DashboardInstaller.Installed.cs`) are compiled into the test project directly; keep them free of
SimHub and WPF types. One test reads `build/openDash.simhubdash` and is skipped until the dash has
been built.

## Package

```
plugin/scripts/package-plugin.sh
```

zips `OpenDash.dll` and `INSTALL.md` into `build/OpenDash-plugin.zip`, the file a release
attaches and `INSTALL.md` describes. It refuses to run without the Release build and warns when
the DLL was built without an embedded dashboard.
