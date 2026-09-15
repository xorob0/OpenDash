# The backlog

The work is tracked as **GitHub issues** on `xorob0/OpenDash`.
Every ticket carries the wave label it was written under and the milestone that holds its
project.

Before 2026-09-15 the backlog lived in Linear, in team Xorob, and a ticket was cited as `XOR-123`.
The free tier stopped accepting new issues, so all 271 of them moved here. Nothing was
rewritten in the move: a body reads as it read in Linear, with its references renumbered.

**Reading anything written before the move.** Commit messages, branch names, pull request
descriptions and old review comments cite `XOR-` numbers, and git history is not rewritten. The
table below is the lookup. In the working tree every reference was renumbered, so a `#` number in a
comment or a document is a GitHub issue.

## What moved and what did not

| | |
|---|---|
| Migrated | 271 tickets, XOR-5 to XOR-275 |
| Done | 70 |
| Backlog | 184 |
| In Progress | 2 |
| Canceled | 13 |
| Duplicate | 2 |
| Not migrated | XOR-1 to XOR-4 — Linear's own onboarding boilerplate, canceled and never ours |

Three things changed shape on the way across:

* Linear **projects** became GitHub **milestones**, because a milestone is the one grouping GitHub
  offers that an issue carries on its own, without the board.
* Linear **status** became open or closed plus a Status column on the board. Done closes as
  completed; Canceled and Duplicate close as not planned. It is deliberately not a label as well:
  one fact, one home.
* One label was too long for GitHub, whose limit is fifty characters:
  `wave-07-alerts-and-the-screens-that-are-not-the-face` is `wave-07-alerts-and-screens-not-the-face` here.

## The readability pass came with it

`docs/design/readability-pass.md` held twenty-three ticket bodies that were never filed, because Linear refused the creates. Its own
header asked for them to be moved across and the file deleted, which is what happened:
[#327](https://github.com/xorob0/OpenDash/issues/327) is the parent and
[#328](https://github.com/xorob0/OpenDash/issues/328) to
[#349](https://github.com/xorob0/OpenDash/issues/349) are the two mechanisms and the twenty pages.
They carry the `readability-pass` label.

## XOR to issue

| Linear | Issue | Status | Title |
|---|---|---|---|
| XOR-5 | [#56](https://github.com/xorob0/OpenDash/issues/56) | Done | Shipped: the MVP face, thirteen cards, the plugin and the settings contract |
| XOR-6 | [#57](https://github.com/xorob0/OpenDash/issues/57) | Done | Shipped: nine further DDU sizes from one layout file each |
| XOR-7 | [#58](https://github.com/xorob0/OpenDash/issues/58) | Done | Shipped: the generator learns the second-screen item kinds |
| XOR-8 | [#59](https://github.com/xorob0/OpenDash/issues/59) | Done | Shipped: the companion and the pit wall, four packages from 21 modules |
| XOR-9 | [#60](https://github.com/xorob0/OpenDash/issues/60) | Done | Shipped: the iRacing telemetry emulator and the Windows test VM |
| XOR-10 | [#61](https://github.com/xorob0/OpenDash/issues/61) | Done | Shipped: CI builds both artifacts, a tag publishes a release |
| XOR-11 | [#62](https://github.com/xorob0/OpenDash/issues/62) | Done | bun run dev: one command brings the whole rig up |
| XOR-12 | [#63](https://github.com/xorob0/OpenDash/issues/63) | Done | VM and SimHub control from a script, not from prose |
| XOR-13 | [#64](https://github.com/xorob0/OpenDash/issues/64) | Done | Emulator runner with a single-writer lock |
| XOR-14 | [#65](https://github.com/xorob0/OpenDash/issues/65) | Backlog | Live reload: a save reaches the screen without a command |
| XOR-15 | [#66](https://github.com/xorob0/OpenDash/issues/66) | Done | Scenarios: four states that hold still long enough to photograph |
| XOR-16 | [#67](https://github.com/xorob0/OpenDash/issues/67) | Backlog | Drive a scenario while watching it: seek, pause, jump to an event |
| XOR-17 | [#68](https://github.com/xorob0/OpenDash/issues/68) | Backlog | Put a zone on a chosen page from the dev loop |
| XOR-18 | [#69](https://github.com/xorob0/OpenDash/issues/69) | Done | ADR 0008: how a pull request renders a dash |
| XOR-19 | [#70](https://github.com/xorob0/OpenDash/issues/70) | Done | Telemetry traces: record a scenario once, replay it anywhere |
| XOR-20 | [#71](https://github.com/xorob0/OpenDash/issues/71) | Backlog | Evaluate our own bindings: an NCalc subset in TypeScript |
| XOR-21 | [#72](https://github.com/xorob0/OpenDash/issues/72) | Backlog | Preview renderer: the scene graph on a web page |
| XOR-22 | [#73](https://github.com/xorob0/OpenDash/issues/73) | Done | Every pull request carries a video of what it changed |
| XOR-23 | [#74](https://github.com/xorob0/OpenDash/issues/74) | Backlog | Pixel goldens: fail when a layout moves and nobody meant it to |
| XOR-24 | [#75](https://github.com/xorob0/OpenDash/issues/75) | Backlog | Fidelity check: prove the preview is not lying |
| XOR-25 | [#76](https://github.com/xorob0/OpenDash/issues/76) | Done | A changelog a user can read, grouped by what it means to them |
| XOR-26 | [#77](https://github.com/xorob0/OpenDash/issues/77) | Backlog | One definition of the build, used by both workflows |
| XOR-27 | [#78](https://github.com/xorob0/OpenDash/issues/78) | Backlog | One command bumps the version, and CI checks it on a pull request |
| XOR-28 | [#79](https://github.com/xorob0/OpenDash/issues/79) | Backlog | The release shows the dashboards moving |
| XOR-29 | [#80](https://github.com/xorob0/OpenDash/issues/80) | Done | ADR: update checks, and what leaves the user's machine |
| XOR-30 | [#81](https://github.com/xorob0/OpenDash/issues/81) | Done | Publish the build manifest with a release, for the consumers that still need one |
| XOR-31 | [#82](https://github.com/xorob0/OpenDash/issues/82) | Done | The plugin says when a newer version exists |
| XOR-32 | [#83](https://github.com/xorob0/OpenDash/issues/83) | Backlog | An update is available: say so on the idle screen |
| XOR-33 | [#84](https://github.com/xorob0/OpenDash/issues/84) | Backlog | Dashboard manager: browse, install and remove packages from inside SimHub |
| XOR-34 | [#85](https://github.com/xorob0/OpenDash/issues/85) | Backlog | First run: the empty rig teaches, instead of a wizard in front of it |
| XOR-35 | [#86](https://github.com/xorob0/OpenDash/issues/86) | Backlog | A website: screenshots, videos, and the right download |
| XOR-36 | [#87](https://github.com/xorob0/OpenDash/issues/87) | Canceled | Promote the second-screen parts into components a card can use |
| XOR-37 | [#88](https://github.com/xorob0/OpenDash/issues/88) | Canceled | Cards: relative ahead, relative behind, leaderboard |
| XOR-38 | [#89](https://github.com/xorob0/OpenDash/issues/89) | Canceled | Cards: track map and radar |
| XOR-39 | [#90](https://github.com/xorob0/OpenDash/issues/90) | Canceled | Cards: sectors and lap history |
| XOR-40 | [#91](https://github.com/xorob0/OpenDash/issues/91) | Canceled | Cards: tyre wear and brake temperatures |
| XOR-41 | [#92](https://github.com/xorob0/OpenDash/issues/92) | Canceled | Cards: inputs, car settings, DRS or push to pass |
| XOR-42 | [#93](https://github.com/xorob0/OpenDash/issues/93) | Canceled | Cards: weather and clock |
| XOR-43 | [#94](https://github.com/xorob0/OpenDash/issues/94) | Canceled | Cards that need openDash to compute: fuel calculator, stint, energy, damage |
| XOR-44 | [#95](https://github.com/xorob0/OpenDash/issues/95) | Canceled | Card: launch, for the start and the pit exit |
| XOR-45 | [#96](https://github.com/xorob0/OpenDash/issues/96) | Backlog | The rev bar in its well, and spotter indicators at its ends |
| XOR-46 | [#97](https://github.com/xorob0/OpenDash/issues/97) | Canceled | Slot presets: a named layout per discipline, and per car |
| XOR-47 | [#98](https://github.com/xorob0/OpenDash/issues/98) | Done | ADR 0009: does the plugin compute? |
| XOR-48 | [#99](https://github.com/xorob0/OpenDash/issues/99) | Backlog | Theme presets, and which one a user starts from |
| XOR-49 | [#100](https://github.com/xorob0/OpenDash/issues/100) | Canceled | Wheel and button control: name the actions a driver can bind |
| XOR-50 | [#101](https://github.com/xorob0/OpenDash/issues/101) | Canceled | Peek: hold a button to glance at a page, release to return |
| XOR-51 | [#102](https://github.com/xorob0/OpenDash/issues/102) | Backlog | Which sim shows what: audit the bindings and publish the table |
| XOR-52 | [#103](https://github.com/xorob0/OpenDash/issues/103) | Backlog | LED profiles that match the dash |
| XOR-53 | [#104](https://github.com/xorob0/OpenDash/issues/104) | Backlog | The idle screen is fully the user's |
| XOR-54 | [#105](https://github.com/xorob0/OpenDash/issues/105) | Backlog | Can a dashboard item write a property? |
| XOR-55 | [#106](https://github.com/xorob0/OpenDash/issues/106) | Canceled | Teammate telemetry for endurance stints |
| XOR-56 | [#107](https://github.com/xorob0/OpenDash/issues/107) | Backlog | ADR 0010: where alert priority is decided |
| XOR-57 | [#108](https://github.com/xorob0/OpenDash/issues/108) | Backlog | Flag formats: compact and full screen, chosen from the plugin |
| XOR-58 | [#109](https://github.com/xorob0/OpenDash/issues/109) | Backlog | The alert catalogue, in priority order |
| XOR-59 | [#110](https://github.com/xorob0/OpenDash/issues/110) | Backlog | Car state and pit alerts |
| XOR-60 | [#111](https://github.com/xorob0/OpenDash/issues/111) | Backlog | Change notifications: the dash tells you what you just turned |
| XOR-61 | [#112](https://github.com/xorob0/OpenDash/issues/112) | Backlog | Pop-ups: the lap at the line, a sector delta, low fuel, a penalty |
| XOR-62 | [#113](https://github.com/xorob0/OpenDash/issues/113) | Backlog | The idle screen: what the DDU shows when no game is running |
| XOR-63 | [#114](https://github.com/xorob0/OpenDash/issues/114) | Backlog | Screen care: dimming and pixel shift on a screen left on for hours |
| XOR-64 | [#115](https://github.com/xorob0/OpenDash/issues/115) | Backlog | The pit screen, and the lap review |
| XOR-65 | [#116](https://github.com/xorob0/OpenDash/issues/116) | Backlog | A flags screen for a second display |
| XOR-66 | [#117](https://github.com/xorob0/OpenDash/issues/117) | Done | The scope document no longer describes the product |
| XOR-67 | [#118](https://github.com/xorob0/OpenDash/issues/118) | Backlog | ADR: the second screens, written down after the fact |
| XOR-68 | [#119](https://github.com/xorob0/OpenDash/issues/119) | Backlog | Make the house rules into checks |
| XOR-69 | [#120](https://github.com/xorob0/OpenDash/issues/120) | Backlog | How many items is too many, and what happens then |
| XOR-70 | [#121](https://github.com/xorob0/OpenDash/issues/121) | Backlog | Let the generator stand on its own |
| XOR-71 | [#122](https://github.com/xorob0/OpenDash/issues/122) | Done | Templates and a definition of done for a pull request |
| XOR-72 | [#123](https://github.com/xorob0/OpenDash/issues/123) | Backlog | A snapshot per page, small enough to read |
| XOR-73 | [#124](https://github.com/xorob0/OpenDash/issues/124) | Done | ADR 0011: how far personalisation reaches into a generated package |
| XOR-74 | [#125](https://github.com/xorob0/OpenDash/issues/125) | Backlog | Frames: borders on or off, their colour, thickness and corner radius |
| XOR-75 | [#126](https://github.com/xorob0/OpenDash/issues/126) | Backlog | Typeface: a curated set, each one proven to fit |
| XOR-76 | [#127](https://github.com/xorob0/OpenDash/issues/127) | Backlog | Colour: every token the user's, with one rule that stays |
| XOR-77 | [#128](https://github.com/xorob0/OpenDash/issues/128) | Backlog | Night mode: dimmer, warmer, and switching by itself |
| XOR-78 | [#129](https://github.com/xorob0/OpenDash/issues/129) | Backlog | Colour vision: presets that work, and meaning that is not only colour |
| XOR-79 | [#130](https://github.com/xorob0/OpenDash/issues/130) | Backlog | Rewrite the brand document as the default, not the law |
| XOR-80 | [#131](https://github.com/xorob0/OpenDash/issues/131) | Backlog | Advanced: edit any token, at whichever layer you mean it |
| XOR-81 | [#132](https://github.com/xorob0/OpenDash/issues/132) | Backlog | Build your own openDash: a token set through CI |
| XOR-82 | [#133](https://github.com/xorob0/OpenDash/issues/133) | Done | Pit wall header: a bound label is boxed from its sample, so the real value clips |
| XOR-83 | [#134](https://github.com/xorob0/OpenDash/issues/134) | Done | ncalc.left() is not a SimHub function, so the chips draw nothing |
| XOR-84 | [#135](https://github.com/xorob0/OpenDash/issues/135) | Done | The settings contract gains zones, and keeps its slots until the cards go |
| XOR-85 | [#136](https://github.com/xorob0/OpenDash/issues/136) | Done | The face is zones: the rev bar, the bar, B \| A \| C and band D, at 1920 by 480 |
| XOR-86 | [#137](https://github.com/xorob0/OpenDash/issues/137) | Done | A page answers to the shape of its zone, not to its width |
| XOR-87 | [#138](https://github.com/xorob0/OpenDash/issues/138) | Done | The token groups the new face draws are not yet in ds |
| XOR-88 | [#139](https://github.com/xorob0/OpenDash/issues/139) | Done | Zones B and C: the twenty-one pages reach the face |
| XOR-89 | [#140](https://github.com/xorob0/OpenDash/issues/140) | Backlog | Zone A: the gear, the speed and the track, in four pages |
| XOR-90 | [#141](https://github.com/xorob0/OpenDash/issues/141) | Backlog | Band D: the pages, two corner blocks, and the flag that takes it over |
| XOR-91 | [#142](https://github.com/xorob0/OpenDash/issues/142) | Done | The bar: the settled values, and the strip that hides what the game does not expose |
| XOR-92 | [#143](https://github.com/xorob0/OpenDash/issues/143) | Done | The plugin panel: four zones and a bar in place of twelve dropdowns |
| XOR-93 | [#144](https://github.com/xorob0/OpenDash/issues/144) | Backlog | Wheel actions: a button per zone, per face, and one held for a glance |
| XOR-94 | [#145](https://github.com/xorob0/OpenDash/issues/145) | Backlog | What a round face does with zones is not yet decided |
| XOR-95 | [#146](https://github.com/xorob0/OpenDash/issues/146) | Backlog | Retire the card path now the zones carry the face |
| XOR-96 | [#147](https://github.com/xorob0/OpenDash/issues/147) | Backlog | The drawn objects: the tyre, the brake disc, the car and the steering |
| XOR-97 | [#148](https://github.com/xorob0/OpenDash/issues/148) | Backlog | Telltales: the ISO 2575 set, and the licence that travels with it |
| XOR-98 | [#149](https://github.com/xorob0/OpenDash/issues/149) | Backlog | Driver rows: the nationality flag, the licence badge and the rating |
| XOR-99 | [#150](https://github.com/xorob0/OpenDash/issues/150) | Done | Three documents still teach the twelve-slot face, and one of them is user-facing |
| XOR-100 | [#151](https://github.com/xorob0/OpenDash/issues/151) | Backlog | second/ is no longer second: the shared parts need a neutral home |
| XOR-101 | [#152](https://github.com/xorob0/OpenDash/issues/152) | Backlog | A launch page, for the start and the pit exit |
| XOR-102 | [#153](https://github.com/xorob0/OpenDash/issues/153) | Done | Prove what a zone shows, and not only where it draws |
| XOR-103 | [#154](https://github.com/xorob0/OpenDash/issues/154) | Backlog | Zone A has no header, so nothing says which of its four pages is showing |
| XOR-104 | [#155](https://github.com/xorob0/OpenDash/issues/155) | Done | What a rank does when a field will not fit, or is not there at all |
| XOR-105 | [#156](https://github.com/xorob0/OpenDash/issues/156) | Backlog | The plugin updates itself, which is harder than updating a dashboard |
| XOR-106 | [#157](https://github.com/xorob0/OpenDash/issues/157) | Done | One click updates an installed dashboard when the release carries a newer one |
| XOR-107 | [#158](https://github.com/xorob0/OpenDash/issues/158) | Done | A monospaced value may only contain glyphs that fit its cell |
| XOR-108 | [#159](https://github.com/xorob0/OpenDash/issues/159) | Done | SimHub does not render the dash in Barlow Condensed: WPF folds it into the Barlow family |
| XOR-109 | [#160](https://github.com/xorob0/OpenDash/issues/160) | Done | The canvas files in design/ describe the slot model the canvas abandoned |
| XOR-110 | [#161](https://github.com/xorob0/OpenDash/issues/161) | Done | Nine tickets cite docs/design/zones.md, and nobody has written it |
| XOR-111 | [#162](https://github.com/xorob0/OpenDash/issues/162) | Done | design/tokens.json is at 0.3.0 and the canvas is at 0.9.2 |
| XOR-112 | [#163](https://github.com/xorob0/OpenDash/issues/163) | Done | One command captures every package on the VM |
| XOR-113 | [#164](https://github.com/xorob0/OpenDash/issues/164) | Done | The boxes secondScreens.test.ts measures are not the boxes the build gives a module |
| XOR-114 | [#165](https://github.com/xorob0/OpenDash/issues/165) | Backlog | Nothing in scripts/ can press a wheel button |
| XOR-115 | [#166](https://github.com/xorob0/OpenDash/issues/166) | Done | SimHub cannot draw an SVG, so the generator needs an image item |
| XOR-116 | [#167](https://github.com/xorob0/OpenDash/issues/167) | Backlog | Four tickets need to show something for three seconds, and the format gives only blink |
| XOR-117 | [#168](https://github.com/xorob0/OpenDash/issues/168) | Done | Every rectangular face is the same five parts |
| XOR-118 | [#169](https://github.com/xorob0/OpenDash/issues/169) | Done | The zone face takes the name openDash |
| XOR-119 | [#170](https://github.com/xorob0/OpenDash/issues/170) | Backlog | An rc.2 user's twelve slot properties cannot vanish without a release of warning |
| XOR-120 | [#171](https://github.com/xorob0/OpenDash/issues/171) | Backlog | Scenarios for behaviour, not for photographs: wet, endurance, the start and the edges |
| XOR-121 | [#172](https://github.com/xorob0/OpenDash/issues/172) | Backlog | A driver name longer than its column has nowhere to go |
| XOR-122 | [#173](https://github.com/xorob0/OpenDash/issues/173) | Done | VersionCompare orders undotted pre-release numbers as text, so rc10 reads as older than rc2 |
| XOR-123 | [#174](https://github.com/xorob0/OpenDash/issues/174) | Done | A released plugin embeds the zone faces that scripts/package.sh excludes |
| XOR-124 | [#175](https://github.com/xorob0/OpenDash/issues/175) | Done | A screen owns its settings: one group per screen, and a package that reads only its own |
| XOR-125 | [#176](https://github.com/xorob0/OpenDash/issues/176) | Done | The settings panel becomes three tabs around a screen |
| XOR-126 | [#177](https://github.com/xorob0/OpenDash/issues/177) | Done | Closing SimHub during an update can leave a dashboard folder half replaced |
| XOR-127 | [#178](https://github.com/xorob0/OpenDash/issues/178) | Done | A user who has edited a dashboard has no way to put the shipped one back |
| XOR-128 | [#179](https://github.com/xorob0/OpenDash/issues/179) | Done | A dashboard folder SimHub cannot read is marked Failed for ever instead of being asked about |
| XOR-129 | [#180](https://github.com/xorob0/OpenDash/issues/180) | Done | Eleven candidates after a stable release would hide that stable release from the update check |
| XOR-130 | [#181](https://github.com/xorob0/OpenDash/issues/181) | Done | The Update button stays on screen after a successful update and does nothing when pressed |
| XOR-131 | [#182](https://github.com/xorob0/OpenDash/issues/182) | Done | Two update tests would still pass if the all-or-nothing guarantee were removed |
| XOR-132 | [#183](https://github.com/xorob0/OpenDash/issues/183) | Done | A background-thread test races its own assertion and can go red for no reason |
| XOR-133 | [#184](https://github.com/xorob0/OpenDash/issues/184) | Done | A stale plugin/OpenDash/Resources makes dotnet test fail after every branch switch |
| XOR-134 | [#185](https://github.com/xorob0/OpenDash/issues/185) | Done | The product is called OpenDash, and half the repository calls it openDash |
| XOR-135 | [#186](https://github.com/xorob0/OpenDash/issues/186) | Done | The README is written for a contributor, and the person deciding whether to install sees no screenshots |
| XOR-136 | [#187](https://github.com/xorob0/OpenDash/issues/187) | Done | openDash has a wordmark and no logo |
| XOR-137 | [#188](https://github.com/xorob0/OpenDash/issues/188) | Backlog | Touch: a tap on a zone cycles it, on the face, the pit wall and the companion |
| XOR-138 | [#189](https://github.com/xorob0/OpenDash/issues/189) | Backlog | The rev bar off entirely, and a top of the face that does not look cropped |
| XOR-139 | [#190](https://github.com/xorob0/OpenDash/issues/190) | Backlog | `bun run shots` says it captures ten faces and captures eighteen |
| XOR-140 | [#191](https://github.com/xorob0/OpenDash/issues/191) | Backlog | dev-loop.md is the loop on one page and never mentions `bun run shots` |
| XOR-141 | [#192](https://github.com/xorob0/OpenDash/issues/192) | Done | main is red: a test asserts the old spelling of the name that XOR-134 changed |
| XOR-142 | [#193](https://github.com/xorob0/OpenDash/issues/193) | Backlog | ADR 0013: what a car theme is, and the scope line it has to move |
| XOR-143 | [#194](https://github.com/xorob0/OpenDash/issues/194) | Backlog | Trade dress: what a theme takes from a real cluster, and what it must not |
| XOR-144 | [#195](https://github.com/xorob0/OpenDash/issues/195) | Backlog | The theme layer: tokens.json becomes a base and a set of overlays |
| XOR-145 | [#196](https://github.com/xorob0/OpenDash/issues/196) | Backlog | A face stops being five fixed parts: the anatomy becomes something a theme declares |
| XOR-146 | [#197](https://github.com/xorob0/OpenDash/issues/197) | Backlog | ADR 0014: how several hundred themed packages reach the user |
| XOR-147 | [#198](https://github.com/xorob0/OpenDash/issues/198) | Backlog | The installer becomes a picker: the user chooses which themed dashes to install |
| XOR-148 | [#199](https://github.com/xorob0/OpenDash/issues/199) | Backlog | Car detection: ship a SimHub per-car playlist so the face follows the car |
| XOR-149 | [#200](https://github.com/xorob0/OpenDash/issues/200) | Backlog | Every theme still shows everything: the conformance harness |
| XOR-150 | [#201](https://github.com/xorob0/OpenDash/issues/201) | Backlog | The build matrix: one package per size per car, and what CI can actually run |
| XOR-151 | [#202](https://github.com/xorob0/OpenDash/issues/202) | Backlog | The theme catalogue in the contract, mirrored on both sides |
| XOR-152 | [#203](https://github.com/xorob0/OpenDash/issues/203) | Backlog | BMW M4 G82 GT4 theme: three clusters and shift lights that close inward |
| XOR-153 | [#204](https://github.com/xorob0/OpenDash/issues/204) | Backlog | Mazda MX-5 Cup theme: an AiM MXL2 ring and six numbered lamps |
| XOR-154 | [#205](https://github.com/xorob0/OpenDash/issues/205) | Backlog | Porsche 911 GT3 R (992) theme: the whole screen changes colour to tell you something |
| XOR-155 | [#206](https://github.com/xorob0/OpenDash/issues/206) | Backlog | Ferrari 296 GT3 theme: a Bosch DDU 10 where black means correct |
| XOR-156 | [#207](https://github.com/xorob0/OpenDash/issues/207) | Backlog | BMW M4 GT3 theme: one page, and a traction ladder per rear wheel |
| XOR-157 | [#208](https://github.com/xorob0/OpenDash/issues/208) | Backlog | Dallara IR18 theme: the border of the screen is the state |
| XOR-158 | [#209](https://github.com/xorob0/OpenDash/issues/209) | Backlog | NASCAR Next Gen theme: three pages and a row of pit speed lights |
| XOR-159 | [#210](https://github.com/xorob0/OpenDash/issues/210) | Backlog | Band D's relative cannot follow the class filter, because it is not a list |
| XOR-160 | [#211](https://github.com/xorob0/OpenDash/issues/211) | Backlog | Porsche 911 GT3 Cup (992.2) theme: a tyre block in the middle and a signed brake bias |
| XOR-161 | [#212](https://github.com/xorob0/OpenDash/issues/212) | Backlog | PositionMode 'class' numbers a leaderboard it did not reorder |
| XOR-162 | [#213](https://github.com/xorob0/OpenDash/issues/213) | Backlog | Audi R8 LMS EVO II GT3 theme: a Bosch DDU S2 Plus with a day page and a night page |
| XOR-163 | [#214](https://github.com/xorob0/OpenDash/issues/214) | Backlog | Acura NSX EVO22 GT3 theme: brake temperatures boxed around the gear |
| XOR-164 | [#215](https://github.com/xorob0/OpenDash/issues/215) | Backlog | BMW M8 GTE theme: a Bosch DDU S2 in three columns with lockup lights by axle |
| XOR-165 | [#216](https://github.com/xorob0/OpenDash/issues/216) | Backlog | Chevrolet Corvette Z06 GT3.R theme: yellow numerals and a colour for every tyre state |
| XOR-166 | [#217](https://github.com/xorob0/OpenDash/issues/217) | Backlog | Ford Mustang GT3 theme: three pages and a fuel delta against a target |
| XOR-167 | [#218](https://github.com/xorob0/OpenDash/issues/218) | Backlog | The MCP server takes the VM without the claim, so two sessions drive one SimHub and captures fail with no reason given |
| XOR-168 | [#219](https://github.com/xorob0/OpenDash/issues/219) | Backlog | McLaren 720S GT3 theme: three pages with a tyre cluster drawn as the car |
| XOR-169 | [#220](https://github.com/xorob0/OpenDash/issues/220) | Backlog | Mercedes-AMG GT3 2020 theme: a strict grid of rows, and a page indicator along the foot |
| XOR-170 | [#221](https://github.com/xorob0/OpenDash/issues/221) | Backlog | Mercedes-AMG GT4 theme: the GT3 grid with three pages instead of two |
| XOR-171 | [#222](https://github.com/xorob0/OpenDash/issues/222) | Backlog | The delta page draws no sector deltas, and the catalogue draws them at all four shapes |
| XOR-172 | [#223](https://github.com/xorob0/OpenDash/issues/223) | Backlog | Lamborghini Huracan GT3 EVO theme: a Bosch DDU S2 with a quali page of its own |
| XOR-173 | [#224](https://github.com/xorob0/OpenDash/issues/224) | Backlog | Aston Martin Vantage GT3 EVO theme: one page that freezes purple when a lap ends |
| XOR-174 | [#225](https://github.com/xorob0/OpenDash/issues/225) | Backlog | Aston Martin Vantage GT4 theme: three pages and light stacks that double as a stall warning |
| XOR-175 | [#226](https://github.com/xorob0/OpenDash/issues/226) | Backlog | Spreading withBindings over an element silently drops the bindings it already had |
| XOR-176 | [#227](https://github.com/xorob0/OpenDash/issues/227) | Backlog | Porsche 718 Cayman GT4 theme: a seven-row single page with lockup and wheelspin stacks |
| XOR-177 | [#228](https://github.com/xorob0/OpenDash/issues/228) | Backlog | BMW M4 F82 GT4 (2018) theme: light stacks that report a setting by colour |
| XOR-178 | [#229](https://github.com/xorob0/OpenDash/issues/229) | Backlog | BMW M2 CS Racing theme: eight named status lamps around a single page |
| XOR-179 | [#230](https://github.com/xorob0/OpenDash/issues/230) | Backlog | McLaren 570S GT4 theme: a forward dash and a rear dash, with ESC in orange |
| XOR-180 | [#231](https://github.com/xorob0/OpenDash/issues/231) | Backlog | Toyota GR86 theme: two analogue-style gauges flanking a white tachometer |
| XOR-181 | [#232](https://github.com/xorob0/OpenDash/issues/232) | Backlog | The written design of the plugin panel is not in main |
| XOR-182 | [#233](https://github.com/xorob0/OpenDash/issues/233) | Backlog | Honda Civic Type R TCR theme: shift lights that go white under the limiter |
| XOR-183 | [#234](https://github.com/xorob0/OpenDash/issues/234) | Backlog | Audi RS 3 LMS TCR theme: numbered LEDs with blink patterns as alarms |
| XOR-184 | [#235](https://github.com/xorob0/OpenDash/issues/235) | Backlog | Hyundai Elantra N TCR theme: a dash that changes page by itself |
| XOR-185 | [#236](https://github.com/xorob0/OpenDash/issues/236) | Backlog | Hyundai Veloster N TCR theme: the Elantra dash on the earlier car |
| XOR-186 | [#237](https://github.com/xorob0/OpenDash/issues/237) | Backlog | Ferrari 296 Challenge theme: a fixed left half and four right-hand pages |
| XOR-187 | [#238](https://github.com/xorob0/OpenDash/issues/238) | Backlog | Gen 3 Australian Supercars theme: a delta bar that grows from the centre |
| XOR-188 | [#239](https://github.com/xorob0/OpenDash/issues/239) | Backlog | Porsche Mission R theme: two clusters, and a rear-view screen where the mirror was |
| XOR-189 | [#240](https://github.com/xorob0/OpenDash/issues/240) | Backlog | Porsche 911 GT3 Cup (992.1) theme: the earlier Cup dash |
| XOR-190 | [#241](https://github.com/xorob0/OpenDash/issues/241) | Backlog | Porsche 963 GTP theme: an energy dash, with a fuel target bar and a state of charge scale |
| XOR-191 | [#242](https://github.com/xorob0/OpenDash/issues/242) | Backlog | Acura ARX-06 GTP theme: the GTP base, with blue for traction and split-axle lockup |
| XOR-192 | [#243](https://github.com/xorob0/OpenDash/issues/243) | Backlog | BMW M Hybrid V8 theme: the GTP base in BMW's register |
| XOR-193 | [#244](https://github.com/xorob0/OpenDash/issues/244) | Backlog | Cadillac V-Series.R GTP theme: ECU maps named by colour |
| XOR-194 | [#245](https://github.com/xorob0/OpenDash/issues/245) | Backlog | Dallara P217 LMP2 theme: three pages and red for everything that is wrong |
| XOR-195 | [#246](https://github.com/xorob0/OpenDash/issues/246) | Backlog | Ligier JS P320 theme: a wheel-mounted display where every setting has its own colour |
| XOR-196 | [#247](https://github.com/xorob0/OpenDash/issues/247) | Backlog | Mercedes-AMG F1 W12 theme: eight gear boxes that turn green as the car warms up |
| XOR-197 | [#248](https://github.com/xorob0/OpenDash/issues/248) | Backlog | Super Formula SF23 theme: four wheel pages and a brake temperature ladder |
| XOR-198 | [#249](https://github.com/xorob0/OpenDash/issues/249) | Backlog | Super Formula Lights theme: a race page and a diagnostic page |
| XOR-199 | [#250](https://github.com/xorob0/OpenDash/issues/250) | Backlog | Dallara iR-01 theme: a screen that turns a colour when something is wrong |
| XOR-200 | [#251](https://github.com/xorob0/OpenDash/issues/251) | Backlog | FIA F4 theme: one page, and a shift ladder documented lamp by lamp |
| XOR-201 | [#252](https://github.com/xorob0/OpenDash/issues/252) | Backlog | Formula Vee theme: the smallest dash in the catalogue |
| XOR-202 | [#253](https://github.com/xorob0/OpenDash/issues/253) | Backlog | Ferrari 488 GT3 EVO 2020 theme: purple front, blue rear, white for no data |
| XOR-203 | [#254](https://github.com/xorob0/OpenDash/issues/254) | Backlog | Porsche 911 GT3 R (legacy) theme: purple for locking, yellow for the rear, blue for traction |
| XOR-204 | [#255](https://github.com/xorob0/OpenDash/issues/255) | Backlog | Mercedes-AMG GT3 (legacy) theme: four rows and a documented shift ladder |
| XOR-205 | [#256](https://github.com/xorob0/OpenDash/issues/256) | Backlog | Audi R8 LMS GT3 (legacy) theme: yellow when a system is switched off |
| XOR-206 | [#257](https://github.com/xorob0/OpenDash/issues/257) | Backlog | A trace carries properties, and not the opponent functions half the pages read |
| XOR-207 | [#258](https://github.com/xorob0/OpenDash/issues/258) | Backlog | Ferrari 488 GT3 theme: five rows and a gear in a large red box |
| XOR-208 | [#259](https://github.com/xorob0/OpenDash/issues/259) | Backlog | Ford GT GT3 theme: a single page and a very wide shift ladder |
| XOR-209 | [#260](https://github.com/xorob0/OpenDash/issues/260) | Backlog | McLaren MP4-12C GT3 theme: cyan dots and orange arrows under the limiter |
| XOR-210 | [#261](https://github.com/xorob0/OpenDash/issues/261) | Backlog | NASCAR Xfinity theme: one tachometer per manufacturer, with the numerals as the warning |
| XOR-211 | [#262](https://github.com/xorob0/OpenDash/issues/262) | Backlog | NASCAR Craftsman Trucks theme: a Spek Pro tachometer and five gauges that go red |
| XOR-212 | [#263](https://github.com/xorob0/OpenDash/issues/263) | Backlog | NASCAR Cup Series Gen 6 theme: three pages, with the numerals carrying the alarm |
| XOR-213 | [#264](https://github.com/xorob0/OpenDash/issues/264) | Backlog | ARCA Series theme: a Holley EFI dash shared by all three cars |
| XOR-214 | [#265](https://github.com/xorob0/OpenDash/issues/265) | Backlog | SRX theme: the Holley dash with ten shift LEDs and an RPM bar that reddens with them |
| XOR-215 | [#266](https://github.com/xorob0/OpenDash/issues/266) | Backlog | Legends Ford Coupe theme: an AiM strip with a red lamp at the shift point |
| XOR-216 | [#267](https://github.com/xorob0/OpenDash/issues/267) | Backlog | FIA Cross Car theme: an AiM MXm with two pages and a choice of backlight colours |
| XOR-217 | [#268](https://github.com/xorob0/OpenDash/issues/268) | Backlog | NASCAR Gen 4 theme: a full set of 2003 analogue gauges, laid out per manufacturer |
| XOR-218 | [#269](https://github.com/xorob0/OpenDash/issues/269) | Backlog | Late Model Stock theme: two gauges that flash red, and nothing else |
| XOR-219 | [#270](https://github.com/xorob0/OpenDash/issues/270) | Backlog | Street Stocks theme: a water temperature gauge and very little else |
| XOR-220 | [#271](https://github.com/xorob0/OpenDash/issues/271) | Backlog | Mini Stock theme: three analogue gauges and a shift light |
| XOR-221 | [#272](https://github.com/xorob0/OpenDash/issues/272) | Backlog | Dirt Mini Stock theme: the Mini Stock cluster on a dirt car |
| XOR-222 | [#273](https://github.com/xorob0/OpenDash/issues/273) | Backlog | Northeast Dirt Modifieds theme: gauges and dials, and the rock screen |
| XOR-223 | [#274](https://github.com/xorob0/OpenDash/issues/274) | Backlog | Dirt Micro Sprint theme: the smallest instrument panel on dirt |
| XOR-224 | [#275](https://github.com/xorob0/OpenDash/issues/275) | Done | The .ledsprofile format, reverse-engineered far enough to generate one |
| XOR-225 | [#276](https://github.com/xorob0/OpenDash/issues/276) | Done | ADR 0013: openDash lights hardware, and the flag box is where it starts |
| XOR-226 | [#277](https://github.com/xorob0/OpenDash/issues/277) | Done | The flag box package: one 8x8 profile, built from source and installed like a dash |
| XOR-227 | [#278](https://github.com/xorob0/OpenDash/issues/278) | Done | The flag catalogue, drawn sixty-four pixels at a time |
| XOR-228 | [#279](https://github.com/xorob0/OpenDash/issues/279) | Backlog | The gear on the matrix, with the shift model as its colour |
| XOR-229 | [#280](https://github.com/xorob0/OpenDash/issues/280) | Backlog | What the box does when you are not racing: idle, ignition, brightness and a box that can be dark |
| XOR-230 | [#281](https://github.com/xorob0/OpenDash/issues/281) | Done | The mirror: the car's own shift lights, as the one shift model |
| XOR-231 | [#282](https://github.com/xorob0/OpenDash/issues/282) | Backlog | Four matrices, each with its own job: the flag box settings and the pane they live in |
| XOR-232 | [#283](https://github.com/xorob0/OpenDash/issues/283) | Done | The rev bar and the rev arc light when the car's lights light |
| XOR-233 | [#284](https://github.com/xorob0/OpenDash/issues/284) | Done | One set of lights per car, and the gears where that is wrong |
| XOR-234 | [#285](https://github.com/xorob0/OpenDash/issues/285) | Backlog | The RPM strip: side, centre, side, generated from the tokens |
| XOR-235 | [#286](https://github.com/xorob0/OpenDash/issues/286) | Done | The side LEDs: brake, ABS, TC, ERS, DRS, spotter, limiter and low fuel |
| XOR-236 | [#287](https://github.com/xorob0/OpenDash/issues/287) | Backlog | The pit family: the limiter, the lane, speeding, and the countdown to your box |
| XOR-237 | [#288](https://github.com/xorob0/OpenDash/issues/288) | Done | Flags on the strip, ranked the way the screen ranks them |
| XOR-238 | [#289](https://github.com/xorob0/OpenDash/issues/289) | Backlog | Spotters: a car alongside, on the side it is actually on |
| XOR-239 | [#290](https://github.com/xorob0/OpenDash/issues/290) | Backlog | The wheel: button LEDs that say what the button does |
| XOR-240 | [#291](https://github.com/xorob0/OpenDash/issues/291) | Done | Warnings a driver would otherwise miss: low fuel, oil and water |
| XOR-241 | [#292](https://github.com/xorob0/OpenDash/issues/292) | In Progress | The brows: a strip above the screen, nine to twenty-five LEDs |
| XOR-242 | [#293](https://github.com/xorob0/OpenDash/issues/293) | Backlog | Sixty-four pixels needs a fit test of its own |
| XOR-243 | [#294](https://github.com/xorob0/OpenDash/issues/294) | Duplicate | The flag box: four 8x8 matrices, drawn as pictures |
| XOR-244 | [#295](https://github.com/xorob0/OpenDash/issues/295) | Backlog | Seeing a flag box without owning one: the VM, the preview and a scenario that drives every pattern |
| XOR-245 | [#296](https://github.com/xorob0/OpenDash/issues/296) | Backlog | Ambient lights: the room says what the car is doing |
| XOR-246 | [#297](https://github.com/xorob0/OpenDash/issues/297) | Backlog | The flag box guide: what somebody with an iFlag in a box actually does |
| XOR-247 | [#298](https://github.com/xorob0/OpenDash/issues/298) | Backlog | Installing the profiles, and keeping them current |
| XOR-248 | [#299](https://github.com/xorob0/OpenDash/issues/299) | In Progress | The lights page in the panel: brightness, night mode, and what each strip shows |
| XOR-249 | [#300](https://github.com/xorob0/OpenDash/issues/300) | Backlog | Out of the car: the idle animation and the engine start |
| XOR-250 | [#301](https://github.com/xorob0/OpenDash/issues/301) | Backlog | Seeing the lights without owning the hardware |
| XOR-251 | [#302](https://github.com/xorob0/OpenDash/issues/302) | Duplicate | Scope: openDash ships lights as well as screens |
| XOR-252 | [#303](https://github.com/xorob0/OpenDash/issues/303) | Backlog | bun run dev cannot open a dashboard, because SimHub does not stay up |
| XOR-253 | [#304](https://github.com/xorob0/OpenDash/issues/304) | Backlog | main is red: the unreadable-folder test asserts the old spelling of the name |
| XOR-254 | [#305](https://github.com/xorob0/OpenDash/issues/305) | Backlog | Nothing says which Bun the lockfile needs, and CI floats on the newest |
| XOR-255 | [#306](https://github.com/xorob0/OpenDash/issues/306) | Backlog | The pit wall reads TCLevel for "has the car got TC at all", which is 0 either way |
| XOR-256 | [#307](https://github.com/xorob0/OpenDash/issues/307) | Backlog | The quali scenario is a timed race, so nothing ever sees a qualifying session |
| XOR-257 | [#308](https://github.com/xorob0/OpenDash/issues/308) | Backlog | openDashboard misses every row when the track-layout offer is still on screen |
| XOR-258 | [#309](https://github.com/xorob0/OpenDash/issues/309) | Backlog | Four things the scope document neither builds nor refuses |
| XOR-259 | [#310](https://github.com/xorob0/OpenDash/issues/310) | Backlog | ADR: where a per-game difference is resolved |
| XOR-260 | [#311](https://github.com/xorob0/OpenDash/issues/311) | Backlog | The mapping layer: one property name per game, decided once rather than per binding |
| XOR-261 | [#312](https://github.com/xorob0/OpenDash/issues/312) | Backlog | Whether the driver is actually in the car is a different question in every sim |
| XOR-262 | [#313](https://github.com/xorob0/OpenDash/issues/313) | Backlog | Track wetness and grip, which every sim scales differently |
| XOR-263 | [#314](https://github.com/xorob0/OpenDash/issues/314) | Backlog | Engine map means a different thing in every sim, and the strip labels it one way |
| XOR-264 | [#315](https://github.com/xorob0/OpenDash/issues/315) | Backlog | Wheel index conventions and compound names are not the same in every sim |
| XOR-265 | [#316](https://github.com/xorob0/OpenDash/issues/316) | Backlog | The panel lets a user choose a page the running sim cannot fill, and says nothing |
| XOR-266 | [#317](https://github.com/xorob0/OpenDash/issues/317) | Backlog | Bite point and the clutch paddles, for any wheel rather than a named one |
| XOR-267 | [#318](https://github.com/xorob0/OpenDash/issues/318) | Backlog | Seven columns the field list does not have |
| XOR-268 | [#319](https://github.com/xorob0/OpenDash/issues/319) | Backlog | Face covers, and a frame that fits a screen it was not drawn for |
| XOR-269 | [#320](https://github.com/xorob0/OpenDash/issues/320) | Backlog | The circle tracker: every car as a marker on a ring, where a map will not fit |
| XOR-270 | [#321](https://github.com/xorob0/OpenDash/issues/321) | Backlog | LEDs along the edges of the screen, driven by the delta |
| XOR-271 | [#322](https://github.com/xorob0/OpenDash/issues/322) | Backlog | The delta reference cannot be the last lap, and its precision is fixed |
| XOR-272 | [#323](https://github.com/xorob0/OpenDash/issues/323) | Backlog | A driver name has one format, and it is not the user's choice |
| XOR-273 | [#324](https://github.com/xorob0/OpenDash/issues/324) | Backlog | The clock is drawn one way, and half the world reads the other |
| XOR-274 | [#325](https://github.com/xorob0/OpenDash/issues/325) | Backlog | The tyre widget draws three quantities and offers no choice among them |
| XOR-275 | [#326](https://github.com/xorob0/OpenDash/issues/326) | Backlog | Lap history has no fuel target, although the canvas already draws one |
