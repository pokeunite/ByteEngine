# Sand lab and engine performance work

The large Workshop map is preserved. New scenes:
- Scenes/SandLab.bytescene: blue sphere reference comparison, WASD movement, Tab twin contact tracks, F6 reset, IJKL orbit, UO zoom, Vehicle Test navigation event.
- Scenes/SandVehicleLab.bytescene: the existing workshop and real vehicle solver on a32m fixed surface. Load your vehicle, then B to drive. Recovery now supports small maps. Dust disabled here to expose the actual surface.

## Reference implementation
The user-supplied video shows displaced surface geometry and reconstructed normals. The supplied shader uses an effect render texture, world-space texture mapping, tessellation, path blending and lighting. Its SnowTessellation.hlsl include is not present in the supplied Pastebin. Patreon returned403 to the local fetch and was unavailable to browsing; no claim that inaccessible code was read.
- https://pastebin.com/sbAzNFJL
- https://www.patreon.com/posts/interactive-snow-25641162

ByteEngine's implementation is newly written: a persistent257x257 field at12.5cm spacing, smooth capsule contact footprints, bounded depression and raised shoulders, disturbed ripple blending, signed16-bit height encoding, GPU vertex displacement and texture-derived normals. Collision sampling shares the CPU field, with encoding error below0.02mm in tests. Fixed terrain meshes remain uploaded; only dirty texture rectangles are transferred. No GPU readback in gameplay. This is the surface simulation demonstrated by the reference, not individual granular particles. Tracks persist during Play; the lab intentionally resets between sessions. Displaced surfaces receive shadows; casting displaced shadow maps is still unsupported and is explicitly disabled rather than casting a flat silhouette.

## General engine changes informed by source review
Stride RenderSystem.cs uses concurrent pools, reusable sorting storage and explicit Extract/Prepare/Draw phases:
https://github.com/stride3d/stride/blob/master/sources/engine/Stride.Rendering/Rendering/RenderSystem.cs
Wicked Engine wiRenderer.cpp UpdateVisibility (source fetched2026-10-07 aroundline3640) dispatches visibility work and compacts visible objects; its renderer schedules GPU work explicitly:
https://github.com/turanszkij/WickedEngine/blob/master/WickedEngine/wiRenderer.cpp

Applied to ByteEngine without replacing it:
1. Stable pooled component-update snapshots replace per-object LINQ sorting allocations. Addition/removal during update retains snapshot semantics.
2. Compatible material render states are cached within a main3D batch. Baseline GL state and samplers restore once at the pass boundary in finally. Auxiliary direct draws retain per-draw isolation.
3. Normal matrices move to CPU per draw instead of matrix inversion in every vertex invocation.
4. Reusable material height-field displacement, conservative displaced culling bounds, and rectangular texture updates. Upload restores caller pixel-store state, with neighbour/state regression checks.
5. Sand collision chunks retain32x32 cells as resolution changes, reducing the size of individual BVH rebuilds; GPU meshes select distance LOD. Large-map moving windows remain available unchanged.

This is an incremental engine improvement, not a claim that every backend subsystem has been rewritten. Multi-threaded GL command submission has not been copied from engines with different backend architectures. Future work should first profile actual engine passes, then prioritize persistent material/instance buffers, explicit per-view resource ownership, instancing and job-based immutable visibility extraction. Each change needs independent scene benchmarks and editor/player comparisons.

## Measured evidence
Same saved21-block vehicle, 1280x720, Radeon RX Vega10:
- Before focused batch/tile/LOD work: median32.70ms /p95 54.93ms.
- After: median30.61ms /p95 40.57ms.
- Sphere surface-only scene: median14.09ms /p95 15.49ms.
Native measurements include explicit GPU completion; they are not claims about editor FPS, and one run is not statistical proof of a universal speedup. Stationary surface: zero uploads across90frames; small contact:360-byte upload.
Component scheduling microbenchmark,1000objects x3components x100passes: LINQ33,604,040bytes /64.84ms; pooled4,000bytes /45.65ms. These are CPU microbenchmark results, not game FPS.

## Verification
Native sphere geometry, untouched regions, GPU/CPU height agreement, stationary upload suppression, regional upload bounds, reset and real WASD movement pass. Saved vehicle drove5.31m and produced a64mm physical groove. Terrain regressions, land physics and offscreen graphics regressions pass, including pixel-store preservation. Visual captures were inspected; an initially blank camera capture was corrected before accepting the test. General stable60FPS remains unverified. Full unrelated regression suite is not claimed passing.

Windows export verified: Builds/Windows/Dune_Company-Windows-20261007-100734-c3cdc3. Packed startup probe responds to input; vehicle lab loads through the player bootstrap, collision/driving and six audio clips pass. Executable smoke-test exit code0. Original Workshop.bytescene and live Saves/vehicle.json preserved; startup now Scenes/SandLab.bytescene. Backup is under Backups/sand-lab-*.
