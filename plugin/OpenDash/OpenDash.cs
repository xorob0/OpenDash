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

        public DashboardInstaller Installer { get; } = new DashboardInstaller();

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
                Installer.EnsureInstalled(false);
            }
            catch (Exception ex)
            {
                Log.Error("Dashboard installation failed", ex);
            }
            AttachProperties();
            AttachActions(pluginManager);
            Log.Info("Dashboard status: " + Installer.Status);
        }

        public void End(PluginManager pluginManager)
        {
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
            foreach (var letter in Contract.FaceZoneLetters)
            {
                var captured = letter;
                this.AttachDelegate(Contract.ZonePageProperty(captured), () => Settings.FaceZone(captured));
                this.AttachDelegate(Contract.ZoneMaskProperty(captured), () => Settings.FaceZoneMask(captured));
                this.AttachDelegate(Contract.ZoneStartProperty(captured), () => Settings.FaceZoneStart(captured));
            }
            foreach (var slot in Contract.BarSlots)
            {
                var captured = slot;
                this.AttachDelegate(Contract.BarFieldProperty(captured), () => Settings.BarField(captured));
            }
            this.AttachDelegate(Contract.QuickGlance, () => Settings.QuickGlance);
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
            foreach (var letter in Contract.FaceZoneLetters)
            {
                var captured = letter;
                pluginManager.AddAction(Contract.CycleZoneAction(captured), typeof(OpenDash), (manager, name) => Settings.CycleFaceZone(captured), null);
            }
            pluginManager.AddAction(
                Contract.HoldQuickGlanceAction,
                typeof(OpenDash),
                (manager, name) => Settings.BeginQuickGlance(),
                (manager, name) => Settings.EndQuickGlance());
        }
    }
}
