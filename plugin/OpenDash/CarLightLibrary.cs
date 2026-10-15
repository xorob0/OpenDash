// CarLightLibrary.cs: getting the measured tables onto the user's machine, and finding one by car.
//
// ADR 0018 decided OpenDash carries none of this data: it is CC BY-NC-SA 4.0, this repository is
// MIT, and data/shift-points.json says OpenDash does not ship measurements it has not made. So the
// tables are fetched, once, and everything after that is local.
//
// **The whole set is fetched in one request rather than one car at a time**, and that is a privacy
// decision rather than an optimisation. A per-car fetch would tell a CDN which car the user got into
// and when, every time they tried a new one -- a session detail leaving the machine, which is the
// line ADR 0012 was written to keep. One archive of every car asks the same question every other
// user asks, discloses nothing about this one, and has the better failure mode besides: a car works
// the first time it is driven, offline, in a session that never reaches the network at all.
//
// It costs 386 KB, once, and only when the driver asks: the tables are fetched by a button on the
// Lights tab and by nothing else (#789). Starting SimHub never reaches out, and neither does the
// update check, which used to carry this along with its own. A copy on disk is the user's own, made
// when they pressed a button that had named the project, the licence, the size and the host -- which
// is what lets OpenDash carry none of the data and still light a wheel with it.
//
// No SimHub or WPF types -- file and zip handling is fine, PackageExtractor does the same and is
// compiled into the tests too. Nothing here throws: a car with no table is a car on the published
// ladder, which is where every car was before this file existed.
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace OpenDashPlugin
{
    /// <summary>What a refresh did, so that the panel can say it and the tests can assert it.</summary>
    public sealed class CarLightRefresh
    {
        public bool Ok { get; set; }

        /// <summary>True when the network was actually reached this time.</summary>
        public bool Fetched { get; set; }

        /// <summary>How many cars are readable afterwards.</summary>
        public int Cars { get; set; }

        /// <summary>Why it did not work, for the log and the panel. Null when it did.</summary>
        public string Reason { get; set; }

        public static CarLightRefresh Failed(string reason, int cars)
        {
            return new CarLightRefresh { Ok = false, Reason = reason, Cars = cars };
        }
    }

    /// <summary>One sim whose tables are read: how SimHub codes it, how a person names it, and the archive's folder for it.</summary>
    public sealed class CarLightSim
    {
        public CarLightSim(string code, string name, string folder)
        {
            Code = code;
            Name = name;
            Folder = folder;
        }

        /// <summary>SimHub's code for the game, as GameData.GameName carries it ("AssettoCorsa").</summary>
        public string Code { get; private set; }

        /// <summary>The game as a person names it ("Assetto Corsa").</summary>
        public string Name { get; private set; }

        /// <summary>The game's folder, under <c>data/</c> in the archive and under the cache on disk.</summary>
        public string Folder { get; private set; }
    }

    /// <summary>
    /// Every table read, one shelf per sim, so that a car is looked up only among its own sim's cars
    /// (#748). Immutable: the service swaps a whole one in, so a frame never reads half of a read.
    /// </summary>
    public sealed class CarLightCars
    {
        private readonly Dictionary<string, Dictionary<string, CarLightTable>> bySim;
        private readonly Dictionary<string, int> counts;

        public static readonly CarLightCars None = new CarLightCars(
            new Dictionary<string, Dictionary<string, CarLightTable>>(StringComparer.Ordinal),
            new Dictionary<string, int>(StringComparer.Ordinal));

        public CarLightCars(Dictionary<string, Dictionary<string, CarLightTable>> bySim, Dictionary<string, int> counts)
        {
            this.bySim = bySim;
            this.counts = counts;
            Count = counts.Values.Sum();
        }

        /// <summary>How many cars there are, in every sim together: what the panel counts.</summary>
        public int Count { get; private set; }

        /// <summary>How many cars one sim has, by its archive folder. Zero for a sim with none.</summary>
        public int CountFor(CarLightSim sim)
        {
            int count;
            return sim != null && counts.TryGetValue(sim.Folder, out count) ? count : 0;
        }

        /// <summary>
        /// The table for a car in a game, or null: no table, or a game whose tables are not read. A car is
        /// never found in another sim's shelf, so a key two sims share lights the right one.
        /// </summary>
        public CarLightTable For(string game, string carId)
        {
            var sim = CarLightLibrary.SimFor(game);
            if (sim == null) return null;
            Dictionary<string, CarLightTable> cars;
            if (!bySim.TryGetValue(sim.Folder, out cars)) return null;
            CarLightTable table;
            return cars.TryGetValue(CarLightLibrary.Key(carId), out table) ? table : null;
        }
    }

    public static class CarLightLibrary
    {
        /// <summary>
        /// The archive of the whole database, from the branch its own README points a consumer at.
        ///
        /// <para>codeload rather than the API: it needs no token, answers one request with one file,
        /// and sends nothing but the User-Agent <see cref="ReleaseClient"/> puts on it (ADR 0012).</para>
        /// </summary>
        public const string ArchiveUrl = "https://codeload.github.com/Lovely-Sim-Racing/lovely-car-data/zip/refs/heads/main";

        /// <summary>
        /// Who measured all this. Shown on the lights page, and not decoration: CC BY-NC-SA 4.0 asks
        /// for attribution, and a user is entitled to know whose numbers are lighting their wheel.
        /// </summary>
        public const string Attribution = "Rev lights come from Lovely Car Data, by Lovely Sim Racing, ATSR and Gomez Sim Industries, under CC BY-NC-SA 4.0.";

        /// <summary>Where the attribution points.</summary>
        public const string ProjectUrl = "https://github.com/Lovely-Sim-Racing/lovely-car-data";

        /// <summary>Under SimHub/OpenDash/, beside the profiles the user picks up. Not PluginsData: that is SimHub's.</summary>
        public const string FolderName = "CarLights";

        /// <summary>Written after a successful fetch, and read to decide whether another is due.</summary>
        public const string StampFile = "fetched.txt";

        /// <summary>
        /// How old a copy has to be before the panel offers a refresh. A week: the database gains a car
        /// every few weeks, and nothing about a wheel's lights is urgent enough to mention sooner.
        ///
        /// <para>It decides a sentence and not a request. Nothing refetches on its own since #789, so a
        /// copy older than this is one whose status line on the LEDs page says it was updated over a week
        /// ago (PanelLights.CarTablesStale); pressing is the driver's business and a rig that never presses
        /// again keeps the cars it has.</para>
        /// </summary>
        public static readonly TimeSpan MaxAge = TimeSpan.FromDays(7);

        /// <summary>
        /// The sims whose folders are taken out of the archive, out of the ten games it carries.
        ///
        /// <para>Each was read from the archive itself rather than from its README: a game's folder is
        /// SimHub's game code folded (<c>data/assettocorsa/</c>), and a file names its car the way SimHub's
        /// <c>GameData.CarId</c> carries it in that game. On Assetto Corsa that is the car's folder under
        /// <c>content/cars</c>, and on ACC the model name its shared memory gives (<c>bmw_m4_gt3</c>), both
        /// read out of SimHub 9.12.6's readers (#748).</para>
        ///
        /// <para>Le Mans Ultimate's folder is in the archive as well and is left there. Its files key a car
        /// by team rather than by model, so ten of them share one key, and one key names both a hypercar and
        /// a GTE. A wrong car's ladder is worse than SimHub's bands, so LMU waits until its keys are
        /// settled.</para>
        /// </summary>
        public static readonly IReadOnlyList<CarLightSim> Sims = new[]
        {
            new CarLightSim(SimCapabilities.IRacing, "iRacing", "iracing"),
            new CarLightSim(SimCapabilities.AssettoCorsa, "Assetto Corsa", "assettocorsa"),
            new CarLightSim(SimCapabilities.AssettoCorsaCompetizione, "Assetto Corsa Competizione", "assettocorsacompetizione"),
        };

        /// <summary>The sim whose tables were the only ones before #748, and whose copy may still lie flat in the folder.</summary>
        public static CarLightSim IRacing
        {
            get { return Sims[0]; }
        }

        /// <summary>The folder the archive keeps its games in, the level above each game's own.</summary>
        private const string ArchiveData = "data";

        /// <summary>
        /// A game as the archive names its folder: lower case, letters and digits only.
        ///
        /// <para>Whatever hands the game over comes out the same. SimHub's code ("AssettoCorsa", which is
        /// GameData.GameName in DataUpdate) and its display name ("Assetto Corsa", which is the panel's
        /// LiveStatus.GameName) both fold to <c>assettocorsa</c>, the archive's folder.</para>
        /// </summary>
        public static string GameKey(string game)
        {
            if (string.IsNullOrWhiteSpace(game)) return string.Empty;
            var text = new StringBuilder();
            foreach (var c in game.Trim().ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(c)) text.Append(c);
            }
            return text.ToString();
        }

        /// <summary>The sims the tables cover, the way a sentence lists them: "iRacing, Assetto Corsa and Assetto Corsa Competizione".</summary>
        public static string SimNames
        {
            get
            {
                var names = Sims.Select(s => s.Name).ToArray();
                if (names.Length == 1) return names[0];
                return string.Join(", ", names, 0, names.Length - 1) + " and " + names[names.Length - 1];
            }
        }

        /// <summary>The sim whose tables cover this game, as SimHub codes it or names it; null for any other.</summary>
        public static CarLightSim SimFor(string game)
        {
            var key = GameKey(game);
            if (key.Length == 0) return null;
            foreach (var sim in Sims)
            {
                if (string.Equals(sim.Folder, key, StringComparison.Ordinal)) return sim;
            }
            return null;
        }

        public static string FolderPath(string simHubRoot)
        {
            return Path.Combine(simHubRoot, FlagBoxProfile.FolderName, FolderName);
        }

        /// <summary>
        /// The key a car is looked up by: lower case, and everything that is not a letter or a digit
        /// folded to a hyphen.
        ///
        /// <para>The files carry the car's iRacing CarPath verbatim, spaces and all
        /// ("stockcars chevycamarozl12022"), while the archive names the file for the hyphenated form.
        /// Folding both makes the lookup indifferent to which one it was given. Assetto Corsa's and ACC's
        /// ids are folder and model names with underscores ("bmw_m4_gt3"), which fold the same way.</para>
        ///
        /// <para>What hands it over matters as much as the folding. The upstream README says the key
        /// is <c>DataCorePlugin.CarId</c> and <b>that property is empty on iRacing</b>: the CarPath is
        /// on <c>GameData.CarId</c> — <c>StatusDataBase.CarId</c>, which is what the plugin passes in.
        /// Measured on the VM, in a Porsche 992R GT3: GameData.CarId was "porsche992rgt3" and
        /// DataCorePlugin.CarId was null. Reading the documented one finds no car, ever, in silence.</para>
        /// </summary>
        public static string Key(string carId)
        {
            if (string.IsNullOrWhiteSpace(carId)) return string.Empty;
            var text = new StringBuilder();
            var lastWasSeparator = true;
            foreach (var c in carId.Trim().ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(c))
                {
                    text.Append(c);
                    lastWasSeparator = false;
                }
                else if (!lastWasSeparator)
                {
                    text.Append('-');
                    lastWasSeparator = true;
                }
            }
            return text.ToString().Trim('-');
        }

        /// <summary>
        /// Every readable table in one folder, keyed by <see cref="Key"/> of its car id. Empty when there is
        /// no folder.
        ///
        /// <para>A table is also found by its file's name when that folds to something else, and only
        /// where no table's own id already claims it. The upstream README names a file for the car's
        /// SimHub id, and on iRacing the two always agree. Assetto Corsa's one table does not:
        /// <c>rss-formula-hybrid-2023.json</c> says <c>rss_formula_hybrid_2023_AC</c> inside. Taking both
        /// finds the car whichever of the two SimHub hands over, and a car is still counted once.</para>
        /// </summary>
        public static Dictionary<string, CarLightTable> ReadFolder(string folder)
        {
            var cars = new Dictionary<string, CarLightTable>(StringComparer.Ordinal);
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return cars;
            var named = new List<KeyValuePair<string, CarLightTable>>();
            foreach (var file in Directory.GetFiles(folder, "*.json"))
            {
                CarLightTable table;
                try
                {
                    table = CarLightTable.Parse(File.ReadAllText(file));
                }
                catch (Exception)
                {
                    // A file that cannot even be read off the disk is one car on the published ladder.
                    continue;
                }
                if (table == null) continue;
                cars[Key(table.CarId)] = table;
                named.Add(new KeyValuePair<string, CarLightTable>(Key(Path.GetFileNameWithoutExtension(file)), table));
            }
            // After every id is in, so that a file's name never takes the key another car's id has.
            foreach (var name in named)
            {
                if (name.Key.Length > 0 && !cars.ContainsKey(name.Key)) cars[name.Key] = name.Value;
            }
            return cars;
        }

        /// <summary>How many cars a folder's read holds: tables, not keys, since a table may be found by two.</summary>
        public static int CountCars(Dictionary<string, CarLightTable> cars)
        {
            if (cars == null) return 0;
            var tables = new HashSet<CarLightTable>();
            foreach (var table in cars.Values) tables.Add(table);
            return tables.Count;
        }

        /// <summary>
        /// Every sim's tables under the cache, each from its own folder (<c>CarLights/assettocorsa/</c>).
        ///
        /// <para>iRacing's are read from the cache's root when there is no <c>iracing/</c> folder yet:
        /// before #748 they were the only tables and were written there, flat, and a rig that fetched
        /// them then keeps its cars until the next download moves them.</para>
        /// </summary>
        public static CarLightCars ReadLibrary(string folder)
        {
            var bySim = new Dictionary<string, Dictionary<string, CarLightTable>>(StringComparer.Ordinal);
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            if (string.IsNullOrWhiteSpace(folder)) return new CarLightCars(bySim, counts);
            foreach (var sim in Sims)
            {
                var own = Path.Combine(folder, sim.Folder);
                var read = sim == IRacing && !Directory.Exists(own) ? folder : own;
                var cars = ReadFolder(read);
                bySim[sim.Folder] = cars;
                counts[sim.Folder] = CountCars(cars);
            }
            return new CarLightCars(bySim, counts);
        }

        /// <summary>
        /// The sim and file an archive entry is, or null for anything else: <c>.../data/&lt;sim&gt;/&lt;car&gt;.json</c>
        /// for a sim in <see cref="Sims"/>, matched by whole path segments.
        ///
        /// <para>Segments rather than a substring, because the archive's <c>src_data/lmu/</c> contains
        /// <c>data/lmu/</c>, and a substring match would take a source file for a car.</para>
        /// </summary>
        private static KeyValuePair<CarLightSim, string>? EntryFor(string fullName)
        {
            var parts = fullName.Replace('\\', '/').Split('/');
            var n = parts.Length;
            if (n < 3) return null;
            var leaf = parts[n - 1];
            if (leaf.Length <= ".json".Length || !leaf.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) return null;
            if (!string.Equals(parts[n - 3], ArchiveData, StringComparison.OrdinalIgnoreCase)) return null;
            foreach (var sim in Sims)
            {
                if (string.Equals(parts[n - 2], sim.Folder, StringComparison.OrdinalIgnoreCase))
                    return new KeyValuePair<CarLightSim, string>(sim, leaf);
            }
            return null;
        }

        /// <summary>
        /// Writes each covered sim's car files out of the archive into that sim's own folder, replacing
        /// whatever was there, and returns how many landed in all.
        ///
        /// <para>Entries are taken by their last three segments rather than by position, because the
        /// archive's top folder is named for the branch and that is upstream's business. Anything that is
        /// not <c>data/&lt;sim&gt;/&lt;car&gt;.json</c> for a sim in <see cref="Sims"/> is skipped, and so is
        /// any entry whose name would escape the folder -- a zip is a file from the internet, and
        /// PackageExtractor takes the same precaution.</para>
        ///
        /// <para>Once iRacing's files are in <c>iracing/</c>, the flat copy an older OpenDash left in the
        /// cache's root is deleted: it is the same cars, and nothing reads it any more.</para>
        /// </summary>
        public static int Extract(byte[] archive, string folder)
        {
            if (archive == null || archive.Length == 0) return 0;
            Directory.CreateDirectory(folder);
            var written = 0;
            var iracing = 0;
            using (var stream = new MemoryStream(archive))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                foreach (var entry in zip.Entries)
                {
                    var found = EntryFor(entry.FullName);
                    if (found == null) continue;
                    var sim = found.Value.Key;
                    var own = Path.Combine(folder, sim.Folder);
                    var target = Path.Combine(own, found.Value.Value);
                    // Belt and braces over the segment match: the written path must still be inside the sim's folder.
                    var inside = Path.GetFullPath(own).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                    if (!Path.GetFullPath(target).StartsWith(inside, StringComparison.OrdinalIgnoreCase)) continue;
                    try
                    {
                        Directory.CreateDirectory(own);
                        entry.ExtractToFile(target, true);
                        written++;
                        if (sim == IRacing) iracing++;
                    }
                    catch (Exception)
                    {
                        // One file that will not write is one car on the published ladder.
                    }
                }
            }
            if (iracing > 0) DropFlatCopy(folder);
            return written;
        }

        /// <summary>The tables an OpenDash before #748 wrote straight into the cache, now in <c>iracing/</c>.</summary>
        private static void DropFlatCopy(string folder)
        {
            try
            {
                foreach (var file in Directory.GetFiles(folder, "*.json")) File.Delete(file);
            }
            catch (Exception)
            {
                // Left behind, they are read by nothing: the iracing folder exists, so it is read instead.
            }
        }

        /// <summary>When the folder was last filled from upstream, or null if it never was.</summary>
        public static DateTime? FetchedAt(string folder)
        {
            try
            {
                var stamp = Path.Combine(folder, StampFile);
                if (!File.Exists(stamp)) return null;
                long ticks;
                if (!long.TryParse(File.ReadAllText(stamp).Trim(), out ticks)) return null;
                return new DateTime(ticks, DateTimeKind.Utc);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Whether the copy is old enough to be worth mentioning: never fetched, or fetched longer ago
        /// than <see cref="MaxAge"/>. Read by the panel, and by nothing that makes a request.
        /// </summary>
        public static bool IsStale(string folder, DateTime nowUtc)
        {
            var fetched = FetchedAt(folder);
            if (fetched == null) return true;
            // A stamp in the future is a clock that moved, not a fresh copy.
            return nowUtc - fetched.Value >= MaxAge || fetched.Value > nowUtc;
        }

        private static void Stamp(string folder, DateTime nowUtc)
        {
            try
            {
                Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder, StampFile), nowUtc.Ticks.ToString());
            }
            catch (Exception)
            {
                // A stamp that will not write costs one extra fetch next time and nothing else.
            }
        }

        /// <summary>
        /// Fetches the archive and writes it out, synchronously. The whole decision is here so that it
        /// is testable; <see cref="CarLightService"/> is what puts it on a thread.
        /// </summary>
        public static CarLightRefresh Fetch(IReleaseSource source, string folder, DateTime nowUtc)
        {
            if (source == null) return CarLightRefresh.Failed("no source", 0);
            FetchResult result;
            try
            {
                result = source.GetBytes(ArchiveUrl);
            }
            catch (Exception e)
            {
                return CarLightRefresh.Failed(e.Message, 0);
            }
            if (result == null || !result.Ok || result.Bytes == null) return CarLightRefresh.Failed(result?.Reason ?? "no answer", 0);

            int written;
            try
            {
                written = Extract(result.Bytes, folder);
            }
            catch (Exception e)
            {
                // Not a zip, a truncated one, or a disk that said no. The copy already on disk stands.
                return CarLightRefresh.Failed(e.Message, 0);
            }
            // Any covered sim's cars make a good copy: an archive with iRacing's and none of Assetto
            // Corsa's is the archive as it stands, not a broken one.
            if (written == 0) return CarLightRefresh.Failed("the archive carried no cars for " + SimNames, 0);
            Stamp(folder, nowUtc);
            return new CarLightRefresh { Ok = true, Fetched = true, Cars = written };
        }
    }
}
