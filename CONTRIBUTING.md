# Contributing to EFT Position

Thanks for helping make Tarkov a little easier to navigate. Bug reports, clearer instructions, translations and focused code changes are all useful. You can write issues in **English or Polish**.

## Report a problem or suggest an idea

Use the [issue forms](https://github.com/Joluette/EFT-Map/issues/new/choose). Check existing issues first, then describe what happened and what you expected.

For bugs, include the app version and enough steps to reproduce the problem. Map name, game mode, Windows version, and whether the main app or minimap was visible can help. Remove personal information from screenshots and log excerpts.

Use the [security policy](SECURITY.md) for suspected vulnerabilities and [accessibility guidance](ACCESSIBILITY.md) for barriers to using the app.

## Build and check a change

The app uses **C#, WinForms, Windows x64 and .NET Framework 4.8**. The required SharpVectors libraries are bundled in `lib`.

1. Fork the repository and make a focused branch.
2. Run `Zbuduj.cmd` or `Build.ps1`.
3. Run the checks relevant to your change. `Testuj.cmd` runs the full local suite; some checks need an interactive Windows desktop.
4. Open a pull request describing the change and how you checked it.

Hosted CI uses explicit noninteractive mode for a few overlay desktop-presentation assertions. Keep the local desktop checks available when changing activation, click-through behavior or window placement.

## Project details that matter

- Use **isolated screenshot folders and caches** for tests. Never point automated fixtures at a real player's screenshot archive.
- Position and heading come from screenshot filenames; automatic map selection reads application logs. Keep that behavior clear in documentation.
- Add interface text through `UiText.cs` for both Polish and English.
- Map changes need calibration/floor checks. Include the source and attribution for any new map, quest, extract or artwork data.
- Keep fixes small. Add a regression check for a reproducible behavior bug when it provides useful coverage; documentation-only edits do not need application tests.
- Do not commit personal preferences, runtime caches, full game logs, private paths, tokens, generated test executables or downloaded map caches.
- Version bumps and release publication are handled by the maintainer.

Application code is GPL-3.0-only. Map artwork and other bundled assets have their own licenses; see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md). Keep the relevant notices and only contribute material you have the right to share.

Please follow the [code of conduct](CODE_OF_CONDUCT.md).
