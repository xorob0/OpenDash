import type { Metadata } from 'next';
import { Section } from '../../components/Section';
import { SurveyForm } from './SurveyForm';

/**
 * The survey lives at an address that is handed out, not found: it is not in `lib/routes.ts`, so
 * the nav, the sitemap and the link test do not know it, and robots are told to keep out. Moving
 * it is renaming this folder.
 */
export const metadata: Metadata = {
  title: 'Sim racer survey',
  robots: { index: false, follow: false },
};

export default function SurveyPage() {
  return (
    <Section
      level={1}
      ruled={false}
      title="Ten minutes of your honesty"
      lede="OpenDash is a free, open source dashboard package for SimHub, currently in beta, and your answers will decide what gets built next. No answer is wrong: what you already use and what you would not pay for are exactly what we need to hear. Nothing is recorded until you press send, and then only your answers are."
    >
      <SurveyForm />
    </Section>
  );
}
