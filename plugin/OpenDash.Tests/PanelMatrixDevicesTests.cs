// PanelMatrixDevicesTests.cs: which of SimHub's matrix devices the flag box profile goes to (#363), and the
// matrix's own record of its device.
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class PanelMatrixDevicesTests
    {
        private static readonly string Wheel = LedBar.DeviceId(new Guid("11111111-1111-1111-1111-111111111111"));
        private static readonly string Plate = LedBar.DeviceId(new Guid("22222222-2222-2222-2222-222222222222"));

        private static LedDeviceEntry Arduino(bool connected = true) => new LedDeviceEntry(LedBar.ArduinoDevice, "Arduino RGB matrix", connected);

        [Fact]
        public void Only_an_8x8_takes_the_flag_box_and_a_device_that_does_not_say_is_one()
        {
            Assert.True(PanelMatrixDevices.Takes("Matrix8x8"));
            Assert.True(PanelMatrixDevices.Takes(null));
            Assert.True(PanelMatrixDevices.Takes(""));
            foreach (var kind in new[] { "Matrix4x4", "Matrix5x4", "Matrix5x7", "PSP", "Any" }) Assert.False(PanelMatrixDevices.Takes(kind));
        }

        [Fact]
        public void The_profile_goes_to_every_device_a_matrix_names_once_and_in_order()
        {
            Assert.Equal(new[] { Wheel, LedBar.ArduinoDevice }, PanelMatrixDevices.Wanted(new[] { Wheel, null, Wheel, "arduino" }, Plate));
            Assert.Equal(new[] { Wheel, Plate }, PanelMatrixDevices.Wanted(new[] { Wheel, Plate }, null));
        }

        [Fact]
        public void A_matrix_with_no_device_recorded_is_the_Arduinos_where_it_was_always_installed()
        {
            Assert.Equal(new[] { LedBar.ArduinoDevice }, PanelMatrixDevices.Wanted(new string[] { null, " " }, Wheel));
        }

        [Fact]
        public void A_rig_with_no_matrix_plans_for_the_device_a_first_matrix_would_open_on()
        {
            Assert.Equal(new[] { Wheel }, PanelMatrixDevices.Wanted(new string[0], Wheel));
            Assert.Equal(new[] { LedBar.ArduinoDevice }, PanelMatrixDevices.Wanted(null, null));
        }

        [Fact]
        public void A_new_matrix_opens_on_a_devices_own_8x8_over_the_Arduinos()
        {
            Assert.Equal(Wheel, PanelMatrixDevices.Preferred(new[] { Arduino(), new LedDeviceEntry(Wheel, "Wheel", false) }));
            Assert.Equal(LedBar.ArduinoDevice, PanelMatrixDevices.Preferred(new[] { Arduino() }));
            Assert.Equal(LedBar.ArduinoDevice, PanelMatrixDevices.Preferred(new LedDeviceEntry[0]));
            Assert.Equal(LedBar.ArduinoDevice, PanelMatrixDevices.Preferred(null));
        }

        [Fact]
        public void The_row_says_why_there_is_nowhere_where_it_goes_or_asks()
        {
            var none = PanelMatrixDevices.DeviceRow(new LedDeviceEntry[0], null);
            Assert.False(none.HasPicker);
            Assert.Equal(PanelMatrixDevices.NoDevices, none.Caption);

            var one = PanelMatrixDevices.DeviceRow(new[] { Arduino() }, null);
            Assert.False(one.HasPicker);
            Assert.Contains("Arduino RGB matrix", one.Caption);

            var two = PanelMatrixDevices.DeviceRow(new[] { Arduino(), new LedDeviceEntry(Wheel, "Wheel", true) }, Wheel);
            Assert.True(two.HasPicker);
            Assert.Equal(new[] { LedBar.ArduinoDevice, Wheel }, two.Ids);
            Assert.Equal(1, two.Selected);
        }

        [Fact]
        public void A_matrix_on_a_device_SimHub_no_longer_lists_keeps_it_labelled_as_gone()
        {
            var row = PanelMatrixDevices.DeviceRow(new[] { Arduino() }, Plate);
            Assert.True(row.HasPicker);
            Assert.Equal(new[] { Plate, LedBar.ArduinoDevice }, row.Ids);
            Assert.Equal(PanelLights.DeviceGone, row.Labels[0]);
            Assert.Equal(0, row.Selected);
        }

        [Fact]
        public void The_title_offers_Install_while_any_device_lacks_the_profile()
        {
            var plan = PanelMatrixDevices.Combine(new[]
            {
                new FlagBoxPlan { State = FlagBoxInstallState.UpToDate, InstalledVersion = "2", EmbeddedVersion = "2" },
                new FlagBoxPlan { State = FlagBoxInstallState.NotInstalled, EmbeddedVersion = "2" },
                new FlagBoxPlan { State = FlagBoxInstallState.Newer, InstalledVersion = "3", EmbeddedVersion = "2" },
            });
            Assert.Equal(FlagBoxInstallState.NotInstalled, plan.State);
            Assert.Null(plan.InstalledVersion);
            Assert.Equal("2", plan.EmbeddedVersion);
        }

        [Fact]
        public void A_failure_anywhere_is_said_first_and_up_to_date_only_where_every_device_is()
        {
            Assert.Equal(FlagBoxInstallState.Failed, PanelMatrixDevices.Combine(new[]
            {
                new FlagBoxPlan { State = FlagBoxInstallState.NotInstalled },
                new FlagBoxPlan { State = FlagBoxInstallState.Failed },
            }).State);
            Assert.Equal(FlagBoxInstallState.Outdated, PanelMatrixDevices.Combine(new[]
            {
                new FlagBoxPlan { State = FlagBoxInstallState.UpToDate },
                new FlagBoxPlan { State = FlagBoxInstallState.Outdated },
            }).State);
            Assert.Equal(FlagBoxInstallState.Unavailable, PanelMatrixDevices.Combine(new[]
            {
                new FlagBoxPlan { State = FlagBoxInstallState.UpToDate },
                new FlagBoxPlan { State = FlagBoxInstallState.Unavailable },
            }).State);
            Assert.Equal(FlagBoxInstallState.UpToDate, PanelMatrixDevices.Combine(new[]
            {
                new FlagBoxPlan { State = FlagBoxInstallState.UpToDate },
                new FlagBoxPlan { State = FlagBoxInstallState.UpToDate },
            }).State);
        }

        [Fact]
        public void One_answer_is_itself_none_is_unavailable_and_a_note_on_any_device_is_kept()
        {
            var only = new FlagBoxPlan { State = FlagBoxInstallState.Outdated, Note = "n" };
            Assert.Same(only, PanelMatrixDevices.Combine(new[] { only }));
            Assert.Equal(FlagBoxInstallState.Unavailable, PanelMatrixDevices.Combine(new FlagBoxPlan[0]).State);
            Assert.Equal(FlagBoxInstallState.Unavailable, PanelMatrixDevices.Combine(null).State);
            var noted = PanelMatrixDevices.Combine(new[]
            {
                new FlagBoxPlan { State = FlagBoxInstallState.UpToDate, Note = "built-in profiles" },
                new FlagBoxPlan { State = FlagBoxInstallState.UpToDate },
            });
            Assert.Equal("built-in profiles", noted.Note);
        }

        [Fact]
        public void A_matrix_records_its_device_and_a_removed_one_forgets_it()
        {
            var settings = new OpenDashSettings();
            settings.Normalise();
            var first = settings.AddMatrixPanel("Wheel", Wheel);
            var second = settings.AddMatrixPanel("Pillar");
            Assert.Equal(Wheel, settings.MatrixDevice(first));
            Assert.Equal(LedBar.ArduinoDevice, settings.MatrixDevice(second));
            Assert.Null(settings.MatrixDevice(3));

            settings.SetMatrixDevice(second, Plate);
            Assert.Equal(Plate, settings.MatrixDevice(second));
            settings.SetMatrixDevice(4, Plate);
            Assert.Null(settings.MatrixDevice(4));

            settings.RemoveMatrixPanel(first);
            Assert.Null(settings.MatrixDevice(first));
            Assert.Null(settings.FlagBoxMatrixDevice[first - 1]);
            // The next matrix in the slot starts on its own device, not the removed one's.
            var again = settings.AddMatrixPanel("Again");
            Assert.Equal(first, again);
            Assert.Equal(LedBar.ArduinoDevice, settings.MatrixDevice(again));
        }

        [Fact]
        public void A_file_from_before_matrices_named_devices_reads_as_the_Arduinos()
        {
            var settings = new OpenDashSettings();
            settings.Normalise();
            var slot = settings.AddMatrixPanel("Old");
            settings.FlagBoxMatrixDevice = null;
            settings.Normalise();
            Assert.Equal(Contract.FlagBoxMatrices.Count, settings.FlagBoxMatrixDevice.Length);
            Assert.Equal(LedBar.ArduinoDevice, settings.MatrixDevice(slot));
        }

        [Fact]
        public void Normalising_drops_a_device_on_an_empty_slot_and_a_copy_does_not_share_the_array()
        {
            var settings = new OpenDashSettings();
            settings.Normalise();
            var slot = settings.AddMatrixPanel("Wheel", Wheel);
            settings.FlagBoxMatrixDevice = new[] { settings.FlagBoxMatrixDevice[0], Plate, " ", null, Plate };
            settings.Normalise();
            Assert.Equal(new[] { Wheel, null, null, null }, settings.FlagBoxMatrixDevice);

            var copy = settings.NormalisedCopy();
            copy.SetMatrixDevice(slot, Plate);
            Assert.Equal(Wheel, settings.MatrixDevice(slot));
        }
    }
}
