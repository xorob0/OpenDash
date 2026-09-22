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

        private static string CarJson(string carId, int leds = 2)
        {
            var colors = string.Join(", ", Enumerable.Repeat("\"#FFFF0000\"", leds + 1));
            var rpm = string.Join(", ", Enumerable.Range(0, leds + 1).Select(i => (7000 - i * 500).ToString()));
            return "{ \"carName\": \"" + carId + "\", \"carId\": \"" + carId + "\", \"ledNumber\": " + leds +
                   ", \"redlineBlinkInterval\": 0, \"ledColor\": [" + colors + "], \"ledRpm\": [{ \"1\": [" + rpm + "] }] }";
        }

        /// <summary>The upstream archive as it really arrives: a top folder named for the branch, ten games' worth.</summary>
        private static byte[] Archive(params string[] carIds)
        {
            var stream = new MemoryStream();
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
            {
                SyntheticPackage.Add(zip, "lovely-car-data-main/README.md", "# not a car");
                SyntheticPackage.Add(zip, "lovely-car-data-main/data/manifest.json", "{}");
                SyntheticPackage.Add(zip, "lovely-car-data-main/data/assettocorsa/some-other-sim.json", CarJson("other sim"));
                foreach (var id in carIds)
                {
                    SyntheticPackage.Add(zip, "lovely-car-data-main/data/iracing/" + id.Replace(' ', '-') + ".json", CarJson(id));
                }
            }
            return stream.ToArray();
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
            public FetchResult GetBytes(string url, Action<double> progress = null)
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
        public void Takes_iracings_cars_out_of_the_archive_and_leaves_the_other_nine_sims_in_it()
        {
            var written = CarLightLibrary.Extract(Archive("test one", "test two"), root);
            Assert.Equal(2, written);
            var files = Directory.GetFiles(root).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal).ToArray();
            // openDash supports one sim, so it keeps one sim's files: 340 KB rather than a megabyte.
            Assert.Equal(new[] { "test-one.json", "test-two.json" }, files);
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
                SyntheticPackage.Add(zip, "x/data/iracing/fine.json", CarJson("fine"));
            }
            Assert.Equal(1, CarLightLibrary.Extract(stream.ToArray(), root));
            Assert.Equal(new[] { "fine.json" }, Directory.GetFiles(root).Select(Path.GetFileName).ToArray());
        }

        [Fact]
        public void Reads_every_car_it_can_and_skips_the_ones_it_cannot()
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "good.json"), CarJson("good car"));
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
        public void An_archive_with_no_iracing_cars_is_not_treated_as_a_good_copy()
        {
            var result = CarLightLibrary.Fetch(new Source { Bytes = Archive() }, root, DateTime.UtcNow);
            Assert.False(result.Ok);
            // Not stamped, so the next start tries again rather than waiting a week on an empty folder.
            Assert.Null(CarLightLibrary.FetchedAt(root));
        }

        [Fact]
        public void The_service_loads_the_cache_and_publishes_a_run_per_length()
        {
            var now = new DateTime(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc);
            var service = new CarLightService(new Source { Bytes = Archive("test one") }, root);
            var loaded = service.Download(now);

            Assert.True(loaded.Ok);
            Assert.Equal(1, service.CarCount);
            service.Update("test one", "1", 6900, MirrorFit.Stretch, true, 0);
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
        }

        [Fact]
        public void A_car_with_no_table_and_a_driver_who_turned_it_off_both_read_as_not_ready()
        {
            var service = new CarLightService(new Source { Bytes = Archive("test one") }, root);
            service.Download(DateTime.UtcNow);

            service.Update("a car nobody measured", "1", 6900, MirrorFit.Stretch, true, 0);
            Assert.False(service.Ready);
            // Still the right width, so a profile that reads it anyway slices dark rather than nothing.
            Assert.Equal(14 * Contract.MirrorColorWidth, service.Run(14).Length);

            service.Update("test one", "1", 6900, MirrorFit.Stretch, false, 0);
            Assert.False(service.Ready);
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
            // #366: openDash never fetches the tables on its own, so the startup path is disk and
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
            Assert.False(service.Stale);
        }

        [Fact]
        public void A_press_fetches_however_fresh_the_copy_is()
        {
            var now = new DateTime(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc);
            var source = new Source { Bytes = Archive("test one") };
            var service = new CarLightService(source, root);

            Assert.True(service.Download(now).Fetched);
            // A press is a person asking, so the week MaxAge describes is not consulted. A second press
            // a minute later asks again, which is what a button that does nothing twice would not.
            Assert.True(service.Download(now.AddMinutes(1)).Fetched);
            Assert.Equal(2, source.Requested.Count);
            Assert.False(service.Stale);
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
            // And what the tables are for, which is the one setting that cannot work without them.
            Assert.Contains("Car-specific", PanelLights.CarTablesCaption);
            // CC BY-NC-SA 4.0 asks for attribution and openDash carries none of the data, so both the
            // licence and the project it came from are on the page for as long as the row is.
            Assert.Contains("CC BY-NC-SA 4.0", PanelLights.CarTablesAttribution);
            Assert.Contains(CarLightLibrary.ProjectUrl, PanelLights.CarTablesAttribution);
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
