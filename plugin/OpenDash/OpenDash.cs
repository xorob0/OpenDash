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

        /// <summary>Monotonic milliseconds for the over-rev flash, which is the only thing openDash times.</summary>
        private readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();

        /// <summary>The embedded profile as JSON, which the lights page installs into SimHub.</summary>
        public string FlagBoxJson
        {
            get { return FlagBox?.Json; }
        }

        public string LeftMenuTitle => "OpenDash";

        public ImageSource PictureIcon => icon ?? (icon = PluginIcon.Create(this));

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

        public void Init(PluginManager pluginManager)
        {
            Log.Info("OpenDash plugin " + Version + " starting");
            LoadSettings();
            // Every zone starts on the page it is set to open on, which is what that setting means.
            Settings.OpenOnStartPages();
            try
            {
                // Before installing, not after: a staging folder left by an interrupted update is a complete
                // extracted dashboard sitting in DashTemplates, and they accumulate one per abandoned update.
                PackageExtractor.RemoveOrphanedStaging(Installer.SimHubRoot, new SimHubInstallLog());
                // The rig decides what is written. Before ADR 0017 this wrote every package the plugin
                // embeds on every start, so a user who owned one screen found fourteen dashboards in
                // SimHub's list; now a screen exists because somebody added it. Nothing outside the rig
                // is deleted -- that is a thing a user asks for -- it is simply no longer rewritten.
                Installer.Wanted = Settings.RigScreens().Select(screen => screen.Folder).Where(folder => folder != null).ToList();
                Installer.EnsureInstalled(false);
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
                // Off the startup thread: it reads a folder and may fetch. CheckForUpdates is the
                // user's one switch for whether openDash reaches the network at all (ADR 0012), and it
                // covers this too -- with it off, whatever is already on disk is used and nothing is
                // asked for.
                CarLights.LoadInBackground(Settings.CheckForUpdates);
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
        /// </remarks>
        private void WriteScreenFolders()
        {
            var log = new SimHubInstallLog();
            RepairScreenSizes(log);
            foreach (var screen in Settings.RigScreens())
            {
                if (screen.IsStock) continue;
                var result = ScreenInstaller.Write(screen, Installer.PackageSource, Installer.SimHubRoot, Installer.Record, log);
                if (!result.Ok) Log.Warn("The screen " + screen.Name + " has no folder: " + result.Error);
            }
        }

        /// <summary>
        /// Gives a size to any screen that has none.
        /// </summary>
        /// <remarks>
        /// A rig migrated from a settings file written before ADR 0017 takes its sizes from the folder
        /// names, and "openDash Companion", "openDash Pit wall" and the round faces carry none, so those
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
        /// One frame of the car's own bar. The only telemetry openDash reads, and the only thing it
        /// computes (ADR 0018).
        ///
        /// <para>It is called at SimHub's data rate, so it does the least it can: with the mirror off
        /// or the sim closed it sets one field and returns, and the table lookup happens on a car
        /// change rather than per frame. Nothing here may throw -- SimHub calls this from its own loop
        /// and an exception here would be one per frame -- so the whole body is guarded and a failure
        /// leaves the strip on the published ladder.</para>
        /// </summary>
        public void DataUpdate(PluginManager pluginManager, ref GameData data)
        {
            try
            {
                var telemetry = data == null ? null : data.NewData;
                // Any bar asking for the car's own is enough, and so is the rig-wide answer a bar with no
                // opinion falls back to: the mirror is one computation feeding every strip, so gating it
                // on one setting would leave a second bar set to the car's own reading a run nothing fills.
                var on = data != null && data.GameRunning && telemetry != null && Settings.AnyCarLadderWanted();
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
            // Last of all, and after the settings are on disk: a plugin the update staged is put in place
            // by a detached process that waits for this one to exit (PluginUpdate). It does nothing at all
            // unless something is staged, and a swap that cannot happen leaves the plugin as it is.
            PluginUpdate.Launch(Installer.SimHubRoot, new SimHubInstallLog());
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
                }
                else if (s.IsPitWall)
                {
                    foreach (var letter in Contract.PitWallZoneLetters)
                    {
                        var captured = letter;
                        this.AttachDelegate(Contract.ZoneProperty(s.Namespace, captured), () => Settings.ScreenZone(s.Namespace, captured));
                    }
                    this.AttachDelegate(Contract.PitWallWideProperty(s.Namespace), () => Settings.ScreenWideZone(s.Namespace));
                    this.AttachDelegate(Contract.WebViewUrlProperty(s.Namespace), () => Settings.ScreenWebViewUrl(s.Namespace));
                    this.AttachDelegate(Contract.PitWallClassOnlyProperty(s.Namespace), () => Settings.ScreenPitWallClassOnly(s.Namespace));
                }
            }
        }

        /// <summary>
        /// The actions a driver binds to a wheel button: five per face, one per zone and one held for a
        /// glance, and two per companion, the next module and a glance held on the same idiom.
        ///
        /// Registered through the PluginManager rather than through `this.AddAction`, and that is not
        /// a style choice. The extension method assigns null over the release callback before passing
        /// it on -- `AddAction(actionName, typeof(T), actionStart, actionEnd = null)`, an assignment
        /// and not a default -- so an action registered that way can never be released. It is written
        /// down in docs/research/simhub-dash-format.md. All five go through the manager, the four that
        /// need no release included, so that nobody has to remember which is which.
        ///
        /// An action only changes the live page. It does not save: the page a zone is showing is live
        /// state, and Init puts every zone back on the page it opens on.
        /// </summary>
        private void AttachActions(PluginManager pluginManager)
        {
            // The rig's faces and not the catalogue's. An action a rig does not have is a button a
            // driver may already have assigned, left bound to nothing, which is why this used to
            // register all eight sizes whatever the rig was -- but a namespace a user typed cannot be
            // enumerated ahead of time, so the rig is the only list there is once screens are
            // instances (ADR 0017). The panel warns before a remove that a button bound to that screen
            // will go quiet, which is the cost said out loud rather than designed around.
            foreach (var screen in Settings.FaceScreens())
            {
                var ns = screen.Namespace;
                foreach (var letter in Contract.FaceZoneLetters)
                {
                    var captured = letter;
                    pluginManager.AddAction(Contract.CycleZoneAction(ns, captured), typeof(OpenDash), (manager, name) => Settings.CycleScreenZone(ns, captured), null);
                }
                pluginManager.AddAction(
                    Contract.HoldQuickGlanceActionFor(ns),
                    typeof(OpenDash),
                    (manager, name) => Settings.ScreenFace(ns).BeginQuickGlance(),
                    (manager, name) => Settings.ScreenFace(ns).EndQuickGlance());
            }
            // A companion has two of its own: one that advances it past the modules the rotation
            // leaves off, and one held for a glance, which is the same pair a face has under other
            // names. They go through the manager for the same reason the face's do.
            //
            // A pit wall has the glance alone: it cycles nothing, every panel being on screen at once,
            // but the canvas asks for a page called up on demand over a zone's assigned one and the
            // hold is the same gesture under whatever SimHub binds it to.
            foreach (var screen in Settings.RigScreens())
            {
                if (screen == null) continue;
                var ns = screen.Namespace;
                if (screen.IsCompanion)
                {
                    pluginManager.AddAction(Contract.NextModuleActionFor(ns), typeof(OpenDash), (manager, name) => Settings.CycleScreenModule(ns), null);
                }
                else if (!screen.IsPitWall)
                {
                    continue;
                }
                pluginManager.AddAction(
                    Contract.HoldQuickGlanceActionFor(ns),
                    typeof(OpenDash),
                    (manager, name) => Settings.BeginScreenGlance(ns),
                    (manager, name) => Settings.EndScreenGlance(ns));
            }
        }
    }
}
