#!/usr/bin/env bash
# compress-profiles.sh: gzips every LED profile in plugin/OpenDash/Resources in place, keeping the
# .ledsprofile name, before the plugin is built. Plain, the hundred and twenty-two profiles are
# forty-four megabytes of NCalc in the assembly; gzipped they are under one, and
# FlagBoxProfile.TextOf sniffs gzip's magic so nothing else in the plugin knows the difference.
#
# One script for every path that builds a plugin: ci.yml, release.yml and scripts/package.sh all run
# it, so the plugin CI tests is the plugin a release ships and the two cannot drift apart (#628).
# A profile that is already gzipped is left as it is, so a second run never wraps one twice.
# Takes the folder as an argument, for the test; paths are relative to the repository root.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
dir="${1:-$root/plugin/OpenDash/Resources}"

shopt -s nullglob
profiles=("$dir"/*.ledsprofile)
if (( ${#profiles[@]} == 0 )); then
  echo "compress-profiles: no .ledsprofile in $dir; copy build/*.ledsprofile there first" >&2
  exit 1
fi

compressed=0
for profile in "${profiles[@]}"; do
  if [[ "$(od -An -tx1 -N2 "$profile" | tr -d ' \n')" == "1f8b" ]]; then
    continue
  fi
  # -n: no name and no mtime in the header. The build rewrites every profile, so without it the
  # same profile gzips to new bytes on every build, and the assembly that embeds it with them (#605).
  gzip -9 -n -c "$profile" > "$profile.gz"
  mv "$profile.gz" "$profile"
  compressed=$((compressed + 1))
done
echo "compress-profiles: gzipped ${compressed} of ${#profiles[@]} LED profile(s) in $dir"
