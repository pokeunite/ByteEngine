# ByteEngine

ByteEngine v0.6 is a lightweight, 3D-first C# game engine and editor built on .NET 9, OpenTK, and ImGui.NET. It now has a real model pipeline, persistent model sub-assets, PBR material data, reusable Byte Blueprints, and the first character/skeleton workflow while retaining its 2D renderer and scene compatibility.

Startup opens the Project Browser. Create Project offers a blank Clean Project or a ready-to-run 3D Starter, with a selectable destination directory. You can also open an existing `.byteproject`, or pass one on the command line.

## Model asset workflow

- Drag files or folders from Windows Explorer into the Assets panel to copy them into the current project's `Assets` directory.
- `.glb` and `.gltf` are the primary model formats. ByteEngine imports node hierarchy, transforms, indexed meshes, normals, UV0, multiple primitives, material assignments, embedded/external textures, first-skin bone data, inverse bind matrices, joint weights, and animation names.
- `.obj` imports positions, indices, normals, UVs, generated normals, and basic MTL diffuse data.
- Model meshes, materials, textures, skeleton data, and animations are stable sub-assets. Scene instances reference the model GUID and sub-asset key instead of duplicating GPU resources.
- Select a model in Assets to inspect mesh/material/skeleton/animation counts, change import scale or normal/material settings, and Reimport. Source edits retain the model GUID and refresh cached resources.
- Drag a model from Assets into the 3D Scene View to instantiate its imported hierarchy on the ground plane. Imported nodes preserve local transforms and mesh/material assignments.

FBX files are recognized so projects do not lose their asset metadata, but direct FBX conversion is not included in v0.6. Export FBX content to binary GLB (recommended) or GLTF before importing it into ByteEngine.

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

The v0.6 skeletal foundation persists skeletons, bones, joint/weight data, `SkeletalMeshRenderer`, character animation-state clip names, and bone-relative sockets with optional preview-asset GUIDs. Runtime GPU skinning, animation playback/blending, and the complete interactive socket/Blueprint authoring tools remain future work.

## Variables and persistence

Number, String, Boolean, Vector2, and Vector3 values share one variable architecture across Global, Scene, Self/Object, and Component scopes. Persistent references use GameObject and asset GUIDs. Nested fields such as `Transform.LocalPosition.X` are writable and survive the owning struct write-back.

Scenes persist model references, character components, variables, and Blueprint instance relationships. Play Mode uses isolated scene/object/global copies; Stop discards runtime mutations.

## Run, test, and publish

```powershell
dotnet run --project Editor/ByteEngine.Editor/ByteEngine.Editor.csproj
dotnet run --project Tests/ByteEngine.Tests/ByteEngine.Tests.csproj
./Tools/PublishEditor.ps1
```

The publish script produces a self-contained Windows x64 editor at `Dist/ByteEngine/ByteEngine.Editor.exe` and copies the launcher to the repository root as `ByteEngine.Editor.exe`.

## Current limits

- FBX needs conversion to GLB/GLTF.
- Imported skeleton and animation data is persistent, but runtime deformation and playback are not implemented yet.
- The Blueprint workspace is an authoring foundation; full component/hierarchy editing, live socket gizmos and attachment previews, event-module picking, and per-instance override UI are incomplete.
- OBJ support is intentionally basic; GLB is the recommended exchange format.
- Shadows, terrain, navigation meshes, and full physics are not yet included.
