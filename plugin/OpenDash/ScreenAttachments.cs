// ScreenAttachments.cs: which of the rig's screens the plugin has published to SimHub, so that a screen
// added while SimHub is running is published when it is added rather than at the next start (#636).
//
// Apart from OpenDash.cs for the reason ScreenActions is: OpenDash.cs holds SimHub's PluginManager and
// the net8.0 test project cannot compile it, and which screens still need attaching is the decision a
// test has to see. Pure: no SimHub types.
using System;
using System.Collections.Generic;

namespace OpenDashPlugin
{
    /// <summary>
    /// The screens whose properties and actions are attached, by kind and namespace.
    /// </summary>
    /// <remarks>
    /// Everything a screen publishes is named after its namespace (Contract.ScreenPropertyNames,
    /// Contract.ScreenActionNames), and its package reads nothing else, so a screen nobody attached reads
    /// the literal default every binding carries: the page it opens on, every zone's pages, the flag format,
    /// all of it. Init attached the rig it found and nothing after it did, so a screen added from the panel
    /// took no setting at all until SimHub restarted, although SimHub listed its dashboard and drew it
    /// (#636). It looked intermittent because a screen added under a namespace Init had already attached --
    /// the first screen of a size, removed and added again -- found its names waiting and worked.
    ///
    /// Keyed by kind as well as namespace, because a namespace outlives its screen: a face called Rim
    /// removed and a companion called Rim added share the namespace and none of the names. Case is
    /// ignored because SimHub lowercases every property and action name before it stores one, so two
    /// namespaces differing in case are one set of names to it.
    ///
    /// A removed screen stays marked. Its delegates and actions stay attached until SimHub restarts, and
    /// they look their screen up by namespace on every read, so a screen added back under the same kind
    /// and namespace is served by them and attaching it again would only write the same names twice.
    /// </remarks>
    public sealed class ScreenAttachments
    {
        private readonly HashSet<string> attached = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// The screens of <paramref name="rig"/> not attached yet, in rig order, each marked attached as it is
        /// handed out: the caller attaches every one it is given.
        /// </summary>
        public IList<ScreenInstance> Take(IEnumerable<ScreenInstance> rig)
        {
            var taken = new List<ScreenInstance>();
            if (rig == null) return taken;
            foreach (var screen in rig)
            {
                if (screen == null || string.IsNullOrEmpty(screen.Namespace)) continue;
                if (attached.Add(Key(screen))) taken.Add(screen);
            }
            return taken;
        }

        private static string Key(ScreenInstance screen)
        {
            return screen.Kind + ":" + screen.Namespace;
        }
    }
}
