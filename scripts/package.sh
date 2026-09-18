#!/usr/bin/env bash
# Builds every release artifact from a clean state: the dashboard packages, the plugin with the
# fresh packages embedded (non-incremental, so a stale embedded copy can never survive), and the
# plugin zip. Output: build/*.simhubdash, build/manifest.json, build/OpenDash-plugin.zip.
set -euo pipefail
cd "$(dirname "$0")/.."
bun run build
rm -rf plugin/OpenDash/Resources/*.simhubdash plugin/OpenDash/Resources/*.ledsprofile plugin/OpenDash/Resources/fonts
# Everything is copied and the csproj decides what is embedded, which is how CI works too: it hands
# the whole dash artefact over. The card faces are excluded there, for the reason written there.
cp build/*.simhubdash plugin/OpenDash/Resources/
# Gzipped, keeping the .ledsprofile name. Sixty-three shapes of a third of a megabyte each is twenty
# megabytes of NCalc in the assembly; the same files pack to about five hundred kilobytes, and
# FlagBoxProfile.TextOf sniffs gzip's magic so nothing else in the plugin knows the difference. The
# release still carries the plain files from build/, which is what somebody importing one by hand gets.
for profile in build/*.ledsprofile; do
  gzip -9 -c "$profile" > "plugin/OpenDash/Resources/$(basename "$profile")"
done
cp -R build/fonts plugin/OpenDash/Resources/fonts
dotnet build plugin/OpenDash -c Release --no-incremental
bash plugin/scripts/package-plugin.sh
embedded=$(ls plugin/OpenDash/Resources/*.simhubdash | grep -vc '/openDash slots ' || true)
profiles=$(ls plugin/OpenDash/Resources/*.ledsprofile | wc -l)
echo "packaged: ${embedded} embedded of $(ls build/*.simhubdash | wc -l) built, ${profiles} LED profile(s), build/OpenDash-plugin.zip"
