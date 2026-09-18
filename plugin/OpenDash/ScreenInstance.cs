// ScreenInstance.cs: one screen the user added -- a name they chose, a kind, a size, the namespace its
// properties carry and the folder it owns under DashTemplates.
//
// A screen used to be a resolution: OpenDashSettings.Faces was keyed by "Face1280x480", so two screens
// of one size were one entry and could not be configured apart. ADR 0017 records why that cannot be
// fixed in the panel -- a package carries its property names as literals and SimHub properties are
// global, so one package folder is one settings namespace -- and decides this shape instead.
//
// The namespace is frozen when the screen is created and a rename never moves it. A property name is a
// public interface under ADR 0003, and a rename that silently re-pointed one would break whatever had
// been bound to it with no diagnostic.
using System;
using System.Collections.Generic;

namespace OpenDashPlugin
{
    /// <summary>One screen on the rig, with whichever settings its kind calls for.</summary>
    public sealed class ScreenInstance
    {
        /// <summary>
        /// What this screen's properties and actions are called: "Face1280x480" for the stock screen at
        /// a size, a slug of the name for any other. Frozen at creation; Rename does not touch it.
        /// </summary>
        public string Namespace { get; set; }

        /// <summary>The user's name for it. A label: it names the card, and the dashboard in SimHub's list.</summary>
        public string Name { get; set; }

        /// <summary>One of Contract.ScreenKinds.</summary>
        public string Kind { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }

        /// <summary>The folder under DashTemplates this screen owns, e.g. "openDash 1280x480" or "openDash Rim".</summary>
        public string Folder { get; set; }

        /// <summary>The embedded package this screen was made from, so it can be written again.</summary>
        public string Package { get; set; }

        /// <summary>The zones, the bar and the glance. Null on a screen that is not a face.</summary>
        public FaceSettings Face { get; set; }

        /// <summary>
        /// How this face draws a flag: "band" or "full". Null on a screen that is not a face.
        /// </summary>
        /// <remarks>
        /// Here rather than on FaceSettings because it is a property of the screen and not of its
        /// zones: it decides what happens to the whole face while a flag is up, and the zones it
        /// covers have no say in it.
        /// </remarks>
        public string FlagFormat { get; set; }

        /// <summary>
        /// When this face shows the lap review: "off", "race" or "all". Null on a screen that is not
        /// a face.
        /// </summary>
        /// <remarks>
        /// Beside FlagFormat and for the same reason, only more so: the review takes the hero for
        /// four seconds at every crossing, so a rig with a display on the desk and a rim in the
        /// driver's hands wants it on the one and certainly not on the other.
        /// </remarks>
        public string LapReview { get; set; }

        /// <summary>
        /// What this face carries at the top: "shift", "rpm" or "off". Null on a screen that is not
        /// a face; null on a face means "whatever the rig says", which is how a settings file written
        /// before the setting was per screen keeps drawing what its owner chose.
        /// </summary>
        /// <remarks>
        /// Beside FlagFormat for the same reason, and it is the one a driver notices first: a wheel
        /// whose rim already carries LEDs across its top wants no rev bar, and the display on the
        /// desk beside it wants one. The rig-wide answer turned both off together.
        /// </remarks>
        public string RevBar { get; set; }

        /// <summary>Page each pit wall data zone shows. Null on a screen that is not a pit wall.</summary>
        public int[] Zones { get; set; }

        /// <summary>Page the full-width zone of the tower page shows.</summary>
        public int WideZone { get; set; }

        /// <summary>The address the Web view zone shows; empty for none.</summary>
        public string WebViewUrl { get; set; }

        /// <summary>Whether this pit wall's board and its zone lists show the player's own class.</summary>
        public bool PitWallClassOnly { get; set; }

        /// <summary>Zone and page a held button shows on this pit wall, released back to where it was,
        /// packed as zone index times a hundred plus the page the way a face's glance is.</summary>
        public int PitWallQuickGlance { get; set; } = Contract.DefaultPitWallQuickGlance;

        /// <summary>Which modules are in the rotation. Null on a screen that is not a companion.</summary>
        public bool[] Modules { get; set; }

        /// <summary>Module this companion is showing, as a page index. Live state, written by a wheel
        /// button, and what the dashboard's widget follows.</summary>
        public int CompanionPage { get; set; }

        /// <summary>Module this companion opens on. A session begins where the driver set it to rather
        /// than where they left it, which is the promise a face zone's start page makes too.</summary>
        public int CompanionStart { get; set; }

        /// <summary>Module a held button shows on this companion, released back to where it was.</summary>
        public int CompanionQuickGlance { get; set; } = Contract.DefaultCompanionQuickGlance;

        private int glanceRestore = -1;

        /// <summary>Which zone a pit wall glance borrowed, so that the release gives back the one it
        /// took. A companion has one page and needs no such thing.</summary>
        private int glanceZone = -1;

        /// <summary>Whether a glance is being held here; a second press while one is does nothing.</summary>
        public bool GlanceHeld { get { return glanceRestore >= 0; } }

        public bool IsFace { get { return string.Equals(Kind, Contract.KindFace, StringComparison.Ordinal); } }
        public bool IsCompanion { get { return string.Equals(Kind, Contract.KindCompanion, StringComparison.Ordinal); } }
        public bool IsPitWall { get { return string.Equals(Kind, Contract.KindPitWall, StringComparison.Ordinal); } }

        /// <summary>
        /// Whether this screen holds the stock package for its size, unmodified.
        /// </summary>
        /// <remarks>
        /// The first screen at a size takes the stock namespace and the stock folder, and its package is
        /// extracted byte for byte as it is embedded. Only a second screen at a size needs the install to
        /// rewrite anything, which is what keeps the common rig producing exactly the files it produced
        /// before ADR 0017.
        /// </remarks>
        public bool IsStock
        {
            get { return Namespace != null && string.Equals(Namespace, StockNamespace, StringComparison.Ordinal); }
        }

        /// <summary>The namespace a screen of this kind and size would take if it were the first of them.</summary>
        public string StockNamespace
        {
            get
            {
                if (IsCompanion) return Contract.CompanionPrefix;
                if (IsPitWall) return Contract.PitWallPrefix;
                // A slots face owns no properties of its own -- it reads the twelve shared Slot ones --
                // so its namespace names nothing and exists only to keep it distinct on the rig. It is
                // taken from the folder rather than the size because the round faces carry no size in
                // their name, and two of them would otherwise be one screen.
                if (string.Equals(Kind, Contract.KindSlots, StringComparison.Ordinal))
                {
                    var from = Contract.Slug(Folder);
                    return "Slots" + (from.Length == 0 ? Width + "x" + Height : from);
                }
                return "Face" + Width + "x" + Height;
            }
        }

        /// <summary>The face shape this screen is, for drawing a plan of it. Null when nothing ships at that size.</summary>
        public Contract.FaceSize? FaceSize
        {
            get
            {
                if (!IsFace) return null;
                foreach (var size in Contract.FaceSizes)
                {
                    if (size.Width == Width && size.Height == Height) return size;
                }
                return null;
            }
        }

        /// <summary>"1280 × 480", for the card and for a default name.</summary>
        public string SizeLabel { get { return Width + " × " + Height; } }

        /// <summary>Every property this screen owns, in attachment order.</summary>
        public IEnumerable<string> PropertyNames()
        {
            return Contract.ScreenPropertyNames(Kind, Namespace);
        }

        /// <summary>Every action this screen registers, in registration order.</summary>
        public IEnumerable<string> ActionNames()
        {
            return Contract.ScreenActionNames(Kind, Namespace);
        }

        /// <summary>
        /// Repairs the instance: a kind that exists, a namespace that is spellable, and exactly the
        /// per-kind state its kind calls for.
        /// </summary>
        /// <remarks>
        /// The per-kind state is created when it is missing and cleared when it belongs to another kind,
        /// so a settings file that carried a face's zones on a pit wall -- which only a hand-edit or a
        /// future version could produce -- does not attach a face's properties to it.
        /// </remarks>
        public void Normalise()
        {
            if (Array.IndexOf(Contract.ScreenKinds, Kind) < 0) Kind = Contract.KindFace;
            if (string.IsNullOrEmpty(Namespace)) Namespace = StockNamespace;
            if (string.IsNullOrEmpty(Name)) Name = SizeLabel;

            if (IsFace)
            {
                if (Face == null) Face = new FaceSettings();
                Face.Normalise();
                FlagFormat = Contract.NormaliseChoice(FlagFormat, Contract.FlagFormats, Contract.DefaultFlagFormat);
                LapReview = Contract.NormaliseChoice(LapReview, Contract.LapReviewModes, Contract.DefaultLapReview);
                // Null is kept rather than defaulted: it is what "this face has not been answered
                // individually" means, and the rig's own answer is what it resolves to.
                if (RevBar != null) RevBar = Contract.NormaliseChoice(RevBar, Contract.RevBarModes, Contract.DefaultRevBar);
            }
            else
            {
                Face = null;
                FlagFormat = null;
                LapReview = null;
                RevBar = null;
            }

            if (IsPitWall)
            {
                // A null Zones array is a pit wall nothing has configured yet -- one just added, or one
                // a settings file names without a state -- so the whole group takes its defaults. Zero
                // is a legal wide page, so WideZone cannot tell "unset" from "the first page" on its
                // own, and a screen added from the panel used to open on the wrong one because of it.
                var fresh = Zones == null;
                var zones = Contract.PitWallDefaultZones();
                if (!fresh)
                {
                    for (var i = 0; i < zones.Length && i < Zones.Length; i++)
                    {
                        zones[i] = Contract.NormaliseZonePage(Zones[i], Contract.PitWallDefaultZonePages[i]);
                    }
                }
                Zones = zones;
                WideZone = fresh ? Contract.DefaultWideZonePage : Contract.NormaliseWideZonePage(WideZone);
                WebViewUrl = fresh ? Contract.DefaultWebViewUrl : Contract.NormaliseUrl(WebViewUrl);
                PitWallQuickGlance = fresh ? Contract.DefaultPitWallQuickGlance : Contract.NormalisePitWallQuickGlance(PitWallQuickGlance);
            }
            else
            {
                Zones = null;
                WideZone = 0;
                WebViewUrl = null;
                PitWallQuickGlance = 0;
                PitWallClassOnly = false;
            }

            if (IsCompanion)
            {
                // A null Modules array is a companion nothing has configured yet, so the whole group
                // takes its defaults. Zero is a legal page, so neither the start nor the glance can
                // tell "unset" from "the first module" on its own; the pit wall's zones are read the
                // same way and for the same reason.
                var fresh = Modules == null;
                var modules = Contract.DefaultModules();
                if (!fresh)
                {
                    for (var i = 0; i < modules.Length && i < Modules.Length; i++) modules[i] = Modules[i];
                }
                Modules = modules;

                CompanionStart = fresh ? Contract.DefaultCompanionStart : Contract.NormalisePage(CompanionStart, OpenDashPlugin.Modules.Count, Contract.DefaultCompanionStart);
                CompanionStart = Contract.FirstEnabledFrom(CompanionStart, ModuleMask(), OpenDashPlugin.Modules.Count);
                CompanionQuickGlance = fresh
                    ? Contract.DefaultCompanionQuickGlance
                    : Contract.NormalisePage(CompanionQuickGlance, OpenDashPlugin.Modules.Count, Contract.DefaultCompanionQuickGlance);
                CompanionPage = fresh ? CompanionStart : Contract.NormalisePage(CompanionPage, OpenDashPlugin.Modules.Count, CompanionStart);
                CompanionPage = Contract.FirstEnabledFrom(CompanionPage, ModuleMask(), OpenDashPlugin.Modules.Count);
            }
            else
            {
                Modules = null;
                CompanionPage = 0;
                CompanionStart = 0;
                CompanionQuickGlance = 0;
            }
        }

        /// <summary>The rotation as a bit mask, bit 0 being module 1, so that a page can be advanced
        /// past what is turned off with the arithmetic a zone's mask already uses.</summary>
        private int ModuleMask()
        {
            var mask = 0;
            if (Modules == null) return (1 << OpenDashPlugin.Modules.Count) - 1;
            for (var i = 0; i < Modules.Length && i < OpenDashPlugin.Modules.Count; i++)
            {
                if (Modules[i]) mask |= 1 << i;
            }
            // A rotation with nothing left on has nothing to draw, so it reads as the whole catalogue
            // rather than leaving the companion on a page its own button can never return to.
            return mask == 0 ? (1 << OpenDashPlugin.Modules.Count) - 1 : mask;
        }

        /// <summary>Puts the companion on the module it opens on, which is what Init does to a face.</summary>
        public void OpenOnStartModule()
        {
            if (!IsCompanion) return;
            CompanionPage = Contract.FirstEnabledFrom(CompanionStart, ModuleMask(), OpenDashPlugin.Modules.Count);
        }

        /// <summary>
        /// Advances to the next module the rotation leaves on, and returns it.
        /// </summary>
        /// <remarks>
        /// Past the modules turned off, because the pane that turns one off is the same pane that
        /// binds this button: a press that landed on a module the driver had switched off would show
        /// a page the companion's own header counts as absent.
        /// </remarks>
        public int CycleModule()
        {
            if (!IsCompanion) return 0;
            var count = OpenDashPlugin.Modules.Count;
            CompanionPage = Contract.FirstEnabledFrom((CompanionPage + 1) % count, ModuleMask(), count);
            return CompanionPage;
        }

        /// <summary>
        /// Shows the glance module, remembering what was there.
        /// </summary>
        /// <remarks>
        /// The module does not have to be one the rotation leaves on, exactly as a face's glance page
        /// does not have to be one its mask enables: a glance is a thing a driver asked for by holding
        /// a button, and the rotation is about what the button steps through.
        /// </remarks>
        public void BeginQuickGlance()
        {
            if (GlanceHeld) return;
            if (IsCompanion)
            {
                glanceRestore = CompanionPage;
                CompanionPage = Contract.NormalisePage(CompanionQuickGlance, OpenDashPlugin.Modules.Count, Contract.DefaultCompanionQuickGlance);
                return;
            }
            if (!IsPitWall || Zones == null) return;
            var glance = Contract.NormalisePitWallQuickGlance(PitWallQuickGlance);
            var zone = Contract.QuickGlanceZone(glance);
            if (zone >= Zones.Length) return;
            glanceZone = zone;
            glanceRestore = Zones[zone];
            Zones[zone] = Contract.QuickGlancePage(glance);
        }

        /// <summary>Puts the screen back where it was. A release with no press does nothing.</summary>
        public void EndQuickGlance()
        {
            if (!GlanceHeld) return;
            if (IsCompanion) CompanionPage = glanceRestore;
            else if (Zones != null && glanceZone >= 0 && glanceZone < Zones.Length) Zones[glanceZone] = glanceRestore;
            glanceZone = -1;
            glanceRestore = -1;
        }

        /// <summary>A copy, for the settings object's own clone.</summary>
        public ScreenInstance Copy()
        {
            return new ScreenInstance
            {
                Namespace = Namespace,
                Name = Name,
                Kind = Kind,
                Width = Width,
                Height = Height,
                Folder = Folder,
                Package = Package,
                Face = Face == null ? null : Face.Clone(),
                FlagFormat = FlagFormat,
                LapReview = LapReview,
                RevBar = RevBar,
                Zones = Zones == null ? null : (int[])Zones.Clone(),
                WideZone = WideZone,
                WebViewUrl = WebViewUrl,
                PitWallQuickGlance = PitWallQuickGlance,
                PitWallClassOnly = PitWallClassOnly,
                Modules = Modules == null ? null : (bool[])Modules.Clone(),
                CompanionPage = CompanionPage,
                CompanionStart = CompanionStart,
                CompanionQuickGlance = CompanionQuickGlance,
            };
        }
    }
}
