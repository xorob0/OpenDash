// FolderFingerprintTests.cs: the check that decides whether a person's Dash Studio work is about to be destroyed.
// Every case here is biased the same way: when it cannot prove the folder is untouched, it says so, because the
// cost of asking unnecessarily is a click and the cost of staying quiet is somebody's afternoon.
using System;
using System.IO;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class FolderFingerprintTests : IDisposable
    {
        private readonly string root;

        public FolderFingerprintTests()
        {
            root = Path.Combine(Path.GetTempPath(), "opendash-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
        }

        public void Dispose()
        {
            try { Directory.Delete(root, true); } catch { }
        }

        private string Folder(string name = "openDash")
        {
            var folder = Path.Combine(root, name);
            Directory.CreateDirectory(folder);
            Directory.CreateDirectory(Path.Combine(folder, "_SHFonts"));
            File.WriteAllText(Path.Combine(folder, name + ".djson"), "{\"Version\":2}");
            File.WriteAllText(Path.Combine(folder, name + ".djson.metadata"), "{\"DashboardVersion\":\"0.1.0\"}");
            File.WriteAllText(Path.Combine(folder, "_SHFonts", "Barlow-Medium.ttf"), "font bytes");
            return folder;
        }

        [Fact]
        public void The_same_folder_hashes_the_same_twice()
        {
            var folder = Folder();
            Assert.Equal(FolderFingerprint.Of(folder), FolderFingerprint.Of(folder));
            Assert.StartsWith("sha256:", FolderFingerprint.Of(folder));
        }

        [Fact]
        public void Editing_a_dashboard_changes_it()
        {
            var folder = Folder();
            var before = FolderFingerprint.Of(folder);

            File.WriteAllText(Path.Combine(folder, "openDash.djson"), "{\"Version\":2,\"edited\":true}");
            Assert.NotEqual(before, FolderFingerprint.Of(folder));
            Assert.False(FolderFingerprint.LooksUntouched(folder, before));
        }

        [Fact]
        public void Adding_or_renaming_an_authored_file_changes_it()
        {
            var folder = Folder();
            var before = FolderFingerprint.Of(folder);

            File.WriteAllText(Path.Combine(folder, "mine.djson"), "{\"Version\":2}");
            var added = FolderFingerprint.Of(folder);
            Assert.NotEqual(before, added);

            File.Move(Path.Combine(folder, "mine.djson"), Path.Combine(folder, "ours.djson"));
            Assert.NotEqual(added, FolderFingerprint.Of(folder));
        }

        [Fact]
        public void A_font_that_differs_is_not_somebody_editing_a_dashboard()
        {
            // Fonts are copied rather than authored, and the renamed faces changed under XOR-108 without anyone
            // touching a dashboard. Stopping to ask about one would be asking about the wrong thing.
            var folder = Folder();
            var before = FolderFingerprint.Of(folder);

            File.WriteAllText(Path.Combine(folder, "_SHFonts", "Barlow-Medium.ttf"), "different font bytes");
            Assert.Equal(before, FolderFingerprint.Of(folder));
            Assert.True(FolderFingerprint.LooksUntouched(folder, before));
        }

        [Fact]
        public void Where_the_folder_sits_does_not_change_it()
        {
            var first = Folder("openDash");
            var recorded = FolderFingerprint.Of(first);

            var elsewhere = Path.Combine(root, "moved");
            Directory.CreateDirectory(elsewhere);
            Directory.Move(first, Path.Combine(elsewhere, "openDash"));

            Assert.Equal(recorded, FolderFingerprint.Of(Path.Combine(elsewhere, "openDash")));
        }

        /// <summary>
        /// A fingerprint that could not be computed must not erase the one we had. Erasing turned "cannot vouch for
        /// this folder" into "this folder is not ours", and the adoption branch would then have recorded whatever was
        /// on disk, including somebody's edit, as openDash's own work.
        /// </summary>
        [Fact]
        public void A_fingerprint_that_could_not_be_computed_does_not_erase_the_one_we_had()
        {
            var settings = new OpenDashSettings();
            var record = new SettingsFolderRecord(() => settings);
            record.Set("openDash", "sha256:abc");

            record.Set("openDash", null);
            Assert.Equal("sha256:abc", record.Get("openDash"));

            record.Set("openDash", "   ");
            Assert.Equal("sha256:abc", record.Get("openDash"));

            record.Set("openDash", "sha256:def");
            Assert.Equal("sha256:def", record.Get("openDash"));
        }

        /// <summary>
        /// The record that actually ships, rather than the one the other tests substitute. It records and does not
        /// save: applying an update reaches it from a thread-pool thread, where serialising the settings would race
        /// the settings page on the UI thread.
        /// </summary>
        [Fact]
        public void The_record_the_plugin_uses_reads_and_writes_the_settings_without_saving()
        {
            var settings = new OpenDashSettings();
            var record = new SettingsFolderRecord(() => settings);

            Assert.Null(record.Get("openDash"));
            record.Set("openDash", "sha256:abc");

            Assert.Equal("sha256:abc", record.Get("openDash"));
            Assert.Equal("sha256:abc", settings.FolderFingerprints["openDash"]);

            // Folder names come from a zip and from a filesystem, so the lookup does not depend on their case.
            Assert.Equal("sha256:abc", record.Get("OPENDASH"));
            Assert.Null(record.Get(null));
            record.Set(null, "sha256:x");
            Assert.Single(settings.FolderFingerprints);
        }

        [Fact]
        public void What_cannot_be_proved_untouched_is_treated_as_touched()
        {
            var folder = Folder();
            var recorded = FolderFingerprint.Of(folder);

            Assert.True(FolderFingerprint.LooksUntouched(folder, recorded));

            // No record kept: the case every folder is in the first time, and after a restore.
            Assert.False(FolderFingerprint.LooksUntouched(folder, null));
            Assert.False(FolderFingerprint.LooksUntouched(folder, "   "));

            // A folder that is not there at all.
            Assert.Null(FolderFingerprint.Of(Path.Combine(root, "absent")));
            Assert.Null(FolderFingerprint.Of(null));
            Assert.False(FolderFingerprint.LooksUntouched(Path.Combine(root, "absent"), recorded));
        }
    }
}
