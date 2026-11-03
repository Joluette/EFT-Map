# Validation · 3.6.1

Prepared on 2026-11-03. This document describes local verification and its limits; the GitHub Actions run attached to the source commit records hosted CI results separately.

- **3.6.0 baseline:** 35,279 checks in 19 suites covering parser/monitoring, map projection, navigation, raster/vector rendering, settings, language, screenshot cleanup, minimap, zoom, loot and independent filters.
- **Final 3.6.1 labels:** 380 checks in MapIcon (43), ZoomPreview (198) and GUI (139). Visual review on Woods confirmed outlined warm lettering, no frames/dots, and larger landmark names. Map geometry and calibration were unchanged; only label priorities were added to the map catalog.
- **Public-data audit:** 34,557 exported location/mode combinations matched the three public source snapshots, including items, handbook categories, room keys, hazard outlines and coverage. The bundled catalog contains 11,519 unique location payloads, 46 filters and 350 loose-item definitions.
- **Zoom benchmark:** 32 cases on Woods, Ground Zero, Factory and Streets at 1920×1080. With grouping and all floors enabled, median frame paint times were 15.40, 17.87, 14.41 and 17.13 ms respectively. Streets without grouping reached 2,049 visible icons and 24.59 ms median. These are test timings, not an FPS guarantee.
- **Installed smoke checks:** demo mode, separate cache, clean exit, unchanged real preference hashes. The six-second 3.6.0 Streets/F11 check recorded about 141 MB peak working set. This does not establish a long-run memory bound.

The offline GUI sweep skipped three uncached remote maps. Lab was tested separately using a copy of an existing cached map. Three physical-desktop presentation assertions were skipped by the overlay suite in explicit noninteractive mode. No 4K or long-duration memory test is claimed.

The repository update also rebuilds the source and runs the hosted workflow's test commands plus clean archive validation before upload. CI publication is disabled on ordinary pushes; only a manual run with `publish_release` enabled can publish a prerelease.