# Shared runtime performance repair

This pass addresses engine costs identified in the Hazard Pay browser captures.
The changes live in Core and the browser player, so future projects use them
without copying Hazard Pay scripts or changing game balance.

## Changes

* Browser frame transfers reuse bounded, exact-length buffers. Mesh, instance,
  texture and UI packets retain the existing wire format and synchronous handle
  lifetime. Texture rectangles copy directly to the destination buffer.
* Draw matrices, instance matrices, shadow instances and wet-contact vectors use
  binary packets instead of per-frame float-to-text conversion and JSON parsing.
  Packet counts/lengths are validated before use; visible and shadow ordering
  have independent numeric regression checks.
* Rigid single-bone mesh parts animate through their model matrix. Nonuniform
  scale, shear and genuinely deforming parts keep CPU skinning. Only bones used
  by a mesh part are evaluated and compared for deformation work.
* Static renderers cache ancestry and world bounds with invalidation for live
  parenting, LOD, mesh and transform changes. Scene render ordering is reused
  until authored order or a renderer's priority changes.
* Runtime blueprint templates are cached only in immutable packaged projects.
  Each instance receives a deep copy of mutable object/component/variable data;
  editor imports continue to observe live changes.
* Referenced spawn assets are prepared before Play, without instantiating
  enemies or running their callbacks. Traversal has a 256-asset limit. Prepared
  browser textures have a 64 MiB residency budget rather than unlimited pinning.
* A* reuses its search state but never caches routes. Current obstacles, heights,
  links and costs still apply. Large grids use sparse state to bound dense
  storage. AI decision scratch collections are also reused.
* Changing browser geometry uses a three-buffer rotation, bounded by a 64 MiB
  global extra-buffer budget. Static geometry retains one buffer. Dirty texture
  chunks rebuild mipmaps once per texture per frame, including interior edits.

## Regression protection

CI runs portable allocation budgets, mutable render-cache invalidation, native
physics/LOD allocation checks, numeric rigid/fallback animation tests, direct
RGBA/HDR upload checks, scene-loading lifecycle checks and browser wire tests.
Browser tests exercise buffer retirement, texture subarray offsets, mipmaps,
mouse-button chords, pause, bounded profiling and the performance release gate.

Use the full-game checks as well as unit tests. A passing unit test does not
establish a particular frame rate on a user's GPU. Compare the same content,
camera, resolution and graphics preset, without a compiler or another game
running. Do not compare deep-profiling FPS against normal gameplay FPS.

## Boundaries

These fixes remove avoidable engine work; they cannot guarantee that every new
scene meets its frame budget. Excessive geometry, transparency, expensive
shaders, large shadow casters, gameplay scripts or unbounded spawning can still
overload a browser. The capture and validation tools remain part of the release
workflow so those costs are identified before publishing another game.

No new game export or itch.io upload is part of this pass. Existing export
content is tested with a runtime overlay, leaving authored assets and saves
untouched.

## Measurements

The archived baseline is `artifacts/all-bottlenecks-baseline/runtime`; captures
use the existing `Hazard_Pay-Web-20261028-012032-492fd5` content at fixed
Balanced/720p. Both runs draw 603 instances in 66 calls at 1280x720, with no
pending texture uploads or reported runtime errors. Compilers were stopped.

| Steady scene metric | Archived runtime, immediate repeat | Final runtime |
| --- | ---: | ---: |
| FPS | 15.70 | 19.25 |
| Managed work, mean | 51.91 ms | 41.49 ms |
| Render preparation allocations, mean | 662,077 B/frame | 90,769 B/frame |
| JSON frame size, mean | 87,986 characters | 7,882 characters |
| GPU time, asynchronous mean | 28.36 ms | 24.37 ms |

This is about 23% more FPS, 20% less managed time, 86% fewer render allocations
and 91% less JSON. The first earlier baseline measured 12.16 FPS, demonstrating
material laptop/run variation. Use the immediate repeat for the conservative
comparison. GPU samples are asynchronous and must not be added to CPU times.

**The final runtime still fails the 30 FPS / 50 ms p95 release gate**: 19.25 FPS
and 66.6 ms p95. This pass is an improvement, not evidence that performance is
solved on this laptop. CPU deformation, scene rendering and GPU work remain
substantial costs; no rendering quality, enemy count or animation update rate
was reduced to obtain this result.

The preceding intermediate combat check reduced the sampled render allocation
from 5,258,504 to 173,120 B/frame and sampled animation work from 21.30 to
10.00 ms. Those are deterministic simulation snapshots, not a combat FPS
benchmark. The final combat test separately verifies moving, animated spiders,
finite poses, player movement, operation start and no frame errors.

Evidence: `artifacts/all-bottlenecks-final/comparison.json`, `idle/game.json`,
`idle/game.json.validation.json`, the archived baseline `repeat/game.json`,
`artifacts/all-bottlenecks-final-combat.log`, and the native/portable regression
logs under `artifacts/all-bottlenecks-*`.

## Installation and final functional checks

The installed editor and Windows player have matching Core hashes. All six
repository/installed Dist, Debug and Release browser-runtime copies match the
published boot manifest. The installed static gameplay plugin matches the
canonical KarlsonDemo 3.47.0 assembly hash.

The final full-game combat test passed. Actual DOM input tests passed aim/fire
chords, independent release and Escape pause/resume. Weapon/machine particle
draws changed 4,612 framebuffer pixels in the sampled effect check. The complete
recorder/download test passed with 596 frames, 42 samples and 32 deep windows;
every deep frame was represented. Recorder testing overlapped installation and
is a correctness check, not a performance benchmark.

No game export was created. Existing exported folders and the live itch.io page
retain their previous runtime until a new export is explicitly requested.
