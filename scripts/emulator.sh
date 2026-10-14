#!/usr/bin/env bash
# Entry point for `bun run emulator`. Everything it does lives in emulator.ts; this exists only so
# that Ctrl-C stops the emulator on the VM.
#
# Bun calls a `process.on('SIGINT', ...)` handler only when its event loop turns, so a script
# blocked in spawnSync carries on past Ctrl-C (see interruptible.sh). A shell trap runs at once, so
# the trap is here and the TypeScript arms nothing.
#
# Only `start --follow` needs it. Every other subcommand returns on its own and is exec'd, so that
# it keeps this script's exit status and signals reach it directly.
set -uo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

follows=0
sim=()
previous=
for arg in "$@"; do
  [ "$arg" = "--follow" ] && follows=1
  # The trap stops the emulator that was started, which is the sim's own: an AC run followed and
  # stopped with Ctrl-C would otherwise ask after iRacing's and leave acs.exe running.
  [ "$previous" = "--sim" ] && sim=(--sim "$arg")
  case "$arg" in --sim=*) sim=("$arg") ;; esac
  previous="$arg"
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
  # The ${sim[@]+...} form, because bash 3.2, which macOS ships, calls an empty array unbound under -u.
  bun "$here/emulator.ts" stop ${sim[@]+"${sim[@]}"}
  exit 0
}
trap cleanup INT TERM

bun "$here/emulator.ts" "$@" &
child=$!
wait "$child"
