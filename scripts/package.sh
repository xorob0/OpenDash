#!/usr/bin/env bash
# Builds every release artifact from a clean state: the dashboard packages, the plugin with the
# fresh packages embedded (non-incremental, so a stale embedded copy can never survive), and the
# plugin zip. Output: build/*.simhubdash, build/manifest.json, build/OpenDash-plugin.zip.
set -euo pipefail
cd "$(dirname "$0")/.."
bun run build
rm -rf plugin/OpenDash/Resources/*.simhubdash plugin/OpenDash/Resources/fonts
# Everything is copied and the csproj decides what is embedded, which is how CI works too: it hands
# the whole dash artefact over. The zone faces are excluded there, for the reason written there.
cp build/*.simhubdash plugin/OpenDash/Resources/
cp -R build/fonts plugin/OpenDash/Resources/fonts
dotnet build plugin/OpenDash -c Release --no-incremental
bash plugin/scripts/package-plugin.sh
embedded=$(ls plugin/OpenDash/Resources/*.simhubdash | grep -vc '/openDash zones ' || true)
echo "packaged: ${embedded} embedded of $(ls build/*.simhubdash | wc -l) built, build/OpenDash-plugin.zip"
