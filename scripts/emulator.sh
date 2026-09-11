#!/usr/bin/env bash
# Entry point for `bun run emulator`. Everything it does lives in emulator.ts; this exists only so
# that Ctrl-C stops the emulator on the VM.
#
# On Bun 1.3.3, `process.on('SIGINT', ...)` registers a handler that is never called, and merely
# registering it suppresses the default action, so a follow loop that armed one could not be
# stopped at all. A shell trap does run, so the trap is here and the TypeScript arms nothing.
#
# Only `start --follow` needs it. Every other subcommand returns on its own and is exec'd, so that
# it keeps this script's exit status and signals reach it directly.
set -uo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

follows=0
for arg in "$@"; do
  [ "$arg" = "--follow" ] && follows=1
done

if [ "${1:-}" != "start" ] || [ "$follows" -eq 0 ]; then
  exec bun "$here/emulator.ts" "$@"
fi

child=
cleanup() {
  trap - INT TERM
  # Bun does terminate on SIGTERM, which is how the follow loop is ended; it is the handler that
  # never runs, not the signal that never arrives.
  [ -n "$child" ] && kill -TERM "$child" 2>/dev/null
  wait "$child" 2>/dev/null
  echo
  echo "stopping the emulator"
  bun "$here/emulator.ts" stop
  exit 0
}
trap cleanup INT TERM

bun "$here/emulator.ts" "$@" &
child=$!
wait "$child"
