import type { Metadata } from 'next';
import Link from 'next/link';
import { Actions, Primary, Secondary } from '../../components/Buttons';
import { Section } from '../../components/Section';
import { Steps } from '../../components/Steps';
import { SIMHUB_VERSION, VERSION } from '../../lib/content.generated';
import { SIMHUB_URL } from '../../lib/site';
import styles from './page.module.css';

export const metadata: Metadata = {
  title: 'Install OpenDash in SimHub',
  description: '2 routes, 5 steps, and the unblock step everyone misses.',
};

export default function Install() {
  return (
    <>
      <Section
        level={1}
        ruled={false}
        id="dashboard"
        label="Install"
        title="5 steps, and 1 that everyone misses."
        lede={
          <>
            Windows and{' '}
            <a href={SIMHUB_URL} className="link" rel="noopener">
              SimHub
            </a>{' '}
            {SIMHUB_VERSION} or later. 2 routes: 1 dashboard file, or the plugin with all 14 inside it.
          </>
        }
        wide
      >
        <h2 className="h3">Route 1: 1 dashboard, no plugin</h2>
        <p className={`prose ${styles.under}`}>The default pages and layout, and no settings. Enough to see it on your screen.</p>
        <Steps
          steps={[
            {
              title: 'Download the file for your screen.',
              body: (
                <>
                  <Link href="/screens#faces" className="link">
                    Find your size
                  </Link>
                  . 850 × 480 is the base size.
                </>
              ),
            },
            { title: 'Double-click it.', body: 'SimHub imports the .simhubdash. Nothing else is needed.' },
            { title: 'Assign it to a display in Dash Studio.', body: 'A DDU, a USB screen, or a phone on the network. Like any other dashboard.' },
          ]}
        />
      </Section>

      <Section id="plugin" label="Route 2" title="The plugin, with everything." lede="1 download with all 14 dashboards and 63 LED profiles inside it, and a settings page in SimHub." wide>
        <Steps
          steps={[
            { title: 'Close SimHub.', body: 'Fully closed, not in the tray.' },
            { title: 'Unzip OpenDash-plugin.zip.', body: `It carries all 14 dashboards and 63 LED profiles. Version ${VERSION}.` },
            {
              title: 'Copy OpenDash.dll next to SimHubWPF.exe.',
              body: (
                <>
                  Usually <code>C:\Program Files (x86)\SimHub</code>. Not a subfolder.
                </>
              ),
            },
            {
              title: 'Unblock the DLL. This is the step everyone misses.',
              body: 'Windows blocks downloaded files and .NET refuses to load them. Right-click the file, Properties, tick Unblock. Or in PowerShell:',
              command: 'Unblock-File "C:\\Program Files (x86)\\SimHub\\OpenDash.dll"',
              important: true,
            },
            { title: 'Start SimHub and accept the plugin.', body: 'OpenDash appears in the left menu with 4 tabs: Rig, Data, Lights, Install.' },
          ]}
        />
        <div className={styles.actions}>
          <Actions>
            <Primary href="/download">Download {VERSION}</Primary>
            <Secondary href="/screens#faces">Find your screen</Secondary>
          </Actions>
        </div>
      </Section>

      <Section id="after" label="After installing" title="Make it yours." lede="Nothing has to be configured to work. The plugin is where you change what each screen shows." wide>
        <ul className={`rows ${styles.points}`}>
          <li>
            <strong>Add your screen on the Rig tab.</strong> Pick the size, choose the pages for each zone, bind the wheel buttons.
          </li>
          <li>
            <strong>Every screen keeps its own settings.</strong> A face on the wheel and a face beside it are set up apart.
          </li>
          <li>
            <strong>Settings are SimHub properties.</strong> Other dashboards and LED profiles can read them. A change applies at once.
          </li>
          <li>
            <strong>Updates.</strong> The plugin checks GitHub once a day, sends nothing about you, and can be switched off. Updating is 1 click, then restart SimHub.
          </li>
        </ul>
      </Section>

      <Section id="nothing-showing" label="Trouble" title="Nothing showing?" lede="The 7 causes, most common first." wide>
        <ul className={`rows ${styles.points}`}>
          <li>
            <strong>SimHub never mentions the plugin.</strong> The DLL is still blocked, or it is in a subfolder. Step 4.
          </li>
          <li>
            <strong>The dashboard shows defaults although you changed them.</strong> The plugin is not enabled. Check Settings, Plugins.
          </li>
          <li>
            <strong>Nothing on the display.</strong> Assign the dashboard in Dash Studio. The plugin installs; SimHub launches.
          </li>
          <li>
            <strong>The track map or radar is empty.</strong> SimHub draws them after 1 recorded lap.
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
        </ul>
      </Section>
    </>
  );
}
