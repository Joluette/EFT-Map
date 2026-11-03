# Map icons · 3.6.0–3.6.1

Source update prepared on 2026-11-03. See [release notes](RELEASE-NOTES.md) for the changes since 3.3.2.

Open **Quests & filters → Loot**. The panel contains four collapsible sections:

- **Containers**: 27 independently selectable source container types, including safes, bank safes, drawers, PCs, jackets, medical bags/cases, weapon/ammo/grenade boxes, stashes and bodies. Variants with the same normalized type share a filter, as on tarkov.dev.
- **Loose loot**: 14 handbook categories: injectors, valuables, storage containers, electronics, medical supplies, information items, electronic/mechanical keys, drinks, flammable materials, energy elements, Battle Pass documents, special equipment and others.
- **Hazards**: mines, sniper zones and other source hazards, including their area outlines.
- **Utilities**: stationary weapons and locked rooms. A locked room denotes a keyed door, with the required key and power requirement in its tooltip. Vehicle trunks are excluded.

Search accepts English/Polish category names and possible loose-item names (for example SJ6). Search changes the list only; it preserves selected filters. Section checkboxes toggle their own children. All/None applies to every type. Ctrl+F focuses search in the active filter page.

Each marker uses cached original pictograms shared with the filter legend. Main-map hazard areas reuse the existing map viewport and zoom preview cache, so their polygons are not repainted on every animation frame. Nearby points can be grouped; the badge counts locations, not items. A mixed loose-loot location remains a single location and appears when any selected item category can occur there. Tooltips exclude hidden categories. Current-floor filtering and the independent minimap switch remain available.

Place names use cached warm lettering with a dark outline and no background frame. Major landmarks use larger text and take priority when labels would overlap. The font uses the Windows Bahnschrift family with a system fallback. Quest/extract pins share the updated palette and retain their existing meaning.

## Data

The bundled public tarkov.dev snapshots contain 11,519 unique location payloads across 13 supported maps. Data includes possible locations and possible items, not guaranteed spawns or live raid contents. Identical payloads share mode flags. English names come from the source, Polish item names use the Polish item snapshot. Mines/snipers are source annotations; this feature does not read game memory.

Update data with `tools/Update-Loot.ps1`. This downloads maps for all three supported modes plus item/category and language dictionaries, rebuilds `assets/loot.json`, audits positions, items, categories, key names, hazard outlines and coverage, then copies the verified data into assets. Runtime map icons need no data API request.

## Compatibility

New installations default to English. Existing English/Polish choices remain intact. Old broad loot categories migrate to equivalent independent selections using stable filter IDs. App and quest preferences are unchanged. `loot.json` uses schema v2; keep the new data together with the new program. Schema v1 can still be read by 3.6.

Keep the executable and its matching map/loot data together when updating. Existing settings are preserved. Repository history contains the previous implementation; personal backup files and preferences are not distributed.
