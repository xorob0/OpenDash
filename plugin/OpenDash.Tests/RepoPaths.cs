// RepoPaths.cs: locates the repository root from the test binary, for the tests that read tokens.json,
// Theme.cs, contract.ts and the dash build output.
using System;
using System.IO;

namespace OpenDashPlugin.Tests
{
    internal static class RepoPaths
    {
        public static string Root()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "VERSION")) && File.Exists(Path.Combine(dir.FullName, "design", "tokens.json")))
                {
                    return dir.FullName;
                }
                dir = dir.Parent;
            }
            throw new DirectoryNotFoundException("Repository root (VERSION + design/tokens.json) not found above " + AppContext.BaseDirectory);
        }

        public static string TokensJson() => Path.Combine(Root(), "design", "tokens.json");

        public static string ThemeCs() => Path.Combine(Root(), "plugin", "OpenDash", "Theme.cs");

        public static string ContractTs() => Path.Combine(Root(), "packages", "dash", "src", "contract.ts");

        /// <summary>The pinned list of every declared property, which both halves of the contract are
        /// checked against. It lives beside the TypeScript test that keeps it current, because
        /// contract.ts is the side it is written from; the file's own header says the rest.</summary>
        public static string DeclaredProperties() => Path.Combine(Root(), "packages", "dash", "test", "declared-properties.txt");

        /// <summary>The mark, which the panel redraws in WPF because WPF cannot render an SVG.</summary>
        public static string LogoSvg() => Path.Combine(Root(), "media", "logo.svg");

        public static string Version() => File.ReadAllText(Path.Combine(Root(), "VERSION")).Trim();

        /// <summary>The package `bun run build` writes; absent until the dash has been built.</summary>
        public static string BuildPackage() => Path.Combine(Root(), "build", "openDash.simhubdash");

        /// <summary>The folder whose contents the plugin embeds. It is gitignored build output, and CI
        /// fills it by downloading the dash job's artifact straight into it before `dotnet test` runs,
        /// which is why a test that wants the real built artefact looks here rather than in build/:
        /// the plugin job never creates build/ at all.</summary>
        public static string EmbeddedResources() => Path.Combine(Root(), "plugin", "OpenDash", "Resources");

        /// <summary>Where `bun run build` leaves the same files locally, for a checkout that has built
        /// the dash but not copied it in yet.</summary>
        public static string BuildOutput() => Path.Combine(Root(), "build");
    }
}
