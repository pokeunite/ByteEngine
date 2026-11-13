# Workspace instructions

## Agent memory

Durable cross-session memory lives in `MemoryBank/`. At the start of a task read
`MemoryBank/activeContext.md` and `MemoryBank/progress.md`; read
`MemoryBank/systemPatterns.md`, `MemoryBank/techContext.md` and
`MemoryBank/decisions.md` before changing the areas they describe. Before
finishing a task that changed files or decisions, refresh `activeContext.md` and
prepend a dated entry to `sessionLog.md` (and `progress.md` or `decisions.md`
when applicable). The protocol and trigger phrases ("update memory bank",
"follow your custom instructions", "catch up") are in
`.clinerules/memory-bank.md`. Memory files are tracked by git and belong in the
same commit as the change they describe.

## User project location

Create new game projects under `C:/Users/codex/Documents`, in a separate named folder for each project. Do not create game projects inside the engine repository. Dune Company is at `C:/Users/codex/Documents/DuneCompany/DuneCompany.byteproject`.

The FPS template working project is `C:/Users/codex/Documents/fps template/fps template.byteproject`. Edit its scenes/assets there; keep reusable engine systems in this repository.

## Dune Company update workflow

Edit game scenes/assets in `C:/Users/codex/Documents/DuneCompany`. Keep canonical plugin source in this engine repository under `Plugins/DuneCompany`; synchronize the built package to the project Plugins and PluginLibrary and the engine PluginLibrary. Keep project PluginSource synchronized. Do not export games unless the user explicitly authorizes exporting again.

When changing Dune Company plugin code, increment its plugin version and refresh the statically linked browser runtime with `Tools/PublishBrowser.ps1`; updating the desktop package alone does not update web gameplay. Keep the bundled Dist and Debug editor BrowserRuntime copies synchronized. Rebuilding the browser runtime is not a game export.

## Repository hygiene

- `Dist/ByteEngine/BrowserRuntime` is the canonical web runtime. After publishing, run `Tools/Sync-BrowserRuntime.ps1` to mirror it into the editor `bin/Debug` and `bin/Release` output folders. `-Check` reports drift without copying.
- `Tools/Clean-Workspace.ps1` reports generated build output and caches. It is a dry run unless `-Apply` is passed; `-Full` also removes `bin` and `obj`. Authored projects and sample content are never touched.
- Engine version comes from `ByteEngineVersion` in the root `Directory.Build.props`. Do not add a second hard-coded version.
- New C# follows the root `.editorconfig`: four-space indentation, Allman braces, file-scoped namespaces, System usings first, and a 240-column review limit.
