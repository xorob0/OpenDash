// NormalisedCopyTests.cs: what a rig button's save writes, against what the live settings hold.
//
// A rig press hands SimHub OpenDashSettings.NormalisedCopy() rather than the live settings, so that
// the save cannot move a zone a quick glance is holding (#791). SimHub's SaveCommonSettings serialises
// the whole object with Json.NET, so a property the copy does not carry is written as its default on
// every press, and stays that way on disk until something saves the live object over it. The first
// version of the copy dropped the update opt-out and the folder fingerprints, which a crash after a
// press would have turned into update checks the driver switched off and Dash Studio edits adopted as
// OpenDash's own. These walk the property lists of the settings and of the records they hold -- a
// strip, a screen of each kind, a face -- rather than naming the fields, so the next one added to any
// of them is held to it without an edit here. A property of a type the walk does not know how to change
// fails it rather than being skipped, and is either taught to Change or named as a nested record.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class NormalisedCopyTests
    {
        /// <summary>A rig upgraded from folders: a face and a companion, remembered by fingerprint, with
        /// a strip and one screen of every kind.</summary>
        private static OpenDashSettings Upgraded()
        {
            var settings = new OpenDashSettings();
            settings.FolderFingerprints["OpenDash 1920x480"] = "abc";
            settings.FolderFingerprints["OpenDash Companion"] = "def";
            settings.Rig = new List<ScreenInstance>
            {
                Screen(Contract.KindFace, 1920, 480),
                Screen(Contract.KindCompanion, 850, 480),
                Screen(Contract.KindPitWall, 1920, 1080),
                Screen(Contract.KindSlots, 800, 800),
            };
            settings.Normalise();
            settings.AddLedBar("4-14-4", "MLD", null);
            settings.Normalise();
            return settings;
        }

        private static ScreenInstance Screen(string kind, int width, int height)
        {
            var screen = new ScreenInstance { Kind = kind, Width = width, Height = height };
            screen.Namespace = screen.StockNamespace;
            screen.Normalise();
            return screen;
        }

        /// <summary>The properties that hold records of their own, which the walk goes into rather than
        /// changing whole.</summary>
        private static readonly HashSet<string> Nested = new HashSet<string>(StringComparer.Ordinal) { "LedBars", "Rig", "Face" };

        private static IReadOnlyList<PropertyInfo> Settable(Type type)
        {
            return type
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0)
                .ToList();
        }

        /// <summary>Sets a property to something other than what it holds; false for a type this does
        /// not know how to change.</summary>
        private static bool Change(object target, PropertyInfo property)
        {
            var type = property.PropertyType;
            var value = property.GetValue(target);
            if (type == typeof(bool)) property.SetValue(target, !(bool)value);
            else if (type == typeof(bool?)) property.SetValue(target, !((bool?)value ?? false));
            else if (type == typeof(int)) property.SetValue(target, (int)value + 1);
            else if (type == typeof(int?)) property.SetValue(target, ((int?)value ?? 0) + 7);
            else if (type == typeof(long)) property.SetValue(target, (long)value + 638000000000000000L);
            else if (type == typeof(string)) property.SetValue(target, "0.4.0");
            else if (type == typeof(int[]))
            {
                var array = value == null || ((int[])value).Length == 0 ? new int[1] : (int[])((int[])value).Clone();
                array[0] += 1;
                property.SetValue(target, array);
            }
            else if (type == typeof(int?[]))
            {
                // A Rig canvas position per matrix slot, which Normalise keeps when it is not negative.
                var array = value == null || ((int?[])value).Length == 0 ? new int?[1] : (int?[])((int?[])value).Clone();
                array[0] = (array[0] ?? 0) + 40;
                property.SetValue(target, array);
            }
            else if (type == typeof(int[][]))
            {
                // An order is a permutation, so swap two pages of the first one rather than adding.
                var rows = value == null ? new int[0][] : ((int[][])value).Select(row => row == null ? null : (int[])row.Clone()).ToArray();
                if (rows.Length == 0 || rows[0] == null || rows[0].Length < 2) rows = new[] { new[] { 1, 0 } };
                else { var first = rows[0][0]; rows[0][0] = rows[0][1]; rows[0][1] = first; }
                property.SetValue(target, rows);
            }
            else if (type == typeof(bool[]))
            {
                var array = value == null || ((bool[])value).Length == 0 ? new bool[1] : (bool[])((bool[])value).Clone();
                array[0] = !array[0];
                property.SetValue(target, array);
            }
            else if (type == typeof(string[]))
            {
                var array = value == null || ((string[])value).Length == 0 ? new string[1] : (string[])((string[])value).Clone();
                array[0] = "Changed";
                property.SetValue(target, array);
            }
            else if (type == typeof(List<string>))
            {
                var list = value == null ? new List<string>() : new List<string>((List<string>)value);
                // A strip's switched-off effects keep only what an effect's id names.
                list.Add(target is LedBar ? "drs" : "Changed");
                property.SetValue(target, list);
            }
            else if (type == typeof(Dictionary<string, string>))
            {
                var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (value != null) foreach (var entry in (Dictionary<string, string>)value) map[entry.Key] = entry.Value;
                map["OpenDash Changed"] = "f00d";
                property.SetValue(target, map);
            }
            else if (type == typeof(Dictionary<string, int>))
            {
                var map = value == null ? new Dictionary<string, int>() : new Dictionary<string, int>((Dictionary<string, int>)value);
                var key = map.Keys.FirstOrDefault() ?? "Changed";
                map[key] = (map.TryGetValue(key, out var held) ? held : 0) + 1;
                property.SetValue(target, map);
            }
            else if (type == typeof(Dictionary<string, FaceSettings>))
            {
                // The faces a file from before ADR 0017 kept by prefix, which Normalise carries onto
                // the rig's screens and then clears.
                var map = value == null ? new Dictionary<string, FaceSettings>(StringComparer.Ordinal)
                    : new Dictionary<string, FaceSettings>((Dictionary<string, FaceSettings>)value, StringComparer.Ordinal);
                map["Face1920x480"] = new FaceSettings { Zones = new[] { 1, 0, 14, 0 } };
                property.SetValue(target, map);
            }
            else return false;
            return true;
        }

        /// <summary>
        /// Every record the walk changes a property of, found afresh in a new fixture each time: the
        /// settings themselves, the strip, each screen, and the face's own settings.
        /// </summary>
        private static IEnumerable<KeyValuePair<string, Func<OpenDashSettings, object>>> Records()
        {
            yield return new KeyValuePair<string, Func<OpenDashSettings, object>>("OpenDashSettings", s => s);
            yield return new KeyValuePair<string, Func<OpenDashSettings, object>>("LedBar", s => s.LedBars[0]);
            var rig = Upgraded().Rig;
            for (var i = 0; i < rig.Count; i++)
            {
                var index = i;
                yield return new KeyValuePair<string, Func<OpenDashSettings, object>>("ScreenInstance(" + rig[i].Kind + ")", s => s.Rig[index]);
                if (rig[i].Face != null)
                {
                    yield return new KeyValuePair<string, Func<OpenDashSettings, object>>("FaceSettings", s => s.Rig[index].Face);
                }
            }
        }

        /// <summary>The names of the top-level members Json.NET writes differently for the two.</summary>
        private static List<string> Differences(OpenDashSettings live, OpenDashSettings copy)
        {
            var written = JObject.FromObject(live);
            var saved = JObject.FromObject(copy);
            return written.Properties().Select(p => p.Name)
                .Union(saved.Properties().Select(p => p.Name))
                .Where(name => !JToken.DeepEquals(written[name], saved[name]))
                .ToList();
        }

        [Fact]
        public void A_normalised_copy_writes_every_property_the_live_settings_hold()
        {
            var fixture = JsonConvert.SerializeObject(Upgraded());
            var changed = new List<string>();
            foreach (var record in Records())
            {
                foreach (var property in Settable(record.Value(Upgraded()).GetType()))
                {
                    if (Nested.Contains(property.Name)) continue;
                    var live = Upgraded();
                    Assert.True(Change(record.Value(live), property),
                        record.Key + "." + property.Name + " is a " + property.PropertyType.Name + ", which the walk does not know how to change.");
                    live.Normalise();
                    // Only what Normalise leaves standing is a value the copy could lose; a change it
                    // repairs back is written the same both ways and proves nothing.
                    if (fixture != JsonConvert.SerializeObject(live)) changed.Add(record.Key + "." + property.Name);
                    var differences = Differences(live, live.NormalisedCopy());
                    Assert.True(differences.Count == 0,
                        "With " + record.Key + "." + property.Name + " changed, a rig press writes " + string.Join(", ", differences) + " differently from the live settings.");
                }
            }
            // The fields that were lost, among the ones the walk above really changed.
            Assert.Contains("OpenDashSettings.CheckForUpdates", changed);
            Assert.Contains("OpenDashSettings.LastUpdateCheckTicks", changed);
            Assert.Contains("OpenDashSettings.OfferedRelease", changed);
            Assert.Contains("OpenDashSettings.ReplaceEditedFor", changed);
            Assert.Contains("OpenDashSettings.FolderFingerprints", changed);
            Assert.Contains("OpenDashSettings.LightsNightMode", changed);
            // And the ones #791 added below the top level.
            Assert.Contains("LedBar.Reversed", changed);
            Assert.Contains("LedBar.Brightness", changed);
            Assert.Contains("LedBar.EffectsOff", changed);
            Assert.Contains("ScreenInstance(face).LayoutX", changed);
            Assert.Contains("ScreenInstance(face).LayoutY", changed);
            Assert.Contains("LedBar.LayoutX", changed);
            Assert.Contains("LedBar.LayoutY", changed);
            Assert.Contains("OpenDashSettings.MatrixLayoutX", changed);
            Assert.Contains("OpenDashSettings.MatrixLayoutY", changed);
            Assert.Contains("FaceSettings.Orders", changed);
        }

        [Fact]
        public void A_normalised_copy_is_the_live_settings_as_json_writes_them()
        {
            var live = Upgraded();
            live.CheckForUpdates = false;
            live.LastUpdateCheckTicks = 638000000000000000L;
            live.OfferedRelease = "0.4.0";
            live.ReplaceEditedFor = "0.4.0";
            live.LightsNightMode = true;
            var copy = live.NormalisedCopy();
            Assert.Equal(JsonConvert.SerializeObject(live), JsonConvert.SerializeObject(copy));
            // Apart, not shared: the installer writing a fingerprint into the live record after the copy
            // was taken does not reach into what is being saved, nor the other way about.
            Assert.NotSame(live.FolderFingerprints, copy.FolderFingerprints);
            copy.FolderFingerprints["OpenDash 1920x480"] = "changed";
            Assert.Equal("abc", live.FolderFingerprints["OpenDash 1920x480"]);
            // Still the record's lookup, which ignores case.
            Assert.Equal("abc", live.NormalisedCopy().FolderFingerprints["opendash 1920X480"]);
        }
    }
}
