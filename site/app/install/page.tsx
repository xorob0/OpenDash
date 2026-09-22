import type { Metadata } from 'next';
import Link from 'next/link';
import { Reveal } from '../../components/Reveal';
import { SectionHead } from '../../components/SectionHead';
import { SIMHUB_VERSION, VERSION } from '../../lib/content.generated';
import styles from './page.module.css';

export const metadata: Metadata = {
  title: 'Install',
  description:
    'How to put OpenDash into SimHub: the one-file dashboard route, the plugin route, why unblocking the DLL matters, and what to do when SimHub does not mention the plugin at all.',
};

export default function Install() {
  return (
    <>
      <section className={`section ${styles.top}`}>
        <div className="page">
          <SectionHead level="h1" label="Install" title={<>Ten minutes, and one step everybody misses.</>}>
            <p>
              You need <strong>Windows</strong> and <strong>SimHub {SIMHUB_VERSION} or later</strong>
              . There are two routes: one file for a single dashboard, or one file for all of them
              plus the settings page.
            </p>
            <p>
              The step that fails silently is unblocking the DLL. It is the second route’s step 4,
              and if you skip it SimHub either reports a loading error or never mentions OpenDash at
              all.
            </p>
          </SectionHead>
        </div>
      </section>

      {/* ------------------------------------------------------------ route one */}
      <section className={`section ruled ${styles.block}`} id="dashboard">
        <div className="page">
          <SectionHead label="Route one" title={<>Just the dashboard.</>}>
            <p>
              The shortest way to see OpenDash on a display. You get the default layout and the
              default pages, and no settings page — which is enough to decide whether you want the
              rest.
            </p>
          </SectionHead>

          <Reveal>
            <ol className={styles.steps}>
              <li className={styles.step}>
                <p className={`num ${styles.n}`}>1</p>
                <div>
                  <h3 className="h3">Take the file for your screen</h3>
                  <p className="prose">
                    <Link href="/dashes#resolutions" className="link">
                      The table of sizes
                    </Link>{' '}
                    says which. If nothing matches exactly, <strong>850 × 480</strong> is the base
                    size and the one to try first.
                  </p>
                </div>
              </li>
              <li className={styles.step}>
                <p className={`num ${styles.n}`}>2</p>
                <div>
                  <h3 className="h3">Double-click it</h3>
                  <p className="prose">
                    SimHub imports the <code>.simhubdash</code> itself. Nothing else is needed.
                  </p>
                </div>
              </li>
              <li className={styles.step}>
                <p className={`num ${styles.n}`}>3</p>
                <div>
                  <h3 className="h3">Assign it to a display</h3>
                  <p className="prose">
                    From Dash Studio, like any other dashboard: an HDMI DDU, a Vocore or USBD480 USB
                    screen, or a phone or tablet on the network.
                  </p>
                </div>
              </li>
            </ol>
          </Reveal>
        </div>
      </section>

      {/* ------------------------------------------------------------ route two */}
      <section className={`section ruled ${styles.block}`} id="plugin">
        <div className="page">
          <SectionHead label="Route two" title={<>The plugin, and everything with it.</>}>
            <p>
              One download that carries every dashboard inside it, extracts them all, and adds an
              OpenDash page to SimHub’s left menu where the settings live. Copying a file by hand is
              how SimHub loads any third-party plugin.
            </p>
          </SectionHead>

          <Reveal>
            <ol className={styles.steps}>
              <li className={styles.step}>
                <p className={`num ${styles.n}`}>1</p>
                <div>
                  <h3 className="h3">Close SimHub</h3>
                  <p className="prose">
                    Properly closed, not minimised to the tray — the file you are about to replace is
                    one it holds open.
                  </p>
                </div>
              </li>
              <li className={styles.step}>
                <p className={`num ${styles.n}`}>2</p>
                <div>
                  <h3 className="h3">
                    Unzip <code>OpenDash-plugin.zip</code>
                  </h3>
                  <p className="prose">
                    <Link href="/download" className="link">
                      Download {VERSION}
                    </Link>
                    . The dashboards are embedded in the DLL, so this is the only file you need.
                  </p>
                </div>
              </li>
              <li className={styles.step}>
                <p className={`num ${styles.n}`}>3</p>
                <div>
                  <h3 className="h3">
                    Copy <code>OpenDash.dll</code> into SimHub’s install folder
                  </h3>
                  <p className="prose">
                    The folder holding <code>SimHubWPF.exe</code>, usually{' '}
                    <code>C:\Program Files (x86)\SimHub</code>. Not a subfolder of it.
                  </p>
                </div>
              </li>
              <li className={`${styles.step} ${styles.important}`}>
                <p className={`num ${styles.n}`}>4</p>
                <div>
                  <h3 className="h3">Unblock it. This is the step that fails silently.</h3>
                  <p className="prose">
                    Windows marks whatever was downloaded, and .NET then refuses to load the plugin.
                    Tick <strong>Unblock</strong> at the bottom of the file’s Properties, or run this
                    in PowerShell:
                  </p>
                  <pre className={styles.code}>
                    <code>{'Unblock-File "C:\\Program Files (x86)\\SimHub\\OpenDash.dll"'}</code>
                  </pre>
                </div>
              </li>
              <li className={styles.step}>
                <p className={`num ${styles.n}`}>5</p>
                <div>
                  <h3 className="h3">Start SimHub and accept the plugin</h3>
                  <p className="prose">
                    SimHub asks once. The plugin then extracts all the dashboards and adds an
                    <strong> OpenDash</strong> page to the left menu.
                  </p>
                </div>
              </li>
            </ol>
          </Reveal>
        </div>
      </section>

      {/* ------------------------------------------------------------ after */}
      <section className={`section ruled ${styles.block}`}>
        <div className="page">
          <SectionHead label="After" title={<>Making it yours.</>}>
            <p>
              Nothing has to be configured for OpenDash to work — every package carries a default
              layout by itself. The plugin is what makes that layout yours.
            </p>
          </SectionHead>

          <div className={styles.notes}>
            <Reveal className={styles.note}>
              <h3 className="h3">Bind the zone buttons</h3>
              <p className="prose">
                Each zone has a next-page action and a hold-to-glance action, bound in SimHub’s own
                Controls page like any other. That is the whole interface: a driver changes the
                dashboard with a thumb, not with a menu.
              </p>
            </Reveal>

            <Reveal delay={80} className={styles.note}>
              <h3 className="h3">Every screen keeps its own settings</h3>
              <p className="prose">
                A rig with a face on the wheel and another beside it configures them apart. The
                Zones section has a <strong>Screen</strong> picker at the top saying which one you
                are configuring.
              </p>
            </Reveal>

            <Reveal delay={160} className={styles.note}>
              <h3 className="h3">The settings are SimHub properties</h3>
              <p className="prose">
                So your other dashboards and LED profiles can read them too, and every change applies
                to the running dashboard immediately.{' '}
                <code>OpenDash.Face850x480ZoneA</code> is the page zone A of the base face is
                showing.
              </p>
            </Reveal>

            <Reveal delay={240} className={styles.note}>
              <h3 className="h3">If SimHub never mentions the plugin</h3>
              <p className="prose">
                It is step 4 nine times out of ten. Check the DLL is unblocked and that it sits
                beside <code>SimHubWPF.exe</code> rather than in a subfolder, then restart SimHub.
              </p>
            </Reveal>
          </div>
        </div>
      </section>
    </>
  );
}
