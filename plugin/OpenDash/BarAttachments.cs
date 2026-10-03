// BarAttachments.cs: which of the rig's strips the plugin has published to SimHub, so that a strip added
// while SimHub is running is published when it is added rather than at the next start (#565).
//
// The strips' twin of ScreenAttachments (#636), apart from OpenDash.cs for the same reason: OpenDash.cs
// holds SimHub's PluginManager and the net8.0 test project cannot compile it, and which strips still need
// attaching is the decision a test has to see. Pure: no SimHub types.
using System;
using System.Collections.Generic;

namespace OpenDashPlugin
{
    /// <summary>
    /// The strips whose own properties are attached, by namespace.
    /// </summary>
    /// <remarks>
    /// The profile installed for a strip reads only that strip's names (LedBarProfile.Properties), so a strip
    /// nobody attached reads the literal default every one of them carries: the centre, the rev style, the
    /// flag animation, the spotter, its brightness and every effect switch. Init attached the strips it
    /// found and nothing after it did, so a strip added from the LEDs page took none of its settings until
    /// SimHub restarted (#565). Removing a strip and adding one of the same name hid it, because the old
    /// delegates look the strip up by namespace and found the new one.
    ///
    /// Keyed by namespace alone, since every strip is the same kind and owns the same names. Case is
    /// ignored because SimHub lowercases every property name before it stores one, and because the rig
    /// hands out strip namespaces ignoring case (OpenDashSettings.FreeBarNamespace).
    ///
    /// A removed strip stays marked. Its delegates stay attached until SimHub restarts and look their
    /// strip up by namespace on every read, so a strip added back under the namespace is served by them,
    /// and attaching it again would only write the same names twice.
    /// </remarks>
    public sealed class BarAttachments
    {
        private readonly HashSet<string> attached = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// The strips of <paramref name="bars"/> not attached yet, in rig order, each marked attached as it is
        /// handed out: the caller attaches every one it is given.
        /// </summary>
        public IList<LedBar> Take(IEnumerable<LedBar> bars)
        {
            var taken = new List<LedBar>();
            if (bars == null) return taken;
            foreach (var bar in bars)
            {
                if (bar == null || string.IsNullOrEmpty(bar.Namespace)) continue;
                if (attached.Add(bar.Namespace)) taken.Add(bar);
            }
            return taken;
        }
    }
}
