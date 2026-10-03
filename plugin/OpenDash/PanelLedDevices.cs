// PanelLedDevices.cs: SimHub's LED devices, read once for each time the panel asks SimHub what it holds, and handed
// to everything on the page that needs them.
//
// Reading them is a walk of every device in SimHub's Devices plugin on the interface thread (LedTargets.All), and the
// LEDs page used to make three of those to build itself -- the census of the rig's strips, the devices for Home's
// facts, and the strip's own header -- then two more for every strip a press installed, one to take its profile out
// of every device and one to find the device to put it in (#611). Generic over what a device is, so that the test
// project counts the reads without SimHub's types; the panel holds a PanelLedDevices<LedTarget> over LedTargets.All.
using System.Collections.Generic;

namespace OpenDashPlugin
{
    /// <summary>Reads SimHub's LED devices: the ones offered, and the names of the ones seen and not offered.</summary>
    public delegate List<T> LedDeviceReader<T>(out IList<string> declined);

    /// <summary>
    /// SimHub's LED devices as last read, read again only after <see cref="Forget"/>.
    /// </summary>
    /// <remarks>
    /// The panel forgets them each time it asks SimHub again (SettingsControl.RefreshAttention, at every Go, Redraw and
    /// return to the panel), so a page build reads them once and a press on the page uses what the build read. That is
    /// safe for a press: a device's profile list is SimHub's live object, so what an install adds is in the list the
    /// build read, and a device can only be added or removed on SimHub's own Devices page, which the panel is left for
    /// and returned from. The interface thread is the only one that reads them, so nothing here is locked.
    /// </remarks>
    public sealed class PanelLedDevices<T>
    {
        private readonly LedDeviceReader<T> read;
        private List<T> targets;
        private IList<string> declined;

        public PanelLedDevices(LedDeviceReader<T> read)
        {
            this.read = read;
        }

        /// <summary>The devices a profile can be installed into, read now if they have not been since the last
        /// <see cref="Forget"/>. Never null.</summary>
        public List<T> Targets
        {
            get
            {
                Read();
                return targets;
            }
        }

        /// <summary>The devices SimHub has that show some sign of LEDs and are not offered, off the same read. Never null.</summary>
        public IList<string> Declined
        {
            get
            {
                Read();
                return declined;
            }
        }

        /// <summary>Lets the last read go, so the next ask reads SimHub again.</summary>
        public void Forget()
        {
            targets = null;
            declined = null;
        }

        private void Read()
        {
            if (targets != null) return;
            IList<string> seen = null;
            var found = read == null ? null : read(out seen);
            targets = found ?? new List<T>();
            declined = seen ?? new List<string>();
        }
    }
}
