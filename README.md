# ByteEngine

ByteEngine is a lightweight C# 2D engine built on .NET 9, OpenTK, and ImGui.NET.

## ByteEngine Editor v0.3

Version 0.3 adds a GUID-based asset pipeline and completes the basic editor-to-scene sprite workflow:

- Every asset receives a persistent adjacent `.meta` file and GUID.
- `AssetDatabase` owns identity, paths, importer metadata, and debounced file watching.
- `AssetManager` owns shared loaded GPU resources and reloads changed textures.
- The Assets panel provides folder navigation, breadcrumbs, type icons, selection, and a fallback Refresh action.
- Drag a PNG from Assets into Scene View to create a selected, persistent Sprite GameObject at the drop position and at the texture's native pixel size.
- SpriteRenderer Inspector texture fields accept asset drops and support clearing/replacing references.
- Texture import settings support persistent Nearest and Linear filtering.
- Scene files serialize SpriteRenderer textures by GUID while v0.2 path references migrate on load.
- Unresolved GUIDs remain intact, log once, and render a checkerboard placeholder.
- Unassigned SpriteRenderers are a distinct `None` state and render nothing; only unresolved assigned GUIDs show the missing-asset checkerboard.
- Inspector supports adding and removing Camera2D and SpriteRenderer components without duplicates.
- `GameObject` offers Create Empty, Create Sprite, and Create Camera.
- Game View renders at the configured project resolution, preserves its aspect ratio with letterboxing, and is driven by the active game Camera2D; Scene View keeps its independent EditorCamera.
- Scene View includes its grid, selection outline, and selected-camera viewport guide.

All v0.2 project/scene persistence, stable scene and GameObject IDs, dirty-state handling, lifecycle isolation, Play/Pause/Stop, docking, shortcuts, and missing-texture behavior remain supported.

## Run

From the repository root:

```powershell
dotnet run --project Editor/ByteEngine.Editor/ByteEngine.Editor.csproj
```

With an explicit project:

```powershell
dotnet run --project Editor/ByteEngine.Editor/ByteEngine.Editor.csproj -- "C:\Path\To\MyGame\MyGame.byteproject"
```

Run the standalone sandbox:

```powershell
dotnet run --project Sandbox/ByteEngine.Sandbox/ByteEngine.Sandbox.csproj
```

## Project layout

`File > New Project` creates:

```text
MyGame/
├── MyGame.byteproject
├── Assets/
├── Scenes/
│   └── Main.bytescene
└── Scripts/
```

Copy a PNG anywhere below `Assets/`. The file watcher imports it automatically and creates a sidecar such as `player.png.meta`. Moving or renaming the asset preserves that metadata and therefore scene references.

Use `File > Save Scene` (`Ctrl+S`) or `File > Save Scene As` (`Ctrl+Shift+S`) to persist edits. An asterisk in the title marks unsaved changes. Play Mode always runs a serialization-isolated clone; stopping discards runtime edits.

The default workspace places Hierarchy and Assets on the left, Scene View/Game View and Console in the center, and Inspector on the right. Use `Window > Reset Layout` at any time. Layout and recent-project preferences live in the user's local editor configuration, outside the repository.
