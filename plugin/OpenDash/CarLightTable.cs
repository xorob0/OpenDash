// CarLightTable.cs: one car's own LED bar, as measured, and the reader that survives the files.
//
// The schema is Lovely Car Data's v2.0.0, written down in docs/research/iracing-led-patterns.md and
// fetched rather than carried: ADR 0018 says why OpenDash ships none of it. This file knows the
// shape and nothing about where the bytes came from, so CarLightStore can be tested without a
// socket and this can be tested without either.
//
// The reader is deliberately suspicious. These are somebody else's files, published continuously,
// and one of the 85 iRacing ones already disagrees with itself (stockcars-fordtaurus03 carries one
// colour for one LED where the format wants two). A car that cannot be read is a car on the
// published ladder, which is what every car had before this existed -- so Parse returns null and
// nothing throws.
//
// No SimHub, WPF or Newtonsoft types: this file is compiled into OpenDash.Tests. JSON is read with
// JsonReaderWriterFactory, which is in the framework on net48 and net8.0 both, for the same reason
// ReleaseFeed.cs uses DataContractJsonSerializer -- and unlike DataContractJsonSerializer it copes
// with the gear keys, which are data ("R", "N", "1" ... "8") rather than a fixed set of members.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace OpenDashPlugin
{
    /// <summary>One gear's thresholds: when each LED lights, and when the bar starts flashing.</summary>
    public sealed class CarLightGear
    {
        /// <summary>RPM the over-rev flash begins at. The file calls it the redline.</summary>
        public int Redline { get; set; }

        /// <summary>One RPM per LED, in the car's own order. A zero means lit from idle.</summary>
        public int[] Thresholds { get; set; }
    }

    /// <summary>
    /// A car's bar: how many LEDs, what colour each one is, and the RPMs they light at in each gear.
    ///
    /// <para>There is no pattern field and there is deliberately none. Left to right is ascending
    /// thresholds, meet in the middle is symmetric ones, a block is a run that shares a threshold and
    /// a gap is a transparent colour -- so the whole vocabulary is these numbers, and OpenDash needs
    /// no case for any of it.</para>
    /// </summary>
    public sealed class CarLightTable
    {
        /// <summary>SimHub's DataCorePlugin.CarId, verbatim: "stockcars chevycamarozl12022", spaces and all.</summary>
        public string CarId { get; set; }

        public string CarName { get; set; }

        /// <summary>How many LEDs the car's own bar has. One to sixteen, over the cars measured so far.</summary>
        public int LedCount { get; set; }

        /// <summary>Half period of the over-rev flash. Zero for the 47 cars in 85 that do not flash at all.</summary>
        public int BlinkIntervalMs { get; set; }

        /// <summary>What the bar flashes in. Transparent on a car that does not flash.</summary>
        public string BlinkColor { get; set; }

        /// <summary>One colour per LED, in the car's own order. Transparent where the bar has a gap.</summary>
        public string[] Colors { get; set; }

        /// <summary>Keyed by the gear as SimHub spells it: "R", "N", "1" ... "8".</summary>
        public Dictionary<string, CarLightGear> Gears { get; set; }

        /// <summary>
        /// A colour that leaves an LED dark. Spelled as hex rather than as the word, because every
        /// colour the plugin publishes is exactly nine characters wide — see <see cref="Normalise"/>.
        /// </summary>
        public const string Transparent = "#00000000";

        /// <summary>
        /// Reads one car file, or returns null if it is not one.
        ///
        /// Everything that would make the bar a guess is a refusal: a missing id, no LEDs, a colour
        /// list that does not match the LED count, a gear row that does not. A car OpenDash refuses
        /// falls back to the ladder iRacing publishes, so the cost of being strict here is that a
        /// driver sees the lights OpenDash drew last month rather than a bar assembled out of
        /// mismatched halves.
        /// </summary>
        public static CarLightTable Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            XElement root;
            try
            {
                using (var reader = JsonReaderWriterFactory.CreateJsonReader(Encoding.UTF8.GetBytes(json), XmlDictionaryReaderQuotas.Max))
                {
                    root = XDocument.Load(reader).Root;
                }
            }
            catch (Exception)
            {
                return null;
            }
            if (root == null) return null;

            var carId = Text(root, "carId");
            var ledCount = Number(root, "ledNumber");
            if (string.IsNullOrWhiteSpace(carId) || ledCount == null || ledCount.Value < 1) return null;
            var count = (int)ledCount.Value;

            // Index 0 of both arrays is the redline entry: the flash colour and the flash RPM. The
            // LEDs are what follows, so each array is one longer than the bar.
            var colors = Member(root, "ledColor");
            if (colors == null) return null;
            var colorValues = Items(colors).Select(i => Normalise(i.Value)).ToArray();
            if (colorValues.Length != count + 1) return null;
            // A colour nobody can read is a bar drawn in the wrong colours, which is worse than the
            // ladder iRacing publishes. The whole car goes rather than one LED.
            if (colorValues.Any(c => c == null)) return null;

            var gears = ParseGears(root, count);
            if (gears == null || gears.Count == 0) return null;

            return new CarLightTable
            {
                CarId = carId,
                CarName = Text(root, "carName") ?? carId,
                LedCount = count,
                BlinkIntervalMs = Math.Max(0, (int)(Number(root, "redlineBlinkInterval") ?? 0)),
                BlinkColor = colorValues[0],
                Colors = colorValues.Skip(1).ToArray(),
                Gears = gears,
            };
        }

        /// <summary>`ledRpm` is an array holding one object, whose members are the gears.</summary>
        private static Dictionary<string, CarLightGear> ParseGears(XElement root, int count)
        {
            var ledRpm = Member(root, "ledRpm");
            if (ledRpm == null) return null;
            var table = Items(ledRpm).FirstOrDefault();
            if (table == null) return null;

            var gears = new Dictionary<string, CarLightGear>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in table.Elements())
            {
                // A JSON key that is not a legal XML name -- every numbered gear -- arrives as
                // <a:item item="1"> rather than as an element named for the key. See the probe in
                // docs/research/iracing-led-patterns.md: the attribute is the key.
                var gear = (string)entry.Attribute("item") ?? entry.Name.LocalName;
                var row = Items(entry).Select(ToInt).ToArray();
                if (row.Length != count + 1) return null;
                if (row.Any(v => v == null)) return null;
                gears[gear] = new CarLightGear
                {
                    Redline = Math.Max(0, row[0].Value),
                    Thresholds = row.Skip(1).Select(v => Math.Max(0, v.Value)).ToArray(),
                };
            }
            return gears;
        }

        /// <summary>
        /// A colour as exactly nine characters of <c>#AARRGGBB</c>, or null when it is not a colour.
        ///
        /// <para>The width is the point. The plugin publishes a whole run as one property and the
        /// profile slices LED <c>k</c> out of it with <c>left(value, k * 9, 9)</c>, which needs every
        /// field to be the same size — so the three spellings the format allows (<c>#AARRGGBB</c>,
        /// <c>#RRGGBB</c> and an HTML colour name) are folded into one here rather than anywhere
        /// later. Every colour in all 85 iRacing files today is already the first of the three; the
        /// other two are read because the format says they are legal.</para>
        ///
        /// <para><c>Color.FromName</c> is System.Drawing.Primitives, which is in the framework on
        /// net48 and net8.0 both and needs no GDI+ for a known colour — so this runs in the tests on
        /// Linux as well as on the user's machine.</para>
        /// </summary>
        public static string Normalise(string value)
        {
            var text = (value ?? string.Empty).Trim();
            if (text.Length == 0) return Transparent;
            if (text[0] == '#')
            {
                var hex = text.Substring(1);
                if (!hex.All(Uri.IsHexDigit)) return null;
                if (hex.Length == 8) return "#" + hex.ToUpperInvariant();
                // #RRGGBB is opaque. Three-digit shorthand is not a spelling SimHub reads either
                // (it parses to a transparent near-black), so it is refused rather than expanded.
                if (hex.Length == 6) return "#FF" + hex.ToUpperInvariant();
                return null;
            }
            var known = System.Drawing.Color.FromName(text);
            if (!known.IsKnownColor) return null;
            return string.Format("#{0:X2}{1:X2}{2:X2}{3:X2}", known.A, known.R, known.G, known.B);
        }

        private static XElement Member(XElement parent, string name)
        {
            return parent.Elements().FirstOrDefault(e => string.Equals((string)e.Attribute("item") ?? e.Name.LocalName, name, StringComparison.Ordinal));
        }

        /// <summary>The elements of a JSON array. Every item is written as &lt;item&gt; whatever it holds.</summary>
        private static IEnumerable<XElement> Items(XElement array)
        {
            return array.Elements();
        }

        private static string Text(XElement parent, string name)
        {
            var found = Member(parent, name);
            return found == null ? null : found.Value;
        }

        private static double? Number(XElement parent, string name)
        {
            var found = Member(parent, name);
            if (found == null) return null;
            double value;
            return double.TryParse(found.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value) ? value : (double?)null;
        }

        private static int? ToInt(XElement item)
        {
            double value;
            if (!double.TryParse(item.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return null;
            if (double.IsNaN(value) || double.IsInfinity(value)) return null;
            return (int)Math.Round(value);
        }
    }
}
