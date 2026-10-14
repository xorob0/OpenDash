/**
 * The survey asks what a visitor needs from the list of what is coming, and that list is the
 * comparison's: a feature that has shipped is not offered as a plan, and neither is one docs/scope.md
 * refuses, since a visitor who ticks a refusal is told their blocker is coming (#548).
 */
import { describe, expect, test } from 'bun:test';
import { rows, scheduled } from '../lib/compare.ts';
import { QUESTIONS, RETIRED, SURVEY, parseSubmission, questionNumber } from '../lib/survey.ts';
import { SAMPLE_COUNTS, SAMPLE_LISTED } from './sampleCounts.ts';

const NEEDED = QUESTIONS.find((q) => q.label === 'Needed before daily use');
/** The two options that are not a row of the table: the sims, which no row covers, and the way out. */
const SIMS = 'Verified support for my sim, beyond iRacing';
const NONE = 'None of these block me';

describe('what the survey offers as coming', () => {
  test('says coming, and is the comparison’s coming rows', () => {
    expect(NEEDED?.prompt).toContain('coming but not built yet');
    const options = (NEEDED?.options ?? []).filter((o) => o !== SIMS && o !== NONE);
    expect(options).toEqual(scheduled(SAMPLE_COUNTS, SAMPLE_LISTED).map((r) => r.label));
  });

  test('offers nothing the scope refuses', () => {
    const refused = rows(SAMPLE_COUNTS, SAMPLE_LISTED)
      .filter((r) => r.cells.opendash.scope !== undefined)
      .map((r) => r.label.toLowerCase());
    const offered = (NEEDED?.options ?? []).map((o) => o.toLowerCase());
    expect(offered.filter((o) => refused.some((r) => o.includes(r)))).toEqual([]);
    // The two refusals the survey offered before, by its own words.
    expect(offered.filter((o) => /stream overlay|changing the layout/.test(o))).toEqual([]);
  });
});

/**
 * What the survey says OpenDash intends. Its OpenDash section said it aimed at feature parity with the
 * dashboards the survey lists, which ship features docs/scope.md refuses and the comparison marks
 * Refused, so a visitor read one aim on the survey and the opposite on /compare (#1095). Every word the
 * form shows is read: the section titles, the notes, the prompts, the hints and the options.
 */
const PARITY = /\bparity\b|\bon par with\b|feature for feature|\bevery feature\b|\bsame features\b|everything (?:they|those dashboards|the others) (?:do|does|offer|ship)/i;
const SHOWN = SURVEY.flatMap((s) => [s.title, s.note ?? '', ...s.questions.flatMap((q) => [q.prompt, q.hint ?? '', ...(q.options ?? []), ...(q.scaleEnds ?? [])])]);

describe('what the survey says OpenDash intends', () => {
  test.each(['it aims at feature parity with them', 'Parity with the paid ones', 'on par with the paid ones', 'matching them feature for feature', 'every feature they ship', 'everything those dashboards do, for free'])(
    '%p is a parity claim',
    (text) => {
      expect(PARITY.test(text)).toBe(true);
    },
  );

  test('claims no parity with the dashboards it lists', () => {
    expect(SHOWN.filter((text) => PARITY.test(text))).toEqual([]);
  });

  test('calls refused what the comparison refuses, wherever a note names it', () => {
    const refused = rows(SAMPLE_COUNTS, SAMPLE_LISTED)
      .filter((r) => r.cells.opendash.word === 'Refused')
      .map((r) => r.label.toLowerCase());
    const wrong = SURVEY.flatMap((s) =>
      refused.filter((label) => (s.note ?? '').toLowerCase().includes(label) && !/\brefused\b/i.test(s.note ?? '')).map((label) => `${s.title}: ${label}`),
    );
    expect(wrong).toEqual([]);
  });
});

describe('a question whose list changed', () => {
  test('takes a new id, so its answers are not counted against the old list', () => {
    // q15 kept its id when six of its options were replaced, and the panel drew the new options as
    // shares of respondents who had answered the old ones (#548).
    const ids = QUESTIONS.map((q) => q.id);
    expect(new Set(ids).size).toBe(ids.length);
    expect(ids.filter((id) => RETIRED.some((r) => r.id === id))).toEqual([]);
    expect(NEEDED?.id).not.toBe('q15');
  });

  test('is still numbered by its place in the form, and the API refuses the retired id', () => {
    expect(QUESTIONS.map(questionNumber)).toEqual(QUESTIONS.map((_, i) => String(i + 1)));
    expect(NEEDED ? questionNumber(NEEDED) : '').toBe('15');
    expect(parseSubmission({ q15: ['A stream overlay'] })).toBeNull();
  });
});
