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

        /// <summary>Page each pit wall data zone shows. Null on a screen that is not a pit wall.</summary>
        public int[] Zones { get; set; }

        /// <summary>Page the full-width zone of the tower page shows.</summary>
        public int WideZone { get; set; }

        /// <summary>The address the Web view zone shows; empty for none.</summary>
        public string WebViewUrl { get; set; }

        /// <summary>Which modules are in the rotation. Null on a screen that is not a companion.</summary>
        public bool[] Modules { get; set; }

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
            }
            else Face = null;

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
            }
            else
            {
                Zones = null;
                WideZone = 0;
                WebViewUrl = null;
            }

            if (IsCompanion)
            {
                var modules = Contract.DefaultModules();
                if (Modules != null)
                {
                    for (var i = 0; i < modules.Length && i < Modules.Length; i++) modules[i] = Modules[i];
                }
                Modules = modules;
            }
            else Modules = null;
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
                Zones = Zones == null ? null : (int[])Zones.Clone(),
                WideZone = WideZone,
                WebViewUrl = WebViewUrl,
                Modules = Modules == null ? null : (bool[])Modules.Clone(),
            };
        }
    }
}
