# Project brief

Updated: 2026-11-13

## What this is

ByteEngine is a lightweight, 3D-first C# game engine and editor. Engine version
is `0.10.0`, defined once as `ByteEngineVersion` in `Directory.Build.props`.
(`README.md` still opens with "v0.7"; that string is stale, the build property is
authoritative.)

## Goals

- One engine that ships a Windows desktop editor/player and a WebGL2 browser
  runtime from the same C# gameplay code.
- Authoring without a heavyweight DCC-style editor: Project Browser, 3D scene
  view, Byte Blueprints, ByteGraph visual logic.
- Stable asset identity. Scenes reference GUIDs and sub-asset keys, so reimport
  and rescale never break references.
- Keep gameplay out of the core: games are plugins (`.byteproject` + plugin
  packages) that compose engine systems.
- Performance that is measured. Crowd animation, navigation, physics and
  geometry have allocation/timing benchmarks with budgets in the test project.

## Non-goals and hard boundaries

- No game content in this repository. Games live in separate project folders
  under `C:/Users/codex/Documents`; see `AGENTS.md` for the current ones.
- The browser runtime is a first-class target, so desktop-only GPU features
  cannot become required. Desktop is OpenGL 3.3; browser is WebGL2 (GLSL ES 3.00).
- Game exports are not run unless the user explicitly authorizes one. Rebuilding
  the browser runtime (`Tools/PublishBrowser.ps1`) is not an export.

## How success is judged

- `dotnet build ByteEngine.sln` is clean and `Tests/ByteEngine.Tests` passes.
- The editor runs from source with no manual asset surgery.
- A gameplay change reaches both desktop and web through the documented publish
  and sync scripts (`Tools/PublishBrowser.ps1`, `Tools/Sync-BrowserRuntime.ps1`).
- Benchmarks in `EngineArchitectureBenchmarks` do not regress on navigation,
  physics and crowd-animation scenarios.

## Read next

- `MemoryBank/systemPatterns.md` for the architecture map.
- `MemoryBank/techContext.md` for commands and the stack.
- `Docs/` for dated investigation write-ups; `Documentation/` for user-facing
  guides.
