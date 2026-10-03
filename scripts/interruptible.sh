#!/usr/bin/env bash
# Entry point for `bun run dev`, `shots`, `record` and `modules`: runs scripts/<name>.ts, and when it
# is stopped with Ctrl-C, puts the VM back the way the script's own `finally` would have (stops the
# emulator, takes out the trace recorder after `record`, releases the claim if it is still ours).
# That is scripts/interrupted.ts; this exists only to run it.
#
# Why a shell. A Bun script with no SIGINT handler dies on the spot and skips every `finally`. One
# with a handler runs it only when its event loop next turns, and these scripts are a chain of
# spawnSync calls that never yields: Ctrl-C kills the SSH call in flight, the script carries on to
# its next step, and the handler waits for the end of the run. Measured on Bun 1.3.3 and 1.4.2. A
# shell trap runs at once.
#
# --help is exec'd, since it touches no VM. --keep is honoured: what it asks to be left is left.
set -uo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
name="${1:?usage: interruptible.sh <script> [args...]}"
shift

keep=0
for arg in "$@"; do
  case "$arg" in
    --help | -h) exec bun "$here/$name.ts" "$@" ;;
    --keep) keep=1 ;;
  esac
done

child=
cleanup() {
  trap - INT TERM
  # Ctrl-C reached the script too, so this is only for a TERM sent to this shell alone.
  [ -n "$child" ] && kill -TERM "$child" 2>/dev/null
  wait "$child" 2>/dev/null
  echo
  if [ "$keep" -eq 1 ]; then
    echo "interrupted; the VM is left as it is (--keep)"
  else
    echo "interrupted; putting the VM back (Ctrl-C again abandons that)"
    bun "$here/interrupted.ts" "$name"
  fi
  exit 130
}
trap cleanup INT TERM

# A command put in the background by a shell without job control starts with SIGINT ignored, so
# Ctrl-C would kill the SSH call in flight and leave Bun to run its next step. The subshell gives
# the script SIGINT back, so that Ctrl-C stops it where it is.
( trap - INT QUIT; exec bun "$here/$name.ts" "$@" ) &
child=$!
wait "$child"
