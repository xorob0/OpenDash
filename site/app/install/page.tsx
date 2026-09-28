import type { Metadata } from 'next';
import { Actions, Primary } from '../../components/Buttons';
import { Section } from '../../components/Section';
import { Steps } from '../../components/Steps';
import { DOWNLOADS, SIMHUB_VERSION, VERSION } from '../../lib/content.generated';
import { weigh } from '../../lib/packages';
import { NO_OTHER_ROUTE, ONLY_WAY_IN, PLUGIN_ZIP, SCOPE_URL, SIMHUB_URL } from '../../lib/site';
import styles from './page.module.css';

export const metadata: Metadata = {
  title: 'Install OpenDash in SimHub',
  description: 'The plugin installs every dashboard and every LED profile in five steps.',
};

export default function Install() {
  const plugin = DOWNLOADS.find((d) => d.file === PLUGIN_ZIP);

  return (
    <>
      <Section
        level={1}
        ruled={false}
        id="plugin"
        title="Install the plugin"
        lede={
          <>
            One file installs all 14 dashboards and 63 LED profiles, and adds an OpenDash page to SimHub where every setting lives. You need Windows and{' '}
            <a href={SIMHUB_URL} className="link" rel="noopener">
              SimHub
            </a>{' '}
            {SIMHUB_VERSION} or later.
          </>
        }
      >
        <div className={styles.get}>
          {plugin ? (
            <Actions>
              <Primary href={`/downloads/${PLUGIN_ZIP}`} download>
                Download {PLUGIN_ZIP} <span className={`num ${styles.weight}`}>{weigh(plugin.bytes)}</span>
              </Primary>
            </Actions>
          ) : (
            <p className="prose">The plugin zip is not in this build. The download page says why.</p>
          )}
        </div>
        <Steps
          steps={[
            { title: 'Close SimHub.', body: 'Fully closed, not in the tray.' },
            { title: `Unzip ${PLUGIN_ZIP}.`, body: `Version ${VERSION}. It carries every dashboard and every LED profile.` },
            {
              title: 'Copy OpenDash.dll next to SimHubWPF.exe.',
              body: (
                <>
                  Usually <code>C:\Program Files (x86)\SimHub</code>. Not a subfolder.
                </>
              ),
            },
            { title: 'Start SimHub and accept the plugin.', body: 'OpenDash appears in the left menu with four tabs: Rig, Data, Lights and Install.' },
            {
              title: 'Add your screens on the Rig tab, then restart SimHub.',
              body: 'The Rig starts empty. Each screen you add is written into SimHub as its own dashboard, and SimHub lists new dashboards when it starts.',
            },
          ]}
        />
      </Section>

      <Section id="after" title="After installing" lede="Nothing has to be configured to work. The plugin is where you change what each screen shows.">
        <ul className={`rows ${styles.points}`}>
          <li>
            <strong>Assign a dashboard to a display in Dash Studio.</strong> A DDU, a USB screen, or a phone on the network, like any other dashboard.
          </li>
          <li>
            <strong>Set each screen up on the Rig tab.</strong> Choose the pages for each zone and bind the wheel buttons.
          </li>
          <li>
            <strong>Every screen keeps its own settings.</strong> A face on the wheel and a face beside it are set up apart.
          </li>
          <li>
            <strong>Updates.</strong> The plugin checks GitHub once a day, sends nothing about you, and can be switched off. Updating is one click, then restart SimHub.
          </li>
        </ul>
      </Section>

      <Section
        id="companion-paging"
        title="Paging a companion"
        lede="SimHub pages a companion, not OpenDash, so its button is bound on the device and not on the OpenDash page."
      >
        <ul className={`rows ${styles.points}`}>
          <li>
            <strong>By touch.</strong> Tap the left or right half of the screen to change module.
          </li>
          <li>
            <strong>By a wheel button.</strong> Open the device or window the companion runs on in SimHub, go to its Controls and events, and bind{' '}
            <code>NextScreen</code>, with <code>PreviousScreen</code> to go back.
          </li>
          <li>
            <strong>One device at a time.</strong> Those bindings belong to that device, so the button that pages the companion does not page your dash.
          </li>
        </ul>
      </Section>

      <Section id="nothing-showing" title="Nothing showing?" lede="The usual causes, most common first.">
        <ul className={`rows ${styles.points}`}>
          <li>
            <strong>SimHub never mentions the plugin.</strong> Either the DLL is in a subfolder rather than beside <code>SimHubWPF.exe</code>, or Windows
            blocked it on the way in and .NET will not load it. Right-click the file, Properties, tick Unblock. Or in PowerShell:
            <pre className="pre">Unblock-File &quot;C:\Program Files (x86)\SimHub\OpenDash.dll&quot;</pre>
          </li>
          <li>
            <strong>The dashboard shows defaults although you changed them.</strong> The plugin is not enabled. Check Settings, Plugins.
          </li>
          <li>
            <strong>Nothing on the display.</strong> Assign the dashboard in Dash Studio. The plugin installs; SimHub launches.
          </li>
          <li>
            <strong>The track map or radar is empty.</strong> SimHub draws them after one recorded lap.
          </li>
          <li>
            <strong>A value reads <code>--</code>.</strong> iRacing has not published it yet, or never does.
          </li>
          <li>
            <strong>The shift lights look generic.</strong> Press Download on the Lights tab to fetch the car tables.
          </li>
          <li>
            <strong>A wheel button does nothing.</strong> Bind the action of the screen you are looking at. Each screen has its own.
          </li>
          <li>
            <strong>The companion does not change page.</strong> Bind <code>NextScreen</code> in the Controls and events of the device the companion runs on, not your
            dash&apos;s.
          </li>
        </ul>
      </Section>

      <Section id="only-way" title="There is no other way in" lede={ONLY_WAY_IN}>
        <p className="prose">
          {NO_OTHER_ROUTE}{' '}
          <a href={SCOPE_URL} className="link" rel="noopener">
            Read the scope
          </a>
          .
        </p>
      </Section>
    </>
  );
}
