# ByteEngine

ByteEngine v0.7 is a lightweight, 3D-first C# game engine and editor built on .NET 9, OpenTK, and ImGui.NET. It combines a persistent model pipeline, reusable Byte Blueprints, executable ByteGraph visual logic, character-controller foundations, and the existing 2D toolset.

Startup opens the Project Browser. Create Project offers a blank Clean Project or a ready-to-run 3D Starter, with a selectable destination directory. You can also open an existing `.byteproject`, or pass one on the command line.

## Model asset workflow

- Drag files or folders from Windows Explorer into the Assets panel to copy them into the current project's `Assets` directory.
- `.fbx`, `.glb`, and `.gltf` are the primary model formats. ByteEngine imports node hierarchy, transforms, indexed meshes, normals, UV0, multiple primitives, material assignments, embedded/external textures, bone data, inverse bind matrices, joint weights, and animation metadata.
- `.obj` imports positions, indices, normals, UVs, generated normals, and basic MTL diffuse data.
- Model meshes, materials, textures, skeleton data, and animations are stable sub-assets. Scene instances reference the model GUID and sub-asset key instead of duplicating GPU resources.
- Select a model in Assets to inspect mesh/material/skeleton/animation counts, change import scale or normal/material settings, and Reimport. Source edits retain the model GUID and refresh cached resources.
- Drag a model from Assets into the 3D Scene View to instantiate its imported hierarchy on the ground plane. Imported nodes preserve local transforms and mesh/material assignments. Each hierarchy carries a persistent model-instance marker so reimport and scale normalization remain scoped when multiple copies use the same asset.

FBX importing is backed by Assimp and supports static geometry, external textures, skeleton metadata, vertex weights, node transforms, and animation clip metadata. Import-scale analysis uses the transformed hierarchy bounds to correct common FBX unit-conversion problems. The Blueprint workspace includes explicit model reimport and scale-normalization tools.

## Rendering and editor

- `Transform` provides Vector3 position/scale, quaternion rotation, world/local matrices, and 3D hierarchy composition.
- The 3D renderer uses cached indexed geometry, depth/culling, base-color and normal textures, metallic/roughness values, ambient light, and directional-light PBR shading.
- `Camera3D` drives Game View when present; `Camera2D` is the fallback. Game View renders at project resolution and letterboxes into the editor panel.
- In 3D Scene View: RMB+mouse looks, RMB+WASD flies, Q/E descends/ascends, MMB pans, the wheel dollies, F frames selection, clicking picks transformed bounds, and XYZ handles move objects.
- Box and capsule collider wireframes are editor-only overlays. They do not appear in Game View.

## Byte Blueprints and characters

Create a `.byteblueprint` from the Assets panel and choose Generic Object or Character. A Character Blueprint starts with:

- a root object;
- `CharacterController3D`;
- `CapsuleCollider3D`;
- `AnimationController`;
- `Health`, `MoveSpeed`, and `Team` variables.

Double-click a Blueprint to open its dedicated workspace with hierarchy/components, a 3D preview viewport, variables, logic-module relationships, skeleton information, and socket data. Drag a Blueprint into the 3D Scene View to create a fresh instance with new object IDs while retaining the Blueprint asset/instance relationship.

The skeletal foundation persists skeletons, bones, joint/weight data, `SkeletalMeshRenderer`, character animation-state clip names, and bone-relative sockets with optional preview-asset GUIDs. Runtime GPU skinning and animation playback/blending remain future work.

`CharacterController3D` includes acceleration, air control, gravity, finite ground-collider bounds, rotated ground normals, maximum-slope rejection, snapping, step tolerance, coyote time, and buffered jumping. Trigger colliders are excluded from grounding.

## ByteGraph visual logic

`.byteevents` assets are executable visual-logic modules. The docked ByteGraph workspace supports condition and action nodes, explicit condition wiring, AND/OR gates, sequential execution wires, true/false branches, keyboard and mouse conditions, object/character/transform/variable operations, comment groups, undo/redo, auto-arrangement, live runtime tracing, and Play-mode hot reload.

Blueprints can attach Event Modules. Spawn Blueprint actions create live runtime instances without modifying editor selection, undo history, or scene dirty state. Required-component validation reports incompatible module targets.

## Variables and persistence

Number, String, Boolean, Vector2, and Vector3 values share one variable architecture across Global, Scene, Self/Object, and Component scopes. Persistent references use GameObject and asset GUIDs. Nested fields such as `Transform.LocalPosition.X` are writable and survive the owning struct write-back.

Scenes persist model references, character components, variables, and Blueprint instance relationships. Play Mode uses isolated scene/object/global copies; Stop discards runtime mutations.

## Run, test, and publish

```powershell
dotnet run --project Editor/ByteEngine.Editor/ByteEngine.Editor.csproj
dotnet run --project Tests/ByteEngine.Tests/ByteEngine.Tests.csproj
./Tools/PublishEditor.ps1
```

The test project is part of `ByteEngine.sln` and covers persistence, generated end-to-end FBX importing, transformed FBX scale analysis, per-instance model markers, finite character grounding, jump assistance, ByteGraph conditions, branching, Trigger Once, and variable actions.

The publish script produces a self-contained Windows x64 editor at `Dist/ByteEngine/ByteEngine.Editor.exe` and creates a zero-copy hard-link launcher at the repository root.

## Current limits

- Imported skeleton and animation data is persistent, but runtime deformation and playback are not implemented yet.
- Character grounding is geometry-aware for bounded ground colliders, but ByteEngine does not yet provide a general rigid-body collision or trigger-event solver.
- Live socket attachment previews and a complete per-instance override UI are incomplete.
- OBJ support is intentionally basic; GLB is the recommended exchange format.
- Shadows, terrain, navigation meshes, and full physics are not yet included.
