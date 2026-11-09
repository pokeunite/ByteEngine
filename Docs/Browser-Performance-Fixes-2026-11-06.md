# Browser fullscreen performance fixes — 6 November 2026

Maintained source: `C:/Users/codex/.codex/worktrees/52af/ByteEngine`.
Installed engine: `C:/Users/codex/ByteEngine`.

## Changes

- Preserve the fixed 720p default and high-DPI cap. Native remains an explicit override rather than an adaptive-resolution target.
- Default programmatic web exports and the Export Web Game panel to Fast at fixed 720p. This removes the sun-shadow pass and edge-smooth post cost for the default web build while retaining textured PBR lighting, sky, fog, HDR tone mapping, and the full-resolution Canvas UI. Balanced and High remain explicit quality choices.
- The supplied High-quality game manifest remains capable of selecting its authored quality; a new export is required to adopt the new default.
- Avoid supersampling the Balanced UI beyond 1920x1080. High retains its larger UI budget.
- Keep Auto world resolution at or above 50% scale. The old 35% floor produced a 672x378 image in the test workload without a meaningful frame-rate gain.
- Compare the exact UI command bytes, logical/output sizes, and image revisions before clearing and redrawing the Canvas2D overlay. Unchanged UI stays on its existing canvas; updated font/image data invalidates it.
- Reuse a sun-shadow map only when its resolution/matrix and every shadow command are unchanged. Changed instance packets, skin palettes, meshes, textures, materials, removed resources, or disabling shadows invalidate it. Float32 rounding in the cache comparison must not cause false invalidation or stale reuse.
- Quantize browser TrueType raster sizes into four buckets (16/32/48/64 px) and scale glyph quads to the requested size. Text dimensions remain unchanged, while each font can retain at most four atlases instead of creating one 1024²/2048² atlas per UI size.
- Label generated font and VFX textures by owner in diagnostics instead of reporting every one as `dynamic/embedded texture`.
- Fast mode memoizes static opaque render commands. Static meshes are separated from animated, skinned, transparent, water-volume, and overlay submissions. Unchanged static meshes reuse their mesh/material IDs, packed matrices, and draw records across camera movement. Any mesh geometry revision, material state, shadow participation, or submission-order change invalidates the whole static cache and rebuilds it safely. Repeated invalidation temporarily routes static objects through the dynamic path instead of rebuilding every frame.
- Group structurally identical opaque material states only after the normal depth ordering pass. Transparent and overlay ordering is untouched; depth ordering is retained within each material group. This reduces the near one-material-change-per-draw pattern reported in the recording.
- Retain released VFX atlases for up to 30 seconds in a 32 MiB LRU. Releasing an effect no longer immediately destroys and later rebuilds its generated atlas.
- Cap tinted UI-image caching at 16 MiB and 16 entries on devices reporting 4 GiB or less; larger devices retain the previous 64 MiB/32-entry policy.
- Reuse BrowserDrawCommand and BrowserUi records from frame-owned pools. UI command state is copied into the pooled record, so subsequent JSON frames remain immutable.
- Reduce retained frame-transfer storage from 64 MiB to 32 MiB. Active frame packets are unchanged; only unused warmed buffers are released sooner.
- Run the expensive software occlusion pass only for Balanced/High. Fast keeps frustum culling and safe depth ordering, but avoids CPU rasterization that was no longer justified after its shadow pass was removed.
- Collect render-stage timings in normal play. They are available through `PerformanceDetails().bridge.stagesMs` without enabling F9 deep profiling.
- Store render batches as value records, avoiding one small managed object per batch per frame.
- Compile browser native/AOT inputs with -O3, without fast-math or relaxed floating-point semantics.
- Allow zero-UI worker probe frames without attempting to decode a nonempty disabled UI buffer.

Game simulation, physics tolerances, transparent draw ordering, HDR lighting, texture filtering, and fixed-step timing were not reduced.

## Regression tests

`Tools/TestBrowserFrameCaching.cjs` covers static UI/shadow reuse and invalidation by texture contents, light matrix, instance matrices, skin palettes, mesh uploads, and shadow disable/re-enable. Existing retained-GPU, packet ownership, uniform-cache, worker compatibility/fallback, binary transfer, resize, and presentation tests remain applicable. `Tests/ByteEngine.Browser.Tests` covers portable runtime behavior and allocation-sensitive render caches.

## Same-game JavaScript A/B capture

Chrome, AMD integrated graphics, Balanced quality, DPR 1.5, existing packaged Hazard Pay benchmark, 30 enemies. Both runs use the same package and installed managed runtime; only the maintained browser JavaScript is overlaid. This is a fullscreen GPU-completion/submission measurement, not a claim of browser-presented FPS. Gameplay evolves during the sequential stages, and background system load can vary.

| Stage | Baseline submissions/s | Optimized submissions/s | Baseline p95 ms | Optimized p95 ms |
|---|---:|---:|---:|---:|
| Fullscreen fixed 720p | 29.76 | 31.28 | 59.9 | 48.0 |
| Fullscreen Auto | 31.66 | 31.69 | 59.0 | 49.0 |
| Fullscreen Native | 20.79 | 22.35 | 80.7 | 75.4 |

The first optimized embedded run skipped 320 of 1,017 UI redraws. Dynamic actors correctly prevented reuse of the full shadow map in that workload; shadow caching is aimed at genuinely static frames, not a claim that all animated scenes stop rendering shadows.

Evidence: `artifacts/browser-optimization/baseline` and `artifacts/browser-optimization/optimized-js-rerun`. A failed first fullscreen attempt was rejected because Chrome returned `TypeError: not granted`; it was not included as a completed comparison. The test now retries a denied transition only through a real click and never fakes fullscreen state.

No game is exported or uploaded by this engine work.
