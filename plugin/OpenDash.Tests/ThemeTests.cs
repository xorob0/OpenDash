// ThemeTests.cs: every constant in Theme.cs carries the token path it mirrors; resolve that path in
// design/tokens.json (following {alias} values) and fail when the two disagree.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class ThemeTests
    {
        private static readonly Regex Constant = new Regex(
            @"public const (?<type>string|double) (?<name>\w+) = (?<value>""[^""]*""|[0-9.]+);\s*//\s*(?<path>[\w.]+)",
            RegexOptions.Compiled);

        public static IEnumerable<object[]> Constants()
        {
            foreach (Match match in Constant.Matches(File.ReadAllText(RepoPaths.ThemeCs())))
            {
                yield return new object[] { match.Groups["name"].Value, match.Groups["value"].Value.Trim('"'), match.Groups["path"].Value };
            }
        }

        [Fact]
        public void Theme_has_annotated_constants()
        {
            var count = 0;
            foreach (var _ in Constants()) count++;
            Assert.True(count >= 20, "expected the theme constants to carry token paths, found " + count);
        }

        [Theory]
        [MemberData(nameof(Constants))]
        public void Theme_constant_mirrors_tokens_json(string name, string value, string path)
        {
            using (var document = JsonDocument.Parse(File.ReadAllText(RepoPaths.TokensJson())))
            {
                var token = Resolve(document.RootElement, path, 0);
                if (token.ValueKind == JsonValueKind.String)
                {
                    var expected = token.GetString();
                    if (expected.EndsWith("em", StringComparison.Ordinal)) expected = expected.Substring(0, expected.Length - 2);
                    Assert.True(string.Equals(expected, value, StringComparison.OrdinalIgnoreCase),
                        name + " is " + value + " but " + path + " is " + expected);
                }
                else if (token.ValueKind == JsonValueKind.Number)
                {
                    Assert.Equal(token.GetDouble(), double.Parse(value, CultureInfo.InvariantCulture));
                }
                else
                {
                    Assert.Fail(path + " resolved to " + token.ValueKind + " for " + name);
                }
            }
        }

        /// <summary>Walks a dotted path; a node with a "value" yields it, and a string of the form {a.b.c} is an alias.</summary>
        private static JsonElement Resolve(JsonElement root, string path, int depth)
        {
            if (depth > 10) throw new InvalidDataException("alias loop at " + path);
            var node = root;
            foreach (var segment in path.Split('.'))
            {
                JsonElement next;
                if (node.ValueKind != JsonValueKind.Object || !node.TryGetProperty(segment, out next))
                {
                    throw new KeyNotFoundException("tokens.json has no " + path + " (missing " + segment + ")");
                }
                node = next;
            }
            if (node.ValueKind == JsonValueKind.Object)
            {
                JsonElement value;
                if (!node.TryGetProperty("value", out value)) throw new KeyNotFoundException(path + " is a group, not a token");
                node = value;
            }
            if (node.ValueKind == JsonValueKind.String)
            {
                var text = node.GetString();
                if (text.StartsWith("{", StringComparison.Ordinal) && text.EndsWith("}", StringComparison.Ordinal))
                {
                    return Resolve(root, text.Substring(1, text.Length - 2), depth + 1);
                }
            }
            return node;
        }
    }
}
