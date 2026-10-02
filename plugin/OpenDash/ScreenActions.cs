// ScreenActions.cs: what the plugin registers with SimHub, and what each of those actions does.
//
// Apart from OpenDash.cs, which holds the PluginManager and so cannot be compiled by the net8.0 test
// project, because the registration is the thing a test has to see. The names come from
// Contract.ScreenActionNames and from nowhere else: this file answers a name, it does not decide which
// names a kind has. Pure: no SimHub types.
using System;

namespace OpenDashPlugin
{
    /// <summary>
    /// Hands one action to whatever registers it: its name, what a press does, and what a release does,
    /// which is null for an action that is pressed and not held.
    /// </summary>
    public delegate void RegisterAction(string name, Action press, Action release);

    public static class ScreenActions
    {
        /// <summary>
        /// Registers every action the rig's screens own, screen by screen in rig order, each screen's
        /// in the order Contract.ScreenActionNames lists them.
        /// </summary>
        /// <remarks>
        /// The list is the contract's and this walks it, rather than deciding per kind a second time.
        /// It used to decide for itself, and the two disagreed: the contract said a companion
        /// registered nothing, and this registered two actions per companion that moved a property no
        /// package read, two dead rows in SimHub's Controls and events (#435). With one list there is
        /// nothing to disagree with.
        ///
        /// The settings are read through a function and not held, both here and in every callback,
        /// because the panel replaces the whole settings object when the user changes something, and a
        /// press has to move the screen the driver is looking at now.
        ///
        /// A name with no answer below throws. The contract grew an action this file does not know how
        /// to perform, and a row in Controls and events that does nothing is what this exists to prevent.
        ///
        /// After the screens, the rig's own three, Contract.RigActionNames: night mode, and the
        /// brightness up and down a step (#791). Unlike a screen's they change a setting rather than a
        /// live page, so each press is followed by <paramref name="persist"/>, which the plugin hands in
        /// as a save; a rig that restarts in the dark should come back dimmed.
        /// </remarks>
        public static void Register(Func<OpenDashSettings> settings, RegisterAction register, Action persist = null)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (register == null) throw new ArgumentNullException(nameof(register));
            var current = settings();
            if (current == null) return;
            foreach (var screen in current.RigScreens())
            {
                if (screen == null) continue;
                foreach (var name in screen.ActionNames())
                {
                    Action press;
                    Action release;
                    Resolve(settings, screen.Namespace, name, out press, out release);
                    register(name, press, release);
                }
            }
            foreach (var name in Contract.RigActionNames())
            {
                register(name, RigPress(settings, name, persist), null);
            }
        }

        /// <summary>What one of the rig's own actions does: the change, then the save.</summary>
        private static Action RigPress(Func<OpenDashSettings> settings, string name, Action persist)
        {
            Action<OpenDashSettings> change;
            if (string.Equals(name, Contract.ToggleNightModeAction, StringComparison.Ordinal)) change = s => s.ToggleNightMode();
            else if (string.Equals(name, Contract.BrightnessUpAction, StringComparison.Ordinal)) change = s => s.StepBrightness(1);
            else if (string.Equals(name, Contract.BrightnessDownAction, StringComparison.Ordinal)) change = s => s.StepBrightness(-1);
            else throw new InvalidOperationException("The contract names a rig action nothing performs: " + name);
            return () =>
            {
                var current = settings();
                if (current == null) return;
                change(current);
                if (persist != null) persist();
            };
        }

        /// <summary>What one of a screen's actions does, found by its name.</summary>
        /// <remarks>
        /// Every callback looks the screen up by namespace when it runs, because an action outlives the
        /// screen it was registered for until SimHub restarts: a press on a removed screen has to do
        /// nothing rather than throw on SimHub's own thread, which the settings' readers already see to.
        ///
        /// The next-module answer is kept although no kind lists it today: it is what a companion
        /// registered before SimHub took its paging, which SimHub's own NextScreen now does.
        /// </remarks>
        private static void Resolve(Func<OpenDashSettings> settings, string ns, string name, out Action press, out Action release)
        {
            foreach (var letter in Contract.FaceZoneLetters)
            {
                if (!string.Equals(name, Contract.CycleZoneAction(ns, letter), StringComparison.Ordinal)) continue;
                var captured = letter;
                press = () => settings().CycleScreenZone(ns, captured);
                release = null;
                return;
            }
            foreach (var letter in Contract.FaceZoneLetters)
            {
                if (!string.Equals(name, Contract.CycleZoneBackAction(ns, letter), StringComparison.Ordinal)) continue;
                var captured = letter;
                press = () => settings().CycleScreenZoneBack(ns, captured);
                release = null;
                return;
            }
            if (string.Equals(name, Contract.HoldQuickGlanceActionFor(ns), StringComparison.Ordinal))
            {
                press = () => settings().BeginScreenGlance(ns);
                release = () => settings().EndScreenGlance(ns);
                return;
            }
            if (string.Equals(name, Contract.NextModuleActionFor(ns), StringComparison.Ordinal))
            {
                press = () => settings().CycleScreenModule(ns);
                release = null;
                return;
            }
            throw new InvalidOperationException("The contract names an action nothing performs: " + name);
        }
    }
}
