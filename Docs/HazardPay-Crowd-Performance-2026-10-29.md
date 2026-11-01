# Crowd profiling: 120 spiders

The test spawns 120 actual animated Hunter/Raider blueprints in the complete Hazard Pay scene. AI, navigation, damage components, real meshes and scenery remain active. It uses a wide camera, 1280x720 and VSync off, with 60 warm-up frames and 180 measured frames. The captured device is AMD Radeon RX Vega 10 Graphics. Timings include opt-in component profiling overhead. It is a deliberately heavy diagnostic case, not a guarantee for every camera or hardware configuration.

| Measurement | Baseline | Final measured pass |
|---|---:|---:|
| Average FPS | 4.08 | 23.12 |
| Average frame | 244.85 ms | 43.25 ms |
| 95th percentile frame | 344.96 ms | 63.94 ms |
| Render CPU/driver time | 68.89 ms | 18.79 ms |
| Final scene physics | — | 2.83 ms |
| Final scene update | — | 18.90 ms |

The 40 FPS target is **not yet met** in this measured workload. A stable target requires roughly 25 ms frames, including demanding frames, rather than a best-case average. Remaining AI query/update work and rendering/driver time are material limits. No artificial FPS cap, removed enemies or frozen gameplay is used to claim success.

## Implemented changes

- Opt-in immutable crowd pose/mesh sharing for repeated simple actors, enabling the existing mesh/material instance batcher to group them. Geometry is cached by model, clip and quantized pose; instance world placement remains independent. Physical reactions, transitions, layered/IK-controlled actors and per-instance mesh hiding retain the private path.
- Bounded shared-pose storage and reference-counted disposal. Pose snapshots and bounds consume the displayed shared geometry. Destroying an actor does not destroy another actor's pose.
- Imported mesh collision geometry polls model replacement once per frame while transform and shape edits remain immediate.
- Primitive world bounds are cached against exact transform, center and size values. Moving or resizing an object invalidates the cached result.
- Static scenery candidate pairs are retained while the static index is unchanged; navigation/character actors use the moving index even without a Rigidbody.
- Static mesh contact cache keys include geometry identity, preserving invalidation on reimport or geometry replacement.
- The optimized diagnostic uses a 0.25-second AI decision interval. Movement and committed attack progression still update each frame. Game blueprint tuning is explicit and should not be mistaken for disabling AI.

The raw reports are `artifacts/spider-stress-baseline.json` and `artifacts/spider-stress-optimized.json`; the reproducible harness is `Tools/MiningPrototype/MiningSpiderStressRun.cs`. Run without other GPU-heavy applications rendering. Further work should measure query refresh and GPU/pass timings before deciding between incremental query broad-phase updates, additional distance LOD and GPU skinning. Do not promise universal 40 FPS from this pass.

Shared pose geometry remains classified as animated for shadow purposes, so crowds do not turn into static scenery casters.
