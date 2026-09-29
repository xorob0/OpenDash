// PanelBindingsTests.cs: how a binding is named on every page that shows one, and the anchor its row carries.
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class PanelBindingsTests
    {
        [Theory]
        // The artboards' one form, "{source} · {input}": Shortcuts.dc.html's "Keyboard · F9" and "CSL Elite · 9".
        [InlineData("KeyboardReaderPlugin.F9", "Keyboard · F9")]
        [InlineData("JoystickPlugin.CSL_Elite_B09", "CSL Elite · 9")]
        [InlineData("JoystickPlugin.FANATEC_Wheel_Button_3", "FANATEC Wheel · 3")]
        [InlineData("JoystickPlugin.Wheel_POV0Up", "Wheel · POV0Up")]
        [InlineData("  ControllerPlugin.Button__A  ", "Controller · Button A")]
        [InlineData("NoPlugin", "NoPlugin")]
        [InlineData("", null)]
        [InlineData(null, null)]
        public void A_trigger_is_named_by_what_reads_it_and_its_input(string trigger, string label)
        {
            Assert.Equal(label, PanelBindings.TriggerLabel(trigger));
        }

        [Fact]
        public void A_chip_names_the_first_trigger_and_counts_the_rest()
        {
            Assert.Null(PanelBindings.ChipText(new string[0]));
            Assert.Null(PanelBindings.ChipText(null));
            Assert.Equal("Keyboard · F5", PanelBindings.ChipText(new[] { "KeyboardReaderPlugin.F5" }));
            Assert.Equal("Keyboard · F5 +2", PanelBindings.ChipText(new[] { "KeyboardReaderPlugin.F5", "JoystickPlugin.Rim_B1", "JoystickPlugin.Rim_B2" }));
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
