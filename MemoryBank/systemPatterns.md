# System patterns

Updated: 2026-11-13

## Solution map

| Path | Role |
| --- | --- |
| `Engine/ByteEngine.Core` | The engine. One library, one folder per domain: Graphics, Scene, Physics, Navigation, Animation, Audio, Assets, Blueprints, VisualLogic, Characters, Constructs, Runtime, Serialization, Plugins, Diagnostics, Variables, Vfx. |
| `Editor/ByteEngine.Editor` | ImGui editor: `Panels/` views, `Commands/` undoable operations, `Gizmos/`, `Selection/`, `Resources/`. Startup opens the Project Browser. |
| `Player/ByteEngine.Player` | Desktop player host. |
| `Player/ByteEngine.Launcher` | Launcher/bootstrapping entry point. |
| `Player/ByteEngine.Browser` | Browser host: C# frame producer plus JS/WebGL2 front end in `wwwroot` (`main.js`, `renderer.js`, `graphics.js`, `frame-wire.js`, `content.js`, `audio.js`, `water.js`). |
| `Sandbox`, `Example`, `Samples` | Small engine-side harness content. |
| `Plugins/<Game>` | Canonical gameplay plugin source; built packages land in `PluginLibrary`. |
| `Tools/*.ps1`, `Tools/*.cjs` | Build, publish, sync, audit, clean and browser smoke tests. |
| `Tests/ByteEngine.Tests` | Custom console test runner (not xUnit); flag-selected suites and diagnostics. |

## Core object model

- `Scene` owns `GameObject`s; `GameObject` owns `Component`s; `Transform`
  composes local/world matrices and 3D hierarchy.
- Components use lifecycle hooks and read `Time.DeltaTime`.
- Play Mode runs isolated copies of scene, object and global state; Stop discards
  runtime mutations.
- Fixed-step simulation is opt-in per scene (`Scene.FixedSimulation`). NavigationAgent3D ticks through OnUpdate in variable simulation and OnFixedUpdate in fixed simulation; callbacks receive the configured fixed delta.

## Data and asset patterns

- Authored data is GUID-addressed. Scenes store GUID + sub-asset keys instead of
  duplicating meshes, textures, materials or skeletons.
- Model import is persistent: meshes, materials, textures, skeletons and clips are
  stable sub-assets, and reimport keeps the model GUID.
- One variable architecture serves Global, Scene, Self/Object and Component
  scopes, including nested writable fields.
- Byte Blueprints are prefab-like reusable assets; `.byteevents` ByteGraph assets
  are executable logic modules attached to Blueprints.

## Build and packaging patterns

- `ByteEngineVersion` in `Directory.Build.props` is the only version source.
  Assembly, file and informational versions derive from it.
- `ByteEngine.Core` emits an assembly metadata fingerprint of its sources; the
  browser runtime uses it to reject stale gameplay assemblies.
- `Dist/ByteEngine/BrowserRuntime` is the canonical web runtime.
  `Tools/Sync-BrowserRuntime.ps1` mirrors it into editor `bin/Debug` and
  `bin/Release` output; `-Check` reports drift only.
- Plugin packages declare `schemaVersion`, `version` and `minimumEngineVersion`
  in `plugin.json`.

## Performance patterns already enforced

- No LINQ, closure or per-frame allocation on agent/entity hot paths. Cache
  lookups (navigation region) and re-resolve only on invalidation or retry.
- Pathfinding keeps search scratch on the grid/mesh and clears it per query;
  returned paths stay private copies so one agent cannot mutate another's path.
- Physics separates broad-phase preparation from solving. Candidate reuse is valid only while collider transforms remain unchanged; contacts/joints can invalidate it.
- Crowd animation shares quantized poses per model. Playback already wraps loop time and clamps one-shot time; do not modulo a terminal one-shot frame.
- Guards live in `Tests/ByteEngine.Tests/EngineAllocationRegressionTests.cs` and
  `Tests/ByteEngine.Tests/EngineArchitectureBenchmarks.cs`.

## Editor patterns

- User actions go through commands so undo/redo stays consistent.
- The Scene View is editor-only for debug overlays; Game View renders at project
  resolution and letterboxes into the panel.
- Blueprint and ByteGraph workspaces are docked panels with their own selection
  and preview state, separate from the main scene selection.

## Conventions

- C#: four-space indentation, Allman braces, file-scoped namespaces, System usings
  first, 240-column review limit (root `.editorconfig`).
- New behavior gets a check in the console test runner; hot paths get an
  allocation budget.
- Point-in-time investigations go in `Docs/` with a dated filename; reusable
  user-facing guides go in `Documentation/`.

## Known architectural tensions

- Main uses CPU crowd deformation. The secondary branch has native GPU skinning with tested main and shadow passes; capabilities are branch-specific.
- Crowd sharing is conservative: hidden mesh parts, blends, IK, physical bones or
  material overrides opt a renderer out.
- `Scene.AddGameObject` is O(n) per call because of `List.Contains`, so spawning
  thousands in one frame is quadratic.
