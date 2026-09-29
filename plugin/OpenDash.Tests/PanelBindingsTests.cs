// PanelBindingsTests.cs: how a binding is named on every page that shows one, and the anchor its row carries.
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class PanelBindingsTests
    {
        [Theory]
        [InlineData("JoystickPlugin.FANATEC_Wheel_Button_3", "FANATEC Wheel Button 3")]
        [InlineData("KeyboardReaderPlugin.F5", "F5")]
        [InlineData("  ControllerPlugin.Button__A  ", "Button A")]
        [InlineData("NoPlugin", "NoPlugin")]
        [InlineData("", null)]
        [InlineData(null, null)]
        public void A_trigger_is_named_by_its_input_without_the_plugin_that_reads_it(string trigger, string label)
        {
            Assert.Equal(label, PanelBindings.TriggerLabel(trigger));
        }

        [Fact]
        public void A_chip_names_the_first_trigger_and_counts_the_rest()
        {
            Assert.Null(PanelBindings.ChipText(new string[0]));
            Assert.Null(PanelBindings.ChipText(null));
            Assert.Equal("F5", PanelBindings.ChipText(new[] { "KeyboardReaderPlugin.F5" }));
            Assert.Equal("F5 +2", PanelBindings.ChipText(new[] { "KeyboardReaderPlugin.F5", "JoystickPlugin.B1", "JoystickPlugin.B2" }));
        }

        [Fact]
        public void A_bindings_row_is_anchored_by_its_action()
        {
            var glance = Contract.HoldQuickGlanceActionFor("Rim");
            Assert.Equal("binding." + glance, PanelBindings.Anchor(glance));
            Assert.Throws<System.ArgumentException>(() => PanelBindings.Anchor(null));
        }
    }
}
