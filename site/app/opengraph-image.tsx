import { readFileSync } from 'node:fs';
import path from 'node:path';
import { ImageResponse } from 'next/og';
import { packageFile } from '../lib/captures';
import { SITE_TAGLINE } from '../lib/site';

/**
 * The picture a link to the site unfurls with: the mark, the tagline, the promise, and the base
 * face's own capture. Rendered at build time from the same still the home page shows.
 */
export const alt = 'OpenDash: free SimHub dashboards for iRacing';
export const size = { width: 1200, height: 630 };
export const contentType = 'image/png';

const MARK = 'M1,27 L1,5 L31,5 L31,27 Z M1,11 L1,8.5 L31,8.5 L31,11 Z M1,24.5 L1,22 L31,22 L31,24.5 Z M8.25,22 L8.25,11 L10.75,11 L10.75,22 Z M21.25,22 L21.25,11 L23.75,11 L23.75,22 Z';

export default function Image() {
  const fonts = path.join(process.cwd(), '..', 'packages', 'dash', 'fonts');
  const condensed = readFileSync(path.join(fonts, 'BarlowCondensed-Bold.ttf'));
  const barlow = readFileSync(path.join(fonts, 'Barlow-Medium.ttf'));
  const still = readFileSync(path.join(process.cwd(), 'public', 'shots', packageFile('OpenDash 850x480')));
  const stillSrc = `data:image/png;base64,${still.toString('base64')}`;

  return new ImageResponse(
    (
      <div style={{ width: '100%', height: '100%', display: 'flex', flexDirection: 'column', background: '#0A0B0D', color: '#F5F7FA', padding: '56px 64px 0', fontFamily: 'Barlow' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 16 }}>
          <svg viewBox="0 0 32 32" width={40} height={40}>
            <path fill="#33D9F2" fillRule="evenodd" d={MARK} />
          </svg>
          <div style={{ display: 'flex', fontFamily: 'Barlow Condensed', fontSize: 44, letterSpacing: -0.5 }}>
            <span style={{ color: '#8A9099' }}>open</span>
            <span>Dash</span>
          </div>
        </div>
        <div style={{ display: 'flex', flexDirection: 'column', marginTop: 28, gap: 10 }}>
          <div style={{ fontFamily: 'Barlow Condensed', fontSize: 84, lineHeight: 1, textTransform: 'uppercase', letterSpacing: -1.5 }}>Free, forever.</div>
          <div style={{ fontSize: 30, color: '#8A9099' }}>{`${SITE_TAGLINE} MIT. No licence, no account, nothing to unlock.`}</div>
        </div>
        <div style={{ display: 'flex', marginTop: 36, border: '1px solid #1C1F24', background: '#060708', width: 1072, height: 605, overflow: 'hidden' }}>
          <img src={stillSrc} width={1072} height={605} />
        </div>
      </div>
    ),
    {
      ...size,
      fonts: [
        { name: 'Barlow Condensed', data: condensed, weight: 700, style: 'normal' },
        { name: 'Barlow', data: barlow, weight: 500, style: 'normal' },
      ],
    },
  );
}
