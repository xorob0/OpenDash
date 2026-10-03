// SettingsControl.Profiles.cs: the light profiles as SimHub holds them -- asking what it has, installing the
// flag box and a strip, the census of the rig's strips, and updating the old ones.
//
// Lifted out of the Install and Lights tabs so that the pages that need them share one copy: LEDs installs and
// moves a strip, Matrix installs the flag box from its header, Updates lists what is in SimHub, and Home asks
// what needs fixing (SettingsControl.Status.cs). With them, what more than one page draws or presses: the
// flag box's glyph sheet, writing a screen back, and the flag box's by-hand import. The shell owns this file.
//
// NOTHING IS WRITTEN TO SIMHUB EXCEPT ON A PRESS, which is the consent half of ADR 0013. SafePlan and
// BarCensus only ask SimHub what it already holds, and the profiles are read out of the assembly rather than
// off disk. A strip's embedded profile is always looked up by the strip's profile shape, which is the reversed
// twin for a strip wired from the far end: looked up by its Shape it finds the plain wiring and installs that.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace OpenDashPlugin
{
    public partial class SettingsControl
    {
        // --- What more than one page draws or does -------------------------------------------------------
        //
        // Held here, in a shell file, rather than in the page that first needed it, so that the page agents
        // rebuilding their files in parallel cannot rename or drop something another page calls.

        /// <summary>The flag box's glyphs, read once, for every picture of a matrix: Matrix, Rig and Home.</summary>
        private PanelGlyphSheet glyphSheet;

        private PanelGlyphSheet GlyphSheet => glyphSheet ?? (glyphSheet = PanelGlyphSheet.Load(typeof(OpenDash).Assembly));

        /// <summary>
        /// Writes a screen's dashboard back into SimHub after its folder has gone, selects the screen on
        /// Screens, redraws the page that is showing and says how it went: Home's fix and the Screens page's
        /// fix box both press this.
        /// </summary>
        private void InstallScreenAgain(ScreenInstance screen)
        {
            WriteScreenThen(() => plugin.Installer.Write(screen), result =>
            {
                Save(screen);
                Select(PanelPage.Screens, screen.Namespace);
                Redraw();
                if (!result.Ok) Log.Warn("Writing " + screen.Name + " again failed: " + result.Error);
                Say(result.Ok ? PanelAddScreen.Reinstalled(screen.Name) : PanelAddScreen.ReinstallFailed(screen.Name, result.Error), result.Ok);
            });
        }

        /// <summary>
        /// Runs a press's writes to DashTemplates off the interface thread, and its ending back on it.
        /// </summary>
        /// <remarks>
        /// Every press that writes a dashboard folder, puts one back or removes one comes through here, because each
        /// used to run on the click and SimHub's whole window stopped answering while a package was extracted, a
        /// folder hashed and a backup zipped (#611). The work is counted as writing (UpdateService.WriteInBackground),
        /// so a SimHub closing meanwhile waits for it and two writers never overlap; whether a press should be refused
        /// while another is running, rather than wait its turn, is #606's, which reads UpdateService.Busy.
        ///
        /// The ending is posted, never Invoked: End runs on the interface thread and waits there for writers, so
        /// nothing on the work's thread may wait on that one. It runs on whatever page shows by then, so it reads the
        /// settings and the disk afresh, and it keeps its own net, since nothing on a posted callback is caught for it.
        /// </remarks>
        /// <param name="write">The disk work, on the work's thread: it touches neither the panel nor SimHub's objects.</param>
        /// <param name="done">The ending, on the interface thread, with null or what the work threw.</param>
        private void WriteThen(Action write, Action<Exception> done)
        {
            var started = plugin.Updates.WriteInBackground(write, failure => Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    done(failure);
                }
                catch (Exception ex)
                {
                    Log.Error("Finishing a write to SimHub's dashboards on the panel failed", ex);
                }
            })));
            // Only a SimHub that is closing turns a write away, and it is not going to draw the answer.
            if (!started) Log.Warn("A write to SimHub's dashboards was not started: SimHub is closing.");
        }

        /// <summary>
        /// <see cref="WriteThen"/> for a press on one screen's folder: the sheet that asked is closed at the press, the
        /// installer reads the disk again after the write on the same thread, and the ending is handed the result, a
        /// failed one where the work threw.
        /// </summary>
        /// <remarks>
        /// Closed at the press rather than by the ending's Redraw, so that a sheet's Save or Add cannot be pressed a
        /// second time while the first press is writing, which would add a second screen. The read after is the
        /// write's because it hashes every folder on the rig, which on the interface thread was most of a second of
        /// the window not answering by itself.
        /// </remarks>
        private void WriteScreenThen(Func<ScreenInstallResult> write, Action<ScreenInstallResult> done)
        {
            CloseSheet();
            ScreenInstallResult result = null;
            WriteThen(() =>
            {
                result = write();
                plugin.Installer.Refresh();
            }, failure => done(result ?? new ScreenInstallResult { Error = failure == null ? "nothing was written" : failure.Message }));
        }

        private TextBlock flagBoxLine;
        private System.Windows.Controls.TextBox flagBoxPath;

        /// <summary>What the last copy said and the path it named, kept across a rebuild of the page (a resize,
        /// a sidebar flip, any press that redraws) so its answer is not lost, and let go when the page is left.</summary>
        private string flagBoxCopied;
        private string flagBoxCopiedPath;

        /// <summary>
        /// The by-hand route for the flag box, only when the one-click one is not there at all: ADR 0013 keeps
        /// the extracted file precisely so that there is something to import when the matrix driver cannot be
        /// reached. Shared by Matrix, which draws it under the profile in its header, and Updates, under its
        /// table.
        /// </summary>
        /// <remarks>
        /// The press and the path box are a wrap rather than a row (#792): side by side they are about 555 px,
        /// which a narrow column does not have, so there the box drops under the press instead of being cut.
        /// Each carries 12 under it and the row takes the last 12 back.
        /// </remarks>
        private FrameworkElement BuildFlagBoxImportFallback(FlagBoxPlan plan)
        {
            OnDrop(() =>
            {
                flagBoxLine = null;
                flagBoxPath = null;
            });
            OnLeave("FlagBox.importCopy", () =>
            {
                flagBoxCopied = null;
                flagBoxCopiedPath = null;
            });
            flagBoxLine = Ui.Caption(flagBoxCopied ?? FlagBoxInstallPlan.Summary(plan, plugin.FlagBox?.Path), BodyWidth);
            var copy = BuildSecondaryButton(PanelCopy.CopyForImport, PanelCopy.CopyForImportTooltip);
            copy.Click += (sender, args) => CopyFlagBoxForImport();
            copy.Margin = new Thickness(0, 0, PanelShell.ImportGap, PanelShell.ImportGap);
            var path = new TextBox
            {
                Width = PanelShell.ImportPathWidth,
                IsReadOnly = true,
                Text = flagBoxCopiedPath ?? plugin.FlagBox?.Path ?? string.Empty,
                ToolTip = PanelCopy.ImportPathTooltip,
                Margin = new Thickness(0, 0, 0, PanelShell.ImportGap),
                VerticalAlignment = VerticalAlignment.Center,
            };
            Ui.Field(path, Theme.ControlHeightSm);
            flagBoxPath = path;
            var row = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, -PanelShell.ImportGap) };
            row.Children.Add(copy);
            row.Children.Add(path);
            var block = Ui.VStack(PanelShell.ImportLineGap, flagBoxLine, row);
            block.HorizontalAlignment = HorizontalAlignment.Left;
            return block;
        }

        /// <summary>Copies the flag box profile to where SimHub's import dialog opens, keeps what it said for a
        /// rebuild, and says it on the route's line.</summary>
        private void CopyFlagBoxForImport()
        {
            var copied = FlagBoxProfile.CopyForImport(plugin.FlagBox, null, new SimHubInstallLog());
            var ok = copied != null && copied.Status != FlagBoxStatus.Failed && copied.Status != FlagBoxStatus.NotEmbedded;
            // Not every refusal reaches the log by itself (no Documents folder), and the line points there.
            if (!ok) Log.Warn("Copying the flag box profile for import failed: " + (copied?.Message ?? "no result"));
            if (copied?.Path != null) flagBoxCopiedPath = copied.Path;
            flagBoxCopied = ok ? PanelCopy.CopiedForImport(copied.Path) : PanelCopy.CopyForImportFailed;
            if (flagBoxPath != null && flagBoxCopiedPath != null) flagBoxPath.Text = flagBoxCopiedPath;
            if (flagBoxLine != null) flagBoxLine.Text = flagBoxCopied;
        }

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
        /// The id of every strip profile this build embedded, the wirings and the far-end twins included,
        /// read off the resource names without opening one: what the Add LEDs sheet offers.
        /// </summary>
        /// <remarks>
        /// The census is what is embedded: a shape this build does not carry is not offered and cannot be
        /// added as a strip whose profile does not exist. The add form's two numbers leave the wirings out
        /// (PanelLights.BarSides and BarCentres); a strip's profile is then resolved through
        /// <see cref="EmbeddedProfileOf"/>.
        /// </remarks>
        private static IList<string> EmbeddedShapeIds()
        {
            var ids = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var resource in FlagBoxProfile.StripResourceNames(typeof(OpenDash).Assembly))
            {
                var id = FlagBoxProfile.ShapeIdOf(resource);
                if (id != null && seen.Add(id)) ids.Add(id);
            }
            return ids;
        }

        /// <summary>The profiles already opened, by shape id: a resource cannot change while the plugin runs.</summary>
        private static readonly Dictionary<string, string> embeddedJson = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>
        /// The embedded profiles of these shape ids only, each decompressed once for the plugin's lifetime.
        /// </summary>
        /// <remarks>
        /// What the attention census and a reinstall need is the rig's own strips' profiles, a handful at
        /// most. Opening all of them -- 122 gzipped profiles, 44 million characters -- took about 70 ms and
        /// 171 MB on every Go, which is every sidebar click, even on a rig with no strips.
        /// </remarks>
        private static IDictionary<string, string> EmbeddedJsonFor(IEnumerable<string> shapeIds)
        {
            var json = new Dictionary<string, string>(StringComparer.Ordinal);
            var wanted = new HashSet<string>((shapeIds ?? Enumerable.Empty<string>()).Where(id => id != null), StringComparer.Ordinal);
            if (wanted.Count == 0) return json;
            lock (embeddedJson)
            {
                foreach (var id in wanted.ToList())
                {
                    string text;
                    if (!embeddedJson.TryGetValue(id, out text)) continue;
                    json[id] = text;
                    wanted.Remove(id);
                }
                if (wanted.Count == 0) return json;
                var assembly = typeof(OpenDash).Assembly;
                var log = new SimHubInstallLog();
                foreach (var resource in FlagBoxProfile.StripResourceNames(assembly))
                {
                    var id = FlagBoxProfile.ShapeIdOf(resource);
                    if (id == null || !wanted.Remove(id)) continue;
                    var text = FlagBoxProfile.ResourceText(assembly, resource, log);
                    if (text == null) continue;
                    embeddedJson[id] = text;
                    json[id] = text;
                    if (wanted.Count == 0) break;
                }
            }
            return json;
        }

        /// <summary>The embedded profile a strip installs, by <see cref="LedBar.ProfileShapeId"/>, or null.</summary>
        private static EmbeddedShape EmbeddedProfileOf(LedBar bar)
        {
            if (bar == null || bar.ProfileShapeId == null) return null;
            string json;
            return EmbeddedJsonFor(new[] { bar.ProfileShapeId }).TryGetValue(bar.ProfileShapeId, out json) ? new EmbeddedShape(bar.ProfileShapeId, json) : null;
        }

        /// <summary>
        /// Installs one strip's profile into the device it names, having taken it out of every other.
        /// </summary>
        /// <remarks>
        /// Out of wherever it was first: a strip that has moved from the Arduino to a wheel must not leave a
        /// copy behind reading properties that now drive the wheel's, and the install only knows about the
        /// device it is going to. Both over <paramref name="targets"/>, the devices the page already read, where
        /// each used to walk SimHub's devices again (#611).
        /// </remarks>
        private static FlagBoxPlan InstallBar(LedBar bar, string embedded, IList<LedTarget> targets)
        {
            try
            {
                StripInstaller.UninstallEverywhere(LedBarProfile.IdFor(bar.Namespace), targets);
                return StripInstaller.Install(LedBarProfile.For(bar, embedded), bar.Device, targets);
            }
            catch (Exception ex)
            {
                Log.Error("Installing the profile for " + bar.Name + " failed", ex);
                return new FlagBoxPlan { State = FlagBoxInstallState.Failed };
            }
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
                devices = StripInstaller.InstalledEverywhere(ledDevices.Targets);
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
    }
}
