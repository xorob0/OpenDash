import type { Metadata } from 'next';
import { CaptureNote } from '../../components/Capture';
import { PageGrid } from '../../components/PageGrid';
import { Section } from '../../components/Section';
import { BAND_D_PAGES, BAR_FIELDS, MODULES, PIT_WALL_ZONE_PAGES, VERSION, ZONE_A_PAGES } from '../../lib/content.generated';
import styles from './page.module.css';

export const metadata: Metadata = {
  title: 'The 21 pages',
  description: 'Lap times, delta, fuel, tyres, radar, relative, leaderboard and 14 more, on every zone and on the companion.',
};

/** Why a page ships off: what the sim does not publish. Replaces the catalogue line for those three. */
const OFF_REASON: Record<string, string> = {
  energy: 'Virtual energy is a Le Mans Ultimate value. iRacing publishes none.',
  damage: 'iRacing publishes no damage values.',
  trackRivals: 'Not a SimHub value. OpenDash does not compute what the sim does not publish.',
};

const names = (list: readonly { name: string }[]): string => list.map((p) => p.name).join(' · ');

export default function Pages() {
  const on = MODULES.filter((m) => m.enabled);
  const off = MODULES.filter((m) => !m.enabled).map((m) => ({ ...m, reason: OFF_REASON[m.id] }));

  return (
    <>
      <Section
        level={1}
        ruled={false}
        id="catalogue"
        label="Pages"
        title="21 pages."
        lede="The same 21 pages serve zone B, zone C, the companion and the pit wall zones. Each is laid out for the box it gets. Captured here on the 850 × 480 companion."
        wide
      >
        <PageGrid pages={on} />
        <div className={styles.note}>
          <CaptureNote served={VERSION} />
        </div>
      </Section>

      <Section id="off" label="Switched off" title={`${off.length} ship switched off.`} lede="They say what is missing rather than drawing zeros. Switch them on for a sim that publishes the data." wide>
        <PageGrid pages={off} />
      </Section>

      <Section id="face" label="The face" title="More pages on the face." lede="Zone A, band D and the bar have catalogues of their own, sized for their shapes." wide>
        <dl className={`rows ${styles.lists}`}>
          <div className={styles.list}>
            <dt className={styles.term}>Zone A, {ZONE_A_PAGES.length} pages</dt>
            <dd className={styles.names}>{names(ZONE_A_PAGES)}</dd>
          </div>
          <div className={styles.list}>
            <dt className={styles.term}>Band D, {BAND_D_PAGES.length} pages</dt>
            <dd className={styles.names}>{names(BAND_D_PAGES)}</dd>
          </div>
          <div className={styles.list}>
            <dt className={styles.term}>The bar, {BAR_FIELDS.length} fields</dt>
            <dd className={styles.names}>{names(BAR_FIELDS)}</dd>
          </div>
          <div className={styles.list}>
            <dt className={styles.term}>Pit wall zones, {PIT_WALL_ZONE_PAGES.length} pages</dt>
            <dd className={styles.names}>{names(PIT_WALL_ZONE_PAGES)}</dd>
          </div>
        </dl>
      </Section>

      <Section id="glance" label="Quick glance" title="Hold a button, see a page." lede="Hold a wheel button to show 1 chosen page in 1 chosen zone. Release, and the zone returns to what it showed." wide />
    </>
  );
}
