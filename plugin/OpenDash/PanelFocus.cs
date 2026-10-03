// PanelFocus.cs: where the keyboard focus was before a rebuild in place, and the control it goes back to
// after it.
//
// A rebuild in place (SettingsControl.RebuildPage) draws the page again and hands the focus back. It found
// the control by its position in the visual tree, which is right only while the rebuild draws the same
// shape: on Settings, widening the panel across AlertSurfacesFrom gives the alert table its four surface
// columns, every cell after the head moves along, and the caret left in Oil temperature came back in Low
// fuel, where the next digit typed went (#645). So the place is recorded by name first: the key of the
// nearest thing above the control, or the control itself, that carries one -- the setting it writes, or
// its row's search anchor -- and the path from there down to it. The position from the page's root is
// kept for a control with no key above it, a press or a chip, and for a key the rebuild no longer draws.
//
// Pure: no WPF and no SimHub types; the tree is walked through the functions the caller passes, which
// SettingsControl fills from VisualTreeHelper. Compiled into OpenDash.Tests by the Panel*.cs wildcard.
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenDashPlugin
{
    /// <summary>Where a focused control sat when a rebuild recorded it.</summary>
    public sealed class PanelFocusPath
    {
        public PanelFocusPath(string key, int ordinal, IEnumerable<int> underKey, IEnumerable<int> fromRoot)
        {
            Key = key;
            Ordinal = ordinal;
            UnderKey = (underKey ?? new int[0]).ToList().AsReadOnly();
            FromRoot = (fromRoot ?? new int[0]).ToList().AsReadOnly();
        }

        /// <summary>The key of the nearest element at or above the control that carries one, or null when
        /// nothing between it and the root does.</summary>
        public string Key { get; private set; }

        /// <summary>Which of the elements carrying <see cref="Key"/> it was, counted in the tree's order from 0,
        /// so a key a page draws twice still finds the one the driver was in.</summary>
        public int Ordinal { get; private set; }

        /// <summary>The child index at each level from the keyed element down to the control; empty when the
        /// control carries the key itself.</summary>
        public IReadOnlyList<int> UnderKey { get; private set; }

        /// <summary>The child index at each level from the root down to the control: the fallback.</summary>
        public IReadOnlyList<int> FromRoot { get; private set; }
    }

    public static class PanelFocus
    {
        /// <summary>
        /// Records where <paramref name="focused"/> sits under <paramref name="root"/>, or null when it is not
        /// under it.
        /// </summary>
        /// <param name="keyOf">The key an element carries, or null. The nearest one above the control wins, so a
        /// box keyed by its setting is found by that rather than by the section around it.</param>
        public static PanelFocusPath Record<T>(T root, T focused, Func<T, T> parent, Func<T, int> count, Func<T, int, T> child, Func<T, string> keyOf) where T : class
        {
            if (root == null || focused == null) return null;
            var path = new List<int>();
            T keyed = null;
            string key = null;
            var under = 0;
            var node = focused;
            while (node != null && node != root)
            {
                if (key == null)
                {
                    var own = keyOf(node);
                    if (!string.IsNullOrEmpty(own))
                    {
                        key = own;
                        keyed = node;
                        under = path.Count;
                    }
                }
                var up = parent(node);
                if (up == null) return null;
                var index = -1;
                var children = count(up);
                for (var i = 0; i < children; i++)
                {
                    if (child(up, i) == node) { index = i; break; }
                }
                if (index < 0) return null;
                path.Insert(0, index);
                node = up;
            }
            if (node != root) return null;
            if (key == null) return new PanelFocusPath(null, 0, null, path);
            var ordinal = Keyed(root, key, count, child, keyOf).TakeWhile(found => found != keyed).Count();
            return new PanelFocusPath(key, ordinal, path.Skip(path.Count - under).ToList(), path);
        }

        /// <summary>
        /// The control to focus in a rebuild: the one under the recorded key when the rebuild still draws it,
        /// otherwise the one at the recorded position from the root; in either, the nearest focusable thing
        /// above the place where the rebuild is shaped differently. Null when nothing on the way is focusable.
        /// </summary>
        public static T Find<T>(T root, PanelFocusPath at, Func<T, int> count, Func<T, int, T> child, Func<T, string> keyOf, Func<T, bool> focusable) where T : class
        {
            if (root == null || at == null) return null;
            if (at.Key != null)
            {
                var keyed = Keyed(root, at.Key, count, child, keyOf).Skip(at.Ordinal).FirstOrDefault();
                var found = keyed == null ? null : Walk(keyed, at.UnderKey, count, child, focusable, true);
                if (found != null) return found;
            }
            return Walk(root, at.FromRoot, count, child, focusable, false);
        }

        /// <summary>Follows a path of child indexes down from <paramref name="from"/> as far as the tree allows,
        /// and returns the last focusable element met (<paramref name="from"/> itself counted when asked).</summary>
        private static T Walk<T>(T from, IEnumerable<int> path, Func<T, int> count, Func<T, int, T> child, Func<T, bool> focusable, bool countStart) where T : class
        {
            var node = from;
            var last = countStart && focusable(node) ? node : null;
            foreach (var index in path)
            {
                if (index < 0 || index >= count(node)) break;
                node = child(node, index);
                if (focusable(node)) last = node;
            }
            return last;
        }

        /// <summary>Every element under <paramref name="root"/> carrying <paramref name="key"/>, in the tree's
        /// order (depth first, children in index order).</summary>
        private static IEnumerable<T> Keyed<T>(T root, string key, Func<T, int> count, Func<T, int, T> child, Func<T, string> keyOf) where T : class
        {
            var stack = new Stack<T>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                var node = stack.Pop();
                if (node != root && string.Equals(keyOf(node), key, StringComparison.Ordinal)) yield return node;
                for (var i = count(node) - 1; i >= 0; i--) stack.Push(child(node, i));
            }
        }
    }
}
