# Main menu, combat pressure and export-runtime repair — 3.42.0

## Installed game changes

The startup scene opens a functional main menu over a wide camp camera. Continue restores the gameplay camera and HUD; Settings uses the existing functional settings pages; Quit confirms before leaving. New Game confirms, backs up the previous company saves and starts fresh while retaining player preferences. Main Menu Camera Offset, Look Offset, Field Of View, Company Title and Show Main Menu On Start are editable on Mining Pause Menu.

The yellow/charcoal pause-and-settings design is a concept sheet for the next UI pass. The existing child settings screens have not been rebuilt to match it yet. The image is in SourceArt/Main Menu and Combat Pass/Pause-Settings-Yellow-Concept.png.

Your supplied CNC-machine recording is the drill operating loop. Its sustained section is converted to mono 48 kHz WAV and looped with a short crossfade; the full converted recording is retained in SourceArt. Camp Music Volume is reduced from 0.38 to 0.25.

Weapon Equipment has Allow Weapon Dropping disabled in this project, so keyboard and Event Sheet drop routes both fail safely. The pause-menu controls no longer offer a drop binding.

Hunter speed is 6 and raider speed is 4.8. Attack windup/recovery/cooldown are 0.32/0.27/0.62 for hunters and 0.50/0.38/0.78 for raiders. Spawn interval is 0.62s. Enemy counts and health are unchanged. All remain editable on Mining Operation and are reflected in the contract previews.

Mines and mortars now use Unity Labs Explosion01-light: 25 frames in a 5x5 atlas. It has a hot fire burst and rolling smoke, layered with a short flash, sparks and a pressure ring. Particle budgets remain capped at 96. Explosion launch speed/lift are 8.5/4.0. Source: https://unity.com/blog/engine-platform/free-vfx-image-sequences-flipbooks — Thomas Iche / Unity Labs Paris, CC0. Original TGA/EXR package retained; runtime uses a PNG conversion.

## Rebuild error and fix

The crash log showed that the exported player could not load ICharacterDebugSource from ByteEngine.Core. That prevented the entire weapon/mining plugin from registering, causing the long missing-component list. The old rebuild entry point used an older source checkout and did not reliably pair the editor with the export runtime.

The familiar command is now updated:

    powershell -ep bypass -file .\Rebuild-ByteEngine-And-Refresh-Dist.ps1

The installed script reads .byteengine-build-source.json and builds the maintained checkout. It refreshes Debug, the selected editor distribution, the Windows player and gameplay package, and verifies matching engine-library hashes. It refuses to terminate an open editor. Debug and Release player folders no longer overwrite one another with incompatible builds. Optional -VerifyOnly checks the installed editor/player pair without rebuilding. -SourceRoot and -InstallRoot can override the configured locations. The current source is C:/Users/codex/.codex/worktrees/52af/ByteEngine.

Both installed editor launch paths and their Windows player runtimes were refreshed. Fourteen AI metadata records marked Unknown by the older editor were corrected to AIBehavior, preserving GUIDs and importer settings.

No game was exported. Re-export from the updated editor to replace the failing old build.

## Verification

Weapon/gameplay regressions passed, alongside native checks for main-menu startup, frozen background, Continue restoring controls, no-drop enforcement, supplied drill playback, unchanged enemy health, faster attack settings, bounded explosion particles, spider knockdown/death/recovery and unchanged saved progress. A separate native scene load used the exact engine library from the published Windows player and succeeded, including the types missing from the crash.
