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

## Cline workflow (2026-11-14)

Use this sequence for every task in this repository.

1. Work in `C:\Users\codex\ByteEngine` only. Never hand-edit `Dist/`, recovery folders
   (`Backups/`, `UpdateBackups/`) or the retired integration worktree
   (`C:\Users\codex\.codex\worktrees\52af\ByteEngine`). `Dist/` changes only through the publish
   scripts.
2. One coding assistant at a time in this folder. Do not run a second agent against the same tree.
3. Read `AGENTS.md` and `MemoryBank/activeContext.md` + `progress.md` before editing; read
   `systemPatterns.md`, `techContext.md` and `decisions.md` for the areas being changed.
4. Make one coherent change, test it, then commit it. Do not bundle unrelated edits.
5. Optimisation claims require before/after measurements, not reasoning alone.
6. After a change is accepted: rebuild with
   `powershell -ep bypass -file .\Rebuild-ByteEngine-And-Refresh-Dist.ps1`, then push.

Boundaries: engine code lives in this repository. Hazard Pay scenes, assets and saves live in
`C:\Users\codex\Documents\palclone`; game work there never moves engine code into the project.



## Canonical source and Hazard Pay


The sole maintained source is C:/Users/codex/ByteEngine on main. Verify
.byteengine-build-source.json before editing or publishing. Do not republish
from historical worktrees or recreate a separate ByteEngine-Palclone engine.
Hazard Pay is C:/Users/codex/Documents/palclone; canonical gameplay source is
Plugins/KarlsonDemo. Preserve project assets and saves. Rebuild/install the
Windows plugin and synchronize project PluginSource after gameplay changes.
Windows development is the default; browser refresh remains opt-in through
-BrowserOnly or -IncludeBrowser. Do not export a game without fresh explicit
user authorization. Rebuilding editor/player runtimes is not a game export.
Use https://www.particlefx.studio/effects first when sourcing VFX assets.
Backups and UpdateBackups are recovery history, not disposable build caches.
