#!/usr/bin/env bash
# Builds every release artifact from a clean state: the dashboard packages, the plugin with the
# fresh packages embedded (non-incremental, so a stale embedded copy can never survive), and the
# plugin zip. Output: build/*.simhubdash, build/manifest.json, build/OpenDash-plugin.zip.
set -euo pipefail
cd "$(dirname "$0")/.."
bun run build
rm -rf plugin/OpenDash/Resources/*.simhubdash plugin/OpenDash/Resources/fonts
# Everything except the zone faces, which are built for review and are not ready to install: a
# plugin that embedded one would put a half-finished face beside the dash a user already has. They
# are copied in by XOR-118, when they take the shipped names.
for pkg in build/*.simhubdash; do
  case "$(basename "$pkg")" in
    'openDash zones '*) continue ;;
  esac
  cp "$pkg" plugin/OpenDash/Resources/
done
cp -R build/fonts plugin/OpenDash/Resources/fonts
dotnet build plugin/OpenDash -c Release --no-incremental
bash plugin/scripts/package-plugin.sh
echo "packaged: $(ls plugin/OpenDash/Resources/*.simhubdash | wc -l) embedded of $(ls build/*.simhubdash | wc -l) built, build/OpenDash-plugin.zip"
