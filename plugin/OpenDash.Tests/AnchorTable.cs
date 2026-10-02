// AnchorTable.cs: a page's anchor ids as one table, for the page tests that pin them.
//
// An anchor id is what search, Home's fix rows and a capture script route to, so one renamed on a page with
// nothing else moving sends each of them to the page's top. Each page's own test holds its table, name and
// value, and fails as well when the page adds an anchor the table does not name.
using System;
using System.Linq;
using System.Reflection;

namespace OpenDashPlugin.Tests
{
    internal static class AnchorTable
    {
        /// <summary>Every public const string named Anchor* on the type, as "Name = value", in name order.</summary>
        public static string[] Of(Type page)
        {
            return page.GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field => field.IsLiteral && field.FieldType == typeof(string) && field.Name.StartsWith("Anchor", StringComparison.Ordinal))
                .Select(field => field.Name + " = " + (string)field.GetRawConstantValue())
                .OrderBy(line => line, StringComparer.Ordinal)
                .ToArray();
        }
    }
}
