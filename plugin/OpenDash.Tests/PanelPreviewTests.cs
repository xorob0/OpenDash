// PanelPreviewTests.cs: the box the live preview is drawn in.
//
// ScreenPreview.cs is WPF and SimHub and cannot be compiled here, which is why PanelPreview.cs exists.
// What is worth holding is the one rule the fit has to keep: it is uniform. A preview that fitted
// width and height separately would draw a face at a shape no screen has, which is a lie of exactly
// the kind the whole approach exists to avoid, and nothing on the VM would make it obvious.
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class PanelPreviewTests
    {
        /// <summary>About the width the pane's body has. The exact number is not what is under test:
        /// what is, is that both sides of the box come from one factor.</summary>
        private const double Available = 896;

        /// <summary>The reference face is wider than the pane, so the width binds and the height follows
        /// it: 1920 x 480 at 896 wide is 224 tall, and 480 divided by 1920 is 224 divided by 896.</summary>
        [Fact]
        public void A_wide_face_is_fitted_to_the_width_it_is_given()
        {
            var box = PanelPreview.Fit(1920, 480, Available, PanelPreview.MaxHeight);
            Assert.Equal(896, box.Width, 3);
            Assert.Equal(224, box.Height, 3);
            Assert.Equal(896d / 1920d, box.Scale, 6);
        }

        /// <summary>The portrait face is 600 x 686 and would take 1024 pixels of the pane if width alone
        /// decided, so the height binds instead and the width comes down with it.</summary>
        [Fact]
        public void A_tall_face_is_fitted_to_the_height_instead()
        {
            var box = PanelPreview.Fit(600, 686, Available, PanelPreview.MaxHeight);
            Assert.Equal(PanelPreview.MaxHeight, box.Height, 3);
            Assert.Equal(PanelPreview.MaxHeight / 686d, box.Scale, 6);
            Assert.True(box.Width < Available);
        }

        /// <summary>Whichever limit binds, one factor scales both sides: the aspect the driver's screen
        /// has is the aspect the preview has.</summary>
        [Theory]
        [InlineData(1920, 480)]
        [InlineData(1280, 720)]
        [InlineData(850, 480)]
        [InlineData(600, 686)]
        [InlineData(1080, 1920)]
        [InlineData(480, 480)]
        public void The_fit_is_uniform(int width, int height)
        {
            var box = PanelPreview.Fit(width, height, Available, PanelPreview.MaxHeight);
            Assert.Equal((double)width / height, box.Width / box.Height, 6);
        }

        /// <summary>A screen smaller than the room it is given is drawn at its own pixels rather than
        /// blown up to fill the pane, which is what makes the 480 round preview worth looking at.</summary>
        [Fact]
        public void A_small_screen_is_drawn_at_actual_size()
        {
            var box = PanelPreview.Fit(480, 480, Available, 640);
            Assert.Equal(1, box.Scale, 6);
            Assert.Equal(480, box.Width, 3);
            Assert.Equal(480, box.Height, 3);
        }

        /// <summary>No room, no size, and nothing drawn: the caller reads a scale of zero as the state in
        /// which SimHub is not asked for a renderer at all.</summary>
        [Theory]
        [InlineData(0, 480, Available, PanelPreview.MaxHeight)]
        [InlineData(1920, 0, Available, PanelPreview.MaxHeight)]
        [InlineData(1920, 480, 0, PanelPreview.MaxHeight)]
        [InlineData(1920, 480, Available, 0)]
        public void A_size_that_cannot_be_drawn_asks_for_nothing(int width, int height, double available, double maxHeight)
        {
            var box = PanelPreview.Fit(width, height, available, maxHeight);
            Assert.Equal(0, box.Scale);
            Assert.Equal(0, box.Width);
            Assert.Equal(0, box.Height);
        }
    }
}
