import type { Metadata, Viewport } from 'next';
import './tokens.css';
import './globals.css';
import { Nav } from '../components/Nav';
import { Footer } from '../components/Footer';
import { SITE_NAME, SITE_TAGLINE, SITE_URL } from '../lib/site';

const TITLE = `${SITE_NAME}: free SimHub dashboards for iRacing`;
const DESCRIPTION = '14 SimHub dashboards, 21 pages and 63 LED profiles for iRacing on Windows. Free, forever, under MIT. Generated from source.';

export const metadata: Metadata = {
  // With NEXT_PUBLIC_SITE_URL unset there is no absolute origin to build on, and Next falls back
  // to relative URLs rather than to a domain this repository would have had to guess.
  ...(SITE_URL ? { metadataBase: new URL(SITE_URL) } : {}),
  title: { default: TITLE, template: `%s · ${SITE_NAME}` },
  description: DESCRIPTION,
  applicationName: SITE_NAME,
  openGraph: { type: 'website', siteName: SITE_NAME, title: TITLE, description: SITE_TAGLINE },
  twitter: { card: 'summary_large_image' },
  icons: { icon: '/icon.svg' },
};

export const viewport: Viewport = {
  themeColor: '#0A0B0D',
  colorScheme: 'dark',
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en">
      <body>
        <a href="#main" className="skip">
          Skip to content
        </a>
        <Nav />
        <main id="main">{children}</main>
        <Footer />
      </body>
    </html>
  );
}
