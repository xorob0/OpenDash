// TextOrder.cs: an order check over source text that cannot pass by finding nothing.
//
// `Assert.True(text.IndexOf(a) < text.IndexOf(b))` passes when `a` is gone, since IndexOf answers -1: a renamed
// anchor turned the check into nothing without a word. Before checks that both are there first.
using System;
using Xunit;

namespace OpenDashPlugin.Tests
{
    internal static class TextOrder
    {
        /// <summary>Asserts that <paramref name="first"/> and <paramref name="then"/> are both in
        /// <paramref name="text"/>, and the first occurrence of <paramref name="first"/> comes before the first of
        /// <paramref name="then"/>.</summary>
        public static void Before(string text, string first, string then, string because = null)
        {
            var a = text.IndexOf(first, StringComparison.Ordinal);
            var b = text.IndexOf(then, StringComparison.Ordinal);
            Assert.True(a >= 0, "not found: " + first);
            Assert.True(b >= 0, "not found: " + then);
            Assert.True(a < b, because ?? first + " comes before " + then);
        }
    }
}
