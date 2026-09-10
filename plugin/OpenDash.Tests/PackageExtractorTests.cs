// PackageExtractorTests.cs: installs a synthetic .simhubdash into a fake SimHub root and checks the
// folder, the sidecar version, the backup, the font copy rules and the refusal of unsafe entries. When the dash
// has been built, the real package is read as well.
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class PackageExtractorTests : IDisposable
    {
        private readonly string root;

        public PackageExtractorTests()
        {
            root = Path.Combine(Path.GetTempPath(), "opendash-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
        }

        public void Dispose()
        {
            try { Directory.Delete(root, true); } catch { }
        }

        private static MemoryStream Package(string folder, string version, params (string name, string content)[] extra)
        {
            var stream = new MemoryStream();
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
            {
                Add(zip, folder + "/" + folder + ".djson", "{\"Version\":2}");
                Add(zip, folder + "/" + folder + ".djson.metadata", "{\"Title\":\"openDash\",\"DashboardVersion\":\"" + version + "\"}");
                Add(zip, folder + "/cards.djson", "{\"Version\":2}");
                Add(zip, folder + "/_SHFonts/Barlow-Medium.ttf", "font-a");
                Add(zip, folder + "/_SHFonts/BarlowCondensed-Bold.ttf", "font-b");
                foreach (var (name, content) in extra) Add(zip, name, content);
            }
            stream.Position = 0;
            return stream;
        }

        private static void Add(ZipArchive zip, string name, string content)
        {
            using (var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false)))
            {
                writer.Write(content);
            }
        }

        private string Templates(string folder) => Path.Combine(root, "DashTemplates", folder);

        [Fact]
        public void ReadPackageVersion_reads_folder_and_version_without_extracting()
        {
            using (var package = Package("openDash", "0.2.0"))
            {
                string folder;
                Assert.Equal("0.2.0", PackageExtractor.ReadPackageVersion(package, out folder));
                Assert.Equal("openDash", folder);
                Assert.Empty(Directory.GetDirectories(root));
            }
        }

        [BuildOutputFact]
        public void The_real_build_output_is_openDash_at_the_repository_version()
        {
            using (var zip = ZipFile.OpenRead(RepoPaths.BuildPackage()))
            {
                Assert.Equal("openDash", PackageExtractor.PackageFolderName(zip));
            }
            using (var package = File.OpenRead(RepoPaths.BuildPackage()))
            {
                string folder;
                Assert.Equal(RepoPaths.Version(), PackageExtractor.ReadPackageVersion(package, out folder));
                Assert.Equal("openDash", folder);
            }
        }

        [Fact]
        public void ReadPackageVersion_returns_null_folder_for_a_zip_without_dashboard()
        {
            var stream = new MemoryStream();
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true)) Add(zip, "readme.txt", "hi");
            stream.Position = 0;
            string folder;
            Assert.Null(PackageExtractor.ReadPackageVersion(stream, out folder));
            Assert.Null(folder);
        }

        [Fact]
        public void Install_extracts_into_DashTemplates_and_copies_fonts()
        {
            Assert.False(PackageExtractor.IsInstalled(root, "openDash"));
            Assert.Null(PackageExtractor.ReadInstalledVersion(root, "openDash"));

            InstallResult result;
            using (var package = Package("openDash", "0.2.0")) result = PackageExtractor.Install(package, root, null);

            Assert.Equal("openDash", result.FolderName);
            Assert.Equal("0.2.0", result.Version);
            Assert.Equal(2, result.FontsCopied);
            Assert.Null(result.BackupPath);
            Assert.True(File.Exists(Path.Combine(Templates("openDash"), "openDash.djson")));
            Assert.True(File.Exists(Path.Combine(Templates("openDash"), "cards.djson")));
            Assert.True(File.Exists(Path.Combine(root, "DashFonts", "Barlow-Medium.ttf")));
            Assert.True(File.Exists(Path.Combine(root, "DashFonts", "BarlowCondensed-Bold.ttf")));
            Assert.True(PackageExtractor.IsInstalled(root, "openDash"));
            Assert.Equal("0.2.0", PackageExtractor.ReadInstalledVersion(root, "openDash"));
            Assert.Empty(Directory.GetDirectories(Path.Combine(root, "DashTemplates"), "_openDash_staging_*"));
        }

        [Fact]
        public void Install_replaces_an_existing_folder_and_keeps_a_backup()
        {
            using (var package = Package("openDash", "0.1.0")) PackageExtractor.Install(package, root, null);
            File.WriteAllText(Path.Combine(Templates("openDash"), "user-edit.txt"), "edited in DashStudio");

            InstallResult result;
            using (var package = Package("openDash", "0.2.0")) result = PackageExtractor.Install(package, root, null);

            Assert.Equal("0.2.0", PackageExtractor.ReadInstalledVersion(root, "openDash"));
            Assert.False(File.Exists(Path.Combine(Templates("openDash"), "user-edit.txt")));
            Assert.Equal(0, result.FontsCopied);
            Assert.Equal(Path.Combine(root, "DashTemplates", "openDash_backup.zip"), result.BackupPath);
            using (var backup = ZipFile.OpenRead(result.BackupPath))
            {
                Assert.Contains(backup.Entries, entry => entry.FullName.EndsWith("user-edit.txt"));
            }
        }

        [Fact]
        public void Fonts_are_skipped_when_the_same_name_or_the_same_bytes_exist()
        {
            var fonts = Path.Combine(root, "DashFonts");
            Directory.CreateDirectory(fonts);
            File.WriteAllText(Path.Combine(fonts, "Barlow-Medium.ttf"), "older bytes, same name");
            File.WriteAllText(Path.Combine(fonts, "Renamed.ttf"), "font-b");

            InstallResult result;
            using (var package = Package("openDash", "0.2.0")) result = PackageExtractor.Install(package, root, null);

            Assert.Equal(0, result.FontsCopied);
            Assert.Equal("older bytes, same name", File.ReadAllText(Path.Combine(fonts, "Barlow-Medium.ttf")));
            Assert.False(File.Exists(Path.Combine(fonts, "BarlowCondensed-Bold.ttf")));
        }

        [Fact]
        public void Install_refuses_entries_outside_the_package()
        {
            using (var package = Package("openDash", "0.2.0", ("../escape.txt", "no")))
            {
                Assert.Throws<InvalidDataException>(() => PackageExtractor.Install(package, root, null));
            }
            Assert.False(File.Exists(Path.Combine(root, "escape.txt")));
            Assert.False(PackageExtractor.IsInstalled(root, "openDash"));
            Assert.Empty(Directory.GetDirectories(Path.Combine(root, "DashTemplates"), "_openDash_staging_*"));
        }

        [Fact]
        public void Install_refuses_a_zip_that_is_not_a_dashboard()
        {
            var stream = new MemoryStream();
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true)) Add(zip, "openDash/other.djson", "{}");
            stream.Position = 0;
            Assert.Throws<InvalidDataException>(() => PackageExtractor.Install(stream, root, null));
        }

        [Fact]
        public void Install_logs_through_the_given_log()
        {
            var log = new ListLog();
            using (var package = Package("openDash", "0.2.0")) PackageExtractor.Install(package, root, log);
            Assert.Contains(log.Lines, line => line.StartsWith("info: Installed openDash 0.2.0"));
            Assert.Equal(2, log.Lines.Count(line => line.StartsWith("info: Installed font")));
        }

        private sealed class ListLog : IInstallLog
        {
            public System.Collections.Generic.List<string> Lines { get; } = new System.Collections.Generic.List<string>();
            public void Info(string message) => Lines.Add("info: " + message);
            public void Warn(string message) => Lines.Add("warn: " + message);
            public void Error(string message) => Lines.Add("error: " + message);
        }
    }

    /// <summary>A fact that needs build/openDash.simhubdash: reported as skipped, not failed, until `bun run build` has run.</summary>
    public sealed class BuildOutputFactAttribute : FactAttribute
    {
        public BuildOutputFactAttribute()
        {
            try
            {
                if (!File.Exists(RepoPaths.BuildPackage())) Skip = "build/openDash.simhubdash is absent; run `bun run build` first.";
            }
            catch (Exception ex)
            {
                Skip = "The dash build output cannot be located: " + ex.Message;
            }
        }
    }
}
