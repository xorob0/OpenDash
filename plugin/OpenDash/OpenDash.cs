// OpenDash.cs: the SimHub plugin. Installs the embedded dashboard on startup, exposes the settings as
// [OpenDash.*] properties and offers the settings panel in SimHub's left menu. It renders nothing and reads
// no telemetry (no IDataPlugin), by design: see docs/decisions/0003-plugin-settings-through-properties.md.
using System;
using System.Linq;
using System.Reflection;
using System.Windows.Controls;
using System.Windows.Media;
using SimHub.Plugins;

namespace OpenDashPlugin
{
    [PluginName("openDash")]
    [PluginAuthor("openDash contributors")]
    public class OpenDash : IPlugin, IWPFSettingsV2
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
        /// Built on first use rather than eagerly, because the record of what openDash wrote into each folder lives
        /// in the settings and the settings are read in Init. The lambda reads Settings each time, so the record
        /// follows the object the panel replaces when a user changes something.
        /// </summary>
        public DashboardInstaller Installer =>
            installer ?? (installer = new DashboardInstaller(new SettingsFolderRecord(() => Settings)));

        public string LeftMenuTitle => "openDash";

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
            Log.Info("openDash plugin " + Version + " starting");
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
            AttachProperties();
            AttachActions(pluginManager);
            Log.Info("Dashboard status: " + Installer.Status);
            // The installer records what it wrote into each folder but never saves; this is the safe moment.
            SaveSettings();
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
                        + " seconds and SimHub is closing anyway; a dashboard may be left as openDash found it.");
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
                        Text = "openDash settings could not be displayed: " + ex.Message,
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

        /// <summary>One delegate per setting. SimHub names them <class name>.<name>, hence OpenDash.ShiftLights.</summary>
        private void AttachProperties()
        {
            this.AttachDelegate(Contract.ShiftLights, () => Settings.ShiftLights);
            this.AttachDelegate(Contract.PositionMode, () => Settings.PositionMode);
            this.AttachDelegate(Contract.DeltaReference, () => Settings.DeltaReference);
            this.AttachDelegate(Contract.SessionProgress, () => Settings.SessionProgress);
            for (var slot = 1; slot <= Contract.SlotCount; slot++)
            {
                var captured = slot;
                this.AttachDelegate(Contract.SlotProperty(captured), () => Settings.Slot(captured));
            }
            // One group per face that ships, so that a rig with two screens configures them apart. A
            // property for a face nobody has installed costs one integer and is never read, which is
            // cheaper than a face that cannot be configured until SimHub is restarted.
            foreach (var face in Contract.FaceSizes)
            {
                var capturedFace = face;
                foreach (var letter in Contract.FaceZoneLetters)
                {
                    var captured = letter;
                    this.AttachDelegate(Contract.ZonePageProperty(capturedFace, captured), () => Settings.FaceZone(capturedFace, captured));
                    this.AttachDelegate(Contract.ZoneMaskProperty(capturedFace, captured), () => Settings.FaceZoneMask(capturedFace, captured));
                    this.AttachDelegate(Contract.ZoneStartProperty(capturedFace, captured), () => Settings.FaceZoneStart(capturedFace, captured));
                }
                foreach (var slot in Contract.BarSlots)
                {
                    var captured = slot;
                    this.AttachDelegate(Contract.BarFieldProperty(capturedFace, captured), () => Settings.BarField(capturedFace, captured));
                }
                this.AttachDelegate(Contract.QuickGlanceProperty(capturedFace), () => Settings.QuickGlanceOf(capturedFace));
            }
            for (var module = 1; module <= Modules.Count; module++)
            {
                var captured = module;
                this.AttachDelegate(Contract.ModuleProperty(captured), () => Settings.Module(captured));
            }
            foreach (var letter in Contract.PitWallZoneLetters)
            {
                var captured = letter;
                this.AttachDelegate(Contract.ZoneProperty(captured), () => Settings.Zone(captured));
            }
            this.AttachDelegate(Contract.PitWallWide, () => Settings.WideZone);
            this.AttachDelegate(Contract.WebViewUrl, () => Settings.WebViewUrl);
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
