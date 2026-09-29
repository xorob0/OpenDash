// SettingsControl.Profiles.cs: the light profiles as SimHub holds them -- asking what it has, installing the
// flag box and a strip, the census of the rig's strips, and updating the old ones.
//
// Lifted out of the Install and Lights tabs so that the pages that need them share one copy: LEDs installs and
// moves a strip, Matrix installs the flag box from its header, Updates lists what is in SimHub, and Home asks
// what needs fixing (SettingsControl.Status.cs). The shell owns this file.
//
// NOTHING IS WRITTEN TO SIMHUB EXCEPT ON A PRESS, which is the consent half of ADR 0013. SafePlan and
// BarCensus only ask SimHub what it already holds, and the profiles are read out of the assembly rather than
// off disk. A strip's embedded profile is always looked up by LedBar.ProfileShapeId, which is the reversed
// twin for a strip wired from the far end: looked up by its Shape it finds the plain wiring and installs that.
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenDashPlugin
{
    public partial class SettingsControl
    {
        /// <summary>Asks SimHub what it holds for the flag box, without touching it.</summary>
        private FlagBoxPlan SafePlan()
        {
            try
            {
                return FlagBoxInstaller.Plan(plugin.FlagBoxJson);
            }
            catch (Exception ex)
            {
                Log.Warn("Reading SimHub's matrix profiles failed: " + ex.Message);
                return new FlagBoxPlan { State = FlagBoxInstallState.Unavailable };
            }
        }

        /// <summary>Installs the flag box profile into SimHub's matrix settings. A press, never on its own.</summary>
        private FlagBoxPlan InstallFlagBox()
        {
            try
            {
                return FlagBoxInstaller.Install(plugin.FlagBoxJson);
            }
            catch (Exception ex)
            {
                Log.Error("Installing the flag box profile failed", ex);
                return new FlagBoxPlan { State = FlagBoxInstallState.Failed };
            }
        }

        /// <summary>What SimHub's own list calls OpenDash's matrix profile: the name the build stamped into it,
        /// and the constant only when this build carries no profile to read it from.</summary>
        private string FlagBoxName()
        {
            return plugin.FlagBox?.ProfileName ?? FlagBoxProfile.ProfileName;
        }

        /// <summary>One shape this build embedded: its id, and the profile written for it.</summary>
        private sealed class EmbeddedShape
        {
            public EmbeddedShape(string id, string json)
            {
                Id = id;
                Json = json;
            }

            public string Id { get; private set; }
            public string Json { get; private set; }
        }

        /// <summary>
        /// Every strip profile this build embedded, by the id the generator wrote it under, the wirings and
        /// the far-end twins included.
        /// </summary>
        /// <remarks>
        /// The census is what is embedded: a shape this build does not carry is not offered and cannot be
        /// added as a strip whose profile does not exist. The add form's two numbers leave the wirings out
        /// (PanelLights.BarSides and BarCentres); this is the lookup a strip's profile is resolved through.
        /// </remarks>
        private static IList<EmbeddedShape> EmbeddedShapes()
        {
            var log = new SimHubInstallLog();
            var assembly = typeof(OpenDash).Assembly;
            var shapes = new List<EmbeddedShape>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var resource in FlagBoxProfile.StripResourceNames(assembly))
            {
                var id = FlagBoxProfile.ShapeIdOf(resource);
                if (id == null || !seen.Add(id)) continue;
                var text = FlagBoxProfile.ResourceText(assembly, resource, log);
                if (text == null) continue;
                shapes.Add(new EmbeddedShape(id, text));
            }
            return shapes;
        }

        /// <summary>The embedded profile a strip installs, by <see cref="LedBar.ProfileShapeId"/>, or null.</summary>
        private static EmbeddedShape EmbeddedProfileOf(LedBar bar, IList<EmbeddedShape> shapes = null)
        {
            if (bar == null) return null;
            return (shapes ?? EmbeddedShapes()).FirstOrDefault(entry => string.Equals(entry.Id, bar.ProfileShapeId, StringComparison.Ordinal));
        }

        /// <summary>
        /// Installs one strip's profile into the device it names, having taken it out of every other.
        /// </summary>
        /// <remarks>
        /// Out of wherever it was first: a strip that has moved from the Arduino to a wheel must not leave a
        /// copy behind reading properties that now drive the wheel's, and the install only knows about the
        /// device it is going to.
        /// </remarks>
        private static FlagBoxPlan InstallBar(LedBar bar, string embedded)
        {
            try
            {
                StripInstaller.UninstallEverywhere(LedBarProfile.IdFor(bar.Namespace));
                return StripInstaller.Install(LedBarProfile.For(bar, embedded), bar.Device);
            }
            catch (Exception ex)
            {
                Log.Error("Installing the profile for " + bar.Name + " failed", ex);
                return new FlagBoxPlan { State = FlagBoxInstallState.Failed };
            }
        }

        /// <summary>Installs a strip's profile again from what this build embeds, by the profile it names.
        /// NotEmbedded when the build carries no profile for it.</summary>
        private FlagBoxPlan ReinstallBar(LedBar bar)
        {
            var found = EmbeddedProfileOf(bar);
            if (found == null) return new FlagBoxPlan { State = FlagBoxInstallState.NotEmbedded };
            return InstallBar(bar, found.Json);
        }

        /// <summary>
        /// Each of the rig's strips with what SimHub holds for it, off one read of every LED device.
        /// </summary>
        /// <remarks>
        /// Reachable is false when no LED device could be read at all, which a page reports as SimHub's LED
        /// settings being unavailable: a state a driver can do nothing about rather than an error.
        /// </remarks>
        private IList<KeyValuePair<LedBar, FlagBoxPlan>> BarCensus(IDictionary<string, string> embedded, out bool reachable)
        {
            List<List<InstalledProfile>> devices;
            try
            {
                devices = StripInstaller.InstalledEverywhere();
            }
            catch (Exception ex)
            {
                Log.Warn("Reading SimHub's LED profiles failed: " + ex.Message);
                devices = new List<List<InstalledProfile>>();
            }
            reachable = devices.Any(device => device != null);
            var census = new List<KeyValuePair<LedBar, FlagBoxPlan>>();
            foreach (var bar in Settings.LedBarList())
            {
                if (bar == null || bar.ProfileShapeId == null) continue;
                string json;
                var description = embedded != null && embedded.TryGetValue(bar.ProfileShapeId, out json) ? FlagBoxProfile.DescriptionOf(json) : null;
                census.Add(new KeyValuePair<LedBar, FlagBoxPlan>(bar, LedBarProfile.Plan(bar, description, devices)));
            }
            return census;
        }

        /// <summary>Every embedded strip profile by its shape id, which is what the census compares against.</summary>
        private static IDictionary<string, string> EmbeddedJsonByShape()
        {
            var json = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var shape in EmbeddedShapes()) json[shape.Id] = shape.Json;
            return json;
        }

        /// <summary>
        /// Rewrites every strip of these shapes whose copy in SimHub is older than this build's, each into the
        /// device it names, and reports the row as it then stands.
        /// </summary>
        /// <remarks>
        /// The census is read again at the press rather than carried from the draw, so what is rewritten is
        /// what SimHub holds when the button is pressed. A strip whose device SimHub no longer has is left
        /// alone and reported as failed with the reason in SimHub's log: InstallBar takes the strip's copy
        /// out of every device first, so running it there would remove a strip that still lights.
        /// </remarks>
        private FlagBoxPlan UpdateBars(IReadOnlyList<string> shapeIds, IDictionary<string, string> embedded)
        {
            bool reachable;
            var outdated = PanelLightRows.OutdatedBars(shapeIds, BarCensus(embedded, out reachable));
            var results = new List<FlagBoxPlan>();
            foreach (var bar in outdated)
            {
                string json;
                if (!embedded.TryGetValue(bar.ProfileShapeId, out json))
                {
                    results.Add(new FlagBoxPlan { State = FlagBoxInstallState.NotEmbedded });
                }
                else if (LedTargets.Find(bar.Device) == null)
                {
                    Log.Warn("The profile for " + bar.Name + " was not updated: the LED device it names is no longer in SimHub.");
                    results.Add(new FlagBoxPlan { State = FlagBoxInstallState.Failed });
                }
                else
                {
                    results.Add(InstallBar(bar, json));
                }
            }
            if (results.Any(plan => plan.State != FlagBoxInstallState.UpToDate)) return FlagBoxInstallPlan.Combine(results);
            return PanelLightRows.RowPlan(shapeIds, BarCensus(embedded, out reachable), reachable);
        }
    }
}
