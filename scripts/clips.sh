#!/usr/bin/env bash
# Entry point for `bun run clips`. Everything it does lives in clips.ts; this exists only so that
# Ctrl-C stops the emulator and releases the VM, since Bun never delivers SIGINT to a handler (see
# emulator.sh). --encode-only touches no VM and is exec'd directly.
set -uo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

for arg in "$@"; do
  if [ "$arg" = "--encode-only" ] || [ "$arg" = "--help" ] || [ "$arg" = "-h" ]; then
    exec bun "$here/clips.ts" "$@"
  fi
done

child=
cleanup() {
  trap - INT TERM
  [ -n "$child" ] && kill -TERM "$child" 2>/dev/null
  wait "$child" 2>/dev/null
  echo
  echo "stopping the emulator and releasing the VM"
  bun "$here/emulator.ts" stop
  bun "$here/vm.ts" release
  exit 130
}
trap cleanup INT TERM

bun "$here/clips.ts" "$@" &
child=$!
wait "$child"
