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

        /// <summary>The mark, which the panel redraws in WPF because WPF cannot render an SVG.</summary>
        public static string LogoSvg() => Path.Combine(Root(), "media", "logo.svg");

        public static string Version() => File.ReadAllText(Path.Combine(Root(), "VERSION")).Trim();

        /// <summary>The package `bun run build` writes; absent until the dash has been built.</summary>
        public static string BuildPackage() => Path.Combine(Root(), "build", "openDash.simhubdash");
    }
}
