// PanelReorder.cs: where a dragged row lands, and the list after it has moved.
//
// Ui.Reorderable draws the rows and their grips and reports a drag as a distance; the arithmetic is here so
// that PanelReorderTests can hold it, since a row that lands one place off is exactly the kind of thing
// nobody notices until they have reordered a zone's pages wrong. The move returns a new array and never
// reverses anything in place, which is the defect SpanOverloadTests exists to keep out.
//
// Pure: no WPF.
using System;
using System.Collections.Generic;

namespace OpenDashPlugin
{
    public static class PanelReorder
    {
        /// <summary>
        /// The index a row dragged by <paramref name="deltaY"/> from <paramref name="from"/> lands on.
        /// </summary>
        /// <remarks>
        /// A row takes a neighbour's place once its leading edge has passed that neighbour's middle: its
        /// bottom edge on the way down, its top edge on the way up. That is half a row of travel for rows of
        /// one height, which answers the hand without flickering at the boundary. Rows may differ in height,
        /// so each is measured where it stands. A drag past either end lands on that end.
        /// </remarks>
        public static int TargetIndex(IList<double> heights, int from, double deltaY)
        {
            if (heights == null || heights.Count == 0) return 0;
            if (from < 0) from = 0;
            if (from >= heights.Count) from = heights.Count - 1;

            var tops = new double[heights.Count];
            var y = 0.0;
            for (var i = 0; i < heights.Count; i++)
            {
                tops[i] = y;
                y += heights[i];
            }
            var top = tops[from] + deltaY;
            var bottom = top + heights[from];

            var target = from;
            for (var i = from + 1; i < heights.Count; i++)
            {
                if (bottom > tops[i] + heights[i] / 2) target = i;
            }
            for (var i = from - 1; i >= 0; i--)
            {
                if (top < tops[i] + heights[i] / 2) target = i;
            }
            return target;
        }

        /// <summary>The list with the item at <paramref name="from"/> moved to <paramref name="to"/>, as a new
        /// array; the list itself is left as it was.</summary>
        public static T[] Move<T>(IList<T> list, int from, int to)
        {
            if (list == null) return new T[0];
            var result = new List<T>(list);
            if (from < 0 || from >= result.Count) return result.ToArray();
            if (to < 0) to = 0;
            if (to >= result.Count) to = result.Count - 1;
            if (from == to) return result.ToArray();
            var item = result[from];
            result.RemoveAt(from);
            result.Insert(to, item);
            return result.ToArray();
        }
    }
}
