/**
 * The one thing the site stores: survey submissions, one PostgreSQL table through Prisma.
 *
 * The table is created on first use rather than by a migration step, because the deployment has
 * nowhere to run one: the container starts `server.js` and nothing else, and a survey with one
 * table does not justify adding a release command to it. The cost of that choice is stated in
 * prisma/schema.prisma: model and CREATE TABLE change together.
 *
 * With DATABASE_URL unset every call rejects; the API route turns that into a 503 with the
 * variable's name in it, which is the whole troubleshooting guide.
 *
 * Server-only: importing this from a client component fails the build, which is the point.
 */
import { PrismaClient } from '@prisma/client';
import type { Answers } from './survey';

export interface StoredResponse {
  id: number;
  submittedAt: string;
  answers: Answers;
}

let client: PrismaClient | undefined;
let tableReady = false;

async function db(): Promise<PrismaClient> {
  client ??= new PrismaClient();
  if (!tableReady) {
    await client.$executeRawUnsafe(`
      CREATE TABLE IF NOT EXISTS survey_response (
        id SERIAL PRIMARY KEY,
        submitted_at TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
        answers JSONB NOT NULL
      );
    `);
    tableReady = true;
  }
  return client;
}

export async function insertResponse(answers: Answers): Promise<void> {
  await (await db()).surveyResponse.create({ data: { answers } });
}

export async function listResponses(): Promise<StoredResponse[]> {
  const rows = await (await db()).surveyResponse.findMany({ orderBy: { id: 'asc' } });
  return rows.map((r) => ({ id: r.id, submittedAt: r.submittedAt.toISOString(), answers: r.answers as Answers }));
}
