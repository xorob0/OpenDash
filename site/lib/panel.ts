/**
 * The plugin's settings panel as the site describes it: the sidebar's eight pages, in the order the
 * sidebar lists them, each with its capture `panel-<id>.png` from `bun run panel-shots`. The ids are
 * the script's `PAGES` (`scripts/panel-shots.ts`), and the words are `docs/design/plugin.md`'s map.
 *
 * No generated import, so `test/captures.test.ts` can read the list from the repository root.
 */
export interface PanelPage {
  id: string;
  name: string;
  /** One sentence: what is on it. */
  body: string;
}

export const PANEL_PAGES: readonly PanelPage[] = [
  { id: 'home', name: 'Home', body: 'What needs fixing, what each device is showing right now, brightness and night mode.' },
  { id: 'rig', name: 'Rig', body: 'Every screen, strip and matrix as a tile laid out like your rig, and the Preview chips that paint a flag, the spotter or the pit lane on all of them at once.' },
  { id: 'screens', name: 'Screens', body: 'One card per screen. The selected one is drawn live by SimHub’s own renderer and set up on a picture of itself.' },
  { id: 'leds', name: 'LEDs', body: 'One card per strip, its shape, effects and colours, and Car Data under Every strip.' },
  { id: 'matrix', name: 'Matrix', body: 'The flag box profile, one card per matrix, and the priority of what it shows.' },
  { id: 'shortcuts', name: 'Shortcuts', body: 'Every action a wheel button can be bound to, in one list: each screen’s zones and quick glance, the lights, the alerts.' },
  { id: 'settings', name: 'Settings', body: 'What is the same on every screen: race data, flags, alerts, lighting, appearance and the driver.' },
  { id: 'updates', name: 'Updates', body: 'The version and the daily check, what OpenDash has written into SimHub, Reinstall everything, Put mine back, and a support report.' },
];
