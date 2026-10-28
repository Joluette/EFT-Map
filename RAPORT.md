# EFT Position 3.3.2 — validation

Validated locally on 28 October 2026, Windows x64 / .NET Framework 4.8, using isolated screenshot folders and caches.

## Local release build

**15 suites / 11,799 checks passed.** The complete public summary is in [reports/3.3.2/tests.txt](reports/3.3.2/tests.txt).

Local release-build executable SHA-256: `3BFEF80FD7B150569C61F4C5314F7651191DC92FBED1D0F5EAD6F9C39E1DF08A`.

The checks cover filename parsing, folder monitoring, calibrated coordinates, tiles, SVG rendering, map navigation, the interface, markers, cleanup, appearance, title bars, raster buffering, languages, extract tooltips, the minimap, shortcut dispatch and log-driven map selection.

The offline GUI suite skips uncached Icebreaker, Labs and Labyrinth images. Raster tests use separate generated images and cached fixtures. A running production instance already owned the normal minimap shortcuts during local validation: the overlay suite exercised the settings actions for those two routes, and two F8-specific checks were skipped. The isolated Ctrl+Alt+F24 dispatch, polling and duplicate-prevention tests ran. Skips are printed explicitly in the test summary. CI runs the same tests and reports availability in its own environment.

## Map switching and minimap regression

Regression fixtures use actual game scene identifiers, including **Shopping_Mall**, **factory_day**, **factory_night**, **Rezerv_Base** and **city**, alongside the older Location fields.

The Customs → Interchange scenario starts with a valid Customs position, creates a new log session, writes the Shopping_Mall scene event in two pieces, and verifies the switch while the main form is minimized. It checks that the old position is cleared, that a fresh screenshot appears on the Interchange minimap before optional deletion, and that restarting with Customs selected reads the existing Interchange log without needing another screenshot.

The project user separately confirmed automatic switching worked during gameplay. The supplied gameplay image shows the minimap above EFT; it is a separate demonstration, not a test fixture.

## Scope

Position and heading are read from screenshot filenames. Map selection reads local application logs. There is no continuous position stream between screenshots, game-memory reading or code injection.

The UI previews are WinForms control renders with sample data, not measurements of presentation latency or game FPS. No new full memory benchmark was run for this release; measurements from older executables are not assigned to this binary or the CI build.

## Published artifacts

GitHub Actions builds from the published source, runs the configured checks and creates the Windows/source ZIPs plus SHA256SUMS.txt. Its executable may have a different hash from the local build. The Windows package records its own hash and source revision in BUILD-INFO.json.

Only reviewed reports for this version and documentation images are included. Personal preferences, screenshot archives, full game logs and runtime caches are excluded. See [image credits](docs/images/README.md) and [third-party notices](THIRD-PARTY-NOTICES.md).
