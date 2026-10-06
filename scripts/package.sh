#!/usr/bin/env bash
# Builds every release artifact from a clean state: the dashboard packages, the plugin with the
# fresh packages embedded (non-incremental, so a stale embedded copy can never survive), and the
# plugin zip. Output: build/*.simhubdash, build/manifest.json, build/OpenDash-plugin.zip.
set -euo pipefail
cd "$(dirname "$0")/.."
# Every theme in the catalogue, since a themed package is embedded like the default ones and written
# only when somebody picks it (ADR 0016).
bun run build --all-themes
rm -rf plugin/OpenDash/Resources/*.simhubdash plugin/OpenDash/Resources/*.ledsprofile plugin/OpenDash/Resources/fonts plugin/OpenDash/Resources/flag-box-glyphs.json
# Everything is copied and the csproj decides what is embedded, which is how CI works too: it hands
# the whole dash artefact over. The card faces are excluded there, for the reason written there.
cp build/*.simhubdash plugin/OpenDash/Resources/
# Gzipped, keeping the .ledsprofile name. A hundred and twenty-one shapes and the flag box, a third of
# a megabyte each, is forty-four megabytes of NCalc in the assembly; the same files pack to under a
# megabyte, and FlagBoxProfile.TextOf sniffs gzip's magic so nothing else in the plugin knows the
# difference. Half of that is the far-end twins (#503). The plain files stay in build/ and are
# published nowhere: the plugin is the only way in (#438). The gzip is the script CI and a release
# run, so the three cannot compress differently (#628).
cp build/*.ledsprofile plugin/OpenDash/Resources/
bash plugin/scripts/compress-profiles.sh
# The flag box's glyphs as data, which the panel draws its previews from (#503). Plain JSON and small,
# so it is copied rather than gzipped; the csproj embeds it as OpenDash.FlagBoxGlyphs.json.
cp build/flag-box-glyphs.json plugin/OpenDash/Resources/
cp -R build/fonts plugin/OpenDash/Resources/fonts
dotnet build plugin/OpenDash -c Release --no-incremental
bash plugin/scripts/package-plugin.sh
# Counted as arrays rather than with `ls | wc -l`, which under pipefail aborts the script on a glob
# that matches nothing instead of counting it as nought (#605).
shopt -s nullglob
built=(build/*.simhubdash)
embedded=(plugin/OpenDash/Resources/*.simhubdash)
slots=(plugin/OpenDash/Resources/"OpenDash slots "*.simhubdash)
profiles=(plugin/OpenDash/Resources/*.ledsprofile)
echo "packaged: $(( ${#embedded[@]} - ${#slots[@]} )) embedded of ${#built[@]} built, ${#profiles[@]} LED profile(s), build/OpenDash-plugin.zip"
