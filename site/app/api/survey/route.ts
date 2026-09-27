/**
 * POST takes a submission from the survey page; GET hands every stored response to whoever
 * carries the admin key.
 *
 * The key is SURVEY_ADMIN_KEY, a runtime environment variable, compared in constant time. With
 * the variable unset GET answers 503 rather than letting an empty comparison through: an
 * unconfigured deployment fails closed. A database that cannot be reached is also a 503, with
 * the variable's name in the answer, because that is the first and usually the last thing to
 * check.
 */
import { timingSafeEqual } from 'node:crypto';
import { parseSubmission } from '../../../lib/survey';
import { insertResponse, listResponses } from '../../../lib/surveyDb';

export const dynamic = 'force-dynamic';

const BODY_LIMIT = 64 * 1024;

const dbDown = () => Response.json({ error: 'The database did not answer. Is DATABASE_URL set and the server reachable?' }, { status: 503 });

export async function POST(request: Request): Promise<Response> {
  const raw = await request.text();
  if (raw.length > BODY_LIMIT) return Response.json({ error: 'Too large.' }, { status: 413 });
  let body: unknown;
  try {
    body = JSON.parse(raw);
  } catch {
    return Response.json({ error: 'Not JSON.' }, { status: 400 });
  }
  const answers = parseSubmission(body);
  if (answers === null) return Response.json({ error: 'Not a survey submission.' }, { status: 400 });
  try {
    await insertResponse(answers);
  } catch {
    return dbDown();
  }
  return Response.json({ ok: true }, { status: 201 });
}

export async function GET(request: Request): Promise<Response> {
  const key = process.env.SURVEY_ADMIN_KEY;
  if (!key) return Response.json({ error: 'SURVEY_ADMIN_KEY is not set on the server.' }, { status: 503 });
  const given = request.headers.get('authorization')?.replace(/^Bearer /, '') ?? '';
  const a = Buffer.from(given);
  const b = Buffer.from(key);
  if (a.length !== b.length || !timingSafeEqual(a, b)) {
    return Response.json({ error: 'Wrong key.' }, { status: 401 });
  }
  try {
    return Response.json({ responses: await listResponses() });
  } catch {
    return dbDown();
  }
}
