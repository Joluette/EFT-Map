# EFT Position 3.3.2 — Minimap & automatic map switching

A borderless minimap and automatic map selection are now available in the public release. This update includes the local 3.3.0–3.3.2 work and the Interchange detection fix confirmed during a user's raid.

![EFT Position 3.3.2 overview](https://raw.githubusercontent.com/Joluette/EFT-Map/3.3.2/docs/images/hero.svg)

![Minimap during gameplay — private path redacted](https://raw.githubusercontent.com/Joluette/EFT-Map/3.3.2/docs/images/gameplay-minimap.webp)

## Added

- **Borderless minimap:** a compact, always-on-top map that stays visible when the main window is minimized. Mouse clicks pass through while it is locked.
- **Placement and appearance controls:** choose a monitor, corner or custom position, size, zoom and transparency. Drag or resize in positioning mode, then press **✓** to lock.
- **Automatic map selection from local EFT logs**, with installation discovery for BSG and Steam and a manual folder picker.
- **F8** to show or hide the minimap. **Ctrl+Alt+M** remains an alternative; **Ctrl+Alt+Shift+M** controls positioning mode.
- Shortcut availability in settings, registration retries, and protection against opening two normal instances with the same cache.
- Polish and English text for the new controls.
- An illustrated [icon legend](https://github.com/Joluette/EFT-Map/tree/3.3.2#icon-legend) in English and Polish, covering player/heading, quests, extracts and older positions.

## Fixed

- **Customs staying selected when entering Interchange.** The game's scene is named **Shopping_Mall**, so the detector now maps that name to Interchange.
- Scene aliases for **Factory day/night**, **Reserve** (`Rezerv_Base`) and **Streets of Tarkov** (`city`).
- Duplicate minimap toggles when both the Windows hotkey message and fallback key-state reader observe the same press.
- Screenshot display acknowledgement when the main window is minimized: a visible minimap can display the position before optional cleanup. An undisplayed screenshot is retained.
- Positions from an earlier raid are cleared when a newer map-load event arrives.

## Enable the new features

1. **Settings → Window & map:** enable automatic map switching and detect or select the game's Logs folder.
2. **Settings → Minimap:** enable the overlay and choose its placement.
3. Run EFT in **borderless/windowed mode**. Use **F8** to show or hide the minimap and take an in-game screenshot to update your position.

**Map selection comes from logs. Position and heading come from screenshot filenames. There is no movement tracking between screenshots.**

## Download / update

Download **EFT-Position-Windows-3.3.2.zip**, extract the complete folder and run **EftPosition.exe**. Windows x64 / .NET Framework 4.8 is required.

Close the old app first and **keep your existing `cache` folder** when updating. New features are enabled in settings; existing screenshot, appearance and quest preferences are preserved. Screenshot deletion after display remains optional and is enabled by default.

Source: **EFT-Map-kod-zrodlowy-3.3.2.zip**. Archive hashes: **SHA256SUMS.txt**. The Windows archive includes `BUILD-INFO.json`.

See [the illustrated README](https://github.com/Joluette/EFT-Map/tree/3.3.2#readme) for previews and controls and [validation details](https://github.com/Joluette/EFT-Map/blob/3.3.2/RAPORT.md). Artwork by **Shebuka**, CC BY-NC-SA 4.0; data from tarkov.dev. The interface previews show sample data. The README also includes a gameplay capture supplied by the project author.

---

## Polski

Nowości względem publicznej 3.2: **minimapa nad grą**, automatyczny wybór mapy z logów, **F8** do pokazywania/ukrywania oraz ustawienia monitora, położenia, rozmiaru, zoomu i przezroczystości.

Naprawiono przejście **Customs → Interchange** oraz nazwy scen Factory, Reserve i Streets. Poprawka Interchange została potwierdzona w grze. Pozycja nadal odświeża się po screenshocie.

Nowe opcje włączysz w **Ustawienia → Okno i mapa** oraz **Ustawienia → Minimapa**. Używaj trybu bez ramek. Przy aktualizacji zachowaj folder **cache**.
