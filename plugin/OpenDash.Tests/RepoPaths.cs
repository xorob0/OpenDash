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

        /// <summary>The panel's own sources, read as text because the csproj excludes every WPF file
        /// from the test project: nothing here compiles them, so a guard over them is a guard over
        /// what they say.</summary>
        public static string[] SettingsControlSources() =>
            Directory.GetFiles(Path.Combine(Root(), "plugin", "OpenDash"), "SettingsControl*.cs");

        /// <summary>
        /// A C# source as code alone: its // and /// comments and its /* */ blocks dropped, its string and
        /// character literals kept whole (a "//" inside a URL is not a comment).
        /// </summary>
        /// <remarks>
        /// A text pin that reads the panel's sources is a pin on what they do, and a comment that names the
        /// thing -- "(Settings.SetRevBar)", "writes Contract.LedRpmStyleCar" -- satisfied it with the code
        /// that does it deleted. Every reader of the panel's sources goes through this.
        /// </remarks>
        public static string Code(string path) => StripComments(File.ReadAllText(path));

        /// <summary>The panel's sources as code alone, in <see cref="SettingsControlSources"/>' order.</summary>
        public static string[] SettingsControlCode() => System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Select(SettingsControlSources(), Code));

        public static string StripComments(string source)
        {
            var output = new System.Text.StringBuilder(source.Length);
            var i = 0;
            while (i < source.Length)
            {
                var c = source[i];
                var next = i + 1 < source.Length ? source[i + 1] : '\0';
                if (c == '/' && next == '/')
                {
                    while (i < source.Length && source[i] != '\n') i++;
                    continue;
                }
                if (c == '/' && next == '*')
                {
                    var end = source.IndexOf("*/", i + 2, StringComparison.Ordinal);
                    i = end < 0 ? source.Length : end + 2;
                    output.Append(' ');
                    continue;
                }
                if (c == '\'')
                {
                    var start = i++;
                    while (i < source.Length && source[i] != '\'' && source[i] != '\n') i += source[i] == '\\' ? 2 : 1;
                    i = Math.Min(source.Length, i + 1);
                    output.Append(source, start, i - start);
                    continue;
                }
                if (StringStarts(source, i))
                {
                    var end = StringEnd(source, i);
                    output.Append(source, i, end - i);
                    i = end;
                    continue;
                }
                output.Append(c);
                i++;
            }
            return output.ToString();
        }

        /// <summary>Whether a string literal starts here: ", @", $", $@" or @$".</summary>
        private static bool StringStarts(string source, int i)
        {
            var c = source[i];
            if (c == '"') return true;
            if (c != '@' && c != '$') return false;
            if (i + 1 < source.Length && source[i + 1] == '"') return true;
            return i + 2 < source.Length && (source[i + 1] == '@' || source[i + 1] == '$') && source[i + 1] != c && source[i + 2] == '"';
        }

        /// <summary>The index just past the string literal that starts at i, holes of an interpolated one
        /// and the strings inside them included.</summary>
        private static int StringEnd(string source, int i)
        {
            var verbatim = false;
            var interpolated = false;
            while (source[i] != '"')
            {
                if (source[i] == '@') verbatim = true;
                if (source[i] == '$') interpolated = true;
                i++;
            }
            i++;
            while (i < source.Length)
            {
                var c = source[i];
                if (interpolated && c == '{')
                {
                    if (i + 1 < source.Length && source[i + 1] == '{') { i += 2; continue; }
                    var depth = 1;
                    i++;
                    while (i < source.Length && depth > 0)
                    {
                        if (StringStarts(source, i)) { i = StringEnd(source, i); continue; }
                        if (source[i] == '{') depth++;
                        else if (source[i] == '}') depth--;
                        i++;
                    }
                    continue;
                }
                if (verbatim)
                {
                    if (c == '"')
                    {
                        if (i + 1 < source.Length && source[i + 1] == '"') { i += 2; continue; }
                        return i + 1;
                    }
                    i++;
                    continue;
                }
                if (c == '\\') { i += 2; continue; }
                if (c == '"' || c == '\n') return i + 1;
                i++;
            }
            return source.Length;
        }

        public static string ContractTs() => Path.Combine(Root(), "packages", "dash", "src", "contract.ts");

        /// <summary>The pinned list of every declared property, which both halves of the contract are
        /// checked against. It lives beside the TypeScript test that keeps it current, because
        /// contract.ts is the side it is written from; the file's own header says the rest.</summary>
        public static string DeclaredProperties() => Path.Combine(Root(), "packages", "dash", "test", "declared-properties.txt");

        /// <summary>The mark, which the panel redraws in WPF because WPF cannot render an SVG.</summary>
        public static string LogoSvg() => Path.Combine(Root(), "media", "logo.svg");

        public static string Version() => File.ReadAllText(Path.Combine(Root(), "VERSION")).Trim();

        /// <summary>The package `bun run build` writes; absent until the dash has been built.</summary>
        public static string BuildPackage() => Path.Combine(Root(), "build", "OpenDash.simhubdash");

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
