# ByteEngine

ByteEngine is a lightweight C# 2D engine built on .NET 9, OpenTK, and ImGui.NET.

## ByteEngine Editor v0.4

Version 0.4 makes Scene View manipulation and scene organization behave like a practical 2D editor:

- Q/W/E/R Select, Move, Rotate, and Scale gizmos with X/Y/free or uniform handles.
- Ctrl snapping plus an optional persistent Snap toggle with 1, 8, 16, 32, and 64 pixel grids; rotation snaps to 15 degrees.
- Direct click-drag placement, rotated hit testing, hover outlines, multi-selection, and multi-object movement.
- Command-based undo/redo with Ctrl+Z, Ctrl+Y, and Ctrl+Shift+Z, grouped drag edits, and saved-revision dirty tracking.
- Ctrl+D duplication and internal Ctrl+C/Ctrl+V copy/paste with new persistent IDs.
- Parent/child GameObject hierarchy, cycle-safe drag parenting, root unparenting, F2 rename, and object context menus.
- Local hierarchy transforms and backward-compatible `parentId` scene persistence.
- SpriteRenderer `OrderInLayer` sorting and Inspector editing.
- Adaptive Scene View grid, stronger origin axes, relationship lines, camera boundary, and Scene View creation menu.

The v0.3 GUID-based asset pipeline remains fully integrated:

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
