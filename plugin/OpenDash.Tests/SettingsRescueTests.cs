// SettingsRescueTests.cs: a settings file SimHub could not read is set aside before OpenDash saves over it, and
// a copy already there is never overwritten (#643). The fixture is the ticket's own damage: "Rig": 5, a number
// where SimHub's Newtonsoft reader wants a list of screens.
using System;
using System.IO;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public sealed class SettingsRescueTests : IDisposable
    {
        private readonly string folder;
        private readonly string settings;

        public SettingsRescueTests()
        {
            folder = Path.Combine(Path.GetTempPath(), "opendash-rescue-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            settings = Path.Combine(folder, "OpenDash.GeneralSettings.json");
        }

        public void Dispose()
        {
            try { Directory.Delete(folder, true); } catch { }
        }

        private static string Unreadable()
        {
            return File.ReadAllText(Path.Combine(RepoPaths.Root(), "plugin", "OpenDash.Tests", "Fixtures", "unreadable-settings.json"));
        }

        [Fact]
        public void The_fixture_is_the_damage_the_ticket_names()
        {
            Assert.Contains("\"Rig\": 5", Unreadable());
        }

        [Fact]
        public void The_copy_sits_beside_the_file_with_unreadable_before_its_extension()
        {
            Assert.Equal(Path.Combine(folder, "OpenDash.GeneralSettings.unreadable.json"), SettingsRescue.CopyPathFor(settings));
            Assert.Null(SettingsRescue.CopyPathFor(null));
        }

        [Fact]
        public void An_unreadable_file_is_copied_aside_before_the_first_save()
        {
            File.WriteAllText(settings, Unreadable());
            var log = new ListLog();
            var rescue = SettingsRescue.Inspect(settings, true, log);
            Assert.True(rescue.Unreadable);
            Assert.Equal(SettingsRescue.CopyPathFor(settings), rescue.CopyPath);
            Assert.Equal(Unreadable(), File.ReadAllText(rescue.CopyPath));
            Assert.Equal(settings, rescue.SettingsPath);
            Assert.Equal(Path.Combine(folder, "_Backups"), rescue.BackupsPath);
            // The original is left where it is: SimHub's save moves it to _b1 and writes the defaults.
            Assert.Equal(Unreadable(), File.ReadAllText(settings));
            Assert.Contains(log.Lines, line => line.StartsWith("warn: ", StringComparison.Ordinal) && line.Contains(rescue.CopyPath));
        }

        [Fact]
        public void A_read_that_did_not_fall_back_to_defaults_sets_nothing_aside()
        {
            File.WriteAllText(settings, Unreadable());
            var rescue = SettingsRescue.Inspect(settings, false, new ListLog());
            Assert.False(rescue.Unreadable);
            Assert.Null(rescue.CopyPath);
            Assert.False(File.Exists(SettingsRescue.CopyPathFor(settings)));
        }

        /// <summary>No file, or a file with nothing in it, is a first run: there is nothing to keep.</summary>
        [Fact]
        public void A_first_run_sets_nothing_aside()
        {
            Assert.False(SettingsRescue.Inspect(settings, true, new ListLog()).Unreadable);
            File.WriteAllText(settings, "  \r\n");
            Assert.False(SettingsRescue.Inspect(settings, true, new ListLog()).Unreadable);
            Assert.False(File.Exists(SettingsRescue.CopyPathFor(settings)));
            Assert.False(SettingsRescue.Inspect(null, true, null).Unreadable);
        }

        /// <summary>The copy is the first unreadable file, perhaps half mended, and is never overwritten. The
        /// same file again is still kept there; another one is not, and SimHub's _Backups holds it.</summary>
        [Fact]
        public void A_copy_already_there_is_never_overwritten()
        {
            File.WriteAllText(settings, Unreadable());
            var first = SettingsRescue.Inspect(settings, true, new ListLog());
            Assert.Equal(SettingsRescue.CopyPathFor(settings), SettingsRescue.Inspect(settings, true, new ListLog()).CopyPath);

            File.WriteAllText(settings, "{ \"Rig\": true }");
            var second = SettingsRescue.Inspect(settings, true, new ListLog());
            Assert.True(second.Unreadable);
            Assert.Null(second.CopyPath);
            Assert.Equal(Unreadable(), File.ReadAllText(first.CopyPath));
        }

        [Fact]
        public void Nothing_read_says_nothing()
        {
            Assert.False(SettingsRescue.Read.Unreadable);
            Assert.Null(SettingsRescue.Read.CopyPath);
            Assert.Null(SettingsRescue.Read.BackupsPath);
        }
    }
}
