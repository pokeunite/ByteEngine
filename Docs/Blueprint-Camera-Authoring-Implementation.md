# Blueprint camera authoring — first implementation stage

FPS working project: `C:/Users/codex/Documents/fps template/fps template.byteproject`.

This stage follows the separation between editor navigation and game camera authoring observed in the authorized UE4 4.27 checkout at `D:/ue4 sources/UnrealEngine-4.27`. ByteEngine implementation is original; no Unreal source is incorporated.

## Implemented

- Blueprint editor navigation persists independently for each project and asset in the user's editor preferences. Corrupt preferences fall back safely.
- Refreshing a Blueprint preview preserves navigation and selection. Model reimport no longer automatically reframes it.
- Frame All and Frame Selected preserve the viewing angle and account for viewport aspect ratio; Reset Editor View is explicit.
- Game Camera View renders the selected camera, or active camera, without changing scene camera ownership or authored transforms. Editor manipulation is disabled in that view.
- The inspector explains Camera Boom runtime ownership.
- FPS setup preserves existing camera positions, rotations and clipping. New eyes default to 1.65 m with a .03 m near plane. Arms are attached using their authored positions instead of inferred mesh bounds.
- FPS runtime no longer overwrites the camera's near plane.
- The user's FPS Blueprint, Main scene and instance baseline were migrated to camera-mounted arms. Existing scene-specific offsets remain overrides. Original files are backed up under the project's `.byteengine/backups/fps-view-20261010-001300`.

## Verification

Release tests and Debug editor builds pass. Focused tests cover saved navigation, project isolation, corrupted preferences, narrow/wide framing, repeated FPS setup, sibling-arm migration, clipping and fixed-eye pitch/yaw. The actual Documents project loads successfully and its Blueprint and placed scene both pass fixed-eye and arm-follow-pitch checks. Existing regression suite also passed during validation.

Evidence: `.artifacts/blueprint-camera-tests.log`, `.artifacts/fps-project-camera-tests.log`, `.artifacts/blueprint-camera-build.log`, `.artifacts/blueprint-camera-debug-build.log`.

The installed editor in `Dist/ByteEngine` and Debug editor output are updated. Existing BrowserRuntime and PlayerRuntime packages were preserved. No game was exported and no Git push performed.

## Remaining rework

This completes the initial camera correction, not the complete Blueprint architecture proposal. Stable component IDs/schema migration, shared resolution and transactional instance propagation remain next. Dedicated FPS view pivot/component, owner-only rendering and the redesigned Blueprint workspace remain subsequent stages. Browser runtime publication must incorporate the core clipping change before future web exports.
