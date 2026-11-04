Changes since EFT Position 3.3.2.

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

Open **Quests & filters → Loot**. Locations and tooltips show possible loot based on the map data.

## Place names and language

- Plain warm lettering with a dark outline, replacing framed labels. Major landmarks use larger text and have priority when names overlap.
- English is the default for new settings. Existing English/Polish choices and screenshot/quest/loot preferences are preserved.
- Old broad loot categories migrate to independent filter IDs.

## Download and update

Download **EFT-Position-Windows-3.6.1.zip**, extract the entire archive and run **EftPosition.exe**. Requires Windows x64 and .NET Framework 4.8. If updating an existing installation, close the app first and keep your `cache` folder to preserve settings and downloaded maps.

The source archive and SHA-256 checksums are attached below. See the [validation report](https://github.com/Joluette/EFT-Map/blob/3.6.1/reports/3.6.1/validation.md) and [map icon guide](https://github.com/Joluette/EFT-Map/blob/3.6.1/MAP-ICONS-3.6.0.md).

## Polski

Ustawienia znajdują się w głównym oknie, poprawiono przezroczystość i płynność zoomu F11. Dodano nowy zestaw ikon, osobne filtry kontenerów i lootu luzem, zagrożenia oraz zamknięte pomieszczenia z nazwą klucza. Podpisy map mają jasny tekst z ciemnym obrysem i większe nazwy ważniejszych miejsc. Zapisane ustawienia i wybrany język pozostają zachowane. Pobierz **EFT-Position-Windows-3.6.1.zip**, rozpakuj całą paczkę i uruchom **EftPosition.exe**. Przy aktualizacji zachowaj folder `cache`.