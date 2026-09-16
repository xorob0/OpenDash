import type { Metadata } from 'next';
import Link from 'next/link';
import { Reveal } from '../../components/Reveal';
import { SectionHead } from '../../components/SectionHead';
import { Shot } from '../../components/Shot';
import { PACKAGES } from '../../lib/content.generated';
import { FACES, NOTES, sizeLabel, shotFor } from '../../lib/packages';
import styles from './page.module.css';

export const metadata: Metadata = {
  title: 'Every dash, and the screen it fits',
  description:
    'Ten openDash faces from 1920 by 480 down to a 480 px round DDU, photographed through SimHub’s own renderer, with the table of which one to take for your display.',
};

export default function Dashes() {
  return (
    <>
      <section className={`section ${styles.top}`}>
        <div className="page">
          <SectionHead level="h1" label="The faces" title={<>Ten sizes. One anatomy.</>}>
            <p>
              A face is the same five parts every time: the rev bar, the bar of settled values, the
              three zones across the body, and the band at the foot. What changes with the screen is
              not how big those parts are drawn but how much each one holds.
            </p>
            <p>
              Every picture below is the package itself, photographed through SimHub’s own renderer
              at its own size, on a green-flag lap at Spa lying third of twenty-four.
            </p>
          </SectionHead>
        </div>
      </section>

      {/* ------------------------------------------------------------ the table */}
      <section className={`section ruled ${styles.block}`} id="resolutions">
        <div className="page">
          <SectionHead label="Supported resolutions" title={<>Take the nearest shape, not the biggest number.</>}>
            <p>
              SimHub will scale whichever face you assign to whatever display you assign it to, so a
              mismatched size is not broken — only drawn at the wrong proportions and the wrong
              density. If nothing matches exactly, <strong>850 × 480 is the base size</strong> and
              the one to try first; <strong>1280 × 480 is the large one</strong>.
            </p>
          </SectionHead>

          <Reveal>
            <div className={styles.tableWrap}>
              <table className={styles.table}>
                <caption className="label">
                  Every package the plugin installs, and what each is for
                </caption>
                <thead>
                  <tr>
                    <th scope="col">Dashboard</th>
                    <th scope="col">Screen</th>
                    <th scope="col">Kind</th>
                    <th scope="col">What it is</th>
                  </tr>
                </thead>
                <tbody>
                  {PACKAGES.map((p) => {
                    const note = NOTES[p.folder];
                    return (
                      <tr key={p.folder} data-emphasis={note?.emphasis}>
                        <th scope="row">{p.folder}</th>
                        <td className="num">{sizeLabel(p)}</td>
                        <td className={styles.kind}>
                          {p.kind === 'dash' ? 'Face' : p.kind === 'companion' ? 'Companion' : 'Pit wall'}
                        </td>
                        <td className={styles.what}>
                          {note?.what}
                          {note?.emphasis === 'base' ? <em className={styles.tag}> — the base size</em> : null}
                          {note?.emphasis === 'large' ? <em className={styles.tag}> — the large size</em> : null}
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          </Reveal>

          <Reveal delay={60}>
            <p className={styles.after}>
              The two round faces are still the twelve-slot design of 0.1.x: what a round face should
              do with zones is not decided, so they were left as they were rather than changed badly.{' '}
              <Link href="/install" className="link">
                How to install any of them
              </Link>
              .
            </p>
          </Reveal>
        </div>
      </section>

      {/* ------------------------------------------------------------ the gallery */}
      <section className={`section ruled ${styles.block}`}>
        <div className="page">
          <SectionHead label="Photographed" title={<>Each one, at its own size.</>}>
            <p>
              The pictures are not scaled to a common width. A face drawn at 850 × 480 is a smaller
              picture here than one drawn at 1920 × 480, because it is a smaller screen — which is
              the whole thing the table above is asking you to judge.
            </p>
          </SectionHead>

          <div className={styles.gallery}>
            {FACES.map((p, i) => (
              <Reveal key={p.folder} delay={Math.min(i, 4) * 60} className={styles.item}>
                <div className={styles.itemHead}>
                  <h2 className={`h2 ${styles.itemName}`}>{sizeLabel(p)}</h2>
                  <p className={styles.itemNote}>{NOTES[p.folder]?.what}</p>
                </div>
                <Shot
                  src={shotFor(p.folder)}
                  alt={`The openDash face at ${sizeLabel(p)}, rendering live telemetry in SimHub`}
                  width={p.width}
                  height={p.height}
                  round={p.round}
                  caption={p.folder}
                  sizes="(min-width: 88rem) 84rem, 100vw"
                />
              </Reveal>
            ))}
          </div>
        </div>
      </section>
    </>
  );
}
