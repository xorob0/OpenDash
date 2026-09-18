// PluginUpdateTests.cs: the half of an update that cannot happen while SimHub is running.
//
// What is pinned is the decision and the artefacts, not the swap: the swap is a `cmd` script run by
// Windows after this process is gone, so what a test can hold is that the script says the right thing
// about the right paths and that nothing is staged from an archive that does not carry a plugin.
using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class PluginUpdateTests : IDisposable
    {
        private readonly string root;

        public PluginUpdateTests()
        {
            root = Path.Combine(Path.GetTempPath(), "opendash-plugin-update-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
        }

        public void Dispose()
        {
            try { Directory.Delete(root, true); } catch (Exception) { }
        }

        private static byte[] Zip(params string[] entries)
        {
            using (var buffer = new MemoryStream())
            {
                using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, true))
                {
                    foreach (var name in entries)
                    {
                        var entry = zip.CreateEntry(name);
                        using (var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false)))
                        {
                            writer.Write("contents of " + name);
                        }
                    }
                }
                return buffer.ToArray();
            }
        }

        [Fact]
        public void Nothing_is_pending_until_something_is_staged()
        {
            Assert.False(PluginUpdate.Pending(root));
            Assert.False(PluginUpdate.Launch(root));
            // And launching nothing writes nothing, so a start on an untouched install is inert.
            Assert.False(File.Exists(PluginUpdate.ScriptPath(root)));
        }

        [Fact]
        public void The_assembly_is_taken_out_of_the_release_zip_and_left_beside_the_one_in_use()
        {
            var result = PluginUpdate.Stage(Zip("INSTALL.md", PluginUpdate.DllName, "OFL.txt"), root);
            Assert.True(result.Ok, result.Error);
            Assert.Equal(PluginUpdate.StagedPath(root), result.Path);
            Assert.Equal("contents of " + PluginUpdate.DllName, File.ReadAllText(result.Path));
            Assert.True(PluginUpdate.Pending(root));
            // The assembly in use is not touched: the swap is the only thing that replaces it, and it
            // runs when this process is gone.
            Assert.False(File.Exists(PluginUpdate.InstalledPath(root)));
            // And nothing half-written is left behind for a later run to mistake for a staged assembly.
            Assert.False(File.Exists(result.Path + ".part"));
        }

        /// <summary>A second update before the first was applied replaces what is waiting rather than
        /// failing on a file that is already there.</summary>
        [Fact]
        public void Staging_twice_leaves_the_newer_one_waiting()
        {
            Assert.True(PluginUpdate.Stage(Zip(PluginUpdate.DllName), root).Ok);
            var second = PluginUpdate.Stage(Zip("nested/" + PluginUpdate.DllName), root);
            Assert.True(second.Ok, second.Error);
            Assert.Equal("contents of nested/" + PluginUpdate.DllName, File.ReadAllText(second.Path));
        }

        /// <summary>
        /// An archive with no assembly in it stages nothing.
        /// </summary>
        /// <remarks>
        /// The alternative is a zero-byte OpenDash.dll staged for a swap that would leave SimHub with no
        /// plugin at all and no way to get one back from inside SimHub, which is the one outcome worse
        /// than not updating.
        /// </remarks>
        [Fact]
        public void An_archive_without_the_assembly_stages_nothing()
        {
            foreach (var bytes in new[] { Zip("INSTALL.md", "OFL.txt"), new byte[0], null })
            {
                var result = PluginUpdate.Stage(bytes, root);
                Assert.False(result.Ok);
                Assert.NotNull(result.Error);
                Assert.Null(result.Path);
                Assert.False(PluginUpdate.Pending(root));
            }
            // Nor does something that is not an archive at all.
            var nonsense = PluginUpdate.Stage(Encoding.UTF8.GetBytes("not a zip"), root);
            Assert.False(nonsense.Ok);
            Assert.False(PluginUpdate.Pending(root));
        }

        /// <summary>
        /// Arming twice does not fail, because the first waiter holds the script open.
        /// </summary>
        /// <remarks>
        /// The script says the same thing either way -- the paths do not change between stagings -- so a
        /// write that cannot happen is not a reason to report the swap unarmed. Reporting false here would
        /// put "openDash itself could not be updated" in front of a driver whose update was fine.
        /// </remarks>
        [Fact]
        public void Arming_again_over_a_script_that_cannot_be_written_still_counts_as_armed()
        {
            Assert.True(PluginUpdate.Stage(Zip(PluginUpdate.DllName), root).Ok);
            File.WriteAllText(PluginUpdate.ScriptPath(root), PluginUpdate.SwapScript(root));
            using (File.Open(PluginUpdate.ScriptPath(root), FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                // Windows refuses the write while it is held; Linux allows it, and both end armed.
                // Arm and not Launch: starting a process is the one thing this cannot do here.
                Assert.True(PluginUpdate.Arm(root));
            }
        }

        [Fact]
        public void The_swap_waits_for_SimHub_moves_the_file_and_keeps_the_one_it_replaced()
        {
            var script = PluginUpdate.SwapScript(root);
            Assert.Contains("SimHubWPF.exe", script);
            // `ping` and not `timeout`, which needs a console this will not have.
            Assert.Contains("ping -n 2", script);
            Assert.DoesNotContain("timeout /t", script);
            Assert.Contains("move /y \"" + PluginUpdate.StagedPath(root) + "\" \"" + PluginUpdate.InstalledPath(root) + "\"", script);
            // The one it replaces is copied aside first, so a release that turns out worse is one rename
            // away from being undone.
            Assert.Contains(PluginUpdate.BackupName, script);
            Assert.True(script.IndexOf("copy /y", StringComparison.Ordinal) < script.IndexOf("move /y", StringComparison.Ordinal));
            // It deletes itself only after the move, so a swap that never happened is tried again next time.
            Assert.True(script.IndexOf("move /y", StringComparison.Ordinal) < script.IndexOf("del \"%~f0\"", StringComparison.Ordinal));
        }

        /// <summary>The staged assembly and the script live in openDash's own folder, not in SimHub's:
        /// PluginsData is SimHub's, and the assembly in use is the only thing openDash puts at the root.</summary>
        [Fact]
        public void What_is_staged_lives_beside_the_flag_box_profile()
        {
            var folder = FlagBoxProfile.FolderPath(root);
            Assert.Equal(folder, Path.GetDirectoryName(PluginUpdate.StagedPath(root)));
            Assert.Equal(folder, Path.GetDirectoryName(PluginUpdate.ScriptPath(root)));
            Assert.Equal(Path.Combine(root, PluginUpdate.DllName), PluginUpdate.InstalledPath(root));
        }
    }
}
