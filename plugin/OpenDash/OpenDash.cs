// OpenDash.cs: the SimHub plugin. Installs the embedded dashboard on startup, exposes the settings as
// [OpenDash.*] properties and offers the settings panel in SimHub's left menu. It renders nothing:
// see docs/decisions/0003-plugin-settings-through-properties.md.
//
// It does read telemetry, for exactly one thing. ADR 0018 reopened ADR 0009 -- "the plugin does not
// compute" -- for the car's own LED bar, because there is no SimHub property to derive it from, no
// expression that could hold an 85-car table and no NCalc clock to flash it with. DataUpdate below
// is the whole of that: three values in, one frame of colours out.
//
// The one other value it reads is SimHub's own, not ours: the best lap of the player's class, which
// SimHub works out every frame and never publishes. DataUpdate copies it out of the finished frame so
// a dashboard need not look it up in the one being built; Contract.ClassBestLap says why.
//
// And it reads the names of things, for the settings panel alone: the game, the car, the track and the
// session, which the Home page prints beside what each device is showing and the LEDs page uses to say
// whether the car is one the tables have measured (#503). Copied out of the frame when one of them
// changes and nothing is computed from them; no dashboard reads them, so they are not properties.
using System;
using System.Collections.Generic;
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
    [PluginDescription(PanelCopy.PluginDescription)]
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

        /// <summary>The screens whose properties and actions are attached; see AttachAddedScreens (#636).</summary>
        private readonly ScreenAttachments attached = new ScreenAttachments();

        /// <summary>What AttachActions registers an action with, kept for the screens added after Init. Null
        /// until Init has registered the rig's.</summary>
        private RegisterAction registerAction;

        /// <summary>
        /// Built on first use rather than eagerly, because the record of what OpenDash wrote into each folder lives
        /// in the settings and the settings are read in Init. The lambdas read Settings each time, so the record and
        /// the rig follow the object the panel replaces when a user changes something.
        /// </summary>
        public DashboardInstaller Installer =>
            installer ?? (installer = new DashboardInstaller(new SettingsFolderRecord(() => Settings)) { Rig = () => Settings.RigScreens() });

        /// <summary>What became of the flag box profile at startup, for the lights page. Null until Init runs.</summary>
        public FlagBoxResult FlagBox { get; private set; }

        /// <summary>
        /// The dashboard folders in DashTemplates when Init had finished writing the rig's own, which are the
        /// templates SimHub loaded: it reads that list once, at startup. A screen whose folder is not in it was
        /// added in this session and waits for a restart (PackageExtractor.WaitsForRestart). Null when the
        /// folder could not be read, which says nothing rather than guess.
        /// </summary>
        public ISet<string> TemplatesAtStart { get; private set; }

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

        /// <summary>Whether a check is in flight now, whose answer <see cref="UpdateChecked"/> will carry.</summary>
        public bool UpdateCheckInFlight => System.Threading.Volatile.Read(ref checking) != 0;

        /// <summary>
        /// Raised on the interface thread after one of the rig's own actions -- night mode, brightness up or
        /// down, pressed on the wheel -- has changed the settings and been saved, so an open panel can show
        /// the state the rig is now in rather than the one it drew.
        /// </summary>
        public event Action RigLightingPressed;

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
            // Every zone starts on the page it is set to open on, which is what that setting means. A
            // companion is held on its start module for a window measured from here rather than from the
            // first frame, because a rig with no game running still loads its dashboards and a companion
            // sitting in the menus should be on its start module too; the force ends by itself.
            Settings.OpenOnStartPages();
            try
            {
                // Before installing, not after: a staging folder left by an interrupted update is a complete
                // extracted dashboard sitting in DashTemplates, and they accumulate one per abandoned update.
                PackageExtractor.RemoveOrphanedStaging(Installer.SimHubRoot, new SimHubInstallLog());
                // And arm the plugin swap again if one is still waiting: the waiter armed when the
                // assembly was staged gives up after a while, and a session that reaches here with a
                // staged assembly is a session where the last swap did not happen. Inert otherwise.
                PluginUpdate.Launch(Installer.SimHubRoot, new SimHubInstallLog());
                // The rig decides what is written, and the installer reads it for itself (DashboardInstaller.Rig).
                // Before ADR 0017 this wrote every package the plugin embeds on every start, so a user who owned
                // one screen found fourteen dashboards in SimHub's list; now a screen exists because somebody
                // added it. Nothing outside the rig is deleted -- that is a thing a user asks for -- it is simply
                // no longer rewritten. The sizes are repaired first, because a screen that remembers no package
                // is matched to one by its kind and its size.
                var log = new SimHubInstallLog();
                RepairScreenSizes(log);
                // A driver who said yes to replacing their edited dashboards said it to the plugin that
                // downloaded this one, and this start is what writes them (#438); see EditedConsent.
                var replaceEdited = EditedConsent.AppliesNow(Settings.ReplaceEditedFor, Version);
                if (replaceEdited) Log.Info("Replacing edited dashboards, as asked when " + Version + " was downloaded");
                Installer.EnsureInstalled(false, replaceEdited);
                if (EditedConsent.Forget(Settings.ReplaceEditedFor, Version, PluginUpdate.Pending(Installer.SimHubRoot))) Settings.ReplaceEditedFor = null;
                RetitleScreens(log);
            }
            catch (Exception ex)
            {
                Log.Error("Dashboard installation failed", ex);
            }
            try
            {
                // After Init's own writes, which are taken as loaded: whether SimHub reads its templates before
                // or after them is not something the plugin can see, and this errs on the side of saying nothing.
                TemplatesAtStart = PackageExtractor.InstalledFolders(Installer.SimHubRoot);
            }
            catch (Exception ex)
            {
                Log.Warn("Could not list the dashboards SimHub loaded at startup: " + ex.Message);
                TemplatesAtStart = null;
            }
            try
            {
                // Extracted, not installed: ADR 0013. The user imports it, and the panel says so.
                // Settings remember what was written, so a file the driver edited is kept (#618); saved below.
                FlagBox = FlagBoxProfile.Extract(Installer.SimHubRoot, typeof(OpenDash).Assembly, new SimHubInstallLog(), Settings);
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
            // The installer records what it wrote into each folder, and spells a stock folder the settings
            // still spell the way they did before #374 as its package does (#467), but never saves; this is
            // the safe moment.
            SaveSettings();
        }

        /// <summary>
        /// Puts every screen's name back into the dashboard SimHub lists, where a folder has lost it.
        /// </summary>
        /// <remarks>
        /// The installer writes each screen's folder, the first of a size and the second alike, and
        /// writes the screen's name in as it does; there is no second writer here any more (#455), which
        /// is what used to leave a second screen of a size on the dashboard it was first written with. What
        /// is left is the repair for a folder somebody else titled: an older plugin, or the update path,
        /// which installs what it downloaded under each package's own title. Cheap where nothing moved:
        /// Retitle compares before it writes and does nothing to a folder whose title is already the screen's.
        /// </remarks>
        private void RetitleScreens(IInstallLog log)
        {
            foreach (var screen in Settings.RigScreens()) ScreenInstaller.Retitle(screen, Installer.SimHubRoot, Installer.Record, log);
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
            // The rig-wide names of what a bar owns, after the runs as the contract declares them. Each
            // answers for a strip nobody added as a bar, which reads these through its own isnull():
            // the lamp at the car's end rather than the whole strip, as bright as the rig, and everything
            // it can draw.
            this.AttachDelegate(Contract.LedSpotterWhole, () => Contract.DefaultLedSpotterWhole);
            this.AttachDelegate(Contract.LedBrightness, () => (int?)null);
            foreach (var setting in Contract.LedEffectSettings())
            {
                this.AttachDelegate(setting, () => Contract.DefaultLedEffect);
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
                // Its own brightness, null while it follows the rig's, and one switch per effect, read
                // through the first effect id each switch answers for. #503.
                this.AttachDelegate(LedBarProfile.Property(ns, Contract.LedBrightness), () => Settings.BarBrightness(ns));
                foreach (var setting in Contract.LedEffectSettings())
                {
                    var effect = Contract.LedEffectPrimaryId(setting);
                    this.AttachDelegate(LedBarProfile.Property(ns, setting), () => Settings.BarEffectEnabled(ns, effect));
                }
            }
        }

        /// <summary>
        /// The best lap of the player's class on the last frame SimHub finished, or null. What
        /// <see cref="Contract.ClassBestLap"/> publishes.
        /// </summary>
        /// <remarks>
        /// Written here, on SimHub's data thread, after the frame is complete and before the next one
        /// starts; read by a dashboard on its own thread whenever it renders. A TimeSpan? is a reference
        /// once boxed by the delegate, so the reader sees one frame's value or the next, never half of
        /// either.
        /// </remarks>
        private volatile object classBestLap;

        /// <summary>
        /// The game, car, track and session SimHub last reported, for the settings panel. Written on the
        /// data thread only when one of them changed, and read on the interface thread as one reference.
        /// </summary>
        private volatile LiveStatus live = LiveStatus.None;

        /// <summary>What the rig is doing right now, as the panel names it. Never null.</summary>
        public LiveStatus Live => live;

        /// <summary>Whether the car in the session is one the fetched tables have measured, which is what
        /// the LEDs page says beside the car's own rev lights. False with no car or no tables.</summary>
        public bool LiveCarHasTable
        {
            get
            {
                var carId = live.CarId;
                return !string.IsNullOrEmpty(carId) && CarLights.For(carId) != null;
            }
        }

        /// <summary>
        /// The game as a person names it ("iRacing", "Assetto Corsa Competizione"), which SimHub's game
        /// manager carries beside the code GameData.GameName holds ("IRacing", "AssettoCorsaCompetizione");
        /// the code when the display name is not there.
        /// </summary>
        private static string GameDisplayName(PluginManager pluginManager, GameData data)
        {
            if (data == null) return null;
            try
            {
                var shown = pluginManager == null || pluginManager.GameManager == null ? null : pluginManager.GameManager.GameDisplayName;
                return string.IsNullOrWhiteSpace(shown) ? data.GameName : shown;
            }
            catch (Exception)
            {
                return data.GameName;
            }
        }

        /// <summary>
        /// One frame of what OpenDash reads from SimHub. Each frame it copies the class best lap out
        /// of the leaderboard, compares the game, car, track and session names the panel shows with
        /// the copy it holds and replaces that copy only when one of them moved, and runs the car's own
        /// bar, the only thing OpenDash computes (ADR 0018).
        ///
        /// <para>It is called at SimHub's data rate, so it does the least it can: with the mirror off
        /// or the sim closed the bar is handed nothing to compute, and the table lookup happens on a
        /// car change rather than per frame. Nothing here may throw -- SimHub calls this from its own
        /// loop and an exception here would be one per frame -- so the whole body is guarded and a
        /// failure leaves the strip on the published ladder.</para>
        /// </summary>
        public void DataUpdate(PluginManager pluginManager, ref GameData data)
        {
            try
            {
                // First and on its own, so that nothing the lights do below can leave it stale. A frame
                // without the game running holds no leaderboard, and neither does the dashboard's field.
                classBestLap = data != null && data.GameRunning && data.NewData != null
                    ? ClassBestLap.Of(data.NewData.BestLapSameClassOpponent?.BestLapTime)
                    : null;

                // What the panel names, compared before anything is built and replaced only when
                // something in it moved, so a frame in which nothing did allocates nothing for the
                // panel's copy and the interface thread is not handed a new object sixty times a second.
                var named = data == null ? null : data.NewData;
                var gameName = GameDisplayName(pluginManager, data);
                var gameRunning = data != null && data.GameRunning;
                var carId = named == null ? null : named.CarId;
                var carModel = named == null ? null : named.CarModel;
                var trackName = named == null ? null : named.TrackName;
                var sessionType = named == null ? null : named.SessionTypeName;
                if (!live.Is(gameName, gameRunning, carId, carModel, trackName, sessionType))
                    live = new LiveStatus(gameName, gameRunning, carId, carModel, trackName, sessionType);

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

        /// <summary>
        /// Applies an update off the interface thread: the run settles the settings itself, the save is posted to
        /// the interface thread, and <paramref name="applied"/> is called on the run's thread for the panel to redraw.
        /// </summary>
        /// <remarks>
        /// The plugin's rather than the panel's, so that nothing the rig is owed waits on a settings page (#613). A
        /// SimHub that closes before the posted save runs saves in End, which waits for the part of the run that
        /// writes and so finds the consent and the screens' names already recorded.
        /// </remarks>
        public void ApplyUpdate(ReleaseInfo release, bool replaceEdited, Action<double> progress, Action<UpdateOutcome> applied)
        {
            Updates.ApplyInBackground(Installer, release, replaceEdited, Settings, progress, outcome =>
            {
                OnInterfaceThread(SaveSettings);
                applied?.Invoke(outcome);
            });
        }

        public void End(PluginManager pluginManager)
        {
            // A download has written nothing, so it is stopped rather than waited for; only a run that has started
            // writing is (UpdateService.Busy). Stopped first, so a download that finishes now cannot start writing
            // after the wait below has decided there was nothing to wait for.
            updates?.StopDownloads();
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
            try
            {
                AttachAddedScreens();
            }
            catch (Exception ex)
            {
                Log.Error("Attaching a screen added in this session failed", ex);
            }
        }

        /// <summary>
        /// Saves after a rig button's press: a normalised copy, not the live settings, so a press while
        /// a quick glance is held on a page its zone's cycle leaves out does not move the glanced zone.
        /// See <see cref="OpenDashSettings.NormalisedCopy"/>.
        /// </summary>
        private void SaveRigPress()
        {
            try
            {
                this.SaveCommonSettings(SettingsKey, Settings.NormalisedCopy());
            }
            catch (Exception ex)
            {
                Log.Error("Saving the settings failed", ex);
            }
            try
            {
                RigLightingPressed?.Invoke();
            }
            catch (Exception ex)
            {
                Log.Error("Showing a wheel press on the panel failed", ex);
            }
        }

        /// <summary>
        /// What the start made of a settings file SimHub could not read: whether it was, and where the file was
        /// set aside. Never null. Home's settings-unreadable issue reads it (#643).
        /// </summary>
        public SettingsRescue Rescue { get; private set; } = SettingsRescue.Read;

        private void LoadSettings()
        {
            // SimHub calls the factory only when neither the file nor any _Backups copy could be read, or there is
            // none; a file that is there with something in it is then one it could not read, and it is set aside
            // here, before Init's first save writes the defaults over it. See SettingsRescue.
            var defaulted = false;
            try
            {
                Settings = this.ReadCommonSettings<OpenDashSettings>(SettingsKey, () => { defaulted = true; return new OpenDashSettings(); });
                if (Settings == null)
                {
                    defaulted = true;
                    Settings = new OpenDashSettings();
                }
            }
            catch (Exception ex)
            {
                Log.Error("Reading the settings failed; using the defaults", ex);
                defaulted = true;
                Settings = new OpenDashSettings();
            }
            if (defaulted)
            {
                try
                {
                    Rescue = SettingsRescue.Inspect(SettingsPath(), true, new SimHubInstallLog());
                }
                catch (Exception ex)
                {
                    Log.Error("Looking at the settings file SimHub could not read failed", ex);
                }
            }
            Settings.Normalise();
        }

        /// <summary>The file ReadCommonSettings reads and SaveCommonSettings writes, named as SimHub names it:
        /// PluginsData\Common\OpenDash.GeneralSettings.json, relative to SimHub's working folder as SimHub reads
        /// it, and made whole here so that the panel can say where the copy is.</summary>
        private string SettingsPath()
        {
            var manager = PluginManager ?? SimHub.Plugins.PluginManager.GetInstance();
            if (manager == null) return null;
            return System.IO.Path.GetFullPath(manager.GetCommonStoragePath(GetType().Name + "." + SettingsKey + ".json"));
        }

        /// <summary>
        /// One delegate per setting. SimHub names them <class name>.<name>, hence OpenDash.ShiftLights.
        /// </summary>
        /// <remarks>
        /// The shared settings first, then one group per screen the rig has, in the order
        /// OpenDashSettings.DeclaredProperties() lists them: a rig and not the catalogue, because eight
        /// faces of twenty-one properties is a hundred and sixty-eight names for a rig of two screens.
        ///
        /// A screen added while SimHub is running is attached when the panel saves it, by
        /// AttachAddedScreens through the same AttachScreenProperties this calls, so the two cannot list
        /// different names. Until #636 nothing did: SimHub listed the new screen's dashboard and drew it,
        /// and every binding it read fell through to its literal default until SimHub restarted.
        /// </remarks>
        private void AttachProperties()
        {
            this.AttachDelegate(Contract.ShiftLights, () => Settings.ShiftLights);
            AttachLightsProperties();
            this.AttachDelegate(Contract.PositionMode, () => Settings.PositionMode);
            this.AttachDelegate(Contract.DeltaReference, () => Settings.DeltaReference);
            this.AttachDelegate(Contract.SessionProgress, () => Settings.SessionProgress);
            // The twelve card slots. They are attached unconditionally and are not deprecated: a round
            // face becomes zones on a ring before 1.0 (#145, #487), and until it does these are the only
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
            // And the class best, filled by DataUpdate from the frame SimHub has finished. Not a setting:
            // published because SimHub keeps it and does not publish it. See Contract.ClassBestLap.
            this.AttachDelegate(Contract.ClassBestLap, () => classBestLap);
            // And the clock format, shared because a driver reads a clock one way on every screen, and
            // every package's idle screen draws the wall clock. #324.
            this.AttachDelegate(Contract.ClockFormat, () => Settings.ClockFormat);
            // And the delta's precision, a choice on the Data tab like the reference it qualifies. #322.
            this.AttachDelegate(Contract.DeltaPrecision, () => Settings.DeltaPrecision);
            // And whether a flag shows in the pit lane, which every surface that draws a flag asks. #503.
            this.AttachDelegate(Contract.FlagsInPitLane, () => Settings.FlagsInPitLane);
            // One group per screen the rig holds, under that screen's own namespace, which is what lets
            // two screens of one size be configured apart (ADR 0017). The screen object is captured
            // rather than looked up per read: the panel replaces the settings object on every change, so
            // a delegate that searched the rig by namespace would be searching a rig that has moved.
            foreach (var screen in attached.Take(Settings.RigScreens())) AttachScreenProperties(screen);
        }

        /// <summary>
        /// One screen's group, under its own namespace: what Init attaches for each screen of the rig and
        /// AttachAddedScreens for each screen added after it (#636).
        /// </summary>
        private void AttachScreenProperties(ScreenInstance s)
        {
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
                // Where each zone's page sits in the order its driver chose, which the zone's header
                // counts; an expression cannot sort, so the plugin says. #503.
                foreach (var letter in Contract.FaceZoneLetters)
                {
                    var captured = letter;
                    this.AttachDelegate(Contract.ZonePositionProperty(s.Namespace, captured), () => Settings.ScreenFace(s.Namespace).Position(captured));
                }
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

        /// <summary>
        /// The actions a driver binds to a wheel button, which are exactly the ones
        /// Contract.ScreenActionNames lists for the rig's screens: nine per face, one per zone each way
        /// and one held for a glance; the glance alone on a pit wall and on a companion, which SimHub
        /// pages itself; and then Contract.RigActionNames, once for the rig. ScreenActions walks those
        /// lists and says what each name does, so the lists and
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
        /// A screen's action only changes the live page. It does not save: the page a zone is showing
        /// is live state, and Init puts every zone back on the page it opens on. The rig's own three --
        /// night mode and the brightness steps (#503) -- change settings, so ScreenActions follows each
        /// of their presses with the save handed in here, queued onto SimHub's interface thread because
        /// a press arrives on whichever thread SimHub reads the button on.
        ///
        /// The registration is kept, so that a screen added later registers its own through the same
        /// call (AttachAddedScreens). SimHub keeps the first action registered under a name and ignores
        /// a second, which is why a screen is registered once, when ScreenAttachments hands it out.
        /// </summary>
        private void AttachActions(PluginManager pluginManager)
        {
            registerAction = (name, press, release) =>
                pluginManager.AddAction(
                    name,
                    typeof(OpenDash),
                    (manager, action) => press(),
                    release == null ? null : (Action<PluginManager, string>)((manager, action) => release()));
            ScreenActions.Register(() => Settings, registerAction, () => OnInterfaceThread(SaveRigPress));
        }

        /// <summary>
        /// Attaches the properties and registers the actions of every screen added since Init, the way Init
        /// did for the rig it found (#636).
        /// </summary>
        /// <remarks>
        /// Called from SaveSettings, which every path that adds a screen ends in -- Add, Duplicate, and
        /// anything later -- so no path has to remember to call it. SimHub reads a property by name every
        /// time a binding asks, retrying a name it has not found, so a dashboard already drawing the screen
        /// picks the names up on its next frames without being reopened; a delegate attached after Init is
        /// a delegate like any other to it. Nothing happens before Init has attached the rig, which is what
        /// a null registration means.
        /// </remarks>
        private void AttachAddedScreens()
        {
            if (registerAction == null) return;
            foreach (var screen in attached.Take(Settings.RigScreens()))
            {
                AttachScreenProperties(screen);
                ScreenActions.RegisterScreen(() => Settings, screen, registerAction);
                Log.Info("Attached the properties and actions of " + screen.Name + " (" + screen.Namespace + "), added in this session");
            }
        }
    }
}
