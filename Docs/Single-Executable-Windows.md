# Single-executable Windows games

Newly refreshed Windows runtimes include a self-contained .NET player under `PlayerRuntime/Bundled`. When that player and its exact Core module identity match the editor, the Windows exporter embeds the runtime, native libraries, fonts, game assets and project runtime plugins into one playable game executable. There is no loose `Runtime` directory or required adjacent `Game.bytepak` in that export. Attribution notices remain separate distribution documents and must still accompany redistribution when required.

This is packaging, not encryption. .NET extracts bundled native/runtime content to its private cache. `SingleExecutablePackage` checks and materializes the embedded asset package into `%LOCALAPPDATA%/ByteEngine/Games/Bundled/<content hash>`; `GameContentSession` then uses its existing private import session. Client files remain recoverable by a determined user. Logs and saves stay outside the executable and do not require installation-folder write permission.

The asset payload has a fixed-size footer with exact bounds and SHA-256. Payload bytes stream in bounded buffers. Corrupt caches are repaired from the executable; corrupt embedded payloads are rejected. The existing `--validate` content validation works through a generated cache manifest. The runtime identity probe starts without a graphics window and records the Core module ID from the actual bundled player, rather than trusting a version string.

`Tools/PublishPlayer.ps1` produces both paired internal staging files (needed by editor validation and legacy exports) and the bundled player. `GamePackageExporter` uses the bundle automatically when present. A stale bundle identity stops export with a refresh instruction. Existing loose exports remain supported. Native imports, plugin reflection and OpenAL are retained; trimming and NativeAOT are not enabled.

Validation includes payload round trips, cache corruption repair, tampered payload rejection, duplicate-payload rejection, legacy fallback and a real bundled-player `--validate` launch using existing exported game content. That repack test is not a new export of the current edited project.
