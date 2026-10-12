# First Person Camera

`FirstPersonCamera3D` is a native engine component available under Camera → First Person Camera. New FPS setups use it instead of a spring arm. It follows the first-person ownership pattern inspected in the authorized UE4 4.27 template: controller owns look rotation, the upright character follows yaw, the child camera owns its saved attachment, and view arms/weapons attach beneath that camera. The implementation is original ByteEngine code.

## Rendered rotation correction

The initial FPS tests checked gameplay forward/up but missed the rendered matrix. A later reproduction at yaw −179°, pitch −89° proved that Transform.WorldRotation composed quaternions in the opposite order to Transform.WorldMatrix. This produced visible banking when camera pitch and parent yaw were combined even though the gameplay direction tests passed.

Transform now composes quaternion world rotation as parent × local, matching System.Numerics row-vector matrices localMatrix × parentMatrix. World assignment uses inverse(parent) × world. Tests compare both rendered and gameplay forward/up, require horizontal camera right (no roll), validate arm position against its render matrix, and cover three-level hierarchies/world assignment/world-preserving reparent. The pre-fix failure is saved in `.artifacts/fps-rotation-before.log`; passing results in `fps-rotation-after.log` and `fps-rotation-engine-tests.log`.

The public-domain [SimpleFPSController Unity source](https://github.com/MBoffin/SimpleFPSController/blob/main/SimpleFPSController.cs), developed from the small Scrappy Defense FPS game, was inspected as an additional reference. Its local camera pitch and separate player yaw confirm the intended hierarchy; no binary decompilation was needed and no source was copied.

## Setup

Use Blueprint → Setup → First Person Shooter. This creates or reuses Player Input, Character Movement, a capsule, health/shooting, Camera and Model. It attaches Model to Camera while preserving authored poses, installs First Person Camera on the character root and removes the old spring arm. New cameras start at `(0, 1.65, 0)` with a .03 near plane. Existing camera offsets/FOV/clipping remain unchanged.

```text
Player (Player Input, Character Movement, capsule, First Person Camera)
  Camera (Camera3D, audio listener, optional launcher)
    Model / view arms
    Viewmodel Socket / weapon
```

Set **View Camera** to the child camera. Position Camera to author the eye anchor, then place arms/weapon relative to Camera. Mouse sensitivity is degrees per pixel; mouse deltas are not multiplied by frame time. Vertical/horizontal inversion and pitch limits are editable. Default limits are −89..89 degrees. The camera has no collision probe, orbit distance, position lag, rotation lag or roll accumulation. Clipping/FOV stay on Camera3D.

Character FaceCamera yaw is immediate when the dedicated FPS view is active. Applying third-person setup replaces FPS ownership with the spring arm. A legacy boom targeting the same camera cannot override an enabled dedicated FPS view.

The eye anchor is captured from the authored attachment on runtime start and follows the character transform, independent of pitch. A zero anchor is valid. `RebindAuthoredEye()` explicitly accepts an eye position changed during runtime. `SnapToView()` updates immediately, useful for teleport/diagnostics. Camera object GUIDs serialize and remap during Blueprint placement.

## FPS project installed

`C:/Users/codex/Documents/fps template` now uses this component in both FPSController.byteblueprint and Main.bytescene. Its instance source baseline is migrated too; scene-specific camera/arm offsets are preserved. Backups are in `.byteengine/backups/fps-component-20261010-003837`. This migration does not change arm geometry or pose alignment.

Following visual verification, a second correction aligns both the source and placed player: Camera local `(0, 1.65, 0)` and Model local `(0, −.35, −.45)` beneath Camera. The previous eye/arms positions are backed up under `.byteengine/backups/fps-alignment-20261010-010044`. This clears the aiming area without rescaling or changing the mannequin geometry. The updated source baseline keeps propagation consistent. Project validation is saved in `.artifacts/fps-rotation-project-tests.log`; native captures span yaw 0/45/90 and pitch 0/±60.

## Verification

- Dedicated component regression: 30/60/144 FPS, ±89 pitch, yaw across wrap boundaries, immediate body yaw, stable arm offsets, sensitivity/inversion, cloning and remapped camera references, repeated setup and FPS→TPS conversion, exact zero anchor.
- Actual project Blueprint and scene load/check pass.
- Native GPU snapshots at neutral/up/down pitch pass without GL errors.
- Existing native engine regression suite passes.
- Release tests, Debug editor and browser source build pass.

Evidence in `.artifacts/fps-component-tests.log`, `fps-project-component-tests.log`, `fps-component-render.log`, `fps-engine-regression.log` and `fps-browser-compile.log`.

Installed desktop editor and existing native player core libraries are updated. Browser source is verified; the previously installed AOT browser runtime is unchanged and needs publication before exporting an FPS web game. No game was exported or Git pushed.
