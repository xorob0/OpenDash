/**
 * The survey's two handlers, with what guards them, apart from the database so a test can drive
 * them with a store of its own (#550, #830). `app/api/survey/route.ts` wires them to Prisma.
 *
 * A submission is taken only as `application/json` from the site's own origin. The type is what
 * makes a cross-site form or a `text/plain` fetch from another page a preflighted request, which
 * nothing here answers, and the origin is checked anyway. The body is read as a stream and dropped
 * the moment it passes BODY_LIMIT bytes, and a declared `Content-Length` above it is refused before
 * a byte is read. A filled honeypot gets the same 201 a person gets and stores nothing.
 *
 * The results answer only to `Authorization: Bearer <SURVEY_ADMIN_KEY>`. Both sides are hashed with
 * SHA-256 before `timingSafeEqual`, so the comparison takes the same time for every length of guess,
 * and a key shorter than ADMIN_KEY_MIN fails closed like a missing one.
 *
 * Both are throttled per client address, in memory. The site is one container with no Redis, so a
 * counter in the process is the whole of it: a restart forgets it, which costs an attacker nothing
 * he could not get by waiting an hour. The address is the last hop of `X-Forwarded-For`, the one
 * Dokploy's Traefik wrote; an earlier hop is whatever the client claimed. Served with nothing in
 * front, as `next start` is on a laptop, Next fills the header from the socket only when the client
 * sent none, so there a client can name its own address. The counters are keyed by a salted hash
 * of the address, the salt drawn when the process starts, so the site holds a count and not who
 * the reader is, which is what its no-tracking line promises.
 *
 * This file must not import `surveyDb.ts`: the tests under `test/` run without a generated Prisma
 * client.
 */
import { createHash, randomBytes, timingSafeEqual } from 'node:crypto';
import { HONEYPOT, parseSubmission, type Answers } from './survey';
import type { StoredResponse } from './surveyDb';

export const BODY_LIMIT = 64 * 1024;
/** Shorter than this, SURVEY_ADMIN_KEY is refused as if unset. docs/deploy.md says how to make one. */
export const ADMIN_KEY_MIN = 16;

/** Submissions per address per window: a person sends once, and a retry or two after a failure. */
export const POST_LIMIT = { count: 10, windowMs: 60 * 60 * 1000 };
/** Wrong keys per address per window, after which even the right one waits. */
export const GET_LIMIT = { count: 10, windowMs: 15 * 60 * 1000 };

export interface SurveyStore {
  insert(answers: Answers): Promise<void>;
  list(): Promise<StoredResponse[]>;
}

export interface SurveyOptions {
  /** The canonical origin, NEXT_PUBLIC_SITE_URL. Empty, a submission has to come from the host it was sent to. */
  siteUrl: string;
  /** Read on every request, as the environment variable is. */
  adminKey: () => string | undefined;
  now?: () => number;
}

/**
 * A fixed-window counter per key. The map is bounded: past MAX_KEYS it sweeps the expired windows,
 * and if that is not enough it drops the oldest, so a flood of addresses costs memory once and no
 * more.
 */
export class Throttle {
  static readonly MAX_KEYS = 10_000;
  private readonly windows = new Map<string, { count: number; resetAt: number }>();

  constructor(
    private readonly limit: { count: number; windowMs: number },
    private readonly now: () => number,
  ) {}

  /** Seconds until the key may try again, or 0 when it may now. Counts nothing. */
  wait(key: string): number {
    const w = this.windows.get(key);
    const t = this.now();
    if (!w || w.resetAt <= t || w.count < this.limit.count) return 0;
    return Math.max(1, Math.ceil((w.resetAt - t) / 1000));
  }

  /** Counts one against the key. */
  hit(key: string): void {
    const t = this.now();
    const w = this.windows.get(key);
    if (w && w.resetAt > t) {
      w.count++;
      return;
    }
    this.windows.delete(key);
    if (this.windows.size >= Throttle.MAX_KEYS) this.sweep(t);
    this.windows.set(key, { count: 1, resetAt: t + this.limit.windowMs });
  }

  private sweep(t: number): void {
    for (const [k, w] of this.windows) if (w.resetAt <= t) this.windows.delete(k);
    for (const k of this.windows.keys()) {
      if (this.windows.size < Throttle.MAX_KEYS) break;
      this.windows.delete(k);
    }
  }
}

/** The address the proxy in front saw: the last hop of X-Forwarded-For. */
export function clientAddress(request: Request): string {
  const hops = request.headers.get('x-forwarded-for')?.split(',') ?? [];
  return hops[hops.length - 1]?.trim() || 'unknown';
}

/** The submission came from a page of this site: its Origin is the canonical one, or this host when there is none. */
export function sameOrigin(request: Request, siteUrl: string): boolean {
  const origin = request.headers.get('origin');
  if (!origin) return false;
  let given: URL;
  try {
    given = new URL(origin);
  } catch {
    return false;
  }
  if (siteUrl) return given.origin === new URL(siteUrl).origin;
  return given.host === request.headers.get('host');
}

const isJson = (request: Request): boolean => request.headers.get('content-type')?.split(';')[0]?.trim().toLowerCase() === 'application/json';

/**
 * The body, read no further than `limit` bytes. Null when it is larger: a declared length is
 * believed without reading, and an undeclared or understated one is cut off where it passes.
 */
export async function readCapped(request: Request, limit: number): Promise<Uint8Array | null> {
  const declared = request.headers.get('content-length');
  if (declared !== null && !(Number(declared) <= limit)) return null;
  if (!request.body) return new Uint8Array(0);
  const reader = request.body.getReader();
  const chunks: Uint8Array[] = [];
  let total = 0;
  for (;;) {
    const { done, value } = await reader.read();
    if (done) break;
    total += value.byteLength;
    if (total > limit) {
      await reader.cancel().catch(() => undefined);
      return null;
    }
    chunks.push(value);
  }
  const out = new Uint8Array(total);
  let at = 0;
  for (const c of chunks) {
    out.set(c, at);
    at += c.byteLength;
  }
  return out;
}

const digest = (s: string): Buffer => createHash('sha256').update(s, 'utf8').digest();

/** The header carries `Bearer <key>` and the key is the configured one, compared over fixed-length digests. */
export function keyMatches(header: string | null, key: string): boolean {
  const given = /^Bearer (.+)$/i.exec(header ?? '')?.[1] ?? '';
  // Both digests are always computed and compared, so a missing scheme costs what a wrong key does.
  return timingSafeEqual(digest(given), digest(key));
}

const json = (body: unknown, status: number, headers?: Record<string, string>): Response => Response.json(body, { status, headers });
const throttled = (seconds: number, error: string): Response => json({ error }, 429, { 'retry-after': String(seconds) });
const dbDown = (): Response => json({ error: 'The database did not answer. Is DATABASE_URL set and the server reachable?' }, 503);

export function surveyHandlers(store: SurveyStore, options: SurveyOptions) {
  const now = options.now ?? Date.now;
  const salt = randomBytes(16);
  const who = (request: Request): string => createHash('sha256').update(salt).update(clientAddress(request)).digest('base64url');
  const posts = new Throttle(POST_LIMIT, now);
  const wrongKeys = new Throttle(GET_LIMIT, now);

  async function POST(request: Request): Promise<Response> {
    const address = who(request);
    const wait = posts.wait(address);
    if (wait > 0) return throttled(wait, 'Too many submissions from this address. Try again later.');
    posts.hit(address);
    if (!isJson(request)) return json({ error: 'Send application/json.' }, 415);
    if (!sameOrigin(request, options.siteUrl)) return json({ error: 'Not from this site.' }, 403);
    const bytes = await readCapped(request, BODY_LIMIT);
    if (bytes === null) return json({ error: 'Too large.' }, 413);
    let body: unknown;
    try {
      body = JSON.parse(new TextDecoder('utf-8', { fatal: true }).decode(bytes));
    } catch {
      return json({ error: 'Not JSON.' }, 400);
    }
    if (typeof body === 'object' && body !== null && HONEYPOT in body) {
      const { [HONEYPOT]: trap, ...rest } = body as Record<string, unknown>;
      if (trap !== '') return json({ ok: true }, 201);
      body = rest;
    }
    const answers = parseSubmission(body);
    if (answers === null) return json({ error: 'Not a survey submission.' }, 400);
    try {
      await store.insert(answers);
    } catch {
      return dbDown();
    }
    return json({ ok: true }, 201);
  }

  async function GET(request: Request): Promise<Response> {
    const key = options.adminKey();
    if (!key) return json({ error: 'SURVEY_ADMIN_KEY is not set on the server.' }, 503);
    if (key.length < ADMIN_KEY_MIN) return json({ error: `SURVEY_ADMIN_KEY is shorter than ${ADMIN_KEY_MIN} characters. Set a longer one on the server.` }, 503);
    const address = who(request);
    const wait = wrongKeys.wait(address);
    if (wait > 0) return throttled(wait, `Too many wrong keys from this address. Try again in ${Math.ceil(wait / 60)} min.`);
    if (!keyMatches(request.headers.get('authorization'), key)) {
      wrongKeys.hit(address);
      return json({ error: 'Wrong key.' }, 401);
    }
    try {
      return json({ responses: await store.list() }, 200);
    } catch {
      return dbDown();
    }
  }

  return { POST, GET };
}
