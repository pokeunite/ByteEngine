# ByteEngine

ByteEngine v0.5 is a lightweight, 3D-first C# game engine and editor built on .NET 9, OpenTK, and ImGui.NET. Its workflow combines component-based GameObjects, reusable object definitions, and a future Conditions → Actions visual-logic model while preserving the existing 2D toolset.

## 2D and 3D

- Universal `Transform` uses Vector3 position/scale, quaternion rotation, world/local matrices, and 3D hierarchy composition.
- Existing v0.4 scenes migrate automatically: XY position becomes XYZ at Z=0, Z rotation is preserved, and sprite dimensions move to `SpriteRenderer.Size`.
- Renderer3D provides cached indexed VAO/VBO/EBO meshes, cube/plane/sphere primitives, perspective cameras, depth/culling, materials, and ambient + Lambert directional lighting.
- Camera3D drives Game View when present; Camera2D remains the fallback. Game View always renders at project resolution and letterboxes to the panel.
- Scene View switches between 2D and 3D. In 3D: RMB+mouse looks, RMB+WASD flies, Q/E descends/ascends, MMB pans, wheel dollies, F frames selection, clicking picks transformed AABBs, and XYZ handles move objects.
- Drag files or folders from Windows Explorer onto the editor to copy them into the open project's `Assets` directory. FBX, OBJ, GLTF, and GLB files are registered as `Model3D` assets; mesh conversion and placement are planned for the model-import milestone.

## Variables and visual-logic direction

One variable architecture serves every future event module. Supported value types are Number, String, Boolean, Vector2, and Vector3. Scopes are Global (project defaults/runtime session), Scene, Self/Object, and Component properties. Persistent references prefer GameObject GUIDs and can resolve exposed paths such as `MainCamera.Camera3D.FieldOfView`.

Select an object to edit Object Variables in Inspector. With no object or asset selected, Inspector exposes Scene Variables and Global Variables. Create, rename, remove, change type/value, and undo/redo are supported. Play creates isolated scene/object values plus a runtime copy of global defaults; Stop discards them.

The `.byteevents`, `.byteblueprint`, and `.byteanimevents` asset types establish reusable event modules with component requirements, reusable GameObject definitions, target-character animation event sheets, and future skeleton/socket metadata. They intentionally do not introduce a node-graph editor in v0.5.

## Run and publish

```powershell
dotnet run --project Editor/ByteEngine.Editor/ByteEngine.Editor.csproj
```

Publish the self-contained Windows x64 editor:

```powershell
./Tools/PublishEditor.ps1
```

The double-clickable executable is written to `Dist/ByteEngine/ByteEngine.Editor.exe` and does not require `dotnet run`.

Run compatibility and isolation checks:

```powershell
dotnet run --project Tests/ByteEngine.Tests/ByteEngine.Tests.csproj
```

## Current limits

v0.5 deliberately uses primitive meshes and simple forward lighting. It does not yet include full physics, shadows/PBR, model import, skeletal playback, terrain/navmesh, or the complete visual-event/Blueprint editors. `CharacterController3D`, automatic ground-surface recognition, basic locomotion state inference, collider models, sockets, and reusable logic data are foundations for those later milestones.
