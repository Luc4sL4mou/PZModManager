# Changelog

All notable changes to PZ Mod Manager. Downloads: [Releases](https://github.com/Luc4sL4mou/PZModManager/releases).

## [1.2.2] - 2026-11-13 (beta)
### Fixed
- The release zip's single-file exe was built with its self-extraction stub compressed, which some antivirus/Windows SmartScreen flagged as a trojan and silently deleted after extraction. Compression is now disabled for the release build, so the `.exe` stays in the zip.

## [1.2.1] - 2026-11-07 (beta)
### Changed
- Public beta: the release title and the READMEs say so; please report problems through the bug report form.
- If the Microsoft Edge WebView2 Runtime is missing, the app now explains what it is and offers to open Microsoft's official download page (instead of a short technical error).

## [1.2.0] - 2026-11-07
### Added
- Bilingual interface: English (default) and Brazilian Portuguese, with an **EN | PT** toggle at the top. Dates, numbers and the app's own messages follow the chosen language.

### Changed
- README screenshots and GIFs recorded in English (the Portuguese README keeps Portuguese ones).

## [1.1.0] - 2026-11-07
### Added
- **Restore originals** button (Dashboard → Installation): puts the game launcher (`ProjectZomboid64.json`/`.bat`) back to how it was before the app's first change.
- MIT license.
- Automated builds: GitHub Actions builds the release zip from the source code, with a build provenance attestation and a SHA-256 file.

### Changed
- README in English, with the Portuguese version in `README.pt-BR.md`.
- The release zip has a fixed name (`PZModManager-win-x64.zip`), so the download link always points to the latest version.

## [1.0.0] - 2026-11-07
### Added
- Mods per profile (new game, each save, each hosted server), with dependencies and load order.
- **Conflicts** tab: what each overlap between two mods causes, who wins, and actions to change the winner, disable a mod or ignore the alert; visual map.
- Detection of obsolete mods and of mods that may become obsolete soon.
- One-click ZombieBuddy + Project Viewpoint setup and a Recommended tab with Viewpoint addons.
- **Game memory (RAM)** slider with a performance indicator and a recommended value (the game's default 3 GB often crashes with many mods).
- Automatic backups before changing game files; safe (atomic) writes; no changes while the game is running.
- Single exe, no installation required.

[1.2.2]: https://github.com/Luc4sL4mou/PZModManager/releases/tag/v1.2.2
[1.2.1]: https://github.com/Luc4sL4mou/PZModManager/releases/tag/v1.2.1
[1.2.0]: https://github.com/Luc4sL4mou/PZModManager/releases/tag/v1.2.0
[1.1.0]: https://github.com/Luc4sL4mou/PZModManager/releases/tag/v1.1.0
[1.0.0]: https://github.com/Luc4sL4mou/PZModManager/releases/tag/v1.0.0
