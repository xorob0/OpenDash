// SpanOverloadTests.cs: the one defect this repository has shipped three times.
//
// From C# 13 the extension methods on Span<T> in System.MemoryExtensions become applicable to an
// array through the implicit array-to-span conversion, and on an array they beat the System.Linq
// ones. Several of them reverse or fill in place and return void, so `array.Reverse()` stops being
// LINQ and starts being a mutation whose result is nothing. Code that consumes the result then
// fails to compile with CS1579, and code that does not consume it silently mutates instead.
//
// It compiles on a machine with the .NET 8 SDK and fails on a runner with a newer one, which is
// exactly how it landed twice on main (2d2683d, 6397a11 -- "Name the LINQ overload again") and a
// third time on this branch. global.json now pins the SDK so the two agree, but a pin is a thing
// somebody will raise one day, and the spelling is wrong whatever compiles it.
//
// The remedy both earlier fixes used is to name the overload: Enumerable.Reverse(x). This holds the
// whole C# surface to it. The sources are read as text because the csproj compiles only the pure
// files into this project, so nothing here can reach the rest of them any other way.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class SpanOverloadTests
    {
        /// <summary>The MemoryExtensions names whose span overload returns void or a span, so that
        /// consuming the result of the extension spelling is either a compile error or a different
        /// method. Sort and Fill have no LINQ counterpart at all and are in-place on both.</summary>
        private static readonly string[] Ambiguous = { "Reverse", "Sort", "Fill", "Clear" };

        /// <summary>Consuming the value is what breaks: chained into another call, iterated, assigned,
        /// returned, or handed to something as an argument. A bare statement such as `list.Sort();` is
        /// an in-place call on a List and is left alone.</summary>
        private static readonly Regex Consumed = new Regex(
            @"(?<lead>\bin\s+|=\s*|\breturn\s+|\(\s*)(?<expr>[A-Za-z_][A-Za-z0-9_\.]*)\.(?<name>" +
            string.Join("|", Ambiguous) + @")\s*\(\s*\)(?<tail>\s*\.|\s*\)|\s*,|\s*;)",
            RegexOptions.Compiled);

        private static IEnumerable<string> Sources()
        {
            var root = RepoPaths.Root();
            foreach (var dir in new[] { Path.Combine(root, "plugin", "OpenDash"), Path.Combine(root, "plugin", "OpenDash.Tests") })
            {
                if (!Directory.Exists(dir)) continue;
                foreach (var file in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
                {
                    if (file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)) continue;
                    if (file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)) continue;
                    // This file carries the defect as a string, on purpose, to prove the pattern still
                    // recognises it; sweeping itself would fail on its own fixture.
                    if (Path.GetFileName(file) == "SpanOverloadTests.cs") continue;
                    yield return file;
                }
            }
        }

        [Fact]
        public void No_call_consumes_the_result_of_a_span_ambiguous_extension()
        {
            var offenders = new List<string>();
            foreach (var file in Sources())
            {
                var text = File.ReadAllText(file);
                var lines = text.Split('\n');
                for (var i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];
                    var code = line.TrimStart();
                    if (code.StartsWith("//", StringComparison.Ordinal) || code.StartsWith("*", StringComparison.Ordinal)) continue;
                    foreach (Match match in Consumed.Matches(line))
                    {
                        offenders.Add($"{Path.GetFileName(file)}:{i + 1}: {code.Trim()}");
                    }
                }
            }

            Assert.Equal(
                Array.Empty<string>(),
                offenders.ToArray());
        }

        [Fact]
        public void The_sweep_reaches_the_file_the_defect_landed_in()
        {
            // A guard that reads no files passes for the wrong reason, so this pins that it reads the
            // one the third instance was found in, and that the pattern still recognises the defect.
            var files = Sources().Select(Path.GetFileName).ToArray();
            Assert.DoesNotContain("SpanOverloadTests.cs", files);
            Assert.Contains("PackageCatalogueTests.cs", files);
            Assert.Matches(Consumed, "foreach (var package in Release.Reverse())");
            Assert.DoesNotMatch(Consumed, "foreach (var package in Enumerable.Reverse(Release))");
        }

        [Fact]
        public void The_sdk_is_pinned_so_that_a_local_build_and_the_runner_agree()
        {
            // The defect is only visible when the two disagree, so the pin is part of the guard.
            var pin = Path.Combine(RepoPaths.Root(), "global.json");
            Assert.True(File.Exists(pin), "global.json is what keeps a local build and CI on one compiler");
            var text = File.ReadAllText(pin);
            Assert.Contains("\"version\": \"8.0", text, StringComparison.Ordinal);
            Assert.Contains("latestFeature", text, StringComparison.Ordinal);
        }

        [Fact]
        public void The_language_version_is_a_version_and_not_latest()
        {
            // The deeper of the two halves. An SDK pin stops the compiler moving; this stops the
            // language moving if somebody raises that pin, which is an ordinary maintenance action
            // nobody would read as a language change. `latest` is what let the semantics float.
            var props = File.ReadAllText(Path.Combine(RepoPaths.Root(), "plugin", "Directory.Build.props"));
            var declared = Regex.Match(props, @"<LangVersion>([^<]+)</LangVersion>");
            Assert.True(declared.Success, "plugin/Directory.Build.props must state a LangVersion");
            var value = declared.Groups[1].Value.Trim();
            Assert.DoesNotContain("latest", value, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("preview", value, StringComparison.OrdinalIgnoreCase);
            Assert.Matches(new Regex(@"^\d+(\.\d+)?$"), value);
        }
    }
}
