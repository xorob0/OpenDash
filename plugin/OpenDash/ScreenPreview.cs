// ScreenPreview.cs: the installed dashboard, drawn live in the panel by SimHub's own renderer.
//
// **The pixels are not ours and that is the whole point.** ADR 0020 decided this, and it decided it
// on one fact read out of `plugin/lib/SimHub.Plugins.dll`: `WPFRenderer` is public, it derives from
// `DashHostRendering` which derives from `DashHostBase` which is a `System.Windows.Controls.UserControl`,
// and `EditorModel.LoadFromFile` is public and static. So a settings page can load the same .djson the
// driver's screen loads and hand it to the same renderer the driver's screen uses. There is no second
// renderer, no second expression evaluator and no fidelity check owed, which is exactly what ADR 0008
// refused to acquire and what ADR 0001 has always been about.
//
// What is ours is four lines of arrangement: which file, which screen, how large, and when to stop.
// Three of them are settled here and the fourth, the fit, is in PanelPreview.cs where a test can hold
// it.
//
// Two things are deliberately defensive rather than confident, and both are marked below. SimHub's
// renderer is built to live in a dash window, not in a pane of SimHub's own settings menu, so every
// call into it is wrapped: a preview that throws must cost the user a line of text, never the settings
// page. And the screen selection is the one piece the dash window would otherwise do for us.
//
// Needs SimHub and WPF types, so it is NOT compiled into OpenDash.Tests. What a test can hold is in
// PanelPreview.cs.
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using SimHub.Plugins.OutputPlugins.GraphicalDash;
using SimHub.Plugins.OutputPlugins.GraphicalDash.Windows.Renderers;
using GDashScreen = SimHub.Plugins.OutputPlugins.GraphicalDash.Screen;

namespace OpenDashPlugin
{
    /// <summary>One live dashboard inside the panel: SimHub's renderer, the model it draws, and the
    /// element the pane puts on screen.</summary>
    public sealed class ScreenPreview : IDisposable
    {
        /// <summary>
        /// How often the shown screen is checked against the screens the dashboard says are enabled.
        /// </summary>
        /// <remarks>
        /// Once a second, because what it follows changes when a setting changes rather than when a
        /// frame is drawn: OpenDash's zones are bindings inside one screen, and the screens a package
        /// carries are the rev bar's three arrangements and the idle screen. Nothing here is drawn on
        /// this clock; the renderer draws at its own rate, and this only ever assigns a property.
        /// </remarks>
        private static readonly TimeSpan ScreenPoll = TimeSpan.FromSeconds(1);

        private WPFRenderer renderer;
        private DispatcherTimer timer;
        private bool disposed;

        private ScreenPreview(WPFRenderer renderer, FrameworkElement element)
        {
            this.renderer = renderer;
            Element = element;
        }

        /// <summary>What the pane should put on screen. Never null.</summary>
        public FrameworkElement Element { get; private set; }

        /// <summary>
        /// Loads a dashboard and hands it to SimHub's renderer, sized to fit the room the pane has.
        /// </summary>
        /// <param name="dashboardFile">DashTemplates/&lt;folder&gt;/&lt;folder&gt;.djson.</param>
        /// <param name="contextId">
        /// Names this preview's own screen state so that it cannot be confused with the running
        /// dashboard's. SimHub remembers which screen a dashboard last showed against this id, and a
        /// preview that shared the driver's id would be writing into what their screen opens on.
        /// </param>
        /// <param name="available">The width the pane can give it.</param>
        public static ScreenPreview Load(string dashboardFile, string contextId, double available)
        {
            EditorModel model;
            try
            {
                model = EditorModel.LoadFromFile(dashboardFile, false, contextId);
            }
            catch (Exception error)
            {
                Log.Warn("The preview could not read " + dashboardFile + ": " + error.Message);
                return Failed();
            }

            if (model == null || model.Dashboard == null) return Failed();

            var box = PanelPreview.Fit(
                model.Dashboard.BaseWidth, model.Dashboard.BaseHeight, available, PanelPreview.MaxHeight);
            if (box.Scale <= 0) return Failed();

            WPFRenderer renderer;
            try
            {
                renderer = new WPFRenderer
                {
                    Width = model.Dashboard.BaseWidth,
                    Height = model.Dashboard.BaseHeight,
                };
                renderer.SetCurrentModel(model, true);
            }
            catch (Exception error)
            {
                Log.Warn("SimHub's renderer would not take " + dashboardFile + ": " + error.Message);
                return Failed();
            }

            // Uniform and downwards only: PanelPreview.Fit never returns a scale above 1, so a screen
            // smaller than the pane is drawn at the pixels the driver's screen draws it at.
            var scaled = new Viewbox
            {
                Width = box.Width,
                Height = box.Height,
                Stretch = Stretch.Uniform,
                StretchDirection = StretchDirection.DownOnly,
                Child = renderer,
                HorizontalAlignment = HorizontalAlignment.Left,
            };

            var frame = new Border
            {
                BorderBrush = Ui.Brush(Theme.Rule),
                BorderThickness = new Thickness(1),
                Background = Ui.Brush(Theme.SurfaceInset),
                HorizontalAlignment = HorizontalAlignment.Left,
                Child = scaled,
            };

            var preview = new ScreenPreview(renderer, frame);
            preview.Start(model);
            return preview;
        }

        private static ScreenPreview Failed()
        {
            return new ScreenPreview(null, Ui.Caption(PanelPreview.FailedLine));
        }

        private void Start(EditorModel model)
        {
            Element.Unloaded += (sender, args) => Dispose();

            // InitialPlacementDone is what a dash window calls once it has put the renderer where it is
            // going to live. Nothing documents whether a renderer that is never told still draws, so it
            // is called when WPF has finished loading the element, which is the same moment.
            Element.Loaded += (sender, args) =>
            {
                try
                {
                    if (renderer != null) renderer.InitialPlacementDone();
                }
                catch (Exception error)
                {
                    Log.Warn("The preview could not be placed: " + error.Message);
                }
            };

            // The one piece of the dash window's job that is done here rather than by SimHub. A window
            // decides which screen is showing; a renderer on its own is handed one. So the screen the
            // dashboard says is enabled is followed, and only where SimHub has not already chosen.
            //
            // TODO: #399 -- on the VM, establish whether SetCurrentModel leaves ShownScreen filled. If it
            // does, this follower is dead weight and should go, since the whole value of this file is
            // that SimHub decides and we do not.
            FollowEnabledScreen(model);
            timer = new DispatcherTimer { Interval = ScreenPoll };
            timer.Tick += (sender, args) => FollowEnabledScreen(model);
            timer.Start();
        }

        private void FollowEnabledScreen(EditorModel model)
        {
            if (disposed || renderer == null) return;
            try
            {
                var wanted = FirstEnabled(model);
                if (wanted != null && !ReferenceEquals(renderer.ShownScreen, wanted)) renderer.ShownScreen = wanted;
            }
            catch (Exception error)
            {
                Log.Warn("The preview could not choose a screen: " + error.Message);
                Stop();
            }
        }

        private static GDashScreen FirstEnabled(EditorModel model)
        {
            var dashboard = model == null ? null : model.Dashboard;
            if (dashboard == null) return null;
            var available = dashboard.AvailableScreens;
            var enabled = available == null ? null : available.FirstOrDefault();
            if (enabled != null) return enabled;
            return dashboard.Screens == null ? null : dashboard.Screens.FirstOrDefault();
        }

        private void Stop()
        {
            if (timer == null) return;
            timer.Stop();
            timer = null;
        }

        /// <summary>Stops the preview and lets SimHub's renderer go. Called when the tab is left, when
        /// the pane is rebuilt and when WPF unloads the element, so it has to tolerate being called more
        /// than once.</summary>
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Stop();

            var going = renderer;
            renderer = null;
            if (going == null) return;
            try
            {
                going.Dispose();
            }
            catch (Exception error)
            {
                Log.Warn("The preview did not close cleanly: " + error.Message);
            }
        }
    }
}
