import type { Metadata } from 'next';
import Link from 'next/link';
import { Capture } from '../../components/Capture';
import { Clip } from '../../components/Clip';
import { Section } from '../../components/Section';
import { SizeList } from '../../components/SizeList';
import { packageFile } from '../../lib/captures';
import { clipFor } from '../../lib/clips';
import { counted } from '../../lib/counts';
import { CAR_THEMES, LARGE_FACE, themeById, themedAt } from '../../lib/faces';
import { COUNTS } from '../../lib/liveCounts';
import { sizeLabel } from '../../lib/packages';
import { FREE_FOREVER, INSTALL, NOTHING_TO_UNLOCK, SCOPE_URL, THEMES_FREE, THEME_TICKETS_URL } from '../../lib/site';
import styles from './page.module.css';

export const metadata: Metadata = {
  title: 'Car themes',
  description: 'A face drawn like your car’s own display, at every size, which SimHub switches to when you drive the car. The Porsche first. Free, like the rest.',
};

/** The sentence under a list of cars: the four the Porsche is drawn for, as iRacing names them. */
const list = (items: readonly string[]): string => (items.length <= 1 ? items.join('') : `${items.slice(0, -1).join(', ')} and ${items[items.length - 1]}`);

export default function Themes() {
  const porsche = themeById('porsche');
  const house = LARGE_FACE;
  const face = porsche && house ? themedAt(porsche, house) : undefined;
  const clip = face ? clipFor(face.folder) : undefined;

  return (
    <>
      <Section
        level={1}
        ruled={false}
        id="porsche"
        title="The Porsche face"
        lede={
          porsche
            ? `Drawn like the 992’s own display, for the ${list(porsche.cars)}. When you drive one of them, SimHub puts this face on the screen and the plain face comes back for every other car.`
            : 'Drawn like the 992’s own display. When you drive one, SimHub puts this face on the screen and the plain face comes back for every other car.'
        }
      >
        <div className={styles.pair}>
          {face ? (
            clip ? (
              <Clip clip={clip} alt={`The Porsche face at ${sizeLabel(face)}, running`} caption={`Porsche ${sizeLabel(face)}`} priority />
            ) : (
              <Capture file={packageFile(face.folder)} alt={`The Porsche face at ${sizeLabel(face)}`} width={face.width} height={face.height} caption={`Porsche ${sizeLabel(face)}`} priority />
            )
          ) : null}
          {house ? <Capture file={packageFile(house.folder)} alt={`The plain face at ${sizeLabel(house)}`} width={house.width} height={house.height} caption={`OpenDash ${sizeLabel(house)}`} /> : null}
        </div>
        <ul className={`rows ${styles.points}`}>
          <li>
            <strong>The car’s shift lights.</strong> Sixteen round dots that light from both ends inwards, three green, three yellow and two red a side, and all sixteen blue and flashing at the shift point.
          </li>
          <li>
            <strong>The car’s top strip.</strong> The session in red at the left, the speed in a grey box over the gear, the lap beside it, and the track state in the teal outline at the right.
          </li>
          <li>
            <strong>The car’s body.</strong> The pages in grey outlines, the gear on a tile of its own, a column of coloured setting boxes down the left and the car’s own telltales down the right.
          </li>
          <li>
            <strong>The car’s limiter.</strong> It takes the whole body: green under the pit lane limit, red over it, with the speed, the limit and the gear still in the middle. A setting you change shows in a blue box, and a tank under ten litres in a red one.
          </li>
          <li>
            <strong>The car’s type.</strong> Barlow at its normal width, labels and values alike. Nothing condensed.
          </li>
          {porsche && porsche.bandPages.length > 0 ? (
            <li>
              <strong>The car’s foot.</strong> One more page on band D, called {list(porsche.bandPages.map((p) => p.name))}: the badge, the traction and ABS boxes, the tyre box and the brake bias. A Porsche face opens on it.
            </li>
          ) : null}
        </ul>
        {porsche && porsche.packages.length > 0 ? (
          <div className={styles.sizes}>
            <h3 className="h3">Every size the plain face comes in</h3>
            <p className="prose">Each one is drawn from the same rules as the 1280 × 480, which has the car’s own 8:3 shape. The round faces are not themed.</p>
            <SizeList packages={porsche.packages} />
          </div>
        ) : null}
      </Section>

      <Section id="what" title="What a theme is" lede="The same dashboard in the car’s register: nothing taken away, and the car’s own way of showing it put on.">
        <ul className={`rows ${styles.points}`}>
          <li>
            <strong>Every page and every field stays.</strong> A theme may change the colours, the type, the chrome and where the parts of the face sit. It may never remove a page or drop a field. That is the line between a theme and a skin.
          </li>
          <li>
            <strong>You choose it when you add a screen.</strong> The plugin’s Add sheet asks the kind, the size, and then the theme where the size has one. A themed screen is a dashboard of its own in SimHub, beside the plain one.
          </li>
          <li>
            <strong>It follows the car.</strong> The plugin writes SimHub’s own per-car playlist, so the display shows the Porsche face when you drive a Porsche and the plain face for everything else. No switch to press.
          </li>
          <li>
            <strong>The crest is yours, not ours.</strong> A manufacturer’s mark cannot ship in an open-source package, so the plugin fetches the crest once, from an address you can change or clear on the Screens page, into your own SimHub folder. Until then the badge is an empty shield. Nothing about which car you drive leaves your machine for it.
          </li>
          <li>
            <strong>Named as a driver says it.</strong> The theme is called Porsche in OpenDash’s own type, never styled as the manufacturer presents it. The{' '}
            <a href={SCOPE_URL} className="link" rel="noopener">
              scope
            </a>{' '}
            says what a theme may take from the car and what it may not.
          </li>
        </ul>
      </Section>

      <Section
        id="follows"
        title="What follows"
        lede={`A theme covers a display family rather than one car: the Porsche’s display is shared by the 992 GT3 R, both Cup cars and the earlier GT3 R, and one theme serves all four. That makes ${counted(COUNTS.themes, 'theme')} so far.`}
      >
        <p className="prose">
          More families are drawn by ticket, one at a time, each researched from the car’s own manual before a line of it is coded. The list is on{' '}
          <a href={THEME_TICKETS_URL} className="link" rel="noopener">
            GitHub
          </a>
          , where a car you drive can be asked for. No dates are promised here.
        </p>
      </Section>

      <Section id="free" title="Free, like the rest" lede={`${FREE_FOREVER} ${THEMES_FREE} ${NOTHING_TO_UNLOCK}`}>
        <p className="prose">
          <Link href={INSTALL.href} className="link">
            Install the plugin
          </Link>{' '}
          and add a Porsche screen from the Screens page.
        </p>
        {CAR_THEMES.length === 0 ? <p className="prose">This build carries no themed package. The themes are built with the plugin and reach SimHub inside it.</p> : null}
      </Section>
    </>
  );
}
