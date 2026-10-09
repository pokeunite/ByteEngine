# Blueprint system rework proposal

Date: 9 October 2026. Scope: reusable engine Blueprint authoring and a reliable FPS starter template. This is a proposal; no runtime or project assets were changed.

## Evidence and research limits

“eu4” is interpreted as Unreal Engine 4. Epic's official repository is https://github.com/EpicGames/UnrealEngine. It returned 404 through the available browser access. Epic documents linked Epic/GitHub account access at https://www.unrealengine.com/ue-on-github/. No authenticated UE4 source checkout was available for inspection. Consequently, the Unreal comparison below is documentation-based, not a claimed source audit. An authorized 4.27 checkout would allow a follow-up review of BlueprintEditor, SimpleConstructionScript, SCS_Node, BlueprintGeneratedClass, and the FPS template implementation. These are intended review targets, not files inspected here.

Primary references:

- UE4 Blueprint anatomy: https://dev.epicgames.com/documentation/en-us/unreal-engine/anatomy-of-a-blueprint?application_version=4.27
- Components window: https://dev.epicgames.com/documentation/unreal-engine/components-window?application_version=4.27
- Blueprint viewport: https://dev.epicgames.com/documentation/en-us/unreal-engine/blueprint-editor-viewport?application_version=4.27
- FPS template: https://dev.epicgames.com/documentation/en-us/unreal-engine/first-person-template?application_version=4.27

UE4 documents a Blueprint class combining a component hierarchy, defaults, construction behavior and runtime behavior. Its FPS template can be played immediately and its arms are replaced through the mesh component's details. ByteEngine should adopt this clarity while retaining its event-sheet workflow.

## Findings in ByteEngine

### Authorized UE4 source follow-up

After the user linked their Epic/GitHub accounts, authenticated Git access succeeded. A shallow, sparse UE4.27 checkout is stored exclusively at `D:/ue4 sources/UnrealEngine-4.27`, commit `3abfe77d0b24a6d8bacebd27766912e5a5fa6f02`. The earlier source-access limitation above describes the initial proposal; the following findings now come from direct source inspection. No Unreal code was copied into ByteEngine and no engine dependencies were downloaded.

- `Engine/Source/Editor/Kismet/Private/SCSEditorViewportClient.cpp`: `InvalidatePreview(bool bResetCamera)` calls `ResetCamera()` conditionally. Another refresh call passes false. `FocusViewportToSelection()` is a separate operation. ByteEngine should similarly separate rebuilding preview content from navigating the editor camera.
- `Engine/Source/Editor/Kismet/Private/SSCSEditorViewport.cpp`: refresh forwards the camera-reset flag, and Reset Camera is bound as an explicit command. This supports predictable editor controls rather than an unconditional reset on rebuild.
- `Engine/Source/Runtime/Engine/Classes/Engine/SCS_Node.h`: construction nodes carry `VariableGuid`, `ComponentTemplate`, child nodes and attachment information. ByteEngine should preserve component identity independently of its type/order and editable label.
- `Engine/Source/Runtime/Engine/Classes/Engine/InheritableComponentHandler.h`: `FComponentKey` associates component identity with an owner class and GUID, and the handler stores component override records/templates. ByteEngine's new patch model should likewise resolve identity before applying overrides; it need not duplicate Unreal's class machinery.
- `Templates/TP_FirstPerson/Source/TP_FirstPerson/TP_FirstPersonCharacter.cpp`: the camera attaches to the capsule with an explicit relative location and pawn control rotation; the first-person mesh attaches to that camera with explicit relative transforms and owner visibility. The gun attaches to a named grip socket in BeginPlay. These are configured asset-specific offsets, not mesh-bounds-derived eye placement. Do not copy their numeric transforms: Unreal uses different units/axes and a different mesh.

This is a focused review of the Blueprint preview, identity/override declarations and C++ FPS template. It does not claim an audit of the full compiler, every lifecycle path or binary Blueprint template assets. Next implementation should start with camera refresh preservation and an explicit FPS hierarchy, followed by stable component IDs and migration.

1. BlueprintDefinition already supports root/children, variables, event modules and a base Blueprint. BlueprintSerializer already supports variants and three-way inheritance merging. Keep these capabilities rather than rebuilding from nothing.
2. Inheritance array matching can fall back to type/name plus occurrence. Instance property overrides also use a component ordinal. Reordering repeated components is therefore a fragile identity boundary. Introduce persistent component IDs and explicit property paths.
3. BlueprintInstance stores a complete SourceSnapshot alongside ObjectMap. Retain snapshots as migration/merge baselines initially, but make explicit override records the normal authoring representation.
4. BlueprintInstanceSynchronizer.Replace destroys the old root before instantiating its replacement. Preserving object GUIDs does not preserve component object references or runtime state. Make propagation transactional, validate before swapping, and favor in-place updates for compatible changes.
5. BlueprintWorkspacePanel.FramePreviewBounds resets yaw/pitch, and RebuildPreview calls it. Consequently rebuilding the preview can discard the view the user established. Framing uses vertical FOV without an explicit horizontal/aspect fit. This is an observed implementation concern, not a reproduction of the user's exact camera failure.
6. SetupPlayablePlayer first calls SetupThirdPersonCharacter, then applies an FPS preset. The initial FPS conversion moves the Model using bounds and reparents it under the camera. This makes camera/arms setup partly heuristic and entangles the visual model with view selection.
7. CameraBoom3D contains both orbit and fixed-eye behaviors, plus authored offsets. Existing FirstPersonCameraTests cover fixed eye position, look rotation, authored positioning, arms visibility and repeat setup. Preserve those regressions; passing them does not verify the editor experience or every imported model.

## Proposed editor layout

Use one Blueprint workspace with a stable layout:

| Region | Contents |
|---|---|
| Toolbar | Save, Validate, Test, asset name, base Blueprint, unsaved status |
| Left | Searchable component/object tree; inherited items locked with an explicit override action; Add Component |
| Center | Viewport / Events / Defaults tabs; optional split Viewport and Events |
| Right | Selected component details, local transform, reference picker, override/reset indicators |
| Bottom | Collapsible validation messages and test output |

Viewport controls: Frame Selected, Frame All, Reset View, front/side/top, projection switch, collision/socket visibility. Persist navigation per asset in editor preferences, not in runtime Blueprint data. Do not auto-frame after ordinary property edits. Fit using both viewport dimensions and handle invalid/empty bounds safely.

Selecting a game camera offers a live camera inset and explicit Pilot Camera mode. Exiting pilot restores the editor view. Moving the editor camera never edits the game camera. Camera properties must show which runtime controller owns its transform and offer one explicit source of truth for eye position.

## Proposed data and runtime model

- Versioned Blueprint schema with persistent object IDs, component IDs and typed asset/component references. Names remain editable labels.
- Each variant or scene instance stores an override patch: set property, add/remove component, add/remove child and reparent. Target IDs and property paths replace occurrence-based matching.
- Preserve root placement as instance data; do not accidentally apply scene placement back to asset defaults.
- Shared resolver produces a validated resolved definition for editor preview, scene placement and runtime spawning. Cache by asset/dependency revision, invalidate on relevant changes.
- Instantiate in stages: resolve dependencies; allocate all objects/components; resolve references; apply defaults/overrides; run bounded construction behavior; register runtime systems; begin gameplay. Audit current SceneSerializer lifecycle before implementing this contract.
- Construction behavior uses existing event-sheet concepts in a restricted editor-safe context. It cannot play audio, read gameplay input, write saves, or recursively regenerate without limits. Generated objects have stable ownership and are replaced cleanly.
- Compile/validate means checking references, types, inheritance cycles, duplicate IDs and requirements, then preparing runtime data. Do not introduce an Unreal-sized visual scripting VM merely to improve the template.
- Update instances transactionally with a change preview. Preserve compatible components in place; rebuild incompatible subtrees only after successful validation, restore selection and undo atomically, report conflicts explicitly.

## FPS template contract

Create an explicit FPS player hierarchy rather than adapting the generic Model implicitly:

```text
FPSPlayer (movement/controller, capsule, health, input)
  WorldBody (optional, independent of view arms)
  ViewPivot (authored eye anchor; pitch)
    Camera (FOV and clipping)
    ViewArms (bundled mannequin arms; local alignment)
      WeaponSocket
        ViewWeapon
  WorldWeapon (optional third-person representation)
```

Use a dedicated FirstPersonView3D behavior, sharing look math where useful with the current camera rig. Player yaw rotates the body; pitch rotates the view pivot; eye position comes from the authored anchor. No orbit collision or model-bounds-derived camera placement. Optional bob/recoil affect a separate presentation offset and cannot corrupt the saved base transform. Declare the engine's forward/up/unit conventions in the editor.

Ship known-good mannequin arm offsets, skeleton/socket bindings and animations with the template. Imported arms use an explicit alignment preview with scale/orientation diagnostics; replacing a mesh must not reposition the camera. Allow owner-only view arms and optional world body through render visibility semantics rather than disabling the body globally.

The starter level includes a PlayerSpawn, configured player Blueprint, input mappings, HUD, a usable weapon and a small test range. Required checks: launch and move/look/fire without manual placement; save/reopen preserves framing and transforms; replace arms without moving eye; variant changes propagate without losing instance overrides; camera ownership is deterministic on spawn and respawn; browser and desktop behave consistently.

## Implementation order and acceptance gates

1. **Camera authoring first:** reproduce the user's asset, persist editor view, add pilot/inset, fix aspect-aware framing, expose transform ownership. Gate: edit/save/reopen/play without repeatedly repositioning either camera.
2. **Identity and compatibility:** add schema version/component IDs, migrate legacy assets once with a backup and ambiguous-match warnings, add reference/override validation. Gate: reorder two same-type components and retain the correct override.
3. **Shared resolver and safe propagation:** unify preview/spawn data resolution, stage lifecycle and transactional updates. Gate: inherited updates, broken-reference errors, failed updates and undo preserve the scene.
4. **New workspace and FPS template:** build the layout, explicit view hierarchy, bundled arms/socket/weapon bindings and test level. Gate: a new Documents project works on its first Play.
5. **Construction/event integration and platform verification:** bounded construction events, dependency caching and migration coverage. Gate: construction is repeatable, no duplicate generated children, identical resolved references on desktop/browser.

Keep legacy .byteblueprint support through migration. Do not change Dune Company's vehicle save Blueprint format: it is a separate game-specific system. No engine-wide rewrite or project conversion should happen until the camera reproduction and migration tests are established.
