# ADR 0005: The plugin is code-only WPF and builds on Linux with the .NET SDK

**Date:** 2026-09-10
**Status:** Accepted

## Context

The scope assumed the plugin would build on a Windows runner with MSBuild, and that the settings
panel would be authored in XAML. The development machine for this project is Linux, and the
Windows VM that runs SimHub is small and slow. XAML compilation needs the WPF build targets,
which only exist on Windows.

## Decision

The plugin targets .NET Framework 4.8 through the `Microsoft.NETFramework.ReferenceAssemblies`
package and references `PresentationFramework`, `PresentationCore`, `WindowsBase` and
`System.Xaml` as plain reference assemblies, without `UseWPF`. The settings panel is built in
C# code: every control is created in a small factory method, and SimHub's own styles are looked
up at runtime with `TryFindResource` and applied when present. The result is that
`dotnet build` produces `OpenDash.dll` on Linux, macOS and Windows alike, in a few seconds, and
the unit tests run on the same machine. A net8 test project compiles the plugin's pure logic
files directly, so version comparison, settings normalisation and the card catalogue are tested
without SimHub.

## Consequences

CI has one Linux job for the dashboard and one for the plugin, and no Windows runner. What
cannot be exercised here is the WPF panel itself, which is verified by hand on the Windows VM.
Contributors who prefer XAML can still open the project in Visual Studio, but converting the
panel to XAML would reintroduce the Windows-only build, so it is not planned.
