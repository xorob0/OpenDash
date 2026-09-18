import type { Metadata, Viewport } from 'next';
import './tokens.css';
import './globals.css';
import { Nav } from '../components/Nav';
import { Footer } from '../components/Footer';
import { SITE_NAME, SITE_TAGLINE, SITE_URL } from '../lib/site';

export const metadata: Metadata = {
  // With NEXT_PUBLIC_SITE_URL unset there is no absolute origin to build on, and Next falls back
  // to relative URLs rather than to a domain this repository would have had to guess.
  ...(SITE_URL ? { metadataBase: new URL(SITE_URL) } : {}),
  title: {
    default: `${SITE_NAME} — a sim racing dashboard for SimHub`,
    template: `%s — ${SITE_NAME}`,
  },
  description:
    'Fourteen SimHub dashboards from 1920 by 480 to a 480 px round DDU, a companion for the phone beside the wheel, and a pit wall screen. One plugin installs them all. Open source, MIT.',
  applicationName: SITE_NAME,
  openGraph: {
    type: 'website',
    siteName: SITE_NAME,
    title: `${SITE_NAME} — a sim racing dashboard for SimHub`,
    description: SITE_TAGLINE,
  },
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
