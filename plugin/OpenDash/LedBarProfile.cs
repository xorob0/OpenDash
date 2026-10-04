// LedBarProfile.cs: the embedded profile for a shape, rewritten as one bar's own.
//
// The same move PackageExtractor makes for a second screen, on a smaller artefact: a profile is written
// for a shape and reads the rig-wide `OpenDash.Led*` names, and a bar is an instance of a shape that has
// to read its own. The bar's own bracketed property references, the profile's name and its id: that is
// the whole of the rewrite, and it is string surgery rather than a JSON round trip on purpose. Re-serialising
// somebody's scene graph through a library we do not control is what PackageExtractor refuses for the
// dashboards, for the same reason -- what SimHub reads back has to be what the build wrote, less exactly
// the two fields and the names BarSettings lists.
//
// Since #694 the rewrite also carries the bar's own colours. SimHub binds no colour on an LED container that
// blinks, so a colour cannot be a property the profile reads the way a switch is; it is written into the
// containers that draw it instead, found by their descriptions, and choosing one installs the bar again.
//
// The id the rewrite gives a bar is also how the bar is found again in SimHub, so the census of what
// SimHub holds for the rig's bars (Plan) lives here beside it rather than beside the embedded profile.
//
// Pure: no SimHub types and no JSON library, so OpenDash.Tests compiles it and pins the rewrite.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace OpenDashPlugin
{
    public static class LedBarProfile
    {
        /// <summary>
        /// The properties a bar owns, which are the ones a rewrite moves under its namespace.
        /// </summary>
        /// <remarks>
        /// The four a strip always had, then its own brightness and its fifteen effect switches (#503), then
        /// whether its aid lamps read the slip estimate: twenty-one names, in the order the contract
        /// declares them. The estimate itself is the car's and stays rig-wide.
        ///
        /// Everything else a strip profile reads stays rig-wide and is deliberately not here.
        /// `LightsBrightness` is the brightness a bar with none of its own falls back to, and
        /// `LightsNightMode` with `LightsNightBrightness` is the rig's night, which dims every bar at
        /// once; `LightsLowFuelLaps` is the rig's one answer to "am I low", read by the faces and the box
        /// as well; and `LedMirrorReady` with the packed `LedMirror<n>` runs is the *car's* own shift
        /// pattern, which is a fact about the car and not a setting on a strip.
        /// </remarks>
        public static readonly string[] BarSettings = OwnSettings();

        private static string[] OwnSettings()
        {
            var names = new List<string>
            {
                Contract.LedCentre, Contract.LedRpmStyle, Contract.LedFlagAnimation, Contract.LedSpotterWhole,
                Contract.LedBrightness,
            };
            names.AddRange(Contract.LedEffectSettings());
            names.Add(Contract.LedInferSlip);
            return names.ToArray();
        }

        /// <summary>`LedCentre` under one bar's namespace: `RimLedCentre`.</summary>
        public static string Property(string ns, string setting)
        {
            if (string.IsNullOrEmpty(ns)) throw new ArgumentException("a bar needs a namespace", "ns");
            return ns + setting;
        }

        /// <summary>Every property one bar owns, in attachment order.</summary>
        public static string[] Properties(string ns)
        {
            var names = new string[BarSettings.Length];
            for (var i = 0; i < BarSettings.Length; i++) names[i] = Property(ns, BarSettings[i]);
            return names;
        }

        /// <summary>
        /// A profile id nothing else will take, derived from the bar's namespace rather than drawn at
        /// random.
        /// </summary>
        /// <remarks>
        /// Derived, because the id is how the installer recognises its own: FlagBoxInstallPlan matches on
        /// it, so a bar reinstalled after a restart has to produce the same id or it would add a second
        /// copy beside the first every time. Deterministic from one string is what the generator's own
        /// stableGuid does for every id it writes, and this is the same idea in the plugin.
        /// </remarks>
        public static Guid IdFor(string ns)
        {
            using (var md5 = MD5.Create())
            {
                var bytes = md5.ComputeHash(Encoding.UTF8.GetBytes("opendash/ledbar/" + (ns ?? string.Empty)));
                // Version 5 and the RFC 4122 variant, so what comes out is a well-formed name-based UUID
                // rather than sixteen bytes wearing a Guid's type. Byte 7 and not byte 6: `new Guid(byte[])`
                // reads the first three groups little-endian, so the byte that lands on the version nibble
                // of the printed form is the high one of the third group.
                bytes[7] = (byte)((bytes[7] & 0x0F) | 0x50);
                bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
                return new Guid(bytes);
            }
        }

        /// <summary>
        /// What SimHub holds for one bar, looked for on every LED device the rig has, by the id the bar
        /// derives.
        /// </summary>
        /// <remarks>
        /// By <see cref="IdFor"/> and never by the embedded profile's own id, which is the whole of #457:
        /// <see cref="For"/> gives every bar an id of its own, so no profile SimHub holds ever carries the
        /// embedded one, and a census asking about it found nothing on a rig full of strips.
        ///
        /// Every device, and the best reading wins (<see cref="FlagBoxInstallPlan.Better"/>): a bar found
        /// on the wheel is installed whatever the Arduino holds, and the install takes the copy out of
        /// every other device, so two readings that disagree are one device that has it and others that
        /// do not. A device whose list could not be read is a null entry and reads Unavailable, which any
        /// device that could be read outranks; a rig with no LED device at all is Unavailable outright.
        ///
        /// The embedded description is the one this build carries for the bar's shape, and the version
        /// in it is what the copy in SimHub is compared with: a bar installed by an older build reads
        /// Outdated, and one installed before strips carried a version at all reads Outdated as well,
        /// because nothing says it is current.
        /// </remarks>
        public static FlagBoxPlan Plan(LedBar bar, string embeddedDescription, IEnumerable<IEnumerable<InstalledProfile>> devices)
        {
            if (bar == null) return new FlagBoxPlan { State = FlagBoxInstallState.NotEmbedded };
            var id = IdFor(bar.Namespace);
            FlagBoxPlan best = null;
            foreach (var device in devices ?? Enumerable.Empty<IEnumerable<InstalledProfile>>())
            {
                best = FlagBoxInstallPlan.Better(best, FlagBoxInstallPlan.Decide(id, embeddedDescription, device));
            }
            return best ?? new FlagBoxPlan
            {
                State = FlagBoxInstallState.Unavailable,
                EmbeddedVersion = FlagBoxInstallPlan.VersionOf(embeddedDescription),
            };
        }

        /// <summary>
        /// The embedded profile for this bar's shape, as the bar's own: its name, its id, its properties.
        /// </summary>
        /// <remarks>
        /// Null in and null out, because a shape this build does not carry has no profile to rewrite and
        /// the caller says so rather than installing something else. The two fields appear exactly once
        /// each in a profile the build wrote -- the containers underneath carry `Description` and no
        /// `Name` at all -- which is what makes replacing the first occurrence of each correct rather
        /// than merely usually right. LedBarTests holds the whole document to that: undoing the rewrite
        /// has to give back the file the build wrote, byte for byte.
        /// </remarks>
        public static string For(LedBar bar, string embeddedJson)
        {
            if (bar == null || embeddedJson == null) return null;
            var json = ReplaceField(embeddedJson, "Name", Escape(bar.Name ?? string.Empty));
            json = ReplaceField(json, "ProfileId", IdFor(bar.Namespace).ToString("D", CultureInfo.InvariantCulture));
            foreach (var setting in BarSettings)
            {
                // Bracketed on both sides, so a name that is the prefix of another cannot be caught by
                // half: `[OpenDash.LedCentre]` and never the bare word.
                json = json.Replace(
                    "[" + Contract.Prefix + "." + setting + "]",
                    "[" + Contract.Prefix + "." + Property(bar.Namespace, setting) + "]");
            }
            return Recolour(json, bar);
        }

        /// <summary>
        /// The profile with the bar's own colours in place of the defaults: every container an effect draws in,
        /// found by its description, has the default replaced wherever it is the lit colour (#694).
        /// </summary>
        /// <remarks>
        /// A container is described by its effect's label, or by that label and a suffix the generator adds for
        /// the same drawing in another form (", held", ", spread", ", spread, held"); a group named for an effect
        /// carries no colour of its own, and its children are described again. So a description opens a window
        /// that runs to the next description or the next ContainerType, which is the one container's own fields,
        /// and only a Color or BlinkingColor equal to the default inside it is replaced: the debris flag's
        /// stripes and every off phase stay what they were. A bar with no colours of its own gives back exactly
        /// what it was given.
        /// </remarks>
        public static string Recolour(string json, LedBar bar)
        {
            if (json == null || bar == null || bar.Colours == null || bar.Colours.Count == 0) return json;
            foreach (var colour in Contract.LedColours)
            {
                if (!bar.HasOwnColour(colour.Key)) continue;
                var hex = bar.ColourOf(colour.Key);
                foreach (var label in colour.Effects) json = RecolourContainers(json, label, colour.DefaultHex, hex);
            }
            return json;
        }

        private const string DescriptionKey = "\"Description\": \"";
        private const string ContainerTypeKey = "\"ContainerType\"";

        private static string RecolourContainers(string json, string label, string from, string to)
        {
            var opening = DescriptionKey + label;
            var text = new StringBuilder(json.Length);
            var at = 0;
            while (true)
            {
                var found = json.IndexOf(opening, at, StringComparison.Ordinal);
                if (found < 0) break;
                var after = found + opening.Length;
                // The label itself, or the label and a suffix, and never a label that merely starts the same way.
                var mine = after < json.Length && (json[after] == '"' || string.CompareOrdinal(json, after, ", ", 0, 2) == 0);
                if (!mine)
                {
                    text.Append(json, at, after - at);
                    at = after;
                    continue;
                }
                var end = NextOf(json, after, DescriptionKey, ContainerTypeKey);
                text.Append(json, at, after - at);
                var fields = json.Substring(after, end - after)
                    .Replace("\"Color\": \"" + from + "\"", "\"Color\": \"" + to + "\"")
                    .Replace("\"BlinkingColor\": \"" + from + "\"", "\"BlinkingColor\": \"" + to + "\"");
                text.Append(fields);
                at = end;
            }
            text.Append(json, at, json.Length - at);
            return text.ToString();
        }

        /// <summary>Where the first of <paramref name="keys"/> after <paramref name="from"/> starts, or the end.</summary>
        private static int NextOf(string json, int from, params string[] keys)
        {
            var next = json.Length;
            foreach (var key in keys)
            {
                var at = json.IndexOf(key, from, StringComparison.Ordinal);
                if (at >= 0 && at < next) next = at;
            }
            return next;
        }

        /// <summary>Replaces one top-level string field's value, leaving everything around it untouched.</summary>
        private static string ReplaceField(string json, string field, string value)
        {
            var key = "\"" + field + "\"";
            var at = json.IndexOf(key, StringComparison.Ordinal);
            if (at < 0) return json;
            var colon = json.IndexOf(':', at + key.Length);
            if (colon < 0) return json;
            var open = json.IndexOf('"', colon + 1);
            if (open < 0) return json;
            var close = open + 1;
            while (close < json.Length && json[close] != '"')
            {
                if (json[close] == '\\') close++;
                close++;
            }
            if (close >= json.Length) return json;
            return json.Substring(0, open + 1) + value + json.Substring(close);
        }

        /// <summary>A name as a JSON string body. A quote or a backslash in what somebody typed would
        /// otherwise end the string early and hand SimHub a file it cannot read.</summary>
        private static string Escape(string value)
        {
            var text = new StringBuilder(value.Length);
            foreach (var ch in value)
            {
                if (ch == '"' || ch == '\\') text.Append('\\').Append(ch);
                else if (ch < ' ') text.Append(' ');
                else text.Append(ch);
            }
            return text.ToString();
        }
    }
}
