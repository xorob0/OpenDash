// SettingsControl.Live.cs: the panel's one clock, and what is kept in step with the plugin between presses --
// the live card, a page's own ticks, and the update check's answer.
//
// One DispatcherTimer for the whole control, started when the panel is on screen and stopped when it is not:
// the sidebar's live card reads what the plugin last copied out of SimHub's frame, and a page that shows
// something live (Home's "Right now", LEDs' car line) asks for a callback with OnTick. A tick reads what is held
// in memory, SimHub's own settings included (the Settings tick reads GameUnitSettings' four Local*UnitString
// properties, which are auto-properties), and never a device, a profile or the disk; the attention checks run on
// Go and on "Check again" (SettingsControl.Status.cs).
//
// The update check's state is the shell's rather than the Updates page's, because the sidebar's badge and
// Home's attention read it on every page. The Updates page draws it, and hears about an answer through
// OnUpdate while it is showing.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Threading;

namespace OpenDashPlugin
{
    public partial class SettingsControl
    {
        private DispatcherTimer clock;
        private readonly List<Action> ticks = new List<Action>();

        private void StartClock()
        {
            if (clock == null)
            {
                clock = new DispatcherTimer(DispatcherPriority.Background, Dispatcher) { Interval = TimeSpan.FromSeconds(1) };
                clock.Tick += (sender, args) => Tick();
            }
            clock.Start();
        }

        private void StopClock()
        {
            if (clock != null) clock.Stop();
        }

        private void Tick()
        {
            RefreshLive();
            foreach (var tick in ticks.ToList())
            {
                try
                {
                    tick();
                }
                catch (Exception ex)
                {
                    Log.Warn("A page's tick failed: " + ex.Message);
                }
            }
        }

        /// <summary>Asks for a callback every second while the page is showing. Cleared on Go.</summary>
        private void OnTick(Action tick)
        {
            if (tick != null) ticks.Add(tick);
        }

        private void ClearTicks()
        {
            ticks.Clear();
        }

        // --- The update check -------------------------------------------------------------------------

        private UpdateStatus updateStatus = new UpdateStatus();

        /// <summary>Whether the answer on its way was asked for by a press, which it owes a sentence either way.</summary>
        private bool askedManually;

        /// <summary>The plugin's, not the panel's own: the check Init queued and the one this panel asks for are
        /// the same service, so the releases either found are the ones an update applies.</summary>
        private UpdateService Updates => plugin.Updates;

        private readonly List<Action> updateChecking = new List<Action>();
        private readonly List<Action<bool>> updateAnswered = new List<Action<bool>>();

        /// <summary>
        /// Hears the update check while the page is showing: <paramref name="checking"/> when a check starts,
        /// <paramref name="answered"/> with whether it was asked for by a press when its answer lands.
        /// Cleared on Go.
        /// </summary>
        private void OnUpdate(Action checking, Action<bool> answered)
        {
            if (checking != null) updateChecking.Add(checking);
            if (answered != null) updateAnswered.Add(answered);
        }

        private void ClearUpdateHandlers()
        {
            updateChecking.Clear();
            updateAnswered.Clear();
        }

        /// <summary>
        /// Asks, off the UI thread, and shows whatever came back when it does.
        /// </summary>
        /// <remarks>
        /// The plugin does the asking (OpenDash.StartUpdateCheck), because Init asks too and the idle screen's
        /// mark reads the same answer; the panel only says "Checking for updates…" when an answer is actually
        /// coming, and draws it in <see cref="ShowUpdateAnswer"/>. Nothing here blocks: a socket that never
        /// answers would otherwise freeze the settings page.
        /// </remarks>
        /// <returns>Whether an answer is coming.</returns>
        private bool Check(bool manual)
        {
            if (!Settings.CheckForUpdates && !manual) return false;
            // Whether a request will be made is decided before saying so, because saying "Checking for updates…"
            // and then not checking left the panel on that sentence for as long as it was open, and hid an offer
            // it had already found.
            if (!plugin.StartUpdateCheck(manual)) return false;

            askedManually |= manual;
            updateStatus = new UpdateStatus { State = UpdateState.Checking, InstalledVersion = plugin.RigVersion, Manual = manual };
            foreach (var checking in updateChecking.ToList()) checking();
            return true;
        }

        /// <summary>
        /// Takes a check's answer, which arrives on the interface thread and may land on any page, and tells
        /// the sidebar and whichever page is listening.
        /// </summary>
        private void ShowUpdateAnswer(UpdateStatus answer)
        {
            var manual = askedManually;
            askedManually = false;
            // The check Init queued can land while an update is installing, and the line and the buttons are
            // the install's until it finishes; what it says about the rig afterwards is its own to decide. The run
            // is the service's (UpdateService.Applying), whichever panel started it (#606).
            if (Updates.Applying) return;
            if (answer == null)
            {
                // The service declined after all. Whatever was showing before is still the truth.
                if (updateStatus.State == UpdateState.Checking)
                {
                    updateStatus = new UpdateStatus { State = UpdateState.Idle, InstalledVersion = plugin.RigVersion };
                }
            }
            else
            {
                // A press answered by the check Init had already started is still a press, and a person who
                // pressed is owed "you have the newest release" where a background check says nothing.
                updateStatus = manual && !answer.Manual ? answer.AsManual() : answer;
            }
            RefreshAttention();
            RefreshSidebar();
            foreach (var answered in updateAnswered.ToList()) answered(manual);
        }
    }
}
