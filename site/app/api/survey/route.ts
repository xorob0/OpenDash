/**
 * POST takes a submission from the survey page; GET hands every stored response to whoever
 * carries the admin key.
 *
 * What guards them is in `lib/surveyApi.ts`, where a test can reach it without a database: a
 * submission is JSON from this site's own origin, read no further than 64 KiB, and throttled per
 * address; the key is SURVEY_ADMIN_KEY, a runtime environment variable, compared in constant time
 * over SHA-256 digests, with wrong guesses throttled per address. With the variable unset GET
 * answers 503 rather than letting an empty comparison through: an unconfigured deployment fails
 * closed. A database that cannot be reached is also a 503, with the variable's name in the answer,
 * because that is the first and usually the last thing to check.
 */
import { SITE_URL } from '../../../lib/site';
import { surveyHandlers } from '../../../lib/surveyApi';
import { insertResponse, listResponses } from '../../../lib/surveyDb';

export const dynamic = 'force-dynamic';

const handlers = surveyHandlers(
  { insert: insertResponse, list: listResponses },
  { siteUrl: SITE_URL, adminKey: () => process.env.SURVEY_ADMIN_KEY },
);

export const POST = handlers.POST;
export const GET = handlers.GET;
