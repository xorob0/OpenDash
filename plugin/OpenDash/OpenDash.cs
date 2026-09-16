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
                Installer.EnsureInstalled(false);
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

        /// <summary>The lights, which no dashboard reads and the lighting profiles do. A profile the user
        /// has not imported costs nothing here: a property nobody reads is one delegate.</summary>
        private void AttachLightsProperties()
        {
            this.AttachDelegate(Contract.LightsBrightness, () => Settings.LightsBrightness);
            this.AttachDelegate(Contract.LightsNightBrightness, () => Settings.LightsNightBrightness);
            this.AttachDelegate(Contract.LightsNightMode, () => Settings.LightsNightMode);
            this.AttachDelegate(Contract.FlagBoxCriticalOnly, () => Settings.FlagBoxCriticalOnly);
            this.AttachDelegate(Contract.FlagBoxGear, () => Settings.FlagBoxGear);
            this.AttachDelegate(Contract.FlagBoxLowFuelLaps, () => Settings.FlagBoxLowFuelLaps);
            // Zero means "not set": the profile then applies its own default, which is per unit, so a
            // driver in Fahrenheit who has never opened this page does not get a Celsius number.
            this.AttachDelegate(Contract.FlagBoxOilTemp, () => Settings.FlagBoxOilTemp == 0 ? (int?)null : Settings.FlagBoxOilTemp);
            this.AttachDelegate(Contract.FlagBoxWaterTemp, () => Settings.FlagBoxWaterTemp == 0 ? (int?)null : Settings.FlagBoxWaterTemp);
            foreach (var matrix in Contract.FlagBoxMatrices)
            {
                var m = matrix;
                this.AttachDelegate(Contract.FlagBoxMatrixProperty(m, "Rest"), () => Settings.MatrixRest(m));
                this.AttachDelegate(Contract.FlagBoxMatrixProperty(m, "Flags"), () => Settings.MatrixFlags(m));
                this.AttachDelegate(Contract.FlagBoxMatrixProperty(m, "Pit"), () => Settings.MatrixPit(m));
                this.AttachDelegate(Contract.FlagBoxMatrixProperty(m, "Spotter"), () => Settings.MatrixSpotter(m));
                this.AttachDelegate(Contract.FlagBoxMatrixProperty(m, "Warnings"), () => Settings.MatrixWarnings(m));
                this.AttachDelegate(Contract.FlagBoxMatrixProperty(m, "Side"), () => Settings.MatrixSide(m));
            }
            // The strips, last, in the order Contract.LightsPropertyNames() declares them. Every
            // generated .ledsprofile reads exactly these two, so a strip with neither attached can only
            // ever draw the defaults its isnull() carries.
            this.AttachDelegate(Contract.LedCentre, () => Settings.LedCentre);
            this.AttachDelegate(Contract.LedRpmStyle, () => Settings.LedRpmStyle);
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
                var on = data != null && data.GameRunning && telemetry != null && Settings.LedRpmStyle == Contract.LedRpmStyleCar;
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
            for (var slot = 1; slot <= Contract.SlotCount; slot++)
            {
                var captured = slot;
                this.AttachDelegate(Contract.SlotProperty(captured), () => Settings.Slot(captured));
            }
            // Shared rather than one face's, because the round faces' rev arc and the companion's
            // speedo read it too, and attached last of the shared group because ShiftLights is one of
            // the names this list has always opened with. XOR-119, XOR-138.
            this.AttachDelegate(Contract.RevBar, () => Settings.RevBarMode());
            foreach (var face in Settings.RigFaces())
            {
                var capturedFace = face;
                foreach (var letter in Contract.FaceZoneLetters)
                {
                    var captured = letter;
                    this.AttachDelegate(Contract.ZonePageProperty(capturedFace, captured), () => Settings.FaceZone(capturedFace, captured));
                    this.AttachDelegate(Contract.ZoneMaskProperty(capturedFace, captured), () => Settings.FaceZoneMask(capturedFace, captured));
                    this.AttachDelegate(Contract.ZoneStartProperty(capturedFace, captured), () => Settings.FaceZoneStart(capturedFace, captured));
                    this.AttachDelegate(Contract.ZoneClassOnlyProperty(capturedFace, captured), () => Settings.FaceZoneIsClassOnly(capturedFace, captured));
                }
                foreach (var slot in Contract.BarSlots)
                {
                    var captured = slot;
                    this.AttachDelegate(Contract.BarFieldProperty(capturedFace, captured), () => Settings.BarField(capturedFace, captured));
                }
                this.AttachDelegate(Contract.QuickGlanceProperty(capturedFace), () => Settings.QuickGlanceOf(capturedFace));
            }
            if (Settings.HasScreen(Contract.CompanionPrefix))
            {
                for (var module = 1; module <= Modules.Count; module++)
                {
                    var captured = module;
                    this.AttachDelegate(Contract.ModuleProperty(captured), () => Settings.Module(captured));
                }
            }
            if (Settings.HasScreen(Contract.PitWallPrefix))
            {
                foreach (var letter in Contract.PitWallZoneLetters)
                {
                    var captured = letter;
                    this.AttachDelegate(Contract.ZoneProperty(captured), () => Settings.Zone(captured));
                }
                this.AttachDelegate(Contract.PitWallWide, () => Settings.WideZone);
                this.AttachDelegate(Contract.WebViewUrl, () => Settings.WebViewUrl);
            }
        }

        /// <summary>
        /// The five actions a driver binds to a wheel button: one per zone, and one held for a glance.
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
            foreach (var face in Contract.FaceSizes)
            {
                var capturedFace = face;
                foreach (var letter in Contract.FaceZoneLetters)
                {
                    var captured = letter;
                    pluginManager.AddAction(Contract.CycleZoneAction(capturedFace, captured), typeof(OpenDash), (manager, name) => Settings.CycleFaceZone(capturedFace, captured), null);
                }
                pluginManager.AddAction(
                    Contract.HoldQuickGlanceActionFor(capturedFace),
                    typeof(OpenDash),
                    (manager, name) => Settings.Face(capturedFace).BeginQuickGlance(),
                    (manager, name) => Settings.Face(capturedFace).EndQuickGlance());
            }
        }
    }
}
