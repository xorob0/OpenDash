// OpenDash.cs: the SimHub plugin. Installs the embedded dashboard on startup, exposes the settings as
// [OpenDash.*] properties and offers the settings panel in SimHub's left menu. It renders nothing:
// see docs/decisions/0003-plugin-settings-through-properties.md.
//
// It does read telemetry, for exactly one thing. ADR 0018 reopened ADR 0009 -- "the plugin does not
// compute" -- for the car's own LED bar, because there is no SimHub property to derive it from, no
// expression that could hold an 85-car table and no NCalc clock to flash it with. DataUpdate below
// is the whole of that: three values in, one frame of colours out, and nothing else in the plugin
// reads a telemetry value.
using System;
using System.Linq;
using System.Reflection;
using System.Windows.Controls;
using System.Windows.Media;
using GameReaderCommon;
using SimHub.Plugins;

namespace OpenDashPlugin
{
    [PluginName("OpenDash")]
    [PluginAuthor("OpenDash contributors")]
    public class OpenDash : IDataPlugin, IWPFSettingsV2
    {
        /// <summary>SimHub stores the settings as PluginsData/Common/OpenDash.GeneralSettings.json.</summary>
        public const string SettingsKey = "GeneralSettings";

        private ImageSource icon;

        public PluginManager PluginManager { get; set; }

        /// <summary>The live settings object. The attached delegates read it, so a change is visible to the
        /// dashboard on the next frame; the panel writes it and calls SaveSettings().</summary>
        public OpenDashSettings Settings { get; private set; } = new OpenDashSettings();

        private DashboardInstaller installer;

        /// <summary>
        /// Built on first use rather than eagerly, because the record of what OpenDash wrote into each folder lives
        /// in the settings and the settings are read in Init. The lambda reads Settings each time, so the record
        /// follows the object the panel replaces when a user changes something.
        /// </summary>
        public DashboardInstaller Installer =>
            installer ?? (installer = new DashboardInstaller(new SettingsFolderRecord(() => Settings)));

        /// <summary>What became of the flag box profile at startup, for the lights page. Null until Init runs.</summary>
        public FlagBoxResult FlagBox { get; private set; }

        /// <summary>
        /// The measured car light tables, fetched onto the machine rather than shipped (ADR 0018).
        /// Built on first use, like the installer, because it needs the SimHub root the settings name.
        /// </summary>
        public CarLightService CarLights =>
            carLights ?? (carLights = new CarLightService(new ReleaseClient(Version), CarLightLibrary.FolderPath(Installer.SimHubRoot)));

        private CarLightService carLights;

        /// <summary>Monotonic milliseconds for the over-rev flash, which is the only thing OpenDash times.</summary>
        private readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();

        /// <summary>The embedded profile as JSON, which the lights page installs into SimHub.</summary>
        public string FlagBoxJson
        {
            get { return FlagBox?.Json; }
        }

        public string LeftMenuTitle => "OpenDash";

        public ImageSource PictureIcon => icon ?? (icon = PluginIcon.Create());

        /// <summary>The plugin version, which is the VERSION file: "0.1.0".</summary>
        public static string Version
        {
            get
            {
                var assembly = typeof(OpenDash).Assembly;
                var informational = assembly.GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false)
                    .OfType<AssemblyInformationalVersionAttribute>()
                    .Select(a => a.InformationalVersion)
                    .FirstOrDefault();
                if (!string.IsNullOrEmpty(informational))
                {
                    var plus = informational.IndexOf('+');
                    return plus > 0 ? informational.Substring(0, plus) : informational;
                }
                var version = assembly.GetName().Version;
                return version == null ? "0.0.0" : version.Major + "." + version.Minor + "." + version.Build;
            }
        }

        /// <summary>
        /// The version this rig runs, which the update check compares, the idle screen's mark compares and every
        /// line of the panel names. UpdateCheck.RigVersion is the definition; this is the one place it is read
        /// from, so that none of them can compose it differently from the others (#458).
        /// </summary>
        public string RigVersion =>
            UpdateCheck.RigVersion(Installer.Packages, Settings.RigScreens().Select(screen => screen.Folder), Version);

        /// <summary>
        /// The one update service, shared by the check that runs from Init and the panel, so the releases a
        /// check found are the ones the panel's Update button applies whichever of the two asked.
        /// </summary>
        public UpdateService Updates =>
            updates ?? (updates = new UpdateService(new ReleaseClient(Version), new SimHubInstallLog()));

        private UpdateService updates;

        /// <summary>What this session's last completed check concluded, or null before one has. The panel opens
        /// on it, so a panel opened after the background check shows the answer that check found.</summary>
        public UpdateStatus LastUpdateStatus { get; private set; }

        /// <summary>
        /// Raised on the interface thread when a check started by <see cref="StartUpdateCheck"/> has finished,
        /// with its answer, or with null when the service declined to ask after all.
        /// </summary>
        public event Action<UpdateStatus> UpdateChecked;

        /// <summary>1 while a check is in flight, so that the panel opening during the one Init queued waits for
        /// its answer rather than asking GitHub a second time.</summary>
        private int checking;

        /// <summary>Whether this start's automatic check has been started, whatever it went on to find, so that
        /// the panel does not ask a second time after an answer nobody could read; see
        /// UpdateCheck.MayAskThisStart. Read and written only while <see cref="checking"/> is held.</summary>
        private bool automaticAsked;

        /// <summary>The release the idle screen's mark offers, or null; see <see cref="RefreshUpdateMark"/>.</summary>
        private volatile string offeredUpdate;

        /// <summary>
        /// Asks GitHub for the newest release, off the calling thread, when the setting and the interval allow.
        /// </summary>
        /// <returns>Whether an answer is coming: a check was started, or one already in flight will answer.</returns>
        /// <remarks>
        /// Called by Init once per start and by the panel. ADR 0012 is the whole of the policy and
        /// UpdateCheck.ShouldCheck and UpdateCheck.MayAskThisStart the whole of its arithmetic: off means nothing
        /// is constructed, let alone fetched; automatic means not within a day of the last answer and only once
        /// per start, answered or not, though a check in flight answers whoever asks while it runs; and nothing
        /// here joins the thread it was called on, so no network, a hung socket or a refused answer cannot delay
        /// SimHub's start. The answer lands on the interface thread, which is the one thread that writes the
        /// settings.
        /// </remarks>
        public bool StartUpdateCheck(bool manual)
        {
            if (!UpdateCheck.ShouldCheck(Settings.CheckForUpdates, Settings.LastUpdateCheckTicks, DateTime.UtcNow, manual)) return false;
            if (System.Threading.Interlocked.CompareExchange(ref checking, 1, 0) != 0) return true;
            if (!UpdateCheck.MayAskThisStart(manual, automaticAsked))
            {
                System.Threading.Interlocked.Exchange(ref checking, 0);
                return false;
            }
            if (!manual) automaticAsked = true;
            var installed = RigVersion;
            var enabled = Settings.CheckForUpdates;
            var ticks = Settings.LastUpdateCheckTicks;
            UpdateService.InBackground(() =>
            {
                UpdateStatus answer = null;
                try
                {
                    answer = Updates.Check(installed, enabled, ref ticks, DateTime.UtcNow, manual);
                }
                finally
                {
                    var answered = answer;
                    var answeredTicks = ticks;
                    OnInterfaceThread(() => ConcludeUpdateCheck(answered, answeredTicks));
                }
            }, new SimHubInstallLog());
            return true;
        }

        private void ConcludeUpdateCheck(UpdateStatus answer, long ticks)
        {
            System.Threading.Interlocked.Exchange(ref checking, 0);
            if (answer != null)
            {
                LastUpdateStatus = answer;
                var remembered = UpdateMark.Remember(Settings.OfferedRelease, answer);
                if (ticks != Settings.LastUpdateCheckTicks || remembered != Settings.OfferedRelease)
                {
                    Settings.LastUpdateCheckTicks = ticks;
                    Settings.OfferedRelease = remembered;
                    SaveSettings();
                }
            }
            RefreshUpdateMark();
            try
            {
                UpdateChecked?.Invoke(answer);
            }
            catch (Exception ex)
            {
                Log.Error("Showing the update check's answer failed", ex);
            }
        }

        /// <summary>
        /// Recomputes what the idle screen's mark offers from the remembered answer and what the rig runs.
        /// </summary>
        /// <remarks>
        /// Computed here, when either side moves, rather than in the delegates: SimHub reads a delegate far more
        /// often than a check finishes or an update lands, and the installed version is not free to work out.
        /// The switch is the exception and is read live by the delegates, so turning the check off silences the
        /// mark on the next frame rather than on the next check.
        /// </remarks>
        public void RefreshUpdateMark()
        {
            offeredUpdate = UpdateMark.Offered(Settings.OfferedRelease, RigVersion, PluginUpdate.Pending(Installer.SimHubRoot));
        }

        /// <summary>The release the idle screen's mark offers whatever the switch says, or null; the panel reads
        /// it so that the two say the same thing.</summary>
        public string OfferedUpdate => offeredUpdate;

        /// <summary>Runs work on SimHub's interface thread, or here when there is none.</summary>
        private static void OnInterfaceThread(Action work)
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher == null)
            {
                work();
                return;
            }
            dispatcher.BeginInvoke(work);
        }

        public void Init(PluginManager pluginManager)
        {
            Log.Info("OpenDash plugin " + Version + " starting");
            LoadSettings();
            // Every zone starts on the page it is set to open on, which is what that setting means.
            Settings.OpenOnStartPages();
            // And the moment the companions stop being held there. Measured from here rather than from
            // the first frame, because a rig with no game running still loads its dashboards and a
            // companion sitting in the menus should be on its start module too.
            releaseStartModulesAt = DateTime.UtcNow + Contract.CompanionOpenOnWindow;
            try
            {
                // Before installing, not after: a staging folder left by an interrupted update is a complete
                // extracted dashboard sitting in DashTemplates, and they accumulate one per abandoned update.
                PackageExtractor.RemoveOrphanedStaging(Installer.SimHubRoot, new SimHubInstallLog());
                // And arm the plugin swap again if one is still waiting: the waiter armed when the
                // assembly was staged gives up after a while, and a session that reaches here with a
                // staged assembly is a session where the last swap did not happen. Inert otherwise.
                PluginUpdate.Launch(Installer.SimHubRoot, new SimHubInstallLog());
                // The rig decides what is written. Before ADR 0017 this wrote every package the plugin
                // embeds on every start, so a user who owned one screen found fourteen dashboards in
                // SimHub's list; now a screen exists because somebody added it. Nothing outside the rig
                // is deleted -- that is a thing a user asks for -- it is simply no longer rewritten.
                Installer.Wanted = Settings.RigScreens().Select(screen => screen.Folder).Where(folder => folder != null).ToList();
                // A driver who said yes to replacing their edited dashboards said it to the plugin that
                // downloaded this one, and this start is what writes them (#438); see EditedConsent.
                var replaceEdited = EditedConsent.AppliesNow(Settings.ReplaceEditedFor, Version);
                if (replaceEdited) Log.Info("Replacing edited dashboards, as asked when " + Version + " was downloaded");
                Installer.EnsureInstalled(false, replaceEdited);
                if (EditedConsent.Forget(Settings.ReplaceEditedFor, Version, PluginUpdate.Pending(Installer.SimHubRoot))) Settings.ReplaceEditedFor = null;
                WriteScreenFolders();
            }
            catch (Exception ex)
            {
                Log.Error("Dashboard installation failed", ex);
            }
            try
            {
                // Extracted, not installed: ADR 0013. The user imports it, and the panel says so.
                FlagBox = FlagBoxProfile.Extract(Installer.SimHubRoot, typeof(OpenDash).Assembly, new SimHubInstallLog());
            }
            catch (Exception ex)
            {
                Log.Error("Writing the flag box profile failed", ex);
            }
            AttachProperties();
            AttachActions(pluginManager);
            try
            {
                // What the idle screen's mark says before anything is asked: the release the last answered
                // check offered, compared with what this rig now runs. Then the day's check itself, queued
                // rather than run, so that a rig with no network starts exactly as fast as one with it.
                RefreshUpdateMark();
                StartUpdateCheck(false);
            }
            catch (Exception ex)
            {
                Log.Error("Starting the update check failed", ex);
            }
            try
            {
                // Off the startup thread because it reads a folder, and it does no more than that:
                // starting SimHub never fetches the tables. They arrive when the driver presses the
                // button on the Lights tab and at no other moment (#366), which is what makes the copy
                // on disk theirs -- see PanelLights.CarTablesCaption and ADR 0018.
                CarLights.LoadInBackground();
            }
            catch (Exception ex)
            {
                Log.Error("Loading the car light tables failed", ex);
            }
            Log.Info("Dashboard status: " + Installer.Status);
            Log.Info(FlagBoxProfile.Summary(FlagBox));
            // The installer records what it wrote into each folder but never saves; this is the safe moment.
            SaveSettings();
        }

        /// <summary>
        /// Writes the folder of any screen that has not got one.
        /// </summary>
        /// <remarks>
        /// The stock screens are DashboardInstaller's, because their folder is a package's own and it
        /// keeps them at the embedded version. A screen with a namespace of its own has a folder no
        /// package writes, so it is written here -- once, when it is missing. A folder somebody deleted
        /// comes back on the next start, which is the same promise the stock ones have always made; the
        /// panel offers the same thing on a button for a user who does not want to restart to get it.
        ///
        /// Then every screen's name goes back into the dashboard SimHub lists, stock ones included. The
        /// installer above has just written the stock folders from their packages, which carries the
        /// package's own title with it, so a screen the driver renamed left SimHub's list under the name
        /// they knew it by at every single update. Cheap where nothing moved: Retitle compares before it
        /// writes and does nothing to a folder whose title is already the screen's.
        /// </remarks>
        private void WriteScreenFolders()
        {
            var log = new SimHubInstallLog();
            RepairScreenSizes(log);
            foreach (var screen in Settings.RigScreens())
            {
                if (!screen.IsStock)
                {
                    var result = ScreenInstaller.Write(screen, Installer.PackageSource, Installer.SimHubRoot, Installer.Record, log);
                    if (!result.Ok)
                    {
                        Log.Warn("The screen " + screen.Name + " has no folder: " + result.Error);
                        continue;
                    }
                }
                ScreenInstaller.Retitle(screen, Installer.SimHubRoot, Installer.Record, log);
            }
        }

        /// <summary>
        /// Gives a size to any screen that has none.
        /// </summary>
        /// <remarks>
        /// A rig migrated from a settings file written before ADR 0017 takes its sizes from the folder
        /// names, and "OpenDash Companion", "OpenDash Pit wall" and the round faces carry none, so those
        /// screens arrived at 0 x 0 and their cards said so. The packages know: the size is in each
        /// one's .djson.metadata. Matched on the folder, because that is the one thing a migrated screen
        /// certainly has.
        /// </remarks>
        private void RepairScreenSizes(IInstallLog log)
        {
            if (!Settings.RigScreens().Any(screen => screen.Width <= 0 || screen.Height <= 0)) return;
            var catalogue = PackageCatalogue.From(Installer.PackageSource, log);
            foreach (var screen in Settings.RigScreens())
            {
                if (screen.Width > 0 && screen.Height > 0) continue;
                var match = catalogue.FirstOrDefault(entry => string.Equals(entry.Folder, screen.Folder, StringComparison.OrdinalIgnoreCase));
                if (match == null || match.Width <= 0) continue;
                screen.Width = match.Width;
                screen.Height = match.Height;
                if (screen.Package == null) screen.Package = match.Package;
                // The name was the folder because there was no size to call it by; a screen the user has
                // renamed keeps whatever they chose.
                if (string.Equals(screen.Name, screen.Folder, StringComparison.Ordinal)) screen.Name = screen.SizeLabel;
            }
        }

        /// <summary>The lights, which no dashboard reads and the lighting profiles do. A profile the user
        /// has not imported costs nothing here: a property nobody reads is one delegate.</summary>
        private void AttachLightsProperties()
        {
            this.AttachDelegate(Contract.LightsBrightness, () => Settings.LightsBrightness);
            this.AttachDelegate(Contract.LightsNightBrightness, () => Settings.LightsNightBrightness);
            this.AttachDelegate(Contract.LightsNightMode, () => Settings.LightsNightMode);
            // One number under two names. LightsLowFuelLaps is what the contract reads first and
            // FlagBoxLowFuelLaps is the name that shipped, so both carry the threshold the driver set
            // and a profile of either vintage finds it. The field keeps the old spelling because that
            // is what a settings file on disk is keyed by.
            this.AttachDelegate(Contract.FlagBoxLowFuelLaps, () => Settings.FlagBoxLowFuelLaps);
            this.AttachDelegate(Contract.LightsLowFuelLaps, () => Settings.FlagBoxLowFuelLaps);
            this.AttachDelegate(Contract.FlagBoxSpotterAnimation, () => Settings.FlagBoxSpotterAnimation);
            // The bands of the car's own measured bar, which the digit on a panel set to it reads.
            // Filled by the same DataUpdate that fills the strips' runs, and -1 whenever there is no
            // table behind them -- a rig with none, a car with no row, or nothing on the rig asking.
            this.AttachDelegate(Contract.CarLadderStage, () => CarLights.Stage);
            this.AttachDelegate(Contract.CarLadderOverRev, () => CarLights.OverRev);
            // The same bar as a count out of a total, which is what a fifteen-segment rev bar fills
            // itself from, and the RPM its top third lights at, which is what the Redline beside it
            // prints. Zero and zero with no table, and a screen then draws the published ladder.
            this.AttachDelegate(Contract.CarLadderLit, () => CarLights.Lit);
            this.AttachDelegate(Contract.CarLadderLamps, () => CarLights.Lamps);
            this.AttachDelegate(Contract.CarLadderTopRpm, () => CarLights.TopRpm);
            // And whether this car flashes at all, which is what lets a screen tell "not over-revving"
            // from "never says so" and keep OpenDash's own redline flash on the 47 measured cars in 85
            // that publish none.
            this.AttachDelegate(Contract.CarLadderFlashes, () => CarLights.Flashes);
            // And whether the rig asked for any of it, which a screen cannot work out for itself: the
            // style is a per-bar setting and a face has no bar. The same reduction that decides whether
            // the tables are walked at all, so the gate and the numbers behind it cannot disagree.
            this.AttachDelegate(Contract.CarLadderChosen, () => Settings.AnyCarLadderWanted());
            foreach (var matrix in Contract.FlagBoxMatrices)
            {
                var m = matrix;
                // What the slot *shows*, not merely what it is set to: a slot nobody has added is dark
                // whatever its settings still say, which is what lets a rig start with no panels at all.
                this.AttachDelegate(Contract.FlagBoxMatrixProperty(m, "Rest"), () => Settings.MatrixShownRest(m));
                this.AttachDelegate(Contract.FlagBoxMatrixProperty(m, "Flags"), () => Settings.MatrixShowsFlags(m));
                this.AttachDelegate(Contract.FlagBoxMatrixProperty(m, "Pit"), () => Settings.MatrixShowsPit(m));
                this.AttachDelegate(Contract.FlagBoxMatrixProperty(m, "Spotter"), () => Settings.MatrixShowsSpotter(m));
                this.AttachDelegate(Contract.FlagBoxMatrixProperty(m, "Warnings"), () => Settings.MatrixShowsWarnings(m));
                this.AttachDelegate(Contract.FlagBoxMatrixProperty(m, "Side"), () => Settings.MatrixSide(m));
                this.AttachDelegate(Contract.FlagBoxMatrixProperty(m, "CriticalOnly"), () => Settings.MatrixCriticalOnly(m));
                this.AttachDelegate(Contract.FlagBoxMatrixProperty(m, "Gear"), () => Settings.MatrixShowsGear(m));
                // Zero means "not set": the profile then applies its own default, which is per unit, so
                // a driver in Fahrenheit who has never opened this page does not get a Celsius number.
                this.AttachDelegate(Contract.FlagBoxMatrixProperty(m, "OilTemp"), () => Settings.MatrixOilTemp(m) == 0 ? (int?)null : Settings.MatrixOilTemp(m));
                this.AttachDelegate(Contract.FlagBoxMatrixProperty(m, "WaterTemp"), () => Settings.MatrixWaterTemp(m) == 0 ? (int?)null : Settings.MatrixWaterTemp(m));
                this.AttachDelegate(Contract.FlagBoxMatrixProperty(m, "GearBlink"), () => Settings.MatrixGearBlink(m));
                this.AttachDelegate(Contract.FlagBoxMatrixProperty(m, "GearBands"), () => Settings.MatrixGearBands(m));
                this.AttachDelegate(Contract.FlagBoxMatrixProperty(m, "GearCarLadder"), () => Settings.MatrixGearCarLadder(m));
            }
            // The strips, last, in the order Contract.LightsPropertyNames() declares them. Every
            // generated .ledsprofile reads these, so a strip with none of them attached can only ever
            // draw the defaults its isnull() carries.
            this.AttachDelegate(Contract.LedCentre, () => Settings.LedCentre);
            this.AttachDelegate(Contract.LedRpmStyle, () => Settings.LedRpmStyle);
            this.AttachDelegate(Contract.LedFlagAnimation, () => Settings.LedFlagAnimation);
            this.AttachDelegate(Contract.LedMirrorFit, () => Settings.LedMirrorFit);
            // The mirror. Ready is the gate every strip profile's car layer hangs on, and each run is
            // one fixed-width string the profile slices a colour out of with left(); DataUpdate fills
            // them. With the tables absent or the car unmeasured, Ready stays 0 and the profile draws
            // the ladder iRacing publishes, which is what it drew before any of this existed.
            this.AttachDelegate(Contract.LedMirrorReady, () => CarLights.Ready ? 1 : 0);
            foreach (var length in Contract.MirrorRunLengths)
            {
                var run = length;
                this.AttachDelegate(Contract.LedMirrorRun(run), () => CarLights.Run(run));
            }
            // One group per bar the rig holds, under that bar's own namespace, which is what lets two
            // strips be configured apart: the profile installed for a bar carries these names as
            // literals, rewritten from the rig-wide ones above by LedBarProfile. The namespace is
            // captured rather than the bar, because the panel replaces the settings object on every
            // change and a delegate holding the old bar would report the old value for ever.
            foreach (var bar in Settings.LedBarList())
            {
                var ns = bar.Namespace;
                this.AttachDelegate(LedBarProfile.Property(ns, Contract.LedCentre), () => Settings.BarCentre(ns));
                this.AttachDelegate(LedBarProfile.Property(ns, Contract.LedRpmStyle), () => Settings.BarRpmStyle(ns));
                this.AttachDelegate(LedBarProfile.Property(ns, Contract.LedFlagAnimation), () => Settings.BarFlagAnimation(ns));
                this.AttachDelegate(LedBarProfile.Property(ns, Contract.LedSpotterWhole), () => Settings.BarSpotterWhole(ns));
            }
        }

        /// <summary>
        /// One frame of the car's own bar. The only telemetry OpenDash reads, and the only thing it
        /// computes (ADR 0018).
        ///
        /// <para>It is called at SimHub's data rate, so it does the least it can: with the mirror off
        /// or the sim closed it sets one field and returns, and the table lookup happens on a car
        /// change rather than per frame. Nothing here may throw -- SimHub calls this from its own loop
        /// and an exception here would be one per frame -- so the whole body is guarded and a failure
        /// leaves the strip on the published ladder.</para>
        /// </summary>
        /// <summary>
        /// When the companions stop being held on their start module, or null once they have been let go.
        /// </summary>
        /// <remarks>
        /// Set by `Init`, read by `DataUpdate`, and the only clock involved. A companion opens on a chosen
        /// module because the plugin leaves exactly one of its screens enabled and SimHub moves off the
        /// rest; this is when that stops and the driver's own taps take over.
        /// </remarks>
        private DateTime? releaseStartModulesAt;

        public void DataUpdate(PluginManager pluginManager, ref GameData data)
        {
            try
            {
                if (releaseStartModulesAt != null && DateTime.UtcNow >= releaseStartModulesAt.Value)
                {
                    releaseStartModulesAt = null;
                    Settings.ReleaseStartModules();
                }
                var telemetry = data == null ? null : data.NewData;
                // Any bar asking for the car's own is enough, and so is the rig-wide answer a bar with no
                // opinion falls back to: the mirror is one computation feeding every strip, so gating it
                // on one setting would leave a second bar set to the car's own reading a run nothing fills.
                var on = data != null && data.GameRunning && telemetry != null
                    && (Settings.AnyCarLadderWanted() || Settings.AnyMatrixCarLadderWanted());
                CarLights.Update(
                    on ? telemetry.CarId : null,
                    on ? telemetry.Gear : null,
                    on ? telemetry.Rpms : 0,
                    Settings.LedMirrorFit == Contract.LedMirrorFitExact ? MirrorFit.Exact : MirrorFit.Stretch,
                    on,
                    clock.ElapsedMilliseconds);
            }
            catch (Exception)
            {
                // Once a frame, silently, and the strip falls back on its own: there is nothing useful
                // to log sixty times a second and nothing to recover.
            }
        }

        /// <summary>How long shutdown waits for an install that is rewriting DashTemplates.</summary>
        /// <remarks>
        /// Long enough for fourteen packages, short enough that a user closing SimHub does not think it has hung.
        /// The alternative to waiting is the process exiting between the delete of a dashboard folder and the move
        /// that replaces it.
        /// </remarks>
        public static readonly TimeSpan ShutdownGrace = TimeSpan.FromSeconds(20);

        public void End(PluginManager pluginManager)
        {
            if (UpdateService.Busy)
            {
                Log.Info("An update is still installing; waiting for it before SimHub closes.");
                if (!UpdateService.WaitForIdle(ShutdownGrace))
                {
                    Log.Warn("The update was still installing after " + ShutdownGrace.TotalSeconds
                        + " seconds and SimHub is closing anyway; a dashboard may be left as OpenDash found it.");
                }
            }
            SaveSettings();
        }

        public Control GetWPFSettingsControl(PluginManager pluginManager)
        {
            try
            {
                return new SettingsControl(this);
            }
            catch (Exception ex)
            {
                Log.Error("The settings panel could not be built", ex);
                return new UserControl
                {
                    Content = new TextBlock
                    {
                        Text = "OpenDash settings could not be displayed: " + ex.Message,
                        TextWrapping = System.Windows.TextWrapping.Wrap,
                        Margin = new System.Windows.Thickness(24),
                    },
                };
            }
        }

        public void SaveSettings()
        {
            try
            {
                Settings.Normalise();
                this.SaveCommonSettings(SettingsKey, Settings);
            }
            catch (Exception ex)
            {
                Log.Error("Saving the settings failed", ex);
            }
        }

        private void LoadSettings()
        {
            try
            {
                Settings = this.ReadCommonSettings<OpenDashSettings>(SettingsKey, () => new OpenDashSettings()) ?? new OpenDashSettings();
            }
            catch (Exception ex)
            {
                Log.Error("Reading the settings failed; using the defaults", ex);
                Settings = new OpenDashSettings();
            }
            Settings.Normalise();
        }

        /// <summary>
        /// One delegate per setting. SimHub names them <class name>.<name>, hence OpenDash.ShiftLights.
        /// </summary>
        /// <remarks>
        /// The shared settings first, then one group per screen the rig has, in the order
        /// OpenDashSettings.DeclaredProperties() lists them: a rig and not the catalogue, because eight
        /// faces of twenty-one properties is a hundred and sixty-eight names for a rig of two screens.
        ///
        /// A screen added while SimHub is running therefore has no properties until it is restarted.
        /// That is not a new limitation: SimHub reads its dashboard list once at startup too, so the
        /// screen a user has just added is not one they can open in this session either. Until then its
        /// bindings fall back to the defaults they carry, which is what a package does with no plugin at
        /// all.
        /// </remarks>
        private void AttachProperties()
        {
            this.AttachDelegate(Contract.ShiftLights, () => Settings.ShiftLights);
            AttachLightsProperties();
            this.AttachDelegate(Contract.PositionMode, () => Settings.PositionMode);
            this.AttachDelegate(Contract.DeltaReference, () => Settings.DeltaReference);
            this.AttachDelegate(Contract.SessionProgress, () => Settings.SessionProgress);
            // The twelve card slots. They are attached unconditionally and are not deprecated: a round
            // face becomes zones on a ring after 1.0 (#145), and until it does these are the only
            // card-slot properties the two round packages read, the published OpenDash slots <size>
            // faces read four to twelve of them besides, and no zone face reads one. Deleting a mode
            // attachment above is not safe for a round face on that account: both round packages also
            // read PositionMode, DeltaReference, SessionProgress, RevBar and ShiftLights. #170.
            for (var slot = 1; slot <= Contract.SlotCount; slot++)
            {
                var captured = slot;
                this.AttachDelegate(Contract.SlotProperty(captured), () => Settings.Slot(captured));
            }
            // Shared rather than one face's, because the round faces' rev arc and the companion's
            // speedo read it too, and attached last of the shared group because ShiftLights is one of
            // the names this list has always opened with. #170, #189.
            this.AttachDelegate(Contract.RevBar, () => Settings.RevBarMode());
            // And after it, for the same reason: every band that writes a name reads this, on a face
            // and on a card face alike, so it belongs to the rig rather than to a screen.
            this.AttachDelegate(Contract.BlueFlagDetail, () => Settings.BlueFlagDetail);
            // And the two that decide how a driver is named, shared for the same reason: a leaderboard
            // on the rim and a board on the pit wall write the same name, and a driver who reads
            // `L. Byrne` reads it on both. #385.
            this.AttachDelegate(Contract.DriverNameFormat, () => Settings.DriverNameFormat);
            this.AttachDelegate(Contract.DriverNameTeam, () => Settings.DriverNameTeam);
            // And the idle screen's mark, shared because every package ends with an idle screen. The switch is
            // read live and the offer is computed when it changes; see RefreshUpdateMark. #83.
            this.AttachDelegate(Contract.UpdateAvailable, () => UpdateMark.Available(Settings.CheckForUpdates, offeredUpdate));
            this.AttachDelegate(Contract.UpdateVersion, () => UpdateMark.Shown(Settings.CheckForUpdates, offeredUpdate));
            // One group per screen the rig holds, under that screen's own namespace, which is what lets
            // two screens of one size be configured apart (ADR 0017). The screen object is captured
            // rather than looked up per read: the panel replaces the settings object on every change, so
            // a delegate that searched the rig by namespace would be searching a rig that has moved.
            foreach (var screen in Settings.RigScreens())
            {
                var s = screen;
                if (s.IsFace)
                {
                    foreach (var letter in Contract.FaceZoneLetters)
                    {
                        var captured = letter;
                        this.AttachDelegate(Contract.ZonePageProperty(s.Namespace, captured), () => Settings.ScreenFace(s.Namespace).Zone(captured));
                        this.AttachDelegate(Contract.ZoneMaskProperty(s.Namespace, captured), () => Settings.ScreenFace(s.Namespace).Mask(captured));
                        this.AttachDelegate(Contract.ZoneStartProperty(s.Namespace, captured), () => Settings.ScreenFace(s.Namespace).Start(captured));
                        this.AttachDelegate(Contract.ZoneClassOnlyProperty(s.Namespace, captured), () => Settings.ScreenFace(s.Namespace).IsClassOnly(captured));
                    }
                    foreach (var slot in Contract.BarSlots)
                    {
                        var captured = slot;
                        this.AttachDelegate(Contract.BarFieldProperty(s.Namespace, captured), () => Settings.ScreenFace(s.Namespace).BarField(captured));
                    }
                    this.AttachDelegate(Contract.QuickGlanceProperty(s.Namespace), () => Contract.NormaliseQuickGlance(Settings.ScreenFace(s.Namespace).QuickGlance));
                    this.AttachDelegate(Contract.FlagFormatProperty(s.Namespace), () => Settings.ScreenFlagFormat(s.Namespace));
                    this.AttachDelegate(Contract.LapReviewProperty(s.Namespace), () => Settings.ScreenLapReview(s.Namespace));
                    this.AttachDelegate(Contract.RevBarProperty(s.Namespace), () => Settings.ScreenRevBar(s.Namespace));
                }
                else if (s.IsCompanion)
                {
                    for (var module = 1; module <= Modules.Count; module++)
                    {
                        var captured = module;
                        this.AttachDelegate(Contract.ModuleProperty(s.Namespace, captured), () => Settings.ScreenModule(s.Namespace, captured));
                    }
                    // The page the companion is on, which its screens' enabled expressions follow. Live
                    // state and not a saved setting: Init puts it back on the start module, exactly as
                    // it puts every zone back on the page it opens on.
                    this.AttachDelegate(Contract.CompanionPageProperty(s.Namespace), () => Settings.ScreenCompanionPage(s.Namespace));
                    this.AttachDelegate(Contract.CompanionFlagFormatProperty(s.Namespace), () => Settings.ScreenCompanionFlagFormat(s.Namespace));
                    this.AttachDelegate(Contract.CompanionOpenOnProperty(s.Namespace), () => Settings.ScreenCompanionOpenOn(s.Namespace));
                }
                else if (s.IsPitWall)
                {
                    foreach (var slot in Contract.PitWallZoneSlots)
                    {
                        var captured = slot;
                        this.AttachDelegate(Contract.ZoneProperty(s.Namespace, captured), () => Settings.ScreenZone(s.Namespace, captured.Key));
                    }
                    this.AttachDelegate(Contract.PitWallPageProperty(s.Namespace), () => Settings.ScreenPitWallPage(s.Namespace));
                    this.AttachDelegate(Contract.WebViewUrlProperty(s.Namespace), () => Settings.ScreenWebViewUrl(s.Namespace));
                    this.AttachDelegate(Contract.PitWallClassOnlyProperty(s.Namespace), () => Settings.ScreenPitWallClassOnly(s.Namespace));
                    this.AttachDelegate(Contract.PitWallFlagFormatProperty(s.Namespace), () => Settings.ScreenPitWallFlagFormat(s.Namespace));
                }
            }
        }

        /// <summary>
        /// The actions a driver binds to a wheel button, which are exactly the ones
        /// Contract.ScreenActionNames lists for the rig's screens: five per face, one per zone and one
        /// held for a glance; the glance alone on a pit wall; and nothing on a companion, which SimHub
        /// pages itself. ScreenActions walks that list and says what each name does, so the list and
        /// the registration cannot disagree, and ScreenActionsTests holds what arrives here.
        ///
        /// Registered through the PluginManager rather than through `this.AddAction`, and that is not
        /// a style choice. The extension method assigns null over the release callback before passing
        /// it on -- `AddAction(actionName, typeof(T), actionStart, actionEnd = null)`, an assignment
        /// and not a default -- so an action registered that way can never be released. It is written
        /// down in docs/research/simhub-dash-format.md. Every action goes through the manager, the ones
        /// that need no release included, so that nobody has to remember which is which.
        ///
        /// The rig's screens and not the catalogue's. An action a rig does not have is a button a
        /// driver may already have assigned, left bound to nothing, which is why this used to register
        /// all eight face sizes whatever the rig was -- but a namespace a user typed cannot be
        /// enumerated ahead of time, so the rig is the only list there is once screens are instances
        /// (ADR 0017). The panel warns before a remove that a button bound to that screen will go
        /// quiet, which is the cost said out loud rather than designed around.
        ///
        /// An action only changes the live page. It does not save: the page a zone is showing is live
        /// state, and Init puts every zone back on the page it opens on.
        /// </summary>
        private void AttachActions(PluginManager pluginManager)
        {
            ScreenActions.Register(() => Settings, (name, press, release) =>
                pluginManager.AddAction(
                    name,
                    typeof(OpenDash),
                    (manager, action) => press(),
                    release == null ? null : (Action<PluginManager, string>)((manager, action) => release())));
        }
    }
}
