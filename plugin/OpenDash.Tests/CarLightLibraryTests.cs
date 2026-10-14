// CarLightLibraryTests: getting somebody else's tables onto the machine without breaking anything
// when it does not work.
//
// Every test here is a failure mode, because the success path is one download and a folder read.
// The failures are what matter: no network, half a network, a proxy answering HTML, an archive that
// is not one, a car the database has never heard of. All of them have the same answer -- the strip
// goes back to the ladder iRacing publishes -- and the point of the tests is that none of them
// throws on the way there.
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class CarLightLibraryTests : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "opendash-carlights-" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            try { Directory.Delete(root, true); } catch { }
        }

        /// <summary>SimHub's codes for the games, as GameData.GameName hands them to DataUpdate.</summary>
        private const string IRacing = SimCapabilities.IRacing;
        private const string AssettoCorsa = SimCapabilities.AssettoCorsa;
        private const string Acc = SimCapabilities.AssettoCorsaCompetizione;

        private static string CarJson(string carId, int leds = 2)
        {
            var colors = string.Join(", ", Enumerable.Repeat("\"#FFFF0000\"", leds + 1));
            var rpm = string.Join(", ", Enumerable.Range(0, leds + 1).Select(i => (7000 - i * 500).ToString()));
            return "{ \"carName\": \"" + carId + "\", \"carId\": \"" + carId + "\", \"ledNumber\": " + leds +
                   ", \"redlineBlinkInterval\": 0, \"ledColor\": [" + colors + "], \"ledRpm\": [{ \"1\": [" + rpm + "] }] }";
        }

        /// <summary>
        /// The upstream archive as it really arrives: a top folder named for the branch, ten games' worth,
        /// and a <c>src_data/</c> beside <c>data/</c>. Upstream's is <c>src_data/lmu/</c>, whose name contains
        /// <c>data/lmu/</c>; here it holds a covered sim's folder, so a match on a substring would take it.
        /// </summary>
        private static byte[] Archive(params string[] carIds)
        {
            return ArchiveOf(carIds.Select(id => ("iracing", id.Replace(' ', '-'), CarJson(id))).ToArray());
        }

        /// <summary>The same archive with each file placed by hand: a game folder, the file's name, its contents.</summary>
        private static byte[] ArchiveOf(params (string sim, string file, string json)[] cars)
        {
            var stream = new MemoryStream();
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
            {
                SyntheticPackage.Add(zip, "car-data-main/README.md", "# not a car");
                SyntheticPackage.Add(zip, "car-data-main/data/manifest.json", "{}");
                SyntheticPackage.Add(zip, "car-data-main/data/car-classes.json", "{}");
                SyntheticPackage.Add(zip, "car-data-main/data/lmu/a-team.json", CarJson("a team"));
                SyntheticPackage.Add(zip, "car-data-main/data/automobilista2/a-car.json", CarJson("a car"));
                SyntheticPackage.Add(zip, "car-data-main/src_data/iracing/a-source.json", CarJson("a source"));
                SyntheticPackage.Add(zip, "car-data-main/src_data/assettocorsa/a-source.json", CarJson("a source"));
                foreach (var car in cars)
                {
                    SyntheticPackage.Add(zip, "car-data-main/data/" + car.sim + "/" + car.file + ".json", car.json);
                }
            }
            return stream.ToArray();
        }

        /// <summary>Every file under the cache, relative to it with forward slashes, in order.</summary>
        private string[] FilesUnderRoot()
        {
            return Directory.GetFiles(root, "*", SearchOption.AllDirectories)
                .Select(f => f.Substring(root.Length + 1).Replace('\\', '/'))
                .Where(f => f != CarLightLibrary.StampFile)
                .OrderBy(f => f, StringComparer.Ordinal)
                .ToArray();
        }

        /// <summary>Answers one URL with bytes, or refuses, the way a machine with no network does.</summary>
        private sealed class Source : IReleaseSource
        {
            public byte[] Bytes { get; set; }
            public string Failure { get; set; }
            public List<string> Requested { get; } = new List<string>();

            public FetchResult GetString(string url) { throw new NotSupportedException("the tables are fetched as bytes"); }

            // The progress callback is accepted and dropped: CarLightLibrary fetches the archive without
            // one, because the tables arrive on a background update rather than behind a bar somebody
            // is watching.
            public FetchResult GetBytes(string url, Action<double> progress = null, System.Threading.CancellationToken cancel = default)
            {
                Requested.Add(url);
                if (Failure != null) return FetchResult.Failed(Failure);
                return new FetchResult { Ok = true, Bytes = Bytes };
            }
        }

        [Theory]
        // The files carry SimHub's CarId with its spaces; the archive names the file for the hyphenated
        // form. Both have to find the same car, or half the database is unreachable by the half of the
        // code that got the other spelling.
        [InlineData("stockcars chevycamarozl12022", "stockcars-chevycamarozl12022")]
        [InlineData("Porsche 992 Cup", "porsche-992-cup")]
        [InlineData("  mx5 mx52016  ", "mx5-mx52016")]
        [InlineData("dallara__ir18", "dallara-ir18")]
        [InlineData("", "")]
        [InlineData(null, "")]
        public void A_car_is_found_however_its_id_is_spelled(string carId, string key)
        {
            Assert.Equal(key, CarLightLibrary.Key(carId));
        }

        [Fact]
        public void Takes_each_covered_sims_cars_into_a_folder_of_its_own_and_leaves_the_other_games_in_the_archive()
        {
            var written = CarLightLibrary.Extract(ArchiveOf(
                ("iracing", "test-one", CarJson("test one")),
                ("iracing", "test-two", CarJson("test two")),
                ("assettocorsa", "ks-test-car", CarJson("ks_test_car")),
                ("assettocorsacompetizione", "test-gt3", CarJson("test_gt3"))), root);
            Assert.Equal(4, written);
            // iRacing, Assetto Corsa and ACC, each in its own folder so a car is looked up among its own
            // sim's cars (#748). LMU, AMS2 and the rest stay in the archive, and so does src_data/, which
            // holds upstream's sources rather than cars.
            Assert.Equal(new[]
            {
                "assettocorsa/ks-test-car.json",
                "assettocorsacompetizione/test-gt3.json",
                "iracing/test-one.json",
                "iracing/test-two.json",
            }, FilesUnderRoot());
        }

        [Fact]
        public void The_covered_sims_are_iracing_assetto_corsa_and_acc_by_their_archive_folders()
        {
            // Read from the archive, not from its README: data/<SimHub's game code, folded>/.
            Assert.Equal(new[] { "iracing", "assettocorsa", "assettocorsacompetizione" }, CarLightLibrary.Sims.Select(s => s.Folder).ToArray());
            Assert.Equal(new[] { IRacing, AssettoCorsa, Acc }, CarLightLibrary.Sims.Select(s => s.Code).ToArray());
            Assert.Same(CarLightLibrary.Sims[0], CarLightLibrary.IRacing);
            Assert.Equal("iRacing, Assetto Corsa and Assetto Corsa Competizione", CarLightLibrary.SimNames);
            foreach (var sim in CarLightLibrary.Sims)
            {
                // The code DataUpdate hands over and the name the panel shows both find the sim's folder.
                Assert.Same(sim, CarLightLibrary.SimFor(sim.Code));
                Assert.Same(sim, CarLightLibrary.SimFor(sim.Name));
                Assert.Same(sim, CarLightLibrary.SimFor(sim.Folder));
            }
        }

        [Theory]
        [InlineData("IRacing", "iracing")]
        [InlineData(" iRacing ", "iracing")]
        [InlineData("AssettoCorsa", "assettocorsa")]
        [InlineData("Assetto Corsa", "assettocorsa")]
        [InlineData("AssettoCorsaCompetizione", "assettocorsacompetizione")]
        [InlineData("Assetto Corsa Competizione", "assettocorsacompetizione")]
        // LMU's keys name a team rather than a car, so its folder is left in the archive.
        [InlineData("LMU", null)]
        [InlineData("Le Mans Ultimate", null)]
        // Folded whole, so a game whose name starts with a covered one's is not taken for it.
        [InlineData("AssettoCorsaEVO", null)]
        [InlineData("Automobilista2", null)]
        [InlineData("", null)]
        [InlineData(null, null)]
        public void A_game_is_matched_to_its_sims_folder_however_simhub_spells_it(string game, string folder)
        {
            var sim = CarLightLibrary.SimFor(game);
            Assert.Equal(folder, sim?.Folder);
        }

        [Fact]
        public void An_entry_that_would_escape_the_folder_is_not_written()
        {
            // A zip is a file off the internet. PackageExtractor takes the same precaution.
            var stream = new MemoryStream();
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
            {
                SyntheticPackage.Add(zip, "x/data/iracing/../../escaped.json", CarJson("escaped"));
                SyntheticPackage.Add(zip, "x/data/iracing/nested/deeper.json", CarJson("deeper"));
                SyntheticPackage.Add(zip, "x/data/assettocorsa/../iracing/sideways.json", CarJson("sideways"));
                SyntheticPackage.Add(zip, "x/data/iracing/.json", CarJson("nameless"));
                SyntheticPackage.Add(zip, "x/data/iracing/fine.json", CarJson("fine"));
            }
            Assert.Equal(1, CarLightLibrary.Extract(stream.ToArray(), root));
            Assert.Equal(new[] { "iracing/fine.json" }, FilesUnderRoot());
        }

        [Fact]
        public void Reads_every_car_it_can_and_skips_the_ones_it_cannot()
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "good-car.json"), CarJson("good car"));
            File.WriteAllText(Path.Combine(root, "broken.json"), "{ not json at all");
            File.WriteAllText(Path.Combine(root, "mismatched.json"), "{ \"carId\": \"x\", \"ledNumber\": 4, \"ledColor\": [\"Red\"], \"ledRpm\": [{}] }");
            File.WriteAllText(Path.Combine(root, "notes.txt"), "ignored");

            var cars = CarLightLibrary.ReadFolder(root);
            // One bad file does not cost the other cars, which is the whole reason this is per file.
            Assert.Equal(new[] { "good-car" }, cars.Keys.ToArray());
        }

        [Fact]
        public void A_folder_that_is_not_there_is_no_cars_rather_than_an_error()
        {
            Assert.Empty(CarLightLibrary.ReadFolder(Path.Combine(root, "never-created")));
            Assert.Empty(CarLightLibrary.ReadFolder(null));
            Assert.Null(CarLightLibrary.FetchedAt(root));
            Assert.True(CarLightLibrary.IsStale(root, DateTime.UtcNow));
        }

        [Fact]
        public void A_fetch_stamps_the_folder_and_the_next_one_waits_a_week()
        {
            var now = new DateTime(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc);
            var source = new Source { Bytes = Archive("test one") };
            var result = CarLightLibrary.Fetch(source, root, now);

            Assert.True(result.Ok);
            Assert.True(result.Fetched);
            Assert.Equal(1, result.Cars);
            // One request, for everybody's copy of everything: nothing in it says which car this user
            // is driving, which is the reason it is one archive rather than one car at a time.
            Assert.Equal(new[] { CarLightLibrary.ArchiveUrl }, source.Requested.ToArray());

            Assert.Equal(now, CarLightLibrary.FetchedAt(root));
            Assert.False(CarLightLibrary.IsStale(root, now.AddDays(6)));
            Assert.True(CarLightLibrary.IsStale(root, now.AddDays(7)));
            // A clock that moved backwards is a clock that moved, not a fresh copy.
            Assert.True(CarLightLibrary.IsStale(root, now.AddDays(-1)));
        }

        [Theory]
        [InlineData("NameResolutionFailure")]
        [InlineData("The remote server returned an error: (403) Forbidden.")]
        public void A_fetch_that_does_not_answer_leaves_what_is_on_disk_alone(string failure)
        {
            CarLightLibrary.Extract(Archive("test one"), root);
            var before = CarLightLibrary.ReadFolder(root);

            var result = CarLightLibrary.Fetch(new Source { Failure = failure }, root, DateTime.UtcNow);
            Assert.False(result.Ok);
            Assert.Equal(failure, result.Reason);
            // The copy that works is still the copy that works, and it is not stamped as fresh.
            Assert.Equal(before.Keys, CarLightLibrary.ReadFolder(root).Keys);
            Assert.Null(CarLightLibrary.FetchedAt(root));
        }

        [Fact]
        public void An_answer_that_is_not_an_archive_is_refused_rather_than_thrown()
        {
            // What a captive portal or a proxy answers with, which is the failure a user on hotel
            // wifi actually gets.
            var html = Encoding.UTF8.GetBytes("<html><body>Sign in to continue</body></html>");
            var result = CarLightLibrary.Fetch(new Source { Bytes = html }, root, DateTime.UtcNow);
            Assert.False(result.Ok);
            Assert.NotNull(result.Reason);
        }

        [Fact]
        public void An_archive_with_no_cars_for_any_covered_sim_is_not_treated_as_a_good_copy()
        {
            // LMU's and AMS2's cars are in it, and they are not cars the plugin reads.
            var result = CarLightLibrary.Fetch(new Source { Bytes = Archive() }, root, DateTime.UtcNow);
            Assert.False(result.Ok);
            Assert.Equal("the archive carried no cars for iRacing, Assetto Corsa and Assetto Corsa Competizione", result.Reason);
            // Not stamped, so the next start tries again rather than waiting a week on an empty folder.
            Assert.Null(CarLightLibrary.FetchedAt(root));
        }

        [Fact]
        public void An_archive_with_iracings_cars_and_none_of_assetto_corsas_is_still_a_good_copy()
        {
            // Which is the archive as it might stand any day: one sim's folder empty is not a broken
            // download, and the cars it does carry are counted and stamped (#748).
            var now = new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);
            var service = new CarLightService(new Source { Bytes = Archive("test one", "test two") }, root);
            var result = service.Download(now);
            Assert.True(result.Ok);
            Assert.Equal(2, result.Cars);
            Assert.Equal(now, CarLightLibrary.FetchedAt(root));
            var read = CarLightLibrary.ReadLibrary(root);
            Assert.Equal(2, read.CountFor(CarLightLibrary.SimFor(IRacing)));
            Assert.Equal(0, read.CountFor(CarLightLibrary.SimFor(AssettoCorsa)));
            Assert.Equal("2 cars, updated just now", service.Status);
        }

        [Fact]
        public void An_archive_with_assetto_corsas_cars_alone_is_a_good_copy_too()
        {
            var now = new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);
            var service = new CarLightService(new Source { Bytes = ArchiveOf(("assettocorsa", "ks-test-car", CarJson("ks_test_car"))) }, root);
            var result = service.Download(now);
            Assert.True(result.Ok);
            Assert.Equal(1, service.CarCount);
            Assert.Equal(1, CarLightLibrary.ReadLibrary(root).CountFor(CarLightLibrary.SimFor("Assetto Corsa")));
            Assert.Equal(now, CarLightLibrary.FetchedAt(root));
        }

        [Fact]
        public void A_car_is_looked_up_among_its_own_sims_cars_alone()
        {
            // One id in two sims, with ladders that differ, which is the case a single dictionary got
            // wrong: the last file read won for both games.
            var service = new CarLightService(new Source { Bytes = ArchiveOf(
                ("iracing", "shared-car", CarJson("shared car", 2)),
                ("assettocorsa", "shared-car", CarJson("shared_car", 4)),
                ("assettocorsa", "ks-only-in-ac", CarJson("ks_only_in_ac", 3))) }, root);
            Assert.True(service.Download(DateTime.UtcNow).Ok);
            Assert.Equal(3, service.CarCount);

            Assert.Equal(2, service.For(IRacing, "shared car").LedCount);
            Assert.Equal(4, service.For(AssettoCorsa, "shared_car").LedCount);
            // SimHub's code and the name the panel shows find the same shelf.
            Assert.Equal(4, service.For("Assetto Corsa", "shared_car").LedCount);
            // A car in one sim is not found from another, nor in a game whose tables are not read.
            Assert.Null(service.For(IRacing, "ks_only_in_ac"));
            Assert.NotNull(service.For(AssettoCorsa, "ks_only_in_ac"));
            Assert.Null(service.For(Acc, "shared_car"));
            Assert.Null(service.For(SimCapabilities.LeMansUltimate, "shared car"));
            Assert.Null(service.For(null, "shared car"));

            // And the frame: the same id lights four lamps in AC and two in iRacing, looked up again
            // when the game changes under an unchanged car id.
            service.Update(AssettoCorsa, "shared_car", "1", 6900, MirrorFit.Stretch, true, 0);
            Assert.True(service.Ready);
            Assert.Equal(4, service.Lamps);
            service.Update(IRacing, "shared_car", "1", 6900, MirrorFit.Stretch, true, 0);
            Assert.Equal(2, service.Lamps);
            service.Update(SimCapabilities.LeMansUltimate, "shared_car", "1", 6900, MirrorFit.Stretch, true, 0);
            // A game with no tables is no mirror, which sends the strip back to SimHub's bands.
            Assert.False(service.Ready);
            Assert.Equal(0, service.Lamps);
        }

        [Fact]
        public void An_assetto_corsa_car_lights_its_own_ladder_by_the_folder_name_simhub_reports()
        {
            // AC's GameData.CarId is the car's folder under content/cars, underscores and all; the
            // archive's file is named for it folded, which is the shape of the one AC table upstream.
            var service = new CarLightService(new Source { Bytes = ArchiveOf(("assettocorsa", "ks-test-gt3-2024", CarJson("ks_test_gt3_2024", 6))) }, root);
            Assert.True(service.Download(DateTime.UtcNow).Ok);

            service.Update(AssettoCorsa, "ks_test_gt3_2024", "1", 6900, MirrorFit.Stretch, true, 0);
            Assert.True(service.Ready);
            Assert.Equal(6, service.Lamps);
            Assert.Equal("ks_test_gt3_2024", service.CarName);

            // A car AC has and the archive has not falls back as it always did: no mirror.
            service.Update(AssettoCorsa, "ks_mazda_mx5_cup", "1", 6900, MirrorFit.Stretch, true, 0);
            Assert.False(service.Ready);
            Assert.Equal(0, service.Lamps);
        }

        [Fact]
        public void A_table_is_found_by_its_files_name_as_well_as_by_the_id_inside_and_counted_once()
        {
            // Upstream's one AC table today: the file is named rss-formula-hybrid-2023, the id inside
            // says rss_formula_hybrid_2023_AC, and its README says a file is named for SimHub's id.
            var service = new CarLightService(new Source { Bytes = ArchiveOf(("assettocorsa", "test-hybrid-2023", CarJson("test_hybrid_2023_AC"))) }, root);
            Assert.True(service.Download(DateTime.UtcNow).Ok);
            Assert.Equal(1, service.CarCount);
            Assert.NotNull(service.For(AssettoCorsa, "test_hybrid_2023"));
            Assert.Same(service.For(AssettoCorsa, "test_hybrid_2023"), service.For(AssettoCorsa, "test_hybrid_2023_AC"));
        }

        [Fact]
        public void A_files_name_never_takes_the_key_another_cars_id_has()
        {
            Directory.CreateDirectory(root);
            // b.json says it is car "a"; a-other.json says it is car "b". Each id is its own car's key.
            File.WriteAllText(Path.Combine(root, "b.json"), CarJson("a", 2));
            File.WriteAllText(Path.Combine(root, "a-other.json"), CarJson("b", 3));
            var cars = CarLightLibrary.ReadFolder(root);
            Assert.Equal(2, cars["a"].LedCount);
            Assert.Equal(3, cars["b"].LedCount);
            Assert.Equal(3, cars["a-other"].LedCount);
            Assert.Equal(2, CarLightLibrary.CountCars(cars));
        }

        [Fact]
        public void A_copy_fetched_before_each_sim_had_a_folder_still_reads_as_iracings_cars()
        {
            // Before #748 the tables were iRacing's alone and lay flat in the cache. A rig that fetched
            // them then keeps its cars, in iRacing and nowhere else, until it downloads again.
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "test-one.json"), CarJson("test one"));
            var service = new CarLightService(new Source { Failure = "should not be asked" }, root);
            Assert.True(service.Load(DateTime.UtcNow).Ok);
            Assert.Equal(1, service.CarCount);
            Assert.NotNull(service.For(IRacing, "test one"));
            Assert.Null(service.For(AssettoCorsa, "test one"));
        }

        [Fact]
        public void A_download_moves_the_flat_copy_into_iracings_folder()
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "old-car.json"), CarJson("old car"));
            var service = new CarLightService(new Source { Bytes = Archive("test one") }, root);
            Assert.True(service.Download(DateTime.UtcNow).Ok);
            // The flat file was the old copy of the same folder, so it goes rather than being read twice.
            Assert.Equal(new[] { "iracing/test-one.json" }, FilesUnderRoot());
            Assert.Equal(1, service.CarCount);
            Assert.Null(service.For(IRacing, "old car"));
        }

        [Fact]
        public void A_download_with_no_iracing_cars_leaves_the_flat_copy_where_it_is_read()
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "old-car.json"), CarJson("old car"));
            var service = new CarLightService(new Source { Bytes = ArchiveOf(("assettocorsa", "ks-test-car", CarJson("ks_test_car"))) }, root);
            Assert.True(service.Download(DateTime.UtcNow).Ok);
            // No iracing folder was written, so the old copy is still iRacing's cars.
            Assert.NotNull(service.For(IRacing, "old car"));
            Assert.NotNull(service.For(AssettoCorsa, "ks_test_car"));
            Assert.Equal(2, service.CarCount);
        }

        [Fact]
        public void The_service_loads_the_cache_and_publishes_a_run_per_length()
        {
            var now = new DateTime(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc);
            var service = new CarLightService(new Source { Bytes = Archive("test one") }, root);
            var loaded = service.Download(now);

            Assert.True(loaded.Ok);
            Assert.Equal(1, service.CarCount);
            service.Update(IRacing, "test one", "1", 6900, MirrorFit.Stretch, true, 0);
            Assert.True(service.Ready);
            Assert.Equal("test one", service.CarName);

            foreach (var length in Contract.MirrorRunLengths)
            {
                var run = service.Run(length);
                // Fixed width is the contract with the profile: LED k is left(run, k * 9, 9).
                Assert.Equal(length * Contract.MirrorColorWidth, run.Length);
                for (var k = 0; k < length; k++)
                {
                    var color = run.Substring(k * Contract.MirrorColorWidth, Contract.MirrorColorWidth);
                    Assert.StartsWith("#", color);
                }
            }
            // A length no strip shape uses is not published, and asking for one is empty rather than an
            // error. The grid reaches twenty-five, so what nobody uses is a run past its end.
            Assert.Equal(string.Empty, service.Run(26));

            // The same frame as three numbers, for the screens (#353). Two lamps at 6500 and 6000, both
            // lit at 6900, and the top third of a two-lamp bar begins at the second of them.
            Assert.Equal(2, service.Lamps);
            Assert.Equal(2, service.Lit);
            Assert.Equal(6500, service.TopRpm);
            Assert.Equal(3, service.Stage);
        }

        [Fact]
        public void A_car_with_no_table_and_a_driver_who_turned_it_off_both_read_as_not_ready()
        {
            var service = new CarLightService(new Source { Bytes = Archive("test one") }, root);
            service.Download(DateTime.UtcNow);

            service.Update(IRacing, "a car nobody measured", "1", 6900, MirrorFit.Stretch, true, 0);
            Assert.False(service.Ready);
            // Still the right width, so a profile that reads it anyway slices dark rather than nothing.
            Assert.Equal(14 * Contract.MirrorColorWidth, service.Run(14).Length);

            service.Update(IRacing, "test one", "1", 6900, MirrorFit.Stretch, false, 0);
            Assert.False(service.Ready);

            // And no ladder either, which is what sends a screen's rev bar back to the published one:
            // zero lamps, and the band the flag box reads back to -1.
            Assert.Equal(0, service.Lamps);
            Assert.Equal(0, service.Lit);
            Assert.Equal(0, service.TopRpm);
            Assert.Equal(-1, service.Stage);
        }

        [Fact]
        public void A_frame_that_throws_leaves_the_strip_on_the_published_ladder_rather_than_on_its_last_frame()
        {
            var service = new CarLightService(new Source { Bytes = Archive("test one") }, root);
            service.Download(DateTime.UtcNow);
            service.Update(IRacing, "test one", "1", 6900, MirrorFit.Stretch, true, 0);
            Assert.True(service.Ready);

            // A table that disagrees with itself, which the reader refuses and so has to be made by hand:
            // one colour for a two-LED bar is an IndexOutOfRangeException in the middle of the frame.
            service.For(IRacing, "test one").Colors = new[] { "#FFFF0000" };
            Assert.ThrowsAny<Exception>(() => service.Update(IRacing, "test one", "1", 6900, MirrorFit.Stretch, true, 0));

            // The frame before was lit, and a failure used to leave it published: a strip frozen on its last
            // good frame for as long as the fault lasted (#577). Now it is no mirror at all, which is what
            // sends the profile back to the ladder the sim publishes.
            Assert.False(service.Ready);
            Assert.Equal(0, service.Lamps);
            Assert.Equal(-1, service.Stage);
            Assert.Equal(CarLightService.Packed(CarLightMirror.Dark(14)), service.Run(14));
        }

        [Fact]
        public void A_frame_the_caller_could_not_work_out_is_cleared_the_same_way()
        {
            var service = new CarLightService(new Source { Bytes = Archive("test one") }, root);
            service.Download(DateTime.UtcNow);
            service.Update(IRacing, "test one", "1", 6900, MirrorFit.Stretch, true, 0);
            Assert.True(service.Ready);

            // What DataUpdate does when something before Update throws, such as reading the telemetry.
            service.Clear();
            Assert.False(service.Ready);
            Assert.Equal(0, service.Lamps);
        }

        [Fact]
        public void A_table_whose_gears_are_zero_padded_mirrors_in_every_gear()
        {
            // End to end, from the file on disk: "01" ... "06" is how one car's gears might be written, and
            // the reader takes keys as given, so the mirror has to read them (#577).
            Directory.CreateDirectory(root);
            var rows = string.Join(", ", Enumerable.Range(1, 6).Select(g => "\"0" + g + "\": [7000, " + (g * 1000) + ", " + (g * 1000 + 500) + "]"));
            File.WriteAllText(Path.Combine(root, "test-padded.json"),
                "{ \"carName\": \"test padded\", \"carId\": \"test padded\", \"ledNumber\": 2, \"redlineBlinkInterval\": 0, " +
                "\"ledColor\": [\"#00000000\", \"#FF00FF00\", \"#FFFF0000\"], \"ledRpm\": [{ " + rows + " }] }");
            var service = new CarLightService(new Source { Failure = "should not be asked" }, root);
            Assert.True(service.Load(DateTime.UtcNow).Ok);

            foreach (var gear in new[] { "1", "3", "6", "7" })
            {
                service.Update(IRacing, "test padded", gear, 6900, MirrorFit.Stretch, true, 0);
                Assert.True(service.Ready);
                Assert.Equal(2, service.Lamps);
            }
        }

        [Fact]
        public void Startup_reads_what_is_on_disk_and_asks_for_nothing()
        {
            CarLightLibrary.Extract(Archive("test one"), root);
            var source = new Source { Failure = "should not be asked" };
            var service = new CarLightService(source, root);

            var loaded = service.Load(DateTime.UtcNow);
            Assert.True(loaded.Ok);
            Assert.False(loaded.Fetched);
            Assert.Equal(1, service.CarCount);
            // #789: OpenDash never fetches the tables on its own, so the startup path is disk and
            // nothing else. A rig that has them keeps working offline, every start, for ever.
            Assert.Empty(source.Requested);
        }

        [Fact]
        public void A_fresh_install_has_no_tables_and_has_asked_for_none()
        {
            var source = new Source { Failure = "should not be asked" };
            var service = new CarLightService(source, root);

            var loaded = service.Load(DateTime.UtcNow);
            Assert.False(loaded.Ok);
            Assert.Equal(0, service.CarCount);
            Assert.Empty(source.Requested);
            // Said as a sentence rather than as a count of zero, because it is the state every rig is
            // in until somebody presses the button, and the button is beside it.
            Assert.Equal(PanelLights.CarTablesNone, service.Status);
            // Nothing is stale that was never fetched: the refresh invitation is for a copy that exists.
            Assert.False(service.Stale(DateTime.UtcNow));
        }

        [Fact]
        public void A_press_fetches_however_fresh_the_copy_is()
        {
            // Anchored to the wall clock rather than written as a date, because the last assertion
            // crosses two clocks: `Download` stamps the folder with the time it is handed, and
            // `Stale` is a live property that asks `DateTime.UtcNow` what the time is now. A fixed
            // date therefore passes only while the real clock is within MaxAge of it, and this test
            // duly began failing a week after the day it was written. An hour back leaves the second
            // press, a minute later, safely in the past and far inside the week.
            var now = DateTime.UtcNow.AddHours(-1);
            var source = new Source { Bytes = Archive("test one") };
            var service = new CarLightService(source, root);

            Assert.True(service.Download(now).Fetched);
            // A press is a person asking, so the week MaxAge describes is not consulted. A second press
            // a minute later asks again, which is what a button that does nothing twice would not.
            Assert.True(service.Download(now.AddMinutes(1)).Fetched);
            Assert.Equal(2, source.Requested.Count);
            Assert.False(service.Stale(now.AddMinutes(1)));
        }

        [Fact]
        public void A_download_that_does_not_answer_is_said_plainly_rather_than_left_blank()
        {
            var service = new CarLightService(new Source { Failure = "NameResolutionFailure" }, root);
            var loaded = service.Download(DateTime.UtcNow);

            Assert.False(loaded.Ok);
            Assert.Equal(0, service.CarCount);
            Assert.Contains("NameResolutionFailure", service.Status);
        }

        [Fact]
        public void The_button_offers_a_download_before_there_are_tables_and_a_refresh_after()
        {
            Assert.Equal("Download", PanelLights.CarTablesButton(0));
            Assert.Equal("Update", PanelLights.CarTablesButton(1));
            Assert.Equal("Update", PanelLights.CarTablesButton(85));
        }

        [Fact]
        public void The_copy_beside_the_button_names_what_is_fetched_and_who_measured_it()
        {
            // The privacy argument ADR 0018 part 1 makes, said where a driver reads it rather than only
            // in the source: one request for every car, so the car you are in is not disclosed.
            Assert.Contains("Every car is downloaded at once", PanelLights.CarTablesCaption);
            Assert.Contains("400 KB", PanelLights.CarTablesCaption);
            // And what the tables are for, which is the one setting that cannot work without them: the LEDs
            // page's switch (#369), by its own words.
            Assert.Contains(PanelLeds.CarRevLightsTitle.Substring(1), PanelLights.CarTablesCaption);
            // CC BY-NC-SA 4.0 asks for attribution and OpenDash carries none of the data, so both the
            // licence and the project it came from are on the page for as long as the row is.
            Assert.Contains("CC BY-NC-SA 4.0", PanelLights.CarTablesAttribution);
            Assert.Contains(CarLightLibrary.ProjectUrl, PanelLights.CarTablesAttribution);
            // The lights are rev lights, the panel's one noun for them (#792).
            Assert.StartsWith("Rev lights come from Lovely Car Data, ", CarLightLibrary.Attribution);
        }

        [Theory]
        [InlineData(0, 0, "84 cars, updated just now")]
        [InlineData(0, 45, "84 cars, updated 45 minutes ago")]
        [InlineData(0, 90, "84 cars, updated an hour ago")]
        [InlineData(0, 60 * 26, "84 cars, updated yesterday")]
        [InlineData(0, 60 * 24 * 5, "84 cars, updated 5 days ago")]
        [InlineData(1, 60 * 24 * 5, "1 car, updated 5 days ago")]
        public void Says_how_many_cars_it_has_and_how_old_they_are(int one, int minutes, string expected)
        {
            var now = new DateTime(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc);
            var line = CarLightService.Describe(one == 1 ? 1 : 84, now.AddMinutes(-minutes), now, null);
            Assert.Equal(expected, line);
        }

        [Fact]
        public void A_failed_check_explains_itself_without_hiding_the_copy_that_works()
        {
            var now = new DateTime(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc);
            var line = CarLightService.Describe(84, now.AddDays(-9), now, CarLightRefresh.Failed("NameResolutionFailure", 84));
            Assert.Equal("84 cars, updated 9 days ago (last download failed: NameResolutionFailure)", line);
        }

        [Fact]
        public void With_no_cars_at_all_the_line_is_the_sentence_and_the_reason()
        {
            var now = new DateTime(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc);
            Assert.Equal(PanelLights.CarTablesNone, CarLightService.Describe(0, null, now, null));
            Assert.Equal(
                PanelLights.CarTablesNone + " Download failed: NameResolutionFailure.",
                CarLightService.Describe(0, null, now, CarLightRefresh.Failed("NameResolutionFailure", 0)));
        }
    }
}
