import type { Metadata } from 'next';
import Link from 'next/link';
import { Capture } from '../../components/Capture';
import { Section } from '../../components/Section';
import { CAPTURES, panelFile } from '../../lib/captures';
import { PANEL_PAGES } from '../../lib/panel';
import { INSTALL, SIMHUB_URL } from '../../lib/site';
import styles from './page.module.css';

export const metadata: Metadata = {
  title: 'The plugin',
  description: 'One page in SimHub with a sidebar of eight: your screens set up on pictures of themselves, your lights, your buttons, and updates that keep what you changed.',
};

/** A panel capture is photographed at whatever size the panel was; the sidecar says, and a page not yet taken gets the sidebar’s proportions. */
function PanelShot({ id, name }: { id: string; name: string }) {
  const file = panelFile(id);
  const entry = CAPTURES.files[file];
  return <Capture file={file} alt={`The ${name} page of the plugin`} width={entry?.width ?? 1400} height={entry?.height ?? 900} caption={name} sizes="(min-width: 62rem) 42rem, 100vw" />;
}

export default function Plugin() {
  return (
    <>
      <Section
        level={1}
        ruled={false}
        id="pages"
        title="One page in SimHub, eight inside it"
        lede={
          <>
            The plugin adds OpenDash to{' '}
            <a href={SIMHUB_URL} className="link" rel="noopener">
              SimHub
            </a>
            ’s left menu. Behind it is a sidebar, one page per thing on the rig, and a search box that finds any setting by name. Nothing has to be set up to work; this is where you change what each screen shows.
          </>
        }
      >
        <ul className={styles.pages}>
          {PANEL_PAGES.map((p) => (
            <li key={p.id} className={styles.pageCard}>
              <PanelShot id={p.id} name={p.name} />
              <p className="prose">{p.body}</p>
            </li>
          ))}
        </ul>
      </Section>

      <Section id="screens" title="A screen is set up on a picture of itself" lede="“Zone C” means nothing until you see where zone C is. The Screens page draws each face to its own proportions, and every part of the drawing opens its settings.">
        <ul className={`rows ${styles.points}`}>
          <li>
            <strong>Live, from SimHub’s own renderer.</strong> The selected screen is drawn by the same engine your display uses, so what you see on the page is what the screen shows.
          </li>
          <li>
            <strong>Press a part to set it.</strong> The bar’s fields, a zone’s pages as a list to tick and drag into order, the rev bar, the flag display, the lap review and the quick glance. Under them, the screen’s SimHub name and folder.
          </li>
          <li>
            <strong>Add asks three questions.</strong> The kind, the size, and the theme where the size has one. Then one restart of SimHub, and the screen is a dashboard of its own to assign to a display.
          </li>
          <li>
            <strong>A themed face has its own rows.</strong> Band D lists the theme’s page after the house ones, and a Porsche face shows where its crest is fetched from, with the address yours to change or clear.
          </li>
          <li>
            <strong>Every screen keeps its own settings.</strong> A face on the wheel and a face beside it are set up apart, and a wheel button is bound to one of them.
          </li>
        </ul>
      </Section>

      <Section id="rig" title="The whole rig at once" lede="The Rig page lays your screens, strips and matrices out as tiles you drag to match the desk. Under them, the Preview chips.">
        <ul className={`rows ${styles.points}`}>
          <li>
            <strong>Paint one moment on everything.</strong> A chip for a flag, the spotter, the pit lane, a warning or the revs puts that moment on every tile, so you can see what a yellow looks like across the rig before one is out.
          </li>
          <li>
            <strong>Home is the short form.</strong> What needs fixing first, then one line per device saying what it shows right now, then brightness and night mode.
          </li>
        </ul>
      </Section>

      <Section id="leds" title="Lights and the flag box" lede="The LEDs page holds your strips, each with its shape, its effects and its colours, and the car data they light from. The Matrix page holds the flag box.">
        <p className="prose">
          <Link href="/lights#plugin" className="link">
            The lights, and how the plugin sets them up
          </Link>
        </p>
      </Section>

      <Section id="updates" title="Updates that keep what you changed" lede="The plugin checks GitHub for a newer release once a day, and replaces itself from the Updates page on the restart it asks for.">
        <ul className={`rows ${styles.points}`}>
          <li>
            <strong>Nothing of yours is overwritten quietly.</strong> A dashboard you edited in Dash Studio is held back; Replace anyway keeps a copy, and Put mine back restores it.
          </li>
          <li>
            <strong>What OpenDash wrote is listed.</strong> One row per dashboard and profile in SimHub, with its version and state, and Reinstall everything under it.
          </li>
          <li>
            <strong>Support is a report to paste.</strong> Versions, devices and the last lines of the log, copied for you. Nothing is sent anywhere.
          </li>
        </ul>
      </Section>

      <Section id="privacy" title="What leaves your machine" lede="An update check to GitHub once a day, which can be switched off. The car light tables when you press Download. A Porsche face’s crest, once. Nothing about you, ever.">
        <p className="prose">
          <Link href={INSTALL.href} className="link">
            Install the plugin
          </Link>
        </p>
      </Section>
    </>
  );
}
