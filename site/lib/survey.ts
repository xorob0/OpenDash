/**
 * The sim racer survey: every question, and the validation the API applies to a submission.
 *
 * The form, the admin tallies and the API all read this file, so a question added or reworded
 * here appears on the page, is accepted by the endpoint and is counted in the panel without any
 * of the three being touched. The page that renders it is deliberately absent from
 * `lib/routes.ts`: it is reached by a link handed to a respondent, not by the nav, the sitemap
 * or a search engine.
 */

export type QuestionKind = 'checks' | 'radio' | 'scale' | 'text' | 'paragraph';

export interface Question {
  id: string;
  /** The short name the admin panel tallies under. */
  label: string;
  /** The full sentence the form asks. */
  prompt: string;
  kind: QuestionKind;
  options?: readonly string[];
  /** The words under 1 and 5 of a scale. */
  scaleEnds?: readonly [string, string];
  /** A checks question with a free "which one?" line behind its last option. */
  other?: boolean;
  hint?: string;
  optional?: boolean;
}

export interface SurveySection {
  title: string;
  note?: string;
  questions: readonly Question[];
}

const ALL = 'Tick all that apply';

export const SURVEY: readonly SurveySection[] = [
  {
    title: 'You and your rig',
    questions: [
      {
        id: 'q1',
        label: 'Sims',
        prompt: 'Which sims do you race the most?',
        kind: 'checks',
        hint: ALL,
        other: true,
        options: [
          'iRacing',
          'Assetto Corsa Competizione',
          'Le Mans Ultimate',
          'Assetto Corsa / Assetto Corsa EVO',
          'Automobilista 2',
          'rFactor 2',
          'An F1 title',
          'Gran Turismo 7',
          'Another one',
        ],
      },
      {
        id: 'q2',
        label: 'Hours per week',
        prompt: 'Roughly how many hours per week do you race?',
        kind: 'radio',
        options: ['Under 2', '2 to 5', '5 to 10', 'More than 10'],
      },
      {
        id: 'q3',
        label: 'Racing info lives',
        prompt: 'Where does your racing info live today?',
        kind: 'checks',
        hint: ALL,
        options: [
          'A screen on the wheel',
          'A separate dash screen (USB or DDU)',
          'A phone or tablet',
          'A second monitor',
          'An overlay on the main screen',
          'LEDs or a flag panel',
          'Nowhere, I race without any of it',
        ],
      },
      {
        id: 'q4',
        label: 'Screens in the rig',
        prompt: 'How many separate screens show racing info in your rig?',
        kind: 'radio',
        options: ['None', '1', '2', '3 or more'],
      },
      {
        id: 'q5',
        label: 'Screen sizes today',
        prompt: 'Which screen sizes do you run today?',
        kind: 'checks',
        hint: ALL,
        options: [
          'Around 5 in (800 x 480 or 850 x 480)',
          '7.8 in wide (1280 x 400)',
          '10 in (1280 x 480)',
          'Ultrawide (1920 x 480)',
          'A round screen',
          'A phone or tablet',
          'A monitor',
          'None of these',
        ],
      },
      {
        id: 'q6',
        label: 'Screen to add',
        prompt: 'Is there a screen you would like to add to your rig, and which one?',
        kind: 'text',
        optional: true,
        hint: 'Optional',
      },
      {
        id: 'q7',
        label: 'LEDs',
        prompt: 'Where do you have LEDs?',
        kind: 'checks',
        hint: ALL,
        options: [
          'On the wheel',
          'A rev strip on or behind the dash',
          'Monitor brows',
          'Ambient or rig lighting',
          'An iFlag or another flag panel',
          'None',
        ],
      },
      {
        id: 'q8',
        label: 'LEDs driven by',
        prompt: 'If you run LEDs or a flag panel, what drives them?',
        kind: 'checks',
        hint: ALL,
        options: [
          "SimHub's default profiles",
          "A creator's profiles (Daniel Newman Racing, Lovely, another)",
          'Profiles I made myself',
          "The device's own software",
          'Not applicable',
        ],
      },
    ],
  },
  {
    title: 'What you use today',
    questions: [
      {
        id: 'q9',
        label: 'Dashboards used',
        prompt: 'Which dashboards have you used?',
        kind: 'checks',
        hint: ALL,
        options: [
          "SimHub's built-in dashboards",
          'Lovely Dashboard',
          'Daniel Newman Racing',
          'One I made myself in Dash Studio',
          "Another creator's",
          'None',
        ],
      },
      {
        id: 'q10',
        label: 'Info on the dash',
        prompt: 'What information is on your dashboard most of the time?',
        kind: 'checks',
        hint: ALL,
        options: [
          'Gear and revs',
          'Lap times and delta',
          'Relative and gaps to nearby cars',
          'Standings or leaderboard',
          'Fuel and pit strategy',
          'Tyres and brakes',
          'Track map',
          'Inputs (throttle, brake, steering)',
          'Flags and warnings',
        ],
      },
      {
        id: 'q11',
        label: 'Ever paid',
        prompt: 'Have you ever paid for a dashboard, an LED profile or a membership around them?',
        kind: 'radio',
        options: [
          'I pay for one today',
          'I have paid in the past',
          'Never, but I would for the right one',
          'Never, and I would not',
        ],
      },
      {
        id: 'q12',
        label: 'What matters most',
        prompt: 'When you pick a dashboard, what matters most?',
        kind: 'checks',
        hint: 'Choose up to three',
        options: [
          'Readability at speed',
          'How it looks',
          'Correct shift lights for each car',
          'It fits my exact screen size',
          'It supports my sim',
          'Easy install and updates',
          'I can change colours and pages',
          'Price',
          'Active development and community',
        ],
      },
      {
        id: 'q13',
        label: 'Features used or missed',
        prompt: 'Which of these features do you actually use, or would you miss if they were gone?',
        kind: 'checks',
        hint: ALL,
        options: [
          'Alerts for flags, penalties and the safety car',
          'Notifications when brake bias, TC or ABS change',
          'A radar for cars alongside',
          'A damage view',
          'A lap review after the lap',
          'An idle screen between sessions',
          'Per-car shift lights and redlines',
          'A stream overlay',
          'Teammate telemetry',
          'Installing and updating dashboards from inside SimHub',
          'None of these',
        ],
      },
    ],
  },
  {
    title: 'OpenDash',
    note:
      'OpenDash is an alternative to the dashboards above. It is free and open source, it aims at feature parity with them, and it intends to stay what it is today: a dashboard and LED tool, nothing more, compatible with as many sims as possible.',
    questions: [
      {
        id: 'q14',
        label: 'Likely to try the beta (1-5)',
        prompt: 'How likely are you to try OpenDash while it is in beta?',
        kind: 'scale',
        scaleEnds: ['Very unlikely', 'Very likely'],
      },
      {
        id: 'q15',
        label: 'Needed before daily use',
        prompt: 'Some things are planned but not built yet. Which would you need before OpenDash could be your daily dashboard?',
        kind: 'checks',
        hint: ALL,
        options: [
          'Verified support for my sim, beyond iRacing',
          'An idle and pit screen',
          'Per-car shift lights and themes',
          'Round screen support',
          'A stream overlay',
          'Changing the layout, not only the colours',
          'None of these block me',
        ],
      },
      {
        id: 'q16',
        label: 'What would make me switch',
        prompt: 'What would make you actually switch from your current setup?',
        kind: 'paragraph',
      },
      {
        id: 'q17',
        label: 'OpenDash parts I would use',
        prompt: 'Which parts of OpenDash would you expect to use?',
        kind: 'checks',
        hint: ALL,
        options: [
          'A face for my wheel or DDU screen',
          'The phone or tablet companion',
          'The pit wall screen for endurance stints',
          'The LED flag box',
          'Colour personalisation',
          'Contributing, since it is open source, even just bug reports',
        ],
      },
    ],
  },
  {
    title: 'Two hardware ideas',
    note: 'Separate from OpenDash, two pieces of hardware are being considered, and your answers decide whether either is worth building.',
    questions: [
      {
        id: 'q18',
        label: 'CarPlay screen interest (1-5)',
        prompt:
          "Idea one: a screen for the rig, in the format of a real car's centre display, that runs Android Auto and Apple CarPlay. You connect your phone, your music plays through the PC, and while racing it can show a dash or a rear view mirror. How interesting is that to you?",
        kind: 'scale',
        scaleEnds: ['Not at all', 'Take my money'],
      },
      {
        id: 'q19',
        label: 'CarPlay screen uses',
        prompt: 'Which of its uses would you actually use?',
        kind: 'checks',
        hint: ALL,
        options: [
          'CarPlay or Android Auto for music and calls while racing',
          'A dash while racing',
          'A rear view mirror',
          'The car-like format itself, making the rig feel like a real cockpit',
          'None of them',
        ],
      },
      {
        id: 'q20',
        label: 'CarPlay screen worth',
        prompt: 'What would that screen be worth to you?',
        kind: 'radio',
        options: ['Under 50 EUR', '50 to 100 EUR', '100 to 150 EUR', '150 to 250 EUR', 'I would not buy it'],
      },
      {
        id: 'q21',
        label: 'Plays GT7',
        prompt: 'Do you play Gran Turismo 7?',
        kind: 'radio',
        options: ['Regularly', 'Sometimes', 'No'],
      },
      {
        id: 'q22',
        label: 'GT7 wheel dash interest',
        prompt:
          'Idea two: a small, affordable dash screen made specifically for GT7, mounting on Fanatec, Moza and Logitech wheels. How interesting is that to you?',
        kind: 'radio',
        options: ['Very: GT7 is exactly where I need a dash', 'Somewhat', 'Not for me', 'I do not play GT7'],
      },
      {
        id: 'q23',
        label: 'Wheel owned',
        prompt: 'Which wheel do you own?',
        kind: 'checks',
        hint: ALL,
        options: ['Fanatec', 'Moza', 'Logitech', 'Thrustmaster', 'A direct drive from another brand', 'No wheel, I play on a controller'],
      },
      {
        id: 'q24',
        label: 'GT7 dash worth',
        prompt: 'What would the GT7 dash be worth to you?',
        kind: 'radio',
        options: ['Under 30 EUR', '30 to 60 EUR', '60 to 100 EUR', 'More than 100 EUR', 'I would not buy it'],
      },
    ],
  },
  {
    title: 'Supporting the project',
    note: 'OpenDash is free and will stay free. This section is about whether donations could sustain it, and honest answers help more than kind ones.',
    questions: [
      {
        id: 'q25',
        label: 'Would donate',
        prompt: 'If you used OpenDash every week and liked it, would you donate?',
        kind: 'radio',
        options: ['Yes, a one-off', 'Yes, something recurring', 'Maybe, if it were easy at the right moment', 'Probably not', 'No'],
      },
      {
        id: 'q26',
        label: 'Donation more likely if',
        prompt: 'Would any of these make a donation more likely?',
        kind: 'checks',
        hint: ALL,
        options: [
          'Nothing extra, I would donate purely to support it',
          'My name in the credits or on the site',
          'A shoutout on Discord or in release notes',
          'Early access to new designs',
          'Priority on my feature requests',
          'None of these would change my mind',
        ],
      },
      {
        id: 'q27',
        label: 'Fair one-off donation',
        prompt: 'If you donated once, what would feel fair?',
        kind: 'radio',
        options: ['Under 5 EUR', '5 to 15 EUR', '15 to 30 EUR', 'More than 30 EUR', 'I would not donate'],
      },
      {
        id: 'q28',
        label: 'Anything else',
        prompt: 'Anything else you would like to say?',
        kind: 'paragraph',
        optional: true,
        hint: 'Optional',
      },
    ],
  },
];

export const QUESTIONS: readonly Question[] = SURVEY.flatMap((s) => s.questions);

const SCALE_VALUES = ['1', '2', '3', '4', '5'] as const;
/** The free line behind a checks question's last option is stored with this prefix. */
export const OTHER_PREFIX = 'Other: ';

/** One respondent's answers. A question left blank is absent, never an empty value. */
export type Answers = Record<string, string | string[]>;

const TEXT_LIMIT = 2000;

function validValue(q: Question, value: unknown): boolean {
  switch (q.kind) {
    case 'text':
    case 'paragraph':
      return typeof value === 'string' && value.length > 0 && value.length <= TEXT_LIMIT;
    case 'scale':
      return typeof value === 'string' && (SCALE_VALUES as readonly string[]).includes(value);
    case 'radio':
      return typeof value === 'string' && (q.options ?? []).includes(value);
    case 'checks':
      return (
        Array.isArray(value) &&
        value.length > 0 &&
        value.length <= (q.options?.length ?? 0) &&
        value.every(
          (v) =>
            typeof v === 'string' &&
            ((q.options ?? []).includes(v) || (q.other === true && v.startsWith(OTHER_PREFIX) && v.length <= TEXT_LIMIT)),
        )
      );
  }
}

/**
 * A submission as the API accepts it, or null. Unknown keys and malformed values reject the whole
 * body rather than being dropped, so a bug on the form side is a loud 400 and not a silent hole
 * in the data.
 */
export function parseSubmission(body: unknown): Answers | null {
  if (typeof body !== 'object' || body === null || Array.isArray(body)) return null;
  const entries = Object.entries(body as Record<string, unknown>);
  if (entries.length === 0) return null;
  const answers: Answers = {};
  for (const [id, value] of entries) {
    const q = QUESTIONS.find((question) => question.id === id);
    if (!q || !validValue(q, value)) return null;
    answers[id] = value as string | string[];
  }
  return answers;
}
