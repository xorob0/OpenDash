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
        /// The script says the same thing either way -- it is the same text for every root and every
        /// staging -- so a write that cannot happen is not a reason to report the swap unarmed. Reporting
        /// false here would put "OpenDash itself could not be updated" in front of a driver whose update
        /// was fine.
        /// </remarks>
        [Fact]
        public void Arming_again_over_a_script_that_cannot_be_written_still_counts_as_armed()
        {
            Assert.True(PluginUpdate.Stage(Zip(PluginUpdate.DllName), root).Ok);
            File.WriteAllText(PluginUpdate.ScriptPath(root), PluginUpdate.SwapScript());
            using (File.Open(PluginUpdate.ScriptPath(root), FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                // Windows refuses the write while it is held; Linux allows it, and both end armed.
                // Arm and not Launch: starting a process is the one thing this cannot do here.
                Assert.True(PluginUpdate.Arm(root));
            }
        }

        /// <summary>
        /// The script as cmd runs it from its place under <paramref name="simHubRoot"/>: `%here%` is the
        /// folder it sits in, as `%~dp0` gives it, and `%root%` the folder above, as `%~dp0..` resolves.
        /// </summary>
        private static string AsRunUnder(string simHubRoot)
        {
            var script = PluginUpdate.SwapScript();
            // The two lines that give the variables those values, so that the substitution below is
            // what the script does rather than what this test supposes.
            Assert.Contains("set \"here=%~dp0\"", script);
            Assert.Contains("for %%I in (\"%~dp0..\") do set \"root=%%~fI\"", script);
            var here = FlagBoxProfile.FolderPath(simHubRoot) + "\\";
            return script.Replace("%here%", here).Replace("%root%", simHubRoot).Replace('\\', Path.DirectorySeparatorChar);
        }

        [Fact]
        public void The_swap_waits_for_SimHub_moves_the_file_and_keeps_the_one_it_replaced()
        {
            var script = AsRunUnder(root);
            Assert.Contains("SimHubWPF.exe", script);
            // `ping` and not `timeout`, which needs a console this will not have.
            Assert.Contains("ping -n 2", script);
            Assert.DoesNotContain("timeout /t", script);
            Assert.Contains("move /y \"" + PluginUpdate.StagedPath(root) + "\" \"" + PluginUpdate.InstalledPath(root) + "\"", script);
            // The one it replaces is copied aside first, so a release that turns out worse is one rename
            // away from being undone.
            Assert.Contains(PluginUpdate.BackupName, script);
            TextOrder.Before(script, "copy /y", "move /y");
            // It deletes itself only after the move, so a swap that never happened is tried again next time.
            TextOrder.Before(script, "move /y", "del \"%~f0\"");
        }

        /// <summary>
        /// The driver can ask for SimHub to come back, and the swap consumes the request either way.
        /// </summary>
        /// <remarks>
        /// Consumed rather than left: a standing "reopen" outlives the update that asked for it, and the
        /// next ordinary shutdown would honour it. An application that comes back from a close you meant
        /// is worse than one that does not come back from a close you did not.
        /// </remarks>
        [Fact]
        public void The_swap_reopens_SimHub_only_when_it_was_asked_to()
        {
            var script = AsRunUnder(root);
            var reopen = PluginUpdate.ReopenPath(root);
            var exe = PluginUpdate.SimHubExePath(root);
            Assert.Equal(Path.Combine(root, "SimHubWPF.exe"), exe);
            Assert.Equal(FlagBoxProfile.FolderPath(root), Path.GetDirectoryName(reopen));

            Assert.Contains("if exist \"" + reopen + "\" set reopen=1", script);
            Assert.Contains("del \"" + reopen + "\"", script);
            Assert.Contains("start \"\" \"" + exe + "\"", script);
            // Read and consumed before the swap, and the relaunch after it: a swap that fails leaves no
            // request behind, and a relaunch never happens before the file it was asked for is in place.
            TextOrder.Before(script, "del \"" + reopen + "\"", "move /y");
            TextOrder.Before(script, "move /y", "start \"\"");
        }

        [Fact]
        public void Asking_to_reopen_writes_the_marker_and_taking_it_back_removes_it()
        {
            Assert.False(File.Exists(PluginUpdate.ReopenPath(root)));

            Assert.True(PluginUpdate.AskToReopen(root, true));
            Assert.True(File.Exists(PluginUpdate.ReopenPath(root)));
            // Twice is the same as once: a second update before the first has been applied must not leave
            // two requests, and there is only one file to leave.
            Assert.True(PluginUpdate.AskToReopen(root, true));
            Assert.True(File.Exists(PluginUpdate.ReopenPath(root)));

            Assert.True(PluginUpdate.AskToReopen(root, false));
            Assert.False(File.Exists(PluginUpdate.ReopenPath(root)));
            // And saying no when nothing was asked for is not an error.
            Assert.True(PluginUpdate.AskToReopen(root, false));
        }

        /// <summary>The staged assembly and the script live in OpenDash's own folder, not in SimHub's:
        /// PluginsData is SimHub's, and the assembly in use is the only thing OpenDash puts at the root.</summary>
        [Fact]
        public void What_is_staged_lives_beside_the_flag_box_profile()
        {
            var folder = FlagBoxProfile.FolderPath(root);
            Assert.Equal(folder, Path.GetDirectoryName(PluginUpdate.StagedPath(root)));
            Assert.Equal(folder, Path.GetDirectoryName(PluginUpdate.ScriptPath(root)));
            Assert.Equal(folder, Path.GetDirectoryName(PluginUpdate.ReopenPath(root)));
            Assert.Equal(Path.Combine(root, PluginUpdate.DllName), PluginUpdate.InstalledPath(root));
            // One level down, exactly: the script finds SimHub's root as the folder above its own, so a
            // folder moved deeper would have it replace a DLL that is not the one SimHub loads.
            Assert.Equal(root, Path.GetDirectoryName(folder));
        }

        /// <summary>
        /// The script reads right whatever SimHub's folder is called, because it carries no path at all.
        /// </summary>
        /// <remarks>
        /// cmd reads a batch file in the console's OEM code page and expands every `%` in it. With the
        /// root written in as UTF-8, a SimHub under `C:\Users\José` was read as `Jos├⌐` in code page 437
        /// and `Sim%Hub` as the start of a variable: the `move` failed, the script gave up, and the plugin
        /// never updated (#601). ASCII bytes read the same in every OEM code page, and the only `%` left
        /// are the ones the script means.
        /// </remarks>
        [Fact]
        public void The_swap_script_written_for_an_accented_root_with_a_percent_sign_is_ASCII_and_names_no_path()
        {
            var odd = Path.Combine(root, "Jos\u00e9", "Sim%Hub!\u5c71");
            Assert.True(PluginUpdate.Stage(Zip(PluginUpdate.DllName), odd).Ok);
            Assert.True(PluginUpdate.Arm(odd));

            var bytes = File.ReadAllBytes(PluginUpdate.ScriptPath(odd));
            var firstWide = Array.FindIndex(bytes, b => b >= 0x80);
            Assert.True(firstWide < 0, "byte 0x" + (firstWide < 0 ? "" : bytes[firstWide].ToString("X2")) + " at " + firstWide + " is not ASCII, so cmd reads it as whatever the console's code page says it is");
            var text = Encoding.ASCII.GetString(bytes);
            Assert.DoesNotContain("Jos", text);
            Assert.DoesNotContain("Sim%Hub", text);

            var meant = new[] { "%~dp0", "%~f0", "%%~fI", "%%I", "%here%", "%root%", "%waited%", "%reopen%" };
            var rest = text;
            foreach (var token in meant) rest = rest.Replace(token, "");
            Assert.DoesNotContain("%", rest);
        }

        /// <summary>The version this checkout builds, which the test assembly carries exactly as
        /// OpenDash.dll does: both read it from VERSION through Directory.Build.props.</summary>
        private static string BuiltVersion()
        {
            return File.ReadAllText(Path.Combine(RepoPaths.Root(), "VERSION")).Trim();
        }

        /// <summary>Stages a real assembly, this one, where <see cref="PluginUpdate.Stage"/> leaves it,
        /// with the reopen request and the script an update would have left beside it.</summary>
        private void StageARealAssembly()
        {
            Directory.CreateDirectory(FlagBoxProfile.FolderPath(root));
            File.Copy(typeof(PluginUpdateTests).Assembly.Location, PluginUpdate.StagedPath(root), true);
            Assert.True(PluginUpdate.AskToReopen(root, true));
            Assert.True(PluginUpdate.Arm(root));
        }

        private void AssertNothingIsLeftToSwap()
        {
            Assert.False(PluginUpdate.Pending(root));
            Assert.False(File.Exists(PluginUpdate.StagedPath(root)), "the staged plugin is still there");
            Assert.False(File.Exists(PluginUpdate.ReopenPath(root)), "the reopen request is still there");
            Assert.False(File.Exists(PluginUpdate.ScriptPath(root)), "the swap script is still there");
            Assert.False(File.Exists(PluginUpdate.FailedSwapsPath(root)), "the failed-swap count is still there");
        }

        /// <summary>The version is read out of the staged file itself, pre-release and all, without
        /// loading it: the assembly version would call 0.3.0-rc.6 and 0.3.0-rc.7 both 0.3.0.0.</summary>
        [Fact]
        public void The_staged_version_is_the_informational_version_of_the_staged_file()
        {
            Assert.Null(PluginUpdate.StagedVersion(root));
            StageARealAssembly();
            Assert.Equal(BuiltVersion(), PluginUpdate.StagedVersion(root));
            // What Stage writes from a zip that carries no real assembly has no version to read.
            Assert.True(PluginUpdate.Stage(Zip(PluginUpdate.DllName), root).Ok);
            Assert.Null(PluginUpdate.StagedVersion(root));
        }

        /// <summary>
        /// A staged plugin at or below the one running is cleared at the start rather than armed.
        /// </summary>
        /// <remarks>
        /// The driver who staged 0.5.0 and then installed 0.6.0 by hand: arming it would put 0.5.0 back
        /// over 0.6.0 at the next close (#598). The same version is cleared too, a swap for its twin being
        /// a swap for nothing that still leaves the panel promising a restart.
        /// </remarks>
        [Fact]
        public void A_staged_version_at_or_below_the_running_one_is_cleared_rather_than_armed()
        {
            foreach (var running in new[] { BuiltVersion(), "999.0.0" })
            {
                StageARealAssembly();
                Assert.Equal(StagedPluginVerdict.NotNewer, PluginUpdate.Review(root, running));
                AssertNothingIsLeftToSwap();
                // And Resume, which is what Init calls, arms nothing on top.
                StageARealAssembly();
                Assert.False(PluginUpdate.Resume(root, running));
                AssertNothingIsLeftToSwap();
            }
        }

        [Fact]
        public void A_newer_staged_version_is_still_armed()
        {
            StageARealAssembly();
            Assert.Equal(StagedPluginVerdict.Arm, PluginUpdate.Review(root, "0.0.1"));
            Assert.True(PluginUpdate.Pending(root));
            Assert.True(File.Exists(PluginUpdate.ReopenPath(root)), "the driver's request to reopen was dropped");
            Assert.Equal(1, PluginUpdate.FailedSwaps(root));
        }

        /// <summary>
        /// A swap that keeps failing is given up on, so the panel cannot say "Restart SimHub to finish
        /// updating" for ever.
        /// </summary>
        [Fact]
        public void A_staged_plugin_still_waiting_after_three_starts_is_given_up_on()
        {
            StageARealAssembly();
            for (var start = 1; start < PluginUpdate.MaxFailedSwaps; start++)
            {
                Assert.Equal(StagedPluginVerdict.Arm, PluginUpdate.Review(root, "0.0.1"));
                Assert.Equal(start, PluginUpdate.FailedSwaps(root));
            }
            Assert.Equal(StagedPluginVerdict.GiveUp, PluginUpdate.Review(root, "0.0.1"));
            AssertNothingIsLeftToSwap();
        }

        /// <summary>A new staging is a new assembly with no failed swaps behind it.</summary>
        [Fact]
        public void Staging_again_starts_the_count_again()
        {
            StageARealAssembly();
            Assert.Equal(StagedPluginVerdict.Arm, PluginUpdate.Review(root, "0.0.1"));
            Assert.Equal(StagedPluginVerdict.Arm, PluginUpdate.Review(root, "0.0.1"));
            Assert.True(PluginUpdate.Stage(Zip(PluginUpdate.DllName), root).Ok);
            Assert.Equal(0, PluginUpdate.FailedSwaps(root));
        }

        /// <summary>A staged file with no version to read cannot be told apart from a downgrade, and is
        /// cleared rather than swapped in on trust.</summary>
        [Fact]
        public void A_staged_file_without_a_version_is_cleared()
        {
            Assert.True(PluginUpdate.Stage(Zip(PluginUpdate.DllName), root).Ok);
            Assert.True(PluginUpdate.Arm(root));
            Assert.Equal(StagedPluginVerdict.Unreadable, PluginUpdate.Review(root, "0.0.1"));
            AssertNothingIsLeftToSwap();
        }

        /// <summary>A start with nothing staged does nothing but tidy the count a swap that worked left.</summary>
        [Fact]
        public void Nothing_staged_reviews_to_nothing()
        {
            Assert.Null(PluginUpdate.Review(root, "0.3.0"));
            Directory.CreateDirectory(FlagBoxProfile.FolderPath(root));
            File.WriteAllText(PluginUpdate.FailedSwapsPath(root), "2");
            Assert.Null(PluginUpdate.Review(root, "0.3.0"));
            Assert.False(File.Exists(PluginUpdate.FailedSwapsPath(root)));
            Assert.False(File.Exists(PluginUpdate.ScriptPath(root)));
        }

        [Theory]
        [InlineData(null, "0.3.0", 1, StagedPluginVerdict.Unreadable)]
        [InlineData("", "0.3.0", 1, StagedPluginVerdict.Unreadable)]
        [InlineData("0.3.0", "0.3.0", 1, StagedPluginVerdict.NotNewer)]
        [InlineData("0.5.0", "0.6.0", 1, StagedPluginVerdict.NotNewer)]
        [InlineData("0.3.0-rc.6", "0.3.0-rc.7", 1, StagedPluginVerdict.NotNewer)]
        [InlineData("0.3.0-rc.7", "0.3.0", 1, StagedPluginVerdict.NotNewer)]
        // Older is older however often it has failed: the log calls it stale rather than a failure.
        [InlineData("0.5.0", "0.6.0", 9, StagedPluginVerdict.NotNewer)]
        [InlineData("0.3.0-rc.7", "0.3.0-rc.6", 1, StagedPluginVerdict.Arm)]
        [InlineData("0.3.0", "0.3.0-rc.7", 2, StagedPluginVerdict.Arm)]
        [InlineData("0.6.0", "0.5.0", 3, StagedPluginVerdict.GiveUp)]
        public void Only_a_newer_plugin_is_armed_and_only_so_many_times(string staged, string running, int failedSwaps, StagedPluginVerdict expected)
        {
            Assert.Equal(expected, PluginUpdate.Judge(staged, running, failedSwaps));
        }
    }
}
