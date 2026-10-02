// Widgets.Automation.cs: the automation peers the kit's own controls need, so a name a page gives one of them
// reaches a screen reader (#792).
//
// WPF gives a Button, a ToggleButton or a UserControl a peer of its own, and gives a Border, a Grid or a
// StackPanel none, so AutomationProperties.SetName on a bare panel names nothing UI Automation can see. Three
// of the kit's controls were bare panels: a greyed row's Soon wrapper (a Border), the slider's surface (a Grid)
// and the segmented bar (a Border). Each now creates a peer here, with the control type and pattern a screen
// reader expects of it. The shell owns this file; pages build with it.
using System;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;

namespace OpenDashPlugin
{
    /// <summary>
    /// A Border that UI Automation sees as a named group: what a Soon wraps its greyed row in, so the row's name
    /// is announced and its controls are read as the group's children, and what a page wraps a panel in whose
    /// name a screen reader should hear (a WrapPanel of chips, the card grid): the pages' one way to name a
    /// group, rather than a peer of their own each.
    /// </summary>
    internal sealed class GroupBorder : Border
    {
        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new GroupBorderPeer(this);
        }

        private sealed class GroupBorderPeer : FrameworkElementAutomationPeer
        {
            public GroupBorderPeer(GroupBorder owner) : base(owner) { }

            protected override AutomationControlType GetAutomationControlTypeCore() { return AutomationControlType.Group; }

            protected override string GetClassNameCore() { return "Group"; }

            protected override bool IsControlElementCore() { return true; }

            protected override bool IsContentElementCore() { return true; }
        }
    }

    /// <summary>
    /// A Border that UI Automation sees as a named picture, the artboard's role=img: what the Matrix page frames
    /// its 8x8 preview in, so the frame's name is read as the picture's alt text.
    /// </summary>
    internal sealed class ImageBorder : Border
    {
        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new ImageBorderPeer(this);
        }

        private sealed class ImageBorderPeer : FrameworkElementAutomationPeer
        {
            public ImageBorderPeer(ImageBorder owner) : base(owner) { }

            protected override AutomationControlType GetAutomationControlTypeCore() { return AutomationControlType.Image; }

            protected override string GetClassNameCore() { return "Image"; }

            protected override bool IsControlElementCore() { return true; }

            protected override bool IsContentElementCore() { return true; }
        }
    }

    /// <summary>
    /// The slider's surface: a Grid that UI Automation sees as a slider from 0 to 100, with the RangeValue
    /// pattern, so its name and value are read and a screen reader can set it.
    /// </summary>
    internal sealed class SliderSurface : System.Windows.Controls.Grid
    {
        /// <summary>The value the surface draws, which the kit keeps current.</summary>
        public int Value { get; private set; }

        /// <summary>Sets the value as a key press would: painted and saved. The kit supplies it.</summary>
        public Action<int> Set { get; set; }

        /// <summary>Records a new value and tells a listening screen reader that it moved.</summary>
        public void Report(int value)
        {
            var was = Value;
            Value = value;
            if (was == value || !AutomationPeer.ListenerExists(AutomationEvents.PropertyChanged)) return;
            var peer = UIElementAutomationPeer.FromElement(this) as SliderSurfacePeer;
            if (peer != null) peer.RaisePropertyChangedEvent(RangeValuePatternIdentifiers.ValueProperty, (double)was, (double)value);
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new SliderSurfacePeer(this);
        }

        private sealed class SliderSurfacePeer : FrameworkElementAutomationPeer, IRangeValueProvider
        {
            private readonly SliderSurface surface;

            public SliderSurfacePeer(SliderSurface owner) : base(owner)
            {
                surface = owner;
            }

            protected override AutomationControlType GetAutomationControlTypeCore() { return AutomationControlType.Slider; }

            protected override string GetClassNameCore() { return "Slider"; }

            protected override bool IsControlElementCore() { return true; }

            protected override bool IsContentElementCore() { return true; }

            /// <summary>Its own name, or, where the kit drew the slider with its value beside it, the name the page
            /// set on the row the kit returned, which is the element the page holds.</summary>
            protected override string GetNameCore()
            {
                var name = base.GetNameCore();
                if (!string.IsNullOrEmpty(name)) return name;
                var row = surface.Parent as FrameworkElement;
                return row == null ? name : AutomationProperties.GetName(row);
            }

            public override object GetPattern(PatternInterface patternInterface)
            {
                return patternInterface == PatternInterface.RangeValue ? this : base.GetPattern(patternInterface);
            }

            public double Value { get { return surface.Value; } }

            public double Minimum { get { return 0; } }

            public double Maximum { get { return 100; } }

            public double SmallChange { get { return 5; } }

            public double LargeChange { get { return 10; } }

            public bool IsReadOnly { get { return !surface.IsEnabled || surface.Set == null; } }

            public void SetValue(double value)
            {
                if (!surface.IsEnabled) throw new ElementNotEnabledException();
                if (surface.Set == null) return;
                surface.Set((int)Math.Round(Math.Max(0, Math.Min(100, value))));
            }
        }
    }

    /// <summary>
    /// The segmented bar's peer: a group with the Value pattern, whose value is the chosen option's word, so a
    /// page names the bar itself and a screen reader reads the name and the choice.
    /// </summary>
    internal sealed class SegmentedPeer : FrameworkElementAutomationPeer, IValueProvider
    {
        private readonly Segmented bar;

        public SegmentedPeer(Segmented owner) : base(owner)
        {
            bar = owner;
        }

        protected override AutomationControlType GetAutomationControlTypeCore() { return AutomationControlType.Group; }

        protected override string GetClassNameCore() { return "Segmented"; }

        protected override bool IsControlElementCore() { return true; }

        protected override bool IsContentElementCore() { return true; }

        public override object GetPattern(PatternInterface patternInterface)
        {
            return patternInterface == PatternInterface.Value ? this : base.GetPattern(patternInterface);
        }

        public string Value { get { return bar.SelectedLabel ?? string.Empty; } }

        public bool IsReadOnly { get { return !bar.IsEnabled; } }

        /// <summary>Chooses the offered option whose word or value is <paramref name="value"/>, as a press would.</summary>
        public void SetValue(string value)
        {
            if (!bar.IsEnabled) throw new ElementNotEnabledException();
            bar.SelectByWord(value);
        }
    }
}
