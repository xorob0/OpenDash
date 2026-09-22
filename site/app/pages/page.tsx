import type { Metadata } from 'next';
import { PageGrid } from '../../components/PageGrid';
import { Section } from '../../components/Section';
import { BAND_D_PAGES, BAR_FIELDS, MODULES, PIT_WALL_ZONE_PAGES, ZONE_A_PAGES } from '../../lib/content.generated';
import styles from './page.module.css';

export const metadata: Metadata = {
  title: 'The 21 pages',
  description: 'Lap times, delta, fuel, tyres, radar, relative, leaderboard and 14 more, on every zone and on the companion.',
};

const names = (list: readonly { name: string }[]): string => list.map((p) => p.name).join(' · ');

export default function Pages() {
  const on = MODULES.filter((m) => m.enabled);

  return (
    <>
      <Section
        level={1}
        ruled={false}
        id="catalogue"
        title="21 pages"
        lede="The same pages serve zone B, zone C, the companion and the pit wall zones, each laid out for the box it gets."
      >
        <PageGrid pages={on} />
        <div className={styles.note}>
          <p className={`prose ${styles.aside}`}>The track page’s outline is what SimHub recorded from the emulator’s lap. On a rig it is the circuit.</p>
        </div>
      </Section>

      <Section id="face" title="More pages on the face" lede="Zone A, band D and the bar have catalogues of their own, sized for their shapes.">
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

      <Section id="glance" title="Quick glance" lede="Hold a wheel button and one zone shows a page you chose. Release it and the zone goes back." />
    </>
  );
}
