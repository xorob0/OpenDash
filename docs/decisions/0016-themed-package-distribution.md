# ADR 0016: How themed packages reach the user

**Date:** 2026-10-06
**Status:** Accepted. The record [#197](https://github.com/xorob0/OpenDash/issues/197) asked for, and
the decision behind the picker ([#198](https://github.com/xorob0/OpenDash/issues/198)) and the per-car
playlist ([#199](https://github.com/xorob0/OpenDash/issues/199)). Takes the multiplication that
[ADR 0015](0015-car-themes.md) acknowledges, and needs no amendment to
[ADR 0012](0012-update-checks.md), since nothing here is fetched.

## Context

#197 was written when the plugin wrote every package it embeds on every start, and its premise was
that several hundred themed packages could be neither embedded nor installed. Two things have moved
since, and both change the answer.

**A rig holds only what its owner added.** Since [ADR 0017](0017-a-screen-is-an-instance.md) the
installer writes the folders of the screens on the rig and no others, and an empty rig, which is a new
user, correctly installs nothing (the `Rig` remarks in `plugin/OpenDash/DashboardInstaller.Core.cs`). A
screen is added from the Add sheet, which asks the kind and then the size
(`plugin/OpenDash/PanelAddScreen.cs`) and writes the screen's folder from the package the assembly
embeds, through the same routine a start and a reinstall use (`plugin/OpenDash/ScreenInstaller.cs`).
So the fear that a themed package would appear in every user's dashboard list no longer follows from
carrying it: carrying a package and writing it have been two different acts since ADR 0017.

**The plugin is the only way in.** Since #438 a release publishes `OpenDash-plugin.zip` and nothing
else (`.github/workflows/release.yml`), and every dashboard reaches SimHub from inside `OpenDash.dll`.
The build still writes one `.simhubdash` per package into `build/`, and the plugin embeds every one of
them except the slots faces (`plugin/OpenDash/OpenDash.csproj`, the `EmbeddedResource` for
`Resources/*.simhubdash`).

What the DLL carries today was measured for this record on `main` at `d984033e`, with `bun run build`
followed by the release's own `compress-profiles.sh` and `dotnet build plugin/OpenDash -c Release`:

| | bytes |
|---|---|
| `OpenDash.dll`, Release | 9,816,064 |
| the fourteen embedded packages | 6,491,953 |
| the 158 LED profiles, gzipped | 1,628,570 |
| the fonts | 637,288 |
| one face package, `OpenDash 1280x480.simhubdash` | 492,632 |
| of which its fonts, compressed | 264,519 |
| of which its scene graphs, compressed | 222,293 |

A theme claiming the seven or eight rectangular faces is therefore between 3.4 and 3.9 MB, and a little
more than half of each of its packages is a copy of fonts that every other package also carries.

## What was decided against

**Downloading on demand from the release.** It is the answer that scales without limit, and it is the
one #197 expected. It is nevertheless rejected for now, for three reasons. The release no longer
attaches any package since #438, so it would mean publishing the themed packages again as release
assets, beside a plugin that is otherwise the only thing a release hands over. It would also make the
Add sheet depend on the network, so that a rig without one could add a default screen and not a
themed one, which is a distinction a driver has no reason to understand. Lastly, the request for a
named package tells GitHub which theme the user picked, which is information about the user rather
than about the release, and ADR 0012 says plainly that anything fetched per user rather than per
release is a new decision rather than an extension of it.

**A separate download the user installs.** It reopens the second way in that #438 closed on purpose,
with its second writer into `DashFonts` and its support conversation that begins by establishing which
route a user took.

**Embedding every themed package and writing all of them.** It is what the plugin did before ADR 0017
and what ADR 0017 ended, and with themes it would put several entries per car into the dashboard list
of every user, most of whom drive none of those cars.

## Decision

**Themed packages are embedded in `OpenDash.dll` like the default ones, and the plugin's Add sheet
writes the one the user picks. Nothing is installed unless it is picked, and a fresh install puts
exactly the default packages in SimHub's list, which means exactly what it puts there today.**

That last clause needs to be stated in numbers, since #197's acceptance asks for a count and the count
it expected, fourteen, has been wrong since ADR 0017. A fresh install writes no dashboard at all until
the driver adds a screen, and each screen added writes one folder. Themes change neither figure: the
Add sheet offers a themed package beside the default one of a size, and a driver who never chooses a
theme has exactly the folders, the titles and the bytes they would have had without themes, which ADR
0015's byte-identical requirement guarantees for the default packages themselves.

**With no network, nothing changes.** Nothing is fetched, so the Add sheet offers and writes a themed
package on a rig with no network exactly as it writes a default one, and there is no state to report.

**The threshold, and what comes after it.** Embedding stays acceptable while `OpenDash.dll` is under
**25 MB**, which is two and a half times its size today and leaves room for about four themes at
every size they claim. The figure is chosen against two facts rather than derived from a limit: the
DLL is the file every update downloads and every first install copies by hand into SimHub's folder
(`plugin/INSTALL.md`), and the project has already judged a heavier assembly not worth carrying, when
the LED profiles were gzipped because plain they were forty-four megabytes of NCalc
(`plugin/scripts/compress-profiles.sh`). When the next theme would take the DLL past it, the path is
download on demand under ADR 0012's rules, with the amendment that ADR 0012 then requires saying what
the request discloses. Before that, sharing the fonts across packages rather than repeating them in
each would roughly halve what a theme weighs; it is worth measuring when the threshold comes near, and
it is not decided here.

### On disk, and in SimHub's list

**A themed package is `DashTemplates/OpenDash <Theme> <W>x<H>`**, which for the Porsche at its
reference size is `OpenDash Porsche 1280x480`. It follows the default packages' convention exactly as
the code spells it: the product name `OpenDash`, a space, then what distinguishes the package, as in
`OpenDash 1280x480` (`PackageCatalogue` in `plugin/OpenDash/PackageCatalogue.cs`). #197 assumed a
folder spelled with a small o; the code spells it `OpenDash` throughout, and the themed folders follow
the code. A themed package always carries its size, at 1920 x 480 too, where the default package is the
bare `OpenDash` (`PackageCatalogue.PrimaryFolder`, "the one package whose name carries no size"),
since that exception is a fact of history and a second theme at that size would otherwise have no
name to take.

The folder is the package's stock folder in ADR 0017's sense, and the rest of that record applies
unchanged. A themed package reads the stock namespace of its size, as the default package of that size
does, since a theme changes the register and never the contract; the first screen of a size on a rig
takes the stock namespace and the folder as built, and a second screen of that size, themed or not,
takes a namespace and a folder of its own, rewritten on the way in. SimHub lists a dashboard by the
`Title` the installer writes into it, which is the screen's name (`ScreenInstaller.TargetFor`), so the
name the Add sheet proposes for a themed screen carries the theme's name; how it is worded is #198's.

### Versions and updates

**Versioning is the comparison the default packages already have, because the copy is embedded.** Each
package carries its `DashboardVersion` in its `.djson.metadata`, and the installer compares the embedded
version with the installed one (`Versioning.Decide` in `plugin/OpenDash/Versioning.cs`) and rewrites a
folder that is missing or older, holding back one whose fingerprint shows somebody has edited it
(`PackageStatus.Edited`, `plugin/OpenDash/FolderFingerprint.cs`). A themed screen is updated by the
plugin update that carries its new version, on the start after the swap, exactly as the dashboards
follow the plugin under the last amendment to ADR 0012.

### Removal

**Removing a themed package is asked about and never assumed**, which is the rule
[ADR 0013](0013-lighting-hardware.md) holds for a profile on a device and ADR 0017 holds for a
screen. A themed screen is removed as any screen is, from the panel, which says what goes with it
before it deletes the folder (`ScreenInstaller.Remove`). The plugin never deletes a folder because a
theme is no longer wanted, and never because a later build no longer carries it.

That second case needs a rule the code does not have yet. A screen whose package has gone from the
build falls back to the package of its kind and size (`ScreenInstaller.PackageNameFor`), which for a
themed screen would rewrite its folder from the default package of the same size at the next start,
silently replacing the car's face with the house's. A themed screen whose package is no longer carried
must instead keep its folder untouched and read as unavailable, which is the behaviour ADR 0017 already
asks for a screen whose size stops shipping; #198 owes it.

### What the release carries

**Nothing changes on the release page.** The build writes one `.simhubdash` per package, themed ones
included, and the plugin embeds them; the release publishes `OpenDash-plugin.zip` alone, as it has
since #438. An archive of themed packages was one of #197's candidates and is not needed, since no
package is published on its own.

### The playlist, as the companion decision

When a themed package is installed, the plugin also writes SimHub's per-car playlist entry for the cars
the theme is drawn for, so that picking the Porsche on the Add sheet and the display following the car
are one act rather than two. It writes only its own entries, never touches an entry it did not write,
and a car with no theme installed falls back to the default face. How a car is identified to SimHub,
and how the entry is written through a supported path, are #199's, and are not decided here.

## The retired #764

#197 anticipated that a build of the user's own tokens through CI
([#764](https://github.com/xorob0/OpenDash/issues/764)) and a matrix of themes would share one path.
#764 was closed on 2026-09-22 as personalisation nobody had asked for, so there is nothing to share
today. Should it reopen, the shape is now plain: a user's token set is a theme whose overlay is theirs,
built by the same path as every theme ([#201](https://github.com/xorob0/OpenDash/issues/201)), and
distributed differently, since a package built for one person is a CI artefact for that person and has
no place in an assembly every user downloads.

## Consequences

### Good

A themed face reaches a driver with one choice on a sheet they already know, on any rig, with or without
a network, and its update needs nothing beyond the plugin update every rig already takes. Nothing new
leaves the user's machine. The mechanisms are the ones the default packages already prove: the same
extraction, the same version comparison, the same hold-back of an edited folder and the same question
before a removal.

### Bad

Every user downloads every theme, including the users who will never drive any of the cars, and the
cost grows with each theme until the threshold above. The DLL is the one file a release hands over, so
its weight is felt at every update rather than once.

The threshold is a judgement rather than a measurement of harm, and nothing in this record shows what
a 25 MB assembly costs SimHub at load. Whoever approaches it should measure that before relying on the
figure.

### Unresolved

**What the Add sheet's question looks like with themes**, whether the theme is a third question after
the kind and the size or a choice within the size, is #198's, and so is the wording of the name it
proposes.

**How SimHub identifies a car to its playlist, and whether an entry can be written through a supported
path**, is #199's. If it cannot, #199 already says that writing nothing and telling the driver how to
bind it is better than guessing, and this record agrees.
