# EFT Position 3.6.1

Changes since the downloadable 3.3.2 prerelease. The repository contains the 3.6.1 source; binary publication is a separate, manually enabled workflow action.

## Interface and performance

- Settings open inside the main window with an animated transition. The empty rectangle during opening has been removed.
- Updated selectors, button focus styling and sliders. Mouse-wheel input no longer changes settings accidentally.
- Fixed native opacity for the main window and minimap, including live adjustment.
- Smoother F11 zoom through a bounded preview cache and deferred full-quality redraw. Hazard outlines reuse the map cache during zoom.

## Loot, hazards and utilities

- Original cached icons and 11,519 possible locations across 13 maps, from public tarkov.dev snapshots.
- Independent filters for 27 container types and 14 loose-loot categories, including injectors, valuables, electronics and keys.
- Search by container/category name or possible item, collapsible sections, section toggles and persistent selections.
- Nearby-location grouping, floor filtering, hover details and a separate minimap switch. Mixed loose-loot points retain every supported category.
- Mines, sniper zones and other source hazards with area outlines.
- Stationary weapons and keyed rooms. Room tooltips identify the required key and power requirement; vehicle trunks are excluded.

Open **Quests & filters → Loot**. Locations and tooltips describe possible loot, not guaranteed spawns or live raid contents. Loose ammunition has not been added without source locations.

## Place names and language

- Plain warm lettering with a dark outline, replacing framed labels. Major landmarks use larger text and have priority when names overlap.
- English is the default for new settings. Existing English/Polish choices and screenshot/quest/loot preferences are preserved.
- Old broad loot categories migrate to independent filter IDs.

## Verification and publication

See [validation scope](reports/3.6.1/validation.md) and the [map icon guide](MAP-ICONS-3.6.0.md).

The Windows workflow builds, tests and packages source updates. Publishing a prerelease requires a manual run with **publish_release** selected. No new executable or private preferences are committed to the repository.

## Polski

Ustawienia znajdują się w głównym oknie, poprawiono przezroczystość i płynność zoomu F11. Dodano nowy zestaw ikon, osobne filtry kontenerów i lootu luzem, zagrożenia oraz zamknięte pomieszczenia z nazwą klucza. Podpisy map mają jasny tekst z ciemnym obrysem i większe nazwy ważniejszych miejsc. Zapisane ustawienia i wybrany język pozostają zachowane. Publikacja paczki wymaga osobnego uruchomienia workflow.