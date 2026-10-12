# Workspace instructions

## User project location

Create new game projects under `C:/Users/codex/Documents`, in a separate named folder for each project. Do not create game projects inside the engine repository. Dune Company is at `C:/Users/codex/Documents/DuneCompany/DuneCompany.byteproject`.

The FPS template working project is `C:/Users/codex/Documents/fps template/fps template.byteproject`. Edit its scenes/assets there; keep reusable engine systems in this repository.

## Dune Company update workflow

Edit game scenes/assets in `C:/Users/codex/Documents/DuneCompany`. Keep canonical plugin source in this engine repository under `Plugins/DuneCompany`; synchronize the built package to the project Plugins and PluginLibrary and the engine PluginLibrary. Keep project PluginSource synchronized. Do not export games unless the user explicitly authorizes exporting again.

When changing Dune Company plugin code, increment its plugin version and refresh the statically linked browser runtime with `Tools/PublishBrowser.ps1`; updating the desktop package alone does not update web gameplay. Keep the bundled Dist and Debug editor BrowserRuntime copies synchronized. Rebuilding the browser runtime is not a game export.
