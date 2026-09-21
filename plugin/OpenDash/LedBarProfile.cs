// LedBarProfile.cs: the embedded profile for a shape, rewritten as one bar's own.
//
// The same move PackageExtractor makes for a second screen, on a smaller artefact: a profile is written
// for a shape and reads the rig-wide `OpenDash.Led*` names, and a bar is an instance of a shape that has
// to read its own. Three bracketed property references, the profile's name and its id: that is the whole
// of the rewrite, and it is string surgery rather than a JSON round trip on purpose. Re-serialising
// somebody's scene graph through a library we do not control is what PackageExtractor refuses for the
// dashboards, for the same reason -- what SimHub reads back has to be what the build wrote, less exactly
// the two fields and three names named here.
//
// Pure: no SimHub types and no JSON library, so OpenDash.Tests compiles it and pins the rewrite.
using System;
using System.Globalization;
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
        /// Everything else a strip profile reads stays rig-wide and is deliberately not here.
        /// `LightsBrightness`, `LightsNightMode` and `LightsNightBrightness` are how bright the rig is;
        /// `LightsLowFuelLaps` is the rig's one answer to "am I low", read by the faces and the box as
        /// well; and `LedMirrorReady` with the packed `LedMirror<n>` runs is the *car's* own shift
        /// pattern, which is a fact about the car and not a setting on a strip.
        /// </remarks>
        public static readonly string[] BarSettings =
        {
            Contract.LedCentre, Contract.LedRpmStyle, Contract.LedCarRevBar, Contract.LedFlagAnimation, Contract.LedSpotterWhole,
        };

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
            return json;
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
