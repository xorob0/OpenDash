// ProfileInstall.Core.cs: the lighting install without SimHub. What is taken out of SimHub's profile lists, what is
// added, when SimHub is asked to save, what is counted and what is logged, over the non-generic IList SimHub's
// collections already are. FlagBoxInstaller.cs is the other half: it reaches the drivers, parses the embedded
// profiles and reads one list entry as a profile, which is the only thing here that would need a SimHub type, so it
// is handed in as `read`. Compiled into OpenDash.Tests: no SimHub or WPF types here.
using System;
using System.Collections;
using System.Collections.Generic;

namespace OpenDashPlugin
{
    internal static partial class ProfileInstall
    {
        /// <summary>What a profile list holds, reduced to what the decision needs, or null when it cannot be
        /// read. Null is "could not be reached", which the plan turns into Unavailable; an empty list is
        /// "SimHub has none of ours", which is NotInstalled, and the two must not be confused.</summary>
        internal static List<InstalledProfile> Census(IEnumerable profiles, Func<object, InstalledProfile> read, string what, IInstallLog log)
        {
            if (profiles == null) return null;
            try
            {
                var census = new List<InstalledProfile>();
                foreach (var entry in profiles)
                {
                    var profile = read(entry);
                    if (profile == null) continue;
                    census.Add(profile);
                }
                return census;
            }
            catch (Exception e)
            {
                log.Warn("SimHub's " + what + " profiles could not be read: " + e.Message);
                return null;
            }
        }

        /// <summary>
        /// Removes our copies, adds the embedded ones and asks SimHub to save once, returning a plan per
        /// member in the order given. A null member is one that could not be parsed: it is reported
        /// NotEmbedded and nothing is touched on its behalf.
        /// </summary>
        /// <param name="read">One list entry as a profile, or null when it is not one. Applied to the
        /// embedded members as well as to the entries already in the lists.</param>
        internal static IList<FlagBoxPlan> Install(
            IList profiles, IList available, IReadOnlyList<object> embedded, Action<int> add, Action save, string what,
            Func<object, InstalledProfile> read, IInstallLog log, bool builtInMode = false, string where = null)
        {
            var results = new FlagBoxPlan[embedded.Count];
            var replacedCopy = new bool[embedded.Count];
            var changed = false;
            for (var i = 0; i < embedded.Count; i++)
            {
                var profile = embedded[i] == null ? null : read(embedded[i]);
                if (profile == null)
                {
                    results[i] = new FlagBoxPlan { State = FlagBoxInstallState.NotEmbedded };
                    continue;
                }
                var takenFromProfiles = new List<KeyValuePair<int, object>>();
                var takenFromAvailable = new List<KeyValuePair<int, object>>();
                try
                {
                    // Ours is the one carrying our ProfileId. Anything else in either list is the user's
                    // and is not touched, which is the whole reason this is safer than merging the file.
                    //
                    // Both lists. AvailableProfiles returns Profiles unless the settings filter by game
                    // family or the device has built-in profiles switched on (ProfileSettingsBase.cs:422),
                    // and a device maker's BuiltInLedsProfiles put the LED driver in the second case, so
                    // there the two genuinely part company and removing from only one would leave a
                    // duplicate. Adding goes to Profiles alone -- see ProfileInstall.WhyNotAddProfile --
                    // and BuiltInProfiles is otherwise never touched: those are the maker's, not ours.
                    //
                    // The old copy goes before the new one is added, never after: both carry the same id,
                    // so a removal by id afterwards would take the newcomer too, and the sorted list the
                    // device's dropdown binds to is refreshed inside `add` and would go on showing a copy
                    // removed after it. What is taken is remembered, with where it stood, so that an add
                    // that fails puts it back rather than leaving the device without the profile.
                    Take(profiles, profile.ProfileId, read, takenFromProfiles);
                    if (!ReferenceEquals(available, profiles))
                    {
                        Take(available, profile.ProfileId, read, takenFromAvailable);
                    }

                    add(i);
                    changed = true;
                    // One profile replaced, however many lists it was in: the two are views of one set
                    // of profiles, and a copy in both is still one copy to the driver reading the log.
                    replacedCopy[i] = takenFromProfiles.Count + takenFromAvailable.Count > 0;
                }
                catch (Exception e)
                {
                    log.Error("Installing the " + Name(profile) + " profile into SimHub failed: " + e);
                    results[i] = new FlagBoxPlan { State = FlagBoxInstallState.Failed };
                    PutBack(profiles, embedded[i], takenFromProfiles, what, log);
                    if (!ReferenceEquals(available, profiles)) PutBack(available, embedded[i], takenFromAvailable, what, log);
                }
            }

            // One save for the whole group, and only when something actually changed. A member whose add
            // failed has had its old copy put back where it stood, so every member is in the list either
            // as installed or exactly as it was before the press; and SimHub serialises its own collection
            // in one call, so the file on disk either holds that list or is untouched. There is no state
            // in which it holds half a group.
            if (changed && !Save(save, what, log))
            {
                for (var i = 0; i < results.Length; i++)
                {
                    if (results[i] == null) results[i] = new FlagBoxPlan { State = FlagBoxInstallState.Failed };
                }
                return results;
            }

            // AddProfile re-GUIDs any profile whose id already exists in the target list
            // (ProfileSettingsBase.cs:853-858), and the newcomer is the one it renames. That cannot
            // happen while the removal above works, but if it ever stops working the symptom is silent:
            // our profile becomes unrecognisable and the next install adds a second copy. Reading the
            // list back costs one comparison per member and turns that into a log line.
            var installed = Census(profiles, read, what, log);
            var added = 0;
            var replaced = 0;
            for (var i = 0; i < results.Length; i++)
            {
                if (results[i] != null) continue;
                var profile = read(embedded[i]);
                var plan = FlagBoxInstallPlan.Decide(profile.ProfileId, profile.Description, installed);
                if (plan.State == FlagBoxInstallState.UpToDate)
                {
                    added++;
                    if (replacedCopy[i]) replaced++;
                }
                else if (installed != null)
                {
                    // Only when the list could be read at all. A list that could not be read says
                    // nothing about whether the profile is in it, and Census has already logged why.
                    log.Warn("The " + Name(profile) + " profile was added but cannot be found again by its id;"
                        + " SimHub may have renumbered it because a copy was already present. Check SimHub's "
                        + what + " profile list.");
                }
                results[i] = plan;
            }

            // Installed correctly and still not in the device's list, which is a state rather than a
            // failure: the dropdown is bound to AvailableProfiles, the user has it pointed at the maker's
            // built-in profiles, and no amount of installing puts ours among those. Said once per install
            // and carried on every plan, so the row and the announcement can both say which switch it is.
            if (builtInMode)
            {
                log.Info(what + ": " + FlagBoxInstallPlan.BuiltInModeNote);
                for (var i = 0; i < results.Length; i++)
                {
                    if (results[i] != null) results[i].Note = FlagBoxInstallPlan.BuiltInModeNote;
                }
            }

            if (added > 0)
            {
                // Named, because "into SimHub" is what the reported bug sounded like from the log: there
                // is no one list, and which device took the profile is the only thing worth saying.
                log.Info("Installed " + added + " " + what + " profile(s) into " + (string.IsNullOrEmpty(where) ? "SimHub" : where) + ", " + replaced
                    + " of them replacing a copy already there. Select one on the device to use it:"
                    + " installing adds a profile, it does not switch to one.");
            }
            return results;
        }

        /// <summary>Removes one profile id from both lists and saves once. False when nothing went,
        /// which is also what a profile that was never there gives.</summary>
        internal static bool Uninstall(IList profiles, IList available, Guid id, Action save, string what, Func<object, InstalledProfile> read, IInstallLog log)
        {
            var gone = Remove(profiles, id, read);
            if (!ReferenceEquals(available, profiles)) gone += Remove(available, id, read);
            if (gone == 0) return false;
            if (!Save(save, what, log)) return false;
            log.Info("Removed " + gone + " " + what + " profile(s) from SimHub.");
            return true;
        }

        private static bool Save(Action save, string what, IInstallLog log)
        {
            try
            {
                save();
                return true;
            }
            catch (Exception e)
            {
                log.Error("SimHub could not save its " + what + " settings after installing: " + e);
                return false;
            }
        }

        /// <summary>Drops every copy of one profile id from a collection; returns how many went.</summary>
        private static int Remove(IList list, Guid id, Func<object, InstalledProfile> read)
        {
            var taken = new List<KeyValuePair<int, object>>();
            Take(list, id, read, taken);
            return taken.Count;
        }

        /// <summary>
        /// Drops every copy of one profile id from a collection, recording each entry and the index it stood
        /// at, in order, into <paramref name="taken"/> as it goes -- so a removal that throws part way has
        /// still recorded everything it took.
        /// </summary>
        private static void Take(IList list, Guid id, Func<object, InstalledProfile> read, List<KeyValuePair<int, object>> taken)
        {
            if (list == null) return;
            var mine = new List<KeyValuePair<int, object>>();
            for (var index = 0; index < list.Count; index++)
            {
                var profile = read(list[index]);
                if (profile != null && profile.ProfileId == id) mine.Add(new KeyValuePair<int, object>(index, list[index]));
            }
            // From the end, so each recorded index is still where its entry stands when it is removed.
            for (var k = mine.Count - 1; k >= 0; k--)
            {
                list.RemoveAt(mine[k].Key);
                taken.Insert(0, mine[k]);
            }
        }

        /// <summary>
        /// Undoes one member's half-done install: takes out the newcomer if the add got as far as adding it,
        /// then puts each entry <see cref="Take"/> removed back at the index it stood at. Logged rather than
        /// thrown when it cannot, because the member is already reported Failed and the rest of the group is
        /// still to go.
        /// </summary>
        private static void PutBack(IList list, object newcomer, List<KeyValuePair<int, object>> taken, string what, IInstallLog log)
        {
            if (list == null) return;
            try
            {
                for (var index = list.Count - 1; index >= 0; index--)
                {
                    if (ReferenceEquals(list[index], newcomer)) list.RemoveAt(index);
                }
                // In ascending order of where they stood, so each lands at its old index: everything before it
                // is back already and nothing else in the list has moved.
                foreach (var entry in taken)
                {
                    list.Insert(Math.Min(entry.Key, list.Count), entry.Value);
                }
            }
            catch (Exception e)
            {
                log.Error("The " + what + " profile list could not be put back as it was after a failed install: " + e);
            }
        }

        private static string Name(InstalledProfile profile)
        {
            return string.IsNullOrEmpty(profile.Name) ? "light" : profile.Name;
        }
    }
}
