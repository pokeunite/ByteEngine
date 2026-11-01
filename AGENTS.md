# Workspace instructions

## User project location

Create new game projects under `C:/Users/codex/Documents`, in a separate named folder for each project. Do not create game projects inside the engine repository. Dune Company is at `C:/Users/codex/Documents/DuneCompany/DuneCompany.byteproject`.

The FPS template working project is `C:/Users/codex/Documents/fps template/fps template.byteproject`. Edit its scenes/assets there; keep reusable engine systems in this repository.

## Dune Company update workflow

Edit game scenes/assets in `C:/Users/codex/Documents/DuneCompany`. Keep canonical plugin source in this engine repository under `Plugins/DuneCompany`; synchronize the built package to the project Plugins and PluginLibrary and the engine PluginLibrary. Keep project PluginSource synchronized. Do not export games unless the user explicitly authorizes exporting again.

When changing gameplay plugin code, increment its plugin version and synchronize the desktop package and project PluginSource. Normal development targets Windows: do not automatically rebuild the browser runtime. The user approved this faster workflow on 2026-11-01. The active Hazard Pay project is `C:/Users/codex/Documents/palclone`; its canonical plugin is `Plugins/KarlsonDemo`.

Refresh the statically linked browser runtime only when the user requests a web runtime refresh or web build, using `Rebuild-ByteEngine-And-Refresh-Dist.ps1 -BrowserOnly` (or `-IncludeBrowser` for a complete rebuild). At that time synchronize Dist and Debug/Release BrowserRuntime copies. A desktop plugin update intentionally leaves web gameplay stale until this explicit refresh. Rebuilding a runtime is not a game export; exporting still requires explicit authorization.
