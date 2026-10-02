// PanelReorderTests.cs: where a dragged row lands, and that a move leaves the list it was given alone.
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class PanelReorderTests
    {
        private static readonly double[] Even = { 32, 32, 32, 32 };

        [Theory]
        [InlineData(0, 0, 0)]
        [InlineData(0, 15, 0)]
        [InlineData(0, 17, 1)]
        [InlineData(0, 40, 1)]
        [InlineData(0, 49, 2)]
        [InlineData(0, 500, 3)]
        [InlineData(3, -17, 2)]
        [InlineData(3, -500, 0)]
        [InlineData(2, 0, 2)]
        public void A_row_lands_where_its_leading_edge_passes_a_neighbours_middle(int from, double delta, int target)
        {
            Assert.Equal(target, PanelReorder.TargetIndex(Even, from, delta));
        }

        [Fact]
        public void Rows_of_different_heights_are_measured_by_their_own()
        {
            var heights = new double[] { 20, 80, 20 };
            // The first row's bottom edge is at 20 and the tall row's middle at 60, so it takes 40 to pass it;
            // the last row's middle is at 110.
            Assert.Equal(0, PanelReorder.TargetIndex(heights, 0, 35));
            Assert.Equal(1, PanelReorder.TargetIndex(heights, 0, 45));
            Assert.Equal(2, PanelReorder.TargetIndex(heights, 0, 95));
            Assert.Equal(0, PanelReorder.TargetIndex(new double[0], 0, 10));
        }

        [Fact]
        public void A_move_returns_a_new_list_and_leaves_the_old_one()
        {
            var list = new[] { "a", "b", "c", "d" };
            Assert.Equal(new[] { "b", "c", "a", "d" }, PanelReorder.Move(list, 0, 2));
            Assert.Equal(new[] { "d", "a", "b", "c" }, PanelReorder.Move(list, 3, 0));
            Assert.Equal(new[] { "a", "b", "c", "d" }, list);
            Assert.Equal(new[] { "a", "b", "c", "d" }, PanelReorder.Move(list, 1, 1));
            Assert.Equal(new[] { "a", "c", "d", "b" }, PanelReorder.Move(list, 1, 99));
            Assert.Equal(new[] { "a", "b", "c", "d" }, PanelReorder.Move(list, 7, 0));
            Assert.Empty(PanelReorder.Move<string>(null, 0, 1));
        }
    }
}
