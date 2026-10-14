/**
 * The survey's API refuses what is not a submission from this site, stops reading a body at the
 * limit, throttles an address that repeats itself, and answers the results only to the Bearer key
 * (#550, #830). The handlers are driven with a store of their own, so nothing here needs a
 * database.
 */
import { describe, expect, test } from 'bun:test';
import { ADMIN_KEY_MIN, BODY_LIMIT, GET_LIMIT, POST_LIMIT, Throttle, keyMatches, surveyHandlers } from '../lib/surveyApi.ts';
import { HONEYPOT, QUESTIONS, RETIRED, type Answers } from '../lib/survey.ts';

const SITE = 'https://opendash.example';
const KEY = 'k'.repeat(ADMIN_KEY_MIN) + '-results';
const TEXT = QUESTIONS.find((q) => q.kind === 'text')!;
const VALID = JSON.stringify({ [TEXT.id]: 'An answer.' });

/** The handlers over an in-memory store and a clock the test moves. A null key is the variable unset. */
function setup(adminKey: string | null = KEY) {
  const stored: Answers[] = [];
  let clock = 1_000_000;
  const api = surveyHandlers(
    { insert: async (a) => void stored.push(a), list: async () => stored.map((answers, i) => ({ id: i + 1, submittedAt: '2026-10-14T00:00:00.000Z', answers })) },
    { siteUrl: SITE, adminKey: () => adminKey ?? undefined, now: () => clock },
  );
  return { api, stored, advance: (ms: number) => void (clock += ms) };
}

const post = (body: BodyInit, headers: Record<string, string> = {}, address = '203.0.113.7') =>
  new Request(`${SITE}/api/survey`, {
    method: 'POST',
    body,
    headers: { 'content-type': 'application/json', origin: SITE, 'x-forwarded-for': address, ...headers },
    duplex: 'half',
  } as RequestInit);

const get = (authorization: string | undefined, address = '203.0.113.7') =>
  new Request(`${SITE}/api/survey`, { headers: { ...(authorization === undefined ? {} : { authorization }), 'x-forwarded-for': address } });

/** A body of `chunks` 16 KiB pieces that counts how many were pulled, and pulls none until asked. */
function counted(chunks: number) {
  const seen = { pulled: 0 };
  const stream = new ReadableStream<Uint8Array>({
    pull(c) {
      if (seen.pulled === chunks) return c.close();
      seen.pulled++;
      c.enqueue(new Uint8Array(16 * 1024).fill(0x20));
    },
  }, { highWaterMark: 0 });
  return { stream, seen };
}

describe('a submission', () => {
  test('from this site, as JSON, is stored', async () => {
    const { api, stored } = setup();
    const res = await api.POST(post(VALID));
    expect(res.status).toBe(201);
    expect(stored).toEqual([{ [TEXT.id]: 'An answer.' }]);
  });

  test('sent as text/plain, the type another page can send without asking, is refused', async () => {
    const { api, stored } = setup();
    expect((await api.POST(post(VALID, { 'content-type': 'text/plain;charset=UTF-8' }))).status).toBe(415);
    expect((await api.POST(post(VALID, { 'content-type': 'application/x-www-form-urlencoded' }))).status).toBe(415);
    expect(stored).toEqual([]);
  });

  test('a charset on the JSON type is fine', async () => {
    const { api } = setup();
    expect((await api.POST(post(VALID, { 'content-type': 'application/json; charset=utf-8' }))).status).toBe(201);
  });

  test('from another origin, or from none, is refused', async () => {
    const { api, stored } = setup();
    expect((await api.POST(post(VALID, { origin: 'https://elsewhere.example' }))).status).toBe(403);
    expect((await api.POST(post(VALID, { origin: 'http://opendash.example' }))).status).toBe(403);
    expect((await api.POST(post(VALID, { origin: 'null' }))).status).toBe(403);
    const bare = post(VALID);
    bare.headers.delete('origin');
    expect((await api.POST(bare)).status).toBe(403);
    expect(stored).toEqual([]);
  });

  test('without a canonical origin, has to come from the host it was sent to', async () => {
    const stored: Answers[] = [];
    const api = surveyHandlers({ insert: async (a) => void stored.push(a), list: async () => [] }, { siteUrl: '', adminKey: () => KEY });
    const at = (origin: string) => post(VALID, { origin, host: '127.0.0.1:3300' });
    expect((await api.POST(at('http://127.0.0.1:3300'))).status).toBe(201);
    expect((await api.POST(at('http://evil.example'))).status).toBe(403);
    expect(stored).toHaveLength(1);
  });

  test('declaring more than the limit is refused before a byte is read', async () => {
    const { api } = setup();
    const { stream, seen } = counted(64);
    const res = await api.POST(post(stream, { 'content-length': String(64 * 16 * 1024) }));
    expect(res.status).toBe(413);
    expect(seen.pulled).toBe(0);
  });

  test('sending more than the limit without saying so is cut off at the limit, not read in full', async () => {
    const { api } = setup();
    const { stream, seen } = counted(64);
    const res = await api.POST(post(stream));
    expect(res.status).toBe(413);
    expect(seen.pulled).toBeLessThanOrEqual(BODY_LIMIT / (16 * 1024) + 2);
  });

  test('the limit is in bytes, not characters', async () => {
    const { api } = setup();
    // 40 000 characters, 80 000 bytes: under the limit as a string's length, over it on the wire.
    const res = await api.POST(post(JSON.stringify({ [TEXT.id]: 'é'.repeat(40_000) })));
    expect(res.status).toBe(413);
  });

  test('a filled honeypot is answered as stored and is not', async () => {
    const { api, stored } = setup();
    const res = await api.POST(post(JSON.stringify({ [TEXT.id]: 'Buy now.', [HONEYPOT]: 'https://spam.example' })));
    expect(res.status).toBe(201);
    expect(stored).toEqual([]);
  });

  test('an empty honeypot is ignored', async () => {
    const { api, stored } = setup();
    expect((await api.POST(post(JSON.stringify({ [TEXT.id]: 'An answer.', [HONEYPOT]: '' })))).status).toBe(201);
    expect(stored).toEqual([{ [TEXT.id]: 'An answer.' }]);
  });

  test('repeated from one address is throttled, and another address is not', async () => {
    const { api, stored, advance } = setup();
    for (let i = 0; i < POST_LIMIT.count; i++) expect((await api.POST(post(VALID))).status).toBe(201);
    const refused = await api.POST(post(VALID));
    expect(refused.status).toBe(429);
    expect(Number(refused.headers.get('retry-after'))).toBeGreaterThan(0);
    expect((await api.POST(post(VALID, {}, '198.51.100.9'))).status).toBe(201);
    expect(stored).toHaveLength(POST_LIMIT.count + 1);
    advance(POST_LIMIT.windowMs);
    expect((await api.POST(post(VALID))).status).toBe(201);
  });

  test('the address is the hop the proxy wrote, not one the client put before it', async () => {
    const { api } = setup();
    for (let i = 0; i < POST_LIMIT.count; i++) await api.POST(post(VALID, {}, `10.0.0.${i}, 203.0.113.7`));
    expect((await api.POST(post(VALID, {}, '10.9.9.9, 203.0.113.7'))).status).toBe(429);
  });

  test('to a retired question is refused like an unknown id, and its replacement is taken (#548)', async () => {
    const { api, stored } = setup();
    for (const old of RETIRED) {
      expect(QUESTIONS.some((q) => q.id === old.id)).toBe(false);
      const res = await api.POST(post(JSON.stringify({ [old.id]: [old.options![0]] })));
      expect(res.status).toBe(400);
    }
    const NEEDED = QUESTIONS.find((q) => q.id === 'q15b')!;
    expect((await api.POST(post(JSON.stringify({ q15b: [NEEDED.options![0]] })))).status).toBe(201);
    expect(stored).toEqual([{ q15b: [NEEDED.options![0]] }]);
  });
});

describe('the results', () => {
  test('still carry the answers stored under a retired question, for the panel to tally on its own (#548)', async () => {
    const stored: Answers[] = [{ q15: ['A stream overlay'] }];
    const api = surveyHandlers(
      { insert: async (a) => void stored.push(a), list: async () => stored.map((answers, i) => ({ id: i + 1, submittedAt: '2026-10-01T00:00:00.000Z', answers })) },
      { siteUrl: SITE, adminKey: () => KEY },
    );
    const res = await api.GET(get(`Bearer ${KEY}`));
    expect(res.status).toBe(200);
    expect(((await res.json()) as { responses: { answers: Answers }[] }).responses.map((r) => r.answers)).toEqual([{ q15: ['A stream overlay'] }]);
  });

  test('answer to the Bearer key', async () => {
    const { api } = setup();
    const res = await api.GET(get(`Bearer ${KEY}`));
    expect(res.status).toBe(200);
    expect(await res.json()).toEqual({ responses: [] });
  });

  test('refuse the key without the Bearer scheme, a wrong key, and a key of another length', async () => {
    const { api } = setup();
    expect((await api.GET(get(KEY))).status).toBe(401);
    expect((await api.GET(get(`Basic ${KEY}`))).status).toBe(401);
    expect((await api.GET(get(`Bearer ${KEY.slice(0, -1)}x`))).status).toBe(401);
    expect((await api.GET(get('Bearer x'))).status).toBe(401);
    expect((await api.GET(get('Bearer '))).status).toBe(401);
    expect((await api.GET(get(undefined))).status).toBe(401);
  });

  test('stop answering an address that keeps guessing, even with the right key, until the window passes', async () => {
    const { api, advance } = setup();
    for (let i = 0; i < GET_LIMIT.count; i++) expect((await api.GET(get(`Bearer guess-${i}`))).status).toBe(401);
    const refused = await api.GET(get(`Bearer ${KEY}`));
    expect(refused.status).toBe(429);
    expect(Number(refused.headers.get('retry-after'))).toBeGreaterThan(0);
    expect((await api.GET(get(`Bearer ${KEY}`, '198.51.100.9'))).status).toBe(200);
    advance(GET_LIMIT.windowMs);
    expect((await api.GET(get(`Bearer ${KEY}`))).status).toBe(200);
  });

  test('the right key is not counted against the address', async () => {
    const { api } = setup();
    for (let i = 0; i < GET_LIMIT.count + 5; i++) expect((await api.GET(get(`Bearer ${KEY}`))).status).toBe(200);
  });

  test('fail closed with the key unset or too short', async () => {
    expect((await setup(null).api.GET(get('Bearer '))).status).toBe(503);
    expect((await setup('').api.GET(get('Bearer '))).status).toBe(503);
    const short = 's'.repeat(ADMIN_KEY_MIN - 1);
    expect((await setup(short).api.GET(get(`Bearer ${short}`))).status).toBe(503);
  });
});

describe('the pieces', () => {
  test('the key comparison never throws on a length that differs', () => {
    expect(keyMatches(`Bearer ${KEY}`, KEY)).toBe(true);
    expect(keyMatches('Bearer a', KEY)).toBe(false);
    expect(keyMatches(`Bearer ${KEY}${'x'.repeat(1000)}`, KEY)).toBe(false);
    expect(keyMatches(null, KEY)).toBe(false);
  });

  test('the throttle holds no more than its bound of addresses', () => {
    let t = 0;
    const throttle = new Throttle({ count: 1, windowMs: 1000 }, () => t);
    for (let i = 0; i < Throttle.MAX_KEYS + 50; i++) throttle.hit(`a${i}`);
    expect((throttle as unknown as { windows: Map<string, unknown> }).windows.size).toBeLessThanOrEqual(Throttle.MAX_KEYS);
    expect(throttle.wait(`a${Throttle.MAX_KEYS + 49}`)).toBe(1);
    t = 1000;
    expect(throttle.wait(`a${Throttle.MAX_KEYS + 49}`)).toBe(0);
  });
});
