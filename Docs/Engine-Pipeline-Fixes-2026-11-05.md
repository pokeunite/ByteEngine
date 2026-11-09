# Engine pipeline fixes — 2026-11-05

Maintained source: `C:/Users/codex/.codex/worktrees/52af/ByteEngine`. Installation: `C:/Users/codex/ByteEngine`. No game export or upload is part of this work.

## Implemented changes

- Browser submission allows two outstanding frames. GPU fences still bound outstanding work and protect packet ownership; the next CPU frame can overlap the previous GPU frame. Upload-bearing frames are never dropped.
- Native and browser camera rejection use the same RenderVisibility implementation and draw ordering. Opaque geometry sorts front to back before batching; transparency retains back-to-front ordering.
- Browser main geometry uses conservative software occlusion. Directional-shadow casters have an independent light-frustum list, including offscreen casters and dedicated shadow meshes. Shadow-only commands do not enter the main pass.
- Native main, directional-shadow and point-shadow shaders perform GPU skinning. Bone palettes use texture buffers, avoiding a small fixed uniform-array limit. Meshes upload immutable influences once and changed palettes when needed. Renderer capability queries are cached. Existing CPU and crowd-pose paths remain available.
- Scene rendering retains membership of objects with render work and caches their actual render callbacks. Component membership/startup changes invalidate it; editor traversal still includes non-rendering objects. Runtime render-order providers remain live.
- Distant skeletal animation has automatic visual sampling budgets, with full-rate close views, explicit author overrides/opt-out, normal playback clocks and immediate bone queries. Physical recovery and hit reactions keep immediate updates.
- Browser matrix and skin packets remain in the rendering worker. Unchanged packets send references instead of binary copies. Changed packets reuse storage. GPU instance streams also remain resident until the packet changes. Stream/packet eviction follows live draw-slot membership.
- FrameTransferBuffers reuses empty size-pool bookkeeping instead of allocating a new linked-list container each warmed frame.

## Verification

- Native GPU skinning: 300-bone palettes and three changing poses match CPU reference color pixels, directional shadow depths and all six point-shadow faces. Following static draws remain correct; no GL errors.
- Existing native graphics suite: instancing, material states, signed/nonuniform normals, static/animated shadow caches, text batching and conservative occlusion pass, including 24 randomized image comparisons.
- Render membership fixture: 10,000 non-rendering objects are excluded; adding/removing renderers and editor traversal remain correct. Near/mid/far animation budgets and explicit opt-out pass.
- Existing engine, animation and dispatch regression suites pass.
- Portable runtime suite passes lifecycle, input, allocation, frame transfer and render-cache invalidation checks.
- Actual browser GPU fixture: unchanged frames have zero packet reads and zero instance-buffer uploads, with identical pixels. Changed transforms, resize, shadow-only isolation and resource retirement pass.
- Browser worker compatibility, retained-packet ownership, frame transfer, uniform caching and presentation sizing checks pass.

These are correctness and removed-work measurements. They are not a claim of a particular FPS improvement for every game or a cross-engine FPS benchmark. Native fullscreen resolution still has its normal pixel cost. Automatic resolution remains an optional presentation policy.

## Test entry points

- `Tests/ByteEngine.Tests`: `--native-skinning`, `--render-pipeline`, `--roadmap-render`, `--engine-optimization`, `--animation-optimization`, `--dispatch-performance`.
- `Tests/ByteEngine.Browser.Tests`.
- `Tools/TestBrowserRetainedGpu.cjs`, `Tools/TestBrowserRetainedPackets.mjs`, `Tools/TestBrowserWorkerCompatibility.cjs`, `Tools/TestBrowserPackedFrames.mjs`, `Tools/TestBrowserFrameBuffers.mjs`, `Tools/TestBrowserUniformCache.mjs`, `Tools/TestBrowserPresentation.mjs`.

## Installed-runtime validation

The final Windows editor/player identity check passes. Installed browser JavaScript matches maintained source. Browser integrity verifies all 59 boot resources and the enabled project plugin fingerprint. The real exported-game workload passes aiming/firing, independent button release, Escape pause/resume, VFX pixel/opacity checks and resizing. Filtered diagnostic passes have a sparse-slot lifetime regression test.

Fullscreen captures under `artifacts/engine-performance-tests/fullscreen` were recorded during compilation and are correctness checks, not before/after performance evidence. The separate `fullscreen-idle` capture runs after compilation.

Idle compiler-free capture, Balanced quality, 30-enemy benchmark, AMD Vega 10: fullscreen fixed 720p 40.2 completed frames/s; fullscreen native 1920x1080 35.8 completed frames/s. These are worker GPU-completion rates, not presentation timestamps or a controlled before/after speedup. All tested transitions recorded zero errors. This workload still has room for further performance work.
