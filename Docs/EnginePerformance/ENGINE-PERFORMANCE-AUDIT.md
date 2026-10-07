# ByteEngine: engine-wide performance audit

5 October 2026. Scope: reusable engine systems and editor/runtime reliability. Goblin Scraper is one regression project, not the optimization specification.

## Crash and compatibility

The supplied archive records `MissingMethodException: SkeletalMeshRenderer.set_AnimationUpdateInterval(Single)` in `VehicleBuilder3D.TickBattlefield`. Plugin 0.4.4 required a method absent from the loaded core. The primary published editor still held a 3 October core while the development builds had been updated on 5 October. This is a package/runtime mismatch; the archive does not establish a driver or memory crash.

Plugin 0.4.5 removes the game-only distance animation thresholds and the new API dependency. The main `Dist/ByteEngine` editor and development builds have been refreshed. Startup diagnostics now identify the loaded core path and module identifier. API version checks remain too coarse: different core API builds report the same engine version, 0.7.0. A separate compatibility contract should precede further plugin-facing API requirements.

The general animation interval API remains at its default full-rate behavior. A shared animation-budget manager is a proposed engine feature. Previous 0.4.4 FPS measurements do not describe the corrected package because its game-specific throttling was removed.

## Comparison with established engines

This compares implementation approaches, not equivalent-scene FPS results. No identical Unity, Godot or Unreal benchmark was run.

| System | ByteEngine source finding | Established approach | Recommended direction |
| --- | --- | --- | --- |
| Profiling | PerformancePanel reports frame/process/memory data; RenderWorldStats has mesh and shadow/main draw counts. Separate GPU pass durations and a full CPU subsystem timeline are missing. | Unity exposes batches, shader-pass changes and geometry counts. Unreal Insights supports CPU/GPU tracing. [Unity profiler](https://docs.unity3d.com/Manual/ProfilerRendering.html), [Unreal Insights](https://dev.epicgames.com/documentation/en-us/unreal-engine/unreal-insights-in-unreal-engine). | Reusable CPU scopes, delayed GPU timer queries, per-frame allocations/GC history, per-viewport costs and capture/export. Read GPU results without stalling the current frame. |
| Repeated 3D geometry | RenderWorld queues and frustum-culls meshes but sends individual visible submissions to Renderer3D.Draw. Foliage submits individual placements too. | Unity uses instancing/material-state batching; Godot provides MultiMesh. MultiMesh grouping affects culling granularity. [Unity draw methods](https://docs.unity3d.com/Manual/optimizing-draw-calls-choose-method.html), [Godot MultiMesh](https://docs.godotengine.org/en/stable/tutorials/performance/using_multimesh.html). | Instanced identical mesh/material/state groups, divided spatially and handled consistently in shadow passes. Measure reduced state work separately from fewer draws. |
| UI and text | Renderer2D.DrawText draws glyphs separately. RenderContext queues individual UI commands rather than a combined vertex batch. | Godot's GLES batching groups compatible rectangles, text and GUI geometry. [Godot batching](https://godotengine.org/article/gles2-renderer-optimization-2d-batching/). | An order-preserving quad batch, flushing on texture, clipping, blend or render-target changes. Preserve transparent UI order. |
| Animation | SkeletalMeshRenderer computes skinning on the CPU; Mesh uploads changed dynamic vertex buffers. Unchanged-pose caches help, but moving meshes retain this cost. | Unity supports CPU/GPU/batched-GPU deformation. Unreal's animation budget allocator favors significant meshes while reducing less important work. [Unity deformation](https://docs.unity.com/ko-kr/engine/6000.3/script-reference/unityeditor/meshdeformation), [Unreal budgets](https://dev.epicgames.com/documentation/en-us/unreal-engine/animation-budget-allocator-in-unreal-engine). | Vertex-shader skinning with CPU skeleton poses for sockets/root motion; a shared opt-in animation budget with interpolation and gameplay-critical safeguards. |
| Scene and transforms | Scene snapshots, per-component queries and ordered traversals recur. Transform recursively rebuilds world matrices/values. | Godot separates high-level scenes from lower-level servers and recommends measured optimization. [Godot servers](https://docs.godotengine.org/en/stable/tutorials/performance/using_servers.html), [optimization workflow](https://docs.godotengine.org/en/stable/tutorials/performance/general_optimization.html). | Reusable mutation-safe snapshots, subsystem registries and revision-cached transforms while retaining editable scenes. A complete ECS rewrite is not justified by this audit. |
| Core physics | PhysicsWorld3D visits every collider pair before AABB rejection. The native solver is linear-only; the vehicle plugin separately uses Bepu. | Bepu has a broadphase and sleeping infrastructure, plus guidance on shapes, solver cost and thread scheduling. [Bepu simulation](https://docs.bepuphysics.com/api/BepuPhysics.Simulation.html), [performance guidance](https://github.com/bepu/bepuphysics2/blob/master/Documentation/PerformanceTips.md). | Spatial candidate generation, then evaluate a common backend behind existing APIs. Preserve static triggers, compound shapes, masks and callback timing. |
| Visibility | Main frustum culling and the recent directional-shadow volume check exist. The inspected path has no general occlusion/LOD system. | Godot supports occlusion culling with scene-dependent CPU overhead. [Godot occlusion](https://docs.godotengine.org/en/stable/tutorials/3d/occlusion_culling.html). | Measure spatial grouping and LOD first. Occlusion is useful where geometry hides other geometry; it is not automatically worthwhile in every open scene. |

Hardware constraint: the user's runtime reports OpenGL 3.3 on Vega 10 integrated graphics. Instancing and vertex-shader bone palettes can target that baseline. Newer compute-shader paths cannot be copied without capability checks. Asset parsing can use workers; graphics uploads require correct context ownership. Avoid thread oversubscription.

## First shared fix implemented

GameObject.GetComponent<T> now scans directly without a LINQ iterator, preserving the first assignable component. Core physics now gathers bodies and colliders into reusable lists instead of nested iterator chains and fresh arrays. Enabled/active filtering, component order and all compound colliders are retained; Reset clears retained references.

There are no game-name, model or enemy conditions in these fixes. Existing general shadow/uniform/unchanged-skin-buffer optimizations remain. Batching, a new broadphase, GPU skinning and shared animation budgets are not implemented yet.

## Engine-only measurements

EngineArchitectureBenchmarks creates synthetic scenes without game assets, AI or game-plugin execution. It measures CPU scene updates only, with 24 warmup and 120 measured updates. Allocation uses current-thread allocated bytes. Raw captures are baseline.json and allocation-fix.json beside this document.

| Workload / logical count | Median ms before -> after | p95 ms before -> after | KiB/update before -> after |
| --- | --- | --- | --- |
| idle-components / 1000 | 1.861 -> 0.414 | 2.778 -> 0.464 | 516.3 -> 94.1 |
| idle-components / 10000 | 4.843 -> 2.145 | 5.296 -> 5.939 | 5156.9 -> 937.9 |
| hierarchy-transform-reads / 1000 | 1.655 -> 1.337 | 1.930 -> 4.179 | 915.1 -> 109.8 |
| hierarchy-transform-reads / 5000 | 6.096 -> 2.428 | 7.196 -> 3.525 | 4571.4 -> 547.3 |
| separated-static-colliders / 100 | 0.266 -> 0.093 | 0.278 -> 0.223 | 65.6 -> 9.8 |
| separated-static-colliders / 500 | 2.640 -> 0.889 | 3.694 -> 1.039 | 324.9 -> 47.3 |
| separated-static-colliders / 1000 | 9.687 -> 2.643 | 10.899 -> 3.878 | 649.1 -> 94.1 |
| ui-buttons-update-only / 100 | 0.047 -> 0.021 | 0.053 -> 0.030 | 86.6 -> 9.8 |
| ui-buttons-update-only / 500 | 0.216 -> 0.119 | 0.316 -> 0.198 | 430.4 -> 47.3 |

The 10,000 empty-component workload reduces allocations by approximately 82%. Its p95 worsened in this capture, so this is not a verified stutter fix. Median timings are indicative single-run snapshots affected by JIT, scheduling and thermal conditions; allocation counts provide stronger evidence. These are not FPS values or GPU measurements.

A thousand separated colliders still entail 499,500 candidate pairs per substep. Lower allocation does not fix that quadratic algorithm.

Reproduce from the repository using the Release ByteEngine.Tests executable with `--engine-performance <output.json>`. Multiple matched-condition trials are required before accepting wider timing claims.

## Prioritized engine work

1. **Reliability and measurement:** publish matched editor/core/plugin builds; add API compatibility gates and subsystem CPU/GPU/allocation captures. Distinguish unavailable GPU measurements from zero time. Keep prior builds recoverable.
2. **Benchmark coverage:** extend the CPU suite with native rendering scenes for repeated meshes, unique materials, transparency, UI/text, moving skeletons, lights/shadows and multiple editor viewports. Add physics stacks, triggers and moving bodies, asset loading and scene reload. Record median/p95, draw/state counts, allocations and visual/correctness checks.
3. **Scene work:** reusable frame snapshots and subsystem registries, then transform revision caches. Test creation/destruction during callbacks, reparenting, parent-scale changes and dynamic ordering. Do not turn editable scenes into opaque combined meshes.
4. **Rendering:** order-preserving UI batching, then compatible 3D instancing/material-state reuse. Test negative scale, culling, transparency and shadows. Preserve context-specific resource ownership and state invalidation.
5. **Physics:** replace all-pairs candidates with a broadphase; then consider sleeping/backend unification. Resolve the existing grounding failure before broadly replacing the solver. ContactKey currently coalesces same-type colliders on one object pair; review that separately rather than changing event semantics silently.
6. **Animation:** validate GPU skinning against CPU poses for sockets, root motion, blending, normals, bounds, shadows and physical/ragdoll deformation. Shared budgets must not throttle gameplay-critical collision poses.
7. **Parallelism and streaming:** introduce bounded workers only for measured, parallel-safe tasks. Keep scene mutation and graphics operations on their owning thread unless explicit synchronization is implemented.

Each stage must improve its generic workloads and pass mixed-scene/project regressions. Reducing visual quality or altering game rules is not an engine-wide performance result.

## Validation and limits

New allocation regression tests cover lookup ordering/base type/removal/absence; static trigger and box/capsule compound contacts; disabled/inactive colliders; dynamic/kinematic bodies; and world reset. Existing pose/socket/late-update startup checks pass. The Goblin native check with plugin 0.4.5 passes import, saved reload, deployment, driving into the arena and enemy reset.

The broad historical test suite retains an unresolved character-grounding failure previously reproduced with original scene-update code. The complete suite is not claimed green. No whole-engine 60 FPS guarantee or cross-engine FPS ranking has been established.


Latest native game check (one run, synchronized 1280x720, editor panels excluded): build mode 17.9 ms / 56.0 FPS; driving 42.3 ms / 23.7 FPS after removing the game-specific animation throttling. This illustrates the remaining combat/animation/rendering cost, not a reliable whole-engine score. Pass-isolation timings varied during that run and must not be interpreted as independent additive CPU/GPU costs. The generic CPU workload evidence above is the basis for the shared allocation improvement.
