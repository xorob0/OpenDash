import type { Metadata } from 'next';
import { Section } from '../../components/Section';
import { Results } from './Results';

/**
 * The counterpart of /survey-k4qf9v: same obscurity rules (not in `lib/routes.ts`, robots kept
 * out, moving it is renaming this folder), plus a real gate. The page itself holds nothing: every
 * response comes from the API, which answers only to SURVEY_ADMIN_KEY.
 */
export const metadata: Metadata = {
  title: 'Survey results',
  robots: { index: false, follow: false },
};

export default function SurveyAdminPage() {
  return (
    <Section level={1} ruled={false} title="Survey results" lede="Enter the admin key to load the responses. The key is SURVEY_ADMIN_KEY on the server.">
      <Results />
    </Section>
  );
}
