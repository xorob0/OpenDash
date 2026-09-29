// NormalisedCopyTests.cs: what a rig button's save writes, against what the live settings hold.
//
// A rig press hands SimHub OpenDashSettings.NormalisedCopy() rather than the live settings, so that
// the save cannot move a zone a quick glance is holding (#503). SimHub's SaveCommonSettings serialises
// the whole object with Json.NET, so a property the copy does not carry is written as its default on
// every press, and stays that way on disk until something saves the live object over it. The first
// version of the copy dropped the update opt-out and the folder fingerprints, which a crash after a
// press would have turned into update checks the driver switched off and Dash Studio edits adopted as
// OpenDash's own. These walk the property list rather than naming the fields, so the next one added
// to the settings is held to it without an edit here.
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
        /// <summary>A rig upgraded from folders: a face and a companion, remembered by fingerprint.</summary>
        private static OpenDashSettings Upgraded()
        {
            var settings = new OpenDashSettings();
            settings.FolderFingerprints["OpenDash 1920x480"] = "abc";
            settings.FolderFingerprints["OpenDash Companion"] = "def";
            settings.Normalise();
            return settings;
        }

        private static IReadOnlyList<PropertyInfo> Settable()
        {
            return typeof(OpenDashSettings)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0)
                .ToList();
        }

        /// <summary>Sets a property to something other than what it holds, when its type is one this
        /// knows how to change; false for the nested objects, which the fixture carries as they are.</summary>
        private static bool Change(OpenDashSettings settings, PropertyInfo property)
        {
            var type = property.PropertyType;
            var value = property.GetValue(settings);
            if (type == typeof(bool)) property.SetValue(settings, !(bool)value);
            else if (type == typeof(int)) property.SetValue(settings, (int)value + 1);
            else if (type == typeof(int?)) property.SetValue(settings, ((int?)value ?? 0) + 7);
            else if (type == typeof(long)) property.SetValue(settings, (long)value + 638000000000000000L);
            else if (type == typeof(string)) property.SetValue(settings, "0.4.0");
            else if (type == typeof(int[]))
            {
                var array = value == null || ((int[])value).Length == 0 ? new int[1] : (int[])((int[])value).Clone();
                array[0] += 1;
                property.SetValue(settings, array);
            }
            else if (type == typeof(bool[]))
            {
                var array = value == null || ((bool[])value).Length == 0 ? new bool[1] : (bool[])((bool[])value).Clone();
                array[0] = !array[0];
                property.SetValue(settings, array);
            }
            else if (type == typeof(string[]))
            {
                var array = value == null || ((string[])value).Length == 0 ? new string[1] : (string[])((string[])value).Clone();
                array[0] = "Changed";
                property.SetValue(settings, array);
            }
            else if (type == typeof(List<string>))
            {
                var list = value == null ? new List<string>() : new List<string>((List<string>)value);
                list.Add("Changed");
                property.SetValue(settings, list);
            }
            else if (type == typeof(Dictionary<string, string>))
            {
                var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (value != null) foreach (var entry in (Dictionary<string, string>)value) map[entry.Key] = entry.Value;
                map["OpenDash Changed"] = "f00d";
                property.SetValue(settings, map);
            }
            else return false;
            return true;
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
            var fixture = JObject.FromObject(Upgraded());
            var changed = new List<string>();
            foreach (var property in Settable())
            {
                var live = Upgraded();
                if (!Change(live, property)) continue;
                live.Normalise();
                // Only what Normalise leaves standing is a value the copy could lose; a change it
                // repairs back is written the same both ways and proves nothing.
                if (!JToken.DeepEquals(fixture[property.Name], JObject.FromObject(live)[property.Name])) changed.Add(property.Name);
                var differences = Differences(live, live.NormalisedCopy());
                Assert.True(differences.Count == 0,
                    "With " + property.Name + " changed, a rig press writes " + string.Join(", ", differences) + " differently from the live settings.");
            }
            // The fields that were lost, among the ones the walk above really changed.
            Assert.Contains("CheckForUpdates", changed);
            Assert.Contains("LastUpdateCheckTicks", changed);
            Assert.Contains("OfferedRelease", changed);
            Assert.Contains("ReplaceEditedFor", changed);
            Assert.Contains("FolderFingerprints", changed);
            Assert.Contains("LightsNightMode", changed);
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
