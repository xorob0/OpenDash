// PanelMatrixDevices.cs: which of SimHub's matrix devices the flag box profile goes to, decided without SimHub (#363).
//
// The flag box profile used to go into `SerialDashPlugin.Settings.RGBMatrixDriver`, the Arduino RGB matrix and
// nothing else. A matrix built into a wheel or a button plate is a `LedModuleSettings.MatrixDriver` under SimHub's
// Devices plugin with a profile list of its own, so a profile installed into the Arduino's was invisible there: the
// strip bug of #361 one device along (ADR 0013, the 2026-09-18 amendment). Each matrix now names its device, as a
// strip does, in the same id vocabulary (LedBar.ArduinoDevice or LedBar.DeviceId), and the one profile every
// matrix shares goes to every device a matrix names.
//
// MatrixTargets.cs reaches SimHub and is not compiled into OpenDash.Tests. What it decides is here, which is: which
// devices the profile goes to and which of them a press writes, which device a new matrix opens on, which of a
// device's matrices can take the flag box at all, what the device row on the Matrix page says, and how the answers
// from several devices read as the one line the page's title carries.
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenDashPlugin
{
    public static class PanelMatrixDevices
    {
        /// <summary>The row's title on the selected matrix: the strip's word for the same question.</summary>
        public const string DeviceTitle = PanelLights.BarDeviceTitle;

        /// <summary>Said in place of the picker when SimHub has no matrix the flag box can be drawn on, which is a
        /// SimHub without its serial dash plugin and no device with an 8x8.</summary>
        public const string NoDevices = "No SimHub device has an 8x8 matrix. Add your wheel or Arduino in SimHub first.";

        /// <summary>What the device row calls SimHub's Arduino RGB matrix, which has no device name of its own.</summary>
        public const string ArduinoName = "Arduino RGB matrix";

        /// <summary>The kind of matrix the flag box profile is drawn for, as SimHub's `DeviceKind` spells it.</summary>
        public const string FlagBoxKind = "Matrix8x8";

        /// <summary>
        /// Whether a device's matrix can take the flag box profile: an 8x8, since that is what every one of the
        /// profile's frames is drawn for.
        /// </summary>
        /// <remarks>
        /// SimHub's matrix driver also drives 4x4, 5x4 and 5x7 panels and a PSP-shaped one, each holding profiles of
        /// its own kind; an 8x8 profile in one of those lists would be listed and draw nothing that reads. A device
        /// that does not say is taken as SimHub's default, which is the 8x8.
        /// </remarks>
        public static bool Takes(string deviceKind)
        {
            return string.IsNullOrEmpty(deviceKind) || string.Equals(deviceKind, FlagBoxKind, StringComparison.Ordinal);
        }

        /// <summary>
        /// The devices the profile goes to: every device a matrix names, each once, in the matrices' order; and on a
        /// rig with no matrix, <paramref name="fallback"/>, the device a first matrix would open on.
        /// </summary>
        /// <remarks>
        /// One profile for every matrix, because SimHub composes the four contents inside one profile: which of them a
        /// panel shows is the content number set on its device. Two matrices on one device are one install there.
        /// A matrix with no device recorded reads as the Arduino's (<see cref="LedBar.NormaliseDevice"/>), because a
        /// matrix added before this existed was installed there.
        /// </remarks>
        public static IList<string> Wanted(IEnumerable<string> matrixDevices, string fallback)
        {
            var wanted = new List<string>();
            foreach (var device in matrixDevices ?? Enumerable.Empty<string>())
            {
                var id = LedBar.NormaliseDevice(device);
                if (!wanted.Contains(id, StringComparer.Ordinal)) wanted.Add(id);
            }
            if (wanted.Count == 0) wanted.Add(LedBar.NormaliseDevice(fallback));
            return wanted;
        }

        /// <summary>
        /// What a new matrix opens on: the first device's matrix over the Arduino's, and the Arduino's only when it is
        /// the one there is.
        /// </summary>
        /// <remarks>
        /// The Arduino's matrix settings exist on every SimHub, matrix or no matrix, so they are always in the list;
        /// a wheel with an 8x8 in it is in the list only because it has one. Somebody who has wired an Arduino matrix
        /// has done it deliberately and will look, as LedTargets.Preferred reasons for the strips. The row shows the choice
        /// either way.
        ///
        /// A wheel SimHub reports as not connected still wins over the Arduino, and on purpose: the Arduino's entry
        /// is listed as connected whether or not there is an Arduino at all (MatrixTargets.Arduino), so preferring a
        /// connected device would always pick it over a wheel whose rim is off its base while the driver sets the
        /// panel up. SimHub keeps a disconnected wheel's 8x8 in its Devices list because the driver added it there,
        /// and installing into it does not need the wheel attached.
        /// </remarks>
        public static string Preferred(IList<LedDeviceEntry> devices)
        {
            var listed = (devices ?? new LedDeviceEntry[0]).Where(device => device != null).ToList();
            var device = listed.FirstOrDefault(d => !string.Equals(d.Id, LedBar.ArduinoDevice, StringComparison.Ordinal)) ?? listed.FirstOrDefault();
            return device == null ? LedBar.ArduinoDevice : device.Id;
        }

        /// <summary>
        /// The device row on the selected matrix, in the strip row's three shapes (<see cref="PanelLeds.DeviceRow"/>):
        /// why there is nowhere to put it, where it goes when there is one place, and a picker otherwise.
        /// </summary>
        /// <remarks>
        /// A matrix pointed at a device SimHub no longer lists keeps that entry, first and labelled as gone, for the
        /// reason the strip's does: dropping it would re-point the matrix at whatever sorted first.
        ///
        /// The devices SimHub has with a matrix that is not an 8x8 (<paramref name="declined"/>) are named on the
        /// caption, as the strip row names the devices it passed over (<see cref="PanelLights.DeviceRowCaption"/>):
        /// a wheel whose matrix was passed over is the driver this row is for, and otherwise the reason is only in
        /// SimHub's log. Said in the matrix's own fact, an 8x8, not the strip's "no LEDs".
        /// </remarks>
        public static LedDeviceRow DeviceRow(IList<LedDeviceEntry> devices, string current, IList<string> declined = null)
        {
            var listed = (devices ?? new LedDeviceEntry[0]).Where(device => device != null).ToList();
            var passed = declined != null && declined.Count > 0;
            if (listed.Count == 0) return new LedDeviceRow(passed ? Unfit(declined) + SeeLog : NoDevices, null, null, -1);
            var row = PanelLeds.DeviceRow(listed, LedBar.NormaliseDevice(current), null);
            if (!passed) return row;
            return new LedDeviceRow(DeclinedCaption(row, listed, declined), row.Ids, row.Labels, row.Selected);
        }

        /// <summary>What the row says with devices passed over: the caption it had, then the devices and why.</summary>
        private static string DeclinedCaption(LedDeviceRow row, IList<LedDeviceEntry> listed, IList<string> declined)
        {
            // One device that is not connected is already a "which" clause; the two facts are joined, each beside
            // its own device, as PanelLights.OneDeviceCaption joins them for a strip.
            if (!row.HasPicker && listed.Count == 1 && !listed[0].Connected)
            {
                return "Goes to " + listed[0].Name + ", which is not connected, and " + Unfit(declined) + SeeLog;
            }
            if (string.IsNullOrEmpty(row.Caption)) return Unfit(declined) + SeeLog;
            return row.Caption.Trim().TrimEnd('.') + ", not " + PanelLights.NameList(declined) + ", which " + Has(declined) + UnfitFact + SeeLog;
        }

        /// <summary>The devices passed over and what they lack: "Wheel has no 8x8 matrix".</summary>
        private static string Unfit(IList<string> declined)
        {
            return PanelLights.NameList(declined) + " " + Has(declined) + UnfitFact;
        }

        private static string Has(IList<string> declined)
        {
            return declined.Count == 1 ? "has" : "have";
        }

        private const string UnfitFact = " no 8x8 matrix";
        private const string SeeLog = ". See SimHub's log.";

        /// <summary>
        /// The devices a press on the profile's line writes, in <paramref name="plans"/>' order, given what each
        /// device holds now.
        /// </summary>
        /// <remarks>
        /// A press made while any device lacks the profile or holds an older copy (the combined state, <see
        /// cref="Combine"/>, is one <see cref="PanelUpdates.BringsForward"/> writes) writes those devices and only
        /// those: a current copy elsewhere is left alone, since a rewrite could only cost the edits made to it, and
        /// so is a newer one, which a rewrite would put back a version without the warning the single-device press
        /// gives. That is the rule Reinstall everything applies to the strips (PanelUpdates.StripsToWrite).
        /// Only where every device the profile goes to holds a current or newer copy is the press the Matrix page's
        /// Reinstall, drawn with its Replaces or ReplacesNewer warning, and then it writes every one of them.
        /// A device SimHub could not be asked is never written.
        /// </remarks>
        public static IList<string> ToWrite(IList<KeyValuePair<string, FlagBoxPlan>> plans)
        {
            var given = (plans ?? new KeyValuePair<string, FlagBoxPlan>[0]).Where(entry => entry.Value != null).ToList();
            var combined = Combine(given.Select(entry => entry.Value).ToList()).State;
            Func<FlagBoxInstallState, bool> writes;
            if (PanelUpdates.BringsForward(combined)) writes = PanelUpdates.BringsForward;
            else if (combined == FlagBoxInstallState.UpToDate || combined == FlagBoxInstallState.Newer)
                writes = state => state == FlagBoxInstallState.UpToDate || state == FlagBoxInstallState.Newer;
            else return new List<string>();
            return given.Where(entry => writes(entry.Value.State)).Select(entry => entry.Key).ToList();
        }

        /// <summary>
        /// How much a device's answer weighs when several are read as one: the first in this list that any device
        /// gave is the line the page's title carries.
        /// </summary>
        /// <remarks>
        /// A press that failed anywhere is said first, since it is the one thing the driver has to act on. Then what
        /// the press would fix -- a device without the profile, then one with an older copy -- so that the title
        /// offers Install while any matrix device lacks it. Then a device SimHub could not be asked, which is a
        /// matrix pointed at a device that has gone. A newer copy is said only where nothing is missing, and up to
        /// date only where every device is.
        /// </remarks>
        private static readonly FlagBoxInstallState[] Weight =
        {
            FlagBoxInstallState.NotEmbedded,
            FlagBoxInstallState.Failed,
            FlagBoxInstallState.NotInstalled,
            FlagBoxInstallState.Outdated,
            FlagBoxInstallState.Unavailable,
            FlagBoxInstallState.Newer,
            FlagBoxInstallState.UpToDate,
        };

        /// <summary>
        /// The answers of every device the profile goes to, as the one plan the page's title, Home and Updates read.
        /// </summary>
        /// <remarks>
        /// The state is the weightiest any device gave (<see cref="Weight"/>), with that device's installed copy and
        /// version; the note is that device's, else the first any device carried, so a built-in-profiles switch on
        /// one device is not lost behind another device's clean install. No answers at all is unavailable: there was
        /// nowhere to ask.
        /// </remarks>
        public static FlagBoxPlan Combine(IList<FlagBoxPlan> plans)
        {
            var given = (plans ?? new FlagBoxPlan[0]).Where(plan => plan != null).ToList();
            if (given.Count == 0) return new FlagBoxPlan { State = FlagBoxInstallState.Unavailable };
            if (given.Count == 1) return given[0];
            var chosen = given.OrderBy(plan => Rank(plan.State)).First();
            return new FlagBoxPlan
            {
                State = chosen.State,
                Existing = chosen.Existing,
                InstalledVersion = chosen.InstalledVersion,
                EmbeddedVersion = chosen.EmbeddedVersion ?? given.Select(plan => plan.EmbeddedVersion).FirstOrDefault(v => v != null),
                Note = chosen.Note ?? given.Select(plan => plan.Note).FirstOrDefault(n => n != null),
            };
        }

        private static int Rank(FlagBoxInstallState state)
        {
            var at = Array.IndexOf(Weight, state);
            return at < 0 ? Weight.Length : at;
        }
    }
}
