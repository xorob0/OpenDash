// PanelObligationsTests.cs: what the settings' #503 changes oblige the panel to do, read off its source.
//
// The contract half of #503 moved two answers under the panel's feet. The temperature thresholds are
// the rig's now, and Normalise writes them into every panel's entry on each save, so a row that writes
// one panel's entry and saves has its edit overwritten straight away. And a reversed strip is its plain
// shape with a switch on, so its profile is found by ProfileShapeId: a lookup by Shape finds the plain
// wiring for a strip wired from the far end, and installs it. Neither shows in a test of the settings,
// which are right; both show only in the panel, which these read the way PanelDataTabTests does.
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class PanelObligationsTests
    {
        private static string Sources()
        {
            return string.Concat(RepoPaths.SettingsControlSources().Select(File.ReadAllText));
        }

        [Fact]
        public void The_temperature_rows_set_the_rigs_thresholds_and_not_one_panels()
        {
            var sources = Sources();
            Assert.Contains("SetLightsOilTemp(", sources);
            Assert.Contains("SetLightsWaterTemp(", sources);
            // An index into the per-panel arrays is a write Normalise undoes on the save that follows it.
            Assert.DoesNotContain("FlagBoxMatrixOilTemp[", sources);
            Assert.DoesNotContain("FlagBoxMatrixWaterTemp[", sources);
        }

        [Fact]
        public void A_strip_is_installed_by_the_profile_it_names()
        {
            var sources = Sources();
            Assert.Contains(".ProfileShapeId", sources);
            // A strip migrated from the 4/14/4 reversed shape is Shape "4-14-4" with Reversed on: looked up
            // by its shape, it finds the plain profile and installs it on a strip wired the other way.
            Assert.DoesNotContain("TryGetValue(bar.Shape", sources);
            Assert.DoesNotContain("entry.Id, bar.Shape", sources);
        }
    }
}
