/**
 * The session names SimHub's readers write into `DataCorePlugin.GameData.SessionTypeName`, sim by
 * sim, as the decompiled readers write them (#1127).
 *
 * Written down here rather than read from `SESSION_NAMES`, because what the tests ask is whether that
 * list, and every box cut from it, holds what the sims write. A list the tests took from the source
 * would agree with it whatever it said, which is how the AiM drew Assetto Corsa's `TIME_ATTACK` a
 * cell wider than its box while every test passed: the source listed iRacing's words alone.
 *
 * - iRacing: `IRacingManager.GD_SessionTypeName` (ICarsReader) passes through the `SessionType` of
 *   the session the telemetry is in, or the weekend's `EventType`; the words are iRacing's.
 * - Assetto Corsa and ACC: `ACManager.GD_SessionTypeName` and `ACCManager.GD_SessionTypeName`
 *   (ACSharedMemory) write `Graphics.Session.ToString().Replace("AC_", "")`, the names of
 *   `AC_SESSION_TYPE` from `AC_UNKNOWN` (-1) to `AC_DRAG` (6). ACC's shared memory goes on to 7, its
 *   hot stint, and 8, its superpole hot lap, which the enum does not name, so `ToString` writes the
 *   number.
 * - Le Mans Ultimate: `LMUManager.GD_SessionTypeName` (RfactorReader) maps `scoringInfo.mSession`, 0
 *   to `Test day`, 1 to 4 to `Practice`, 5 to 8 to `Qualify`, 9 to `Warmup` and 10 to 13 to `Race`.
 */
export const WRITTEN_SESSION_NAMES: Readonly<Record<string, readonly string[]>> = {
  'iRacing': ['Offline Testing', 'Practice', 'Open Practice', 'Lone Practice', 'Open Qualify', 'Lone Qualify', 'Qualify', 'Warmup', 'Heat', 'Consolation', 'Race', 'Test', 'Time Trial'],
  'Assetto Corsa': ['PRACTICE', 'QUALIFY', 'RACE', 'HOTLAP', 'TIME_ATTACK', 'DRIFT', 'DRAG', 'UNKNOWN'],
  'ACC': ['PRACTICE', 'QUALIFY', 'RACE', 'HOTLAP', 'TIME_ATTACK', 'DRIFT', 'DRAG', 'UNKNOWN', '7', '8'],
  'Le Mans Ultimate': ['Test day', 'Practice', 'Qualify', 'Warmup', 'Race'],
};
