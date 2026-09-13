# Competitor analysis

**Last updated:** 2026-09-10

## Correction worth recording

An early assumption in this project was that Lovely SimRacing is a standalone application with
its own telemetry ingestion, and therefore a fundamentally different architecture. That was
wrong. Lovely is a SimHub dashboard, and so is Daniel Newman Racing. Both competitors sit on
the same substrate we do, which means the competition is on design, breadth and distribution
rather than on telemetry plumbing.

## Lovely SimRacing

[github.com/Lovely-Sim-Racing/lovely-dashboard](https://github.com/Lovely-Sim-Racing/lovely-dashboard)

A SimHub DDU dashboard family plus a matching stream overlay. Distribution goes through an
installer from the Lovely website, whose plugin contains a Dashboard Manager that lets users
browse, install and update dashes from inside SimHub, after which wheel or button box actions
are bound for navigation. This is the distribution model worth copying.

Sizes shipped, from the project README:

| Variant | Resolution | Target |
|---|---|---|
| Lovely Dashboard, Curved, Rallye, TK Edition, Companion, Flags | 850 by 480 | 5 in DDU |
| XL, XLC | 1280 by 480 | 10 in |
| MXL | 1280 by 400 | 7.8 in |
| UXL | 1920 by 480 | ultrawide, the OpenDash MVP size |
| Round, Square, Flags Square | 480 by 480 | round and square DDUs |
| Flags Round | 800 by 800 | round |
| Nano | 800 by 286 | compact |
| Companion Portrait | 480 by 850 | vertical secondary display |
| Tower | 370 by 850 | leaderboard tower for streaming |
| DisplayDash | 600 by 686 | vertical display |
| Overlay | 1140 by 360 | stream overlay |

Features include native support for seventeen or more sims, TeamLINQ real-time teammate
telemetry, a "True Dark Mode" with blue light reduction, and vendor integrations such as Ascher
Racing.

### Licence restriction

Lovely's licence explicitly covers its user interface design:

> "usage of its user interface design (UI) is also subject to the license it is distributed
> under. Any use in commercial or marketing material [...] is forbidden unless direct consent
> is given."

OpenDash's design must therefore be independently derived. Do not copy Lovely layouts or visual
language, and do not put Lovely screenshots in OpenDash marketing, documentation or comparison
material. This is why the design direction is the Porsche GT3 R and 963 race dash rather than
anything "Lovely-like"; see [../design/brand.md](../design/brand.md).

## Daniel Newman Racing

[danielnewmanracing.com](https://www.danielnewmanracing.com/)

Paid SimHub dashboard and LED profile packs with a strong presence in iRacing, ACC and LMU, on
the same substrate and with the same plugin-centred distribution. DNR is the reference for the
end goal of OpenDash, and three of its packages were inspected in order to understand how a
mature product is built.

| Product | Resolution | Target |
|---|---|---|
| Endurance | 1280 by 720 | 4, 4.4, 5 and 6.8 in wheel and dash screens |
| Speedway, Rally | standard and 10 in XL, pixel sizes not published | oval and stage racing |
| Chrono | 800 by 480 | small wheel-mounted and round displays, with bezel covers |
| Race Control | 1920 by 1080 | secondary monitor, no touch |
| Co-Pilot | not published | second screen companion |
| Invisible | not published | see-through overlay |

### How the packages are built

Every DNR dashboard is a main `.djson` that includes widgets: a header, a car panel, a flags
widget, an alert system, and the "adaptive display zones", which are one `.djson` with a screen
per page, included once per zone. A shared set of JavaScript extensions (configuration,
constants, functions, race data rows, notifications, themes, version) is bundled in
`JavascriptExtensions/`, and nearly every binding is JavaScript. The plugin, `DNRLEDs`, exposes
every option as a property, and a dashboard shows nothing but an install notice until the
plugin is detected; a single LED profile reads 145 distinct plugin settings.

### The feature set, as the end goal

A feature report of the three packages was written separately. Its families are, in short:

- a common foundation: per-game handling for a dozen sims, a data engine in the plugin with a
  fallback to native SimHub properties, dark and light themes with accent presets, driver name
  and rating formatting, tyre and brake widgets with automatic fallback of the displayed
  quantity, deltas against the last lap, the personal best and the all-time best, sector times
  and mini sectors;
- a notification engine for changes to brake bias, TC, ABS, engine map, ERS and wheel switches,
  and an alert engine that ranks some thirty flag, penalty and safety car conditions;
- an idle screen with animated backgrounds, logos and a driver block;
- adaptive zones offering fifteen pages each (lap times, sectors, fuel, virtual energy, map,
  opponents, relative, leaderboard, tyres, inputs, radar, pit view, damage, lap history, track
  rivals), with per-side page lists, a start page and a quick glance page;
- a lap review pop-up, an invisible dash mode, and touch control panels for the LED hardware;
- twelve LED profiles for RPM strips, wheels, brows, matrix flag boxes and ambient lighting,
  with per-car redline tables for hundreds of cars.

Every one of those items maps onto a card, a screen or a plugin property in the OpenDash model,
which is the reason the MVP invests in the slot mechanism and the property contract.

## Where OpenDash can actually win

Both competitors maintain every screen size as a separate `.simhubdash`, and every feature
change is redone by hand in each variant. Neither can accept outside contributions in any
meaningful way, because the artifact is an opaque binary.

OpenDash's generator turns the hand work into a build step. The claim has to be stated
carefully: a new aspect ratio still needs a designed layout, so a new size is not free. What is
free is everything else, since the cards, the bindings, the plugin contract and the tests are
shared, and a feature change lands in every size at once.

| | Competitors | OpenDash |
|---|---|---|
| New screen size | rebuild the dashboard by hand | write one layout function |
| Feature change | repeat in every variant | once, in source |
| Outside contributions | effectively impossible | a normal pull request |
| Price and licence | paid, or free with a licence that restricts reuse of the UI design | free, MIT |

The risk is symmetrical and should be stated plainly: they have shipped and we have not. Their
design maturity, sim coverage and community are real and substantial, and our advantage is
structural, which means it only pays off if the generator works. That is why the MVP has a
[spike gate](../scope-mvp.md#gate-before-build-starts), in the closed MVP document.

## Naming caution

Blumlaut ships dashes named "Porsche 992 GT3" and "Porsche 992 GT3 Cup", so there is precedent
for trading on the association. Do not follow it. A design inspired by GT3 R visual language is
fine; using Porsche marks, logos or model names in OpenDash branding is not.

## Sources

- [Lovely dashboard README](https://github.com/Lovely-Sim-Racing/lovely-dashboard/blob/main/README.md)
- [Lovely Sim Racing store](https://store.lsr.gg/pages/lovely-dashboard)
- [Daniel Newman Racing dashboards](https://www.danielnewmanracing.com/products/dashboards)
- [Daniel Newman Racing, which dashboards are there](https://www.danielnewmanracing.com/support/faq/dashboards-which-ones)
