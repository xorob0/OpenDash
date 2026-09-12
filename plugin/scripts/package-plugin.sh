#!/usr/bin/env bash
# package-plugin.sh: zips the Release build of the plugin and INSTALL.md into build/OpenDash-plugin.zip, the file a
# user downloads (INSTALL.md describes its contents). Run after `dotnet build plugin/OpenDash -c Release`; see
# plugin/README.md. Paths are relative to the repository root, so it works from any directory.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
dll="$root/plugin/OpenDash/bin/Release/net48/OpenDash.dll"
install_md="$root/plugin/INSTALL.md"
# The panel draws in Barlow and the DLL embeds the faces, so the zip is a redistribution of them
# and owes the OFL notice. It is embedded in the DLL too, where nobody can read it.
licence="$root/packages/dash/fonts/OFL.txt"
out_dir="$root/build"
out="$out_dir/OpenDash-plugin.zip"

if [[ ! -f "$dll" ]]; then
  echo "package-plugin: $dll is missing; run: dotnet build plugin/OpenDash -c Release" >&2
  exit 1
fi
if [[ ! -f "$install_md" ]]; then
  echo "package-plugin: $install_md is missing" >&2
  exit 1
fi
if [[ ! -f "$licence" ]]; then
  echo "package-plugin: $licence is missing; the zip may not ship without it" >&2
  exit 1
fi
if ! command -v zip >/dev/null 2>&1; then
  echo "package-plugin: zip is not installed" >&2
  exit 1
fi

# The dashboard package is an embedded resource whose name ends in .simhubdash; the manifest stores that name as
# plain UTF-8, so its absence from the binary means the plugin was built without a dashboard (Resources/README.md).
if ! grep -a -q 'simhubdash' "$dll"; then
  echo "package-plugin: warning: OpenDash.dll carries no .simhubdash; copy build/*.simhubdash into plugin/OpenDash/Resources/ and rebuild" >&2
fi

mkdir -p "$out_dir"
rm -f "$out"
zip -j -q "$out" "$dll" "$install_md" "$licence"
echo "Wrote $out"
unzip -l "$out"
