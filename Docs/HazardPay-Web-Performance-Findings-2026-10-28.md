# Hazard Pay: findings from the supplied web recordings

Both recordings and their companion traces were inspected. They share a runtime
fingerprint and report AMD Radeon RX Vega 10 through ANGLE/D3D11. They are from
the same continuing session and overlap briefly. They do not establish results
on a different desktop GPU.

## Normal active-play evidence

Mouse-captured frames, with deep profiling off and both rendering probes off,
provide a useful active-play subset for this third-person game:

| Measurement | 15:50 recording | 15:52 recording |
| --- | ---: | ---: |
| Retained mouse-captured normal frames | 903 | 314 |
| FPS from mean frame interval | 17.3 | 20.7 |
| Managed CPU work per frame | 39.2 ms | 28.8 ms |
| Render-preparation allocation per frame | 2.23 MB | 1.04 MB |
| Scene-update allocation per frame | 0.142 MB | 0.058 MB |
| Mesh upload per frame | 2.05 MB | 0.925 MB |
| Texture upload per frame | 0.290 MB | 0.128 MB |
| Browser upload CPU time | 3.29 ms | 1.70 ms |
| Recorder transfer/collection work | 0.83 ms | 0.81 ms |

These are different gameplay intervals with changing automatic resolution, not
an A/B benchmark. MB here means decimal megabytes. Mouse capture is a proxy for
active play, not a guarantee that every frame is combat. CPU and GPU durations
overlap and must not be added to predict frame time.

The second recording's overall normal average is 32.3 FPS, but that includes
mouse-free/menu intervals rendered as low as 576x324. It masks the 20.7 FPS
mouse-captured subset. Overall report FPS is not sufficient for this capture.

## Confirmed pressures and interpretation

**Per-frame allocation and transfer churn.** The normal frame measurements above
show substantial engine allocations before frame encoding, plus recurring mesh
uploads. Under the heaviest deep-profiled captured interval, render allocation
averaged 4.40 MB/frame and combined mesh/texture upload 4.52 MB/frame. Profiling
adds overhead, so those latter figures are workload evidence, not baseline FPS.

The browser sink's `Buffer(ReadOnlySpan<byte>)` makes a fresh `ToArray()` copy.
Changed mesh versions call it for vertex data; JavaScript subsequently uploads
that data to WebGL. This is a verified allocation path and a strong first target.
It does not prove that every uploaded byte was unnecessary. Animation genuinely
changes vertices, so the fix must distinguish necessary updates from avoidable
copies or repeated work.

**Static render submission.** Complete deep windows inside the retained frame
window attribute about 5.6–5.8 ms per profiled frame to `MeshRenderer.Render`,
collectively. A busy sample has 2,496 scene objects, 1,035 submitted render items,
571 visible items and 134 batches. This supports caching static submission data
and avoiding repeated traversal/material work. It is not evidence that draw-call
count alone is the principal problem.

**CPU animation increases during combat.** In the second retained deep window,
`SkeletalMeshRenderer.Update` totals about 8.0 ms per profiled frame. The busiest
sample has 13 active skeletal renderers, 108 runtime mesh parts and 250,589 model
vertices. Spider models have roughly 19,600–19,900 vertices across 8–10 mesh parts
each. Those counts are model workload, not measured visible/skinned vertices for
every frame. They justify checking redundant deformation/upload work and rigid
machine parts before reducing the visual design.

**Large operation spikes.** A profiled `MiningOperation3D.Update` call reached
587.8 ms. A separate normal captured frame lasted 610.5 ms, with 589.8 ms in the
scene-update step and 15.3 MB of scene-update allocations. These support examining
operation transitions/spawning. The source synchronously instantiates enemy
blueprints in that update path; the existing capture does not resolve which
internal spawn/asset step caused the profiled spike. Pooling/prewarming should be
measured against that path rather than assumed to solve every hitch.

**AI and queries are secondary measurable costs.** In the retained second deep
window, AI updates collectively average about 3.0 ms/frame. Physics-query scopes
are nested inside their callers and cannot be added as independent exclusive
cost. Navigation searches include a 29.5 ms outlier despite a much smaller mean.
Budgeting/staggering expensive decisions and path searches is worth a later pass.

**GPU cost remains relevant.** In the heavier captured deep interval at 1152x648,
GPU world/shadow time averages about 30.5 ms and managed work about 54.0 ms.
Normal captured groups at the same resolution have about 23–25 ms GPU time.
Larger-resolution mouse-free intervals reach much higher GPU times. Lowering
resolution can help that part, but cannot remove CPU traversal, deformation,
allocation or spawning costs. Asynchronous GPU results are not exact same-frame
CPU/GPU pairs.

## Recommended order

1. Reuse browser transfer storage; remove avoidable intermediate allocations and
   repeated unchanged mesh uploads, while preserving correct dynamic geometry.
2. Cache static render submission/material data and reduce repeated scene walks.
3. Prewarm/pool enemy creation and measure operation-transition spikes.
4. Optimize animation/deformation and uploads, especially rigid machine parts.
5. Budget AI/path queries, then re-evaluate remaining GPU work with controlled
   camera/resolution comparisons.

The collector saw high allocation/GC activity. Retained heap growth alone does
not prove a leak, and these reports do not directly attribute GC pause duration.
The normal recorder overhead is much smaller than the measured managed/render
work; deep-mode FPS must still be interpreted separately.

## Evidence and analysis safeguards

Input files: `ByteEngine-performance-2026-10-28T15-50-18-954Z.json`,
`ByteEngine-performance-2026-10-28T15-52-14-835Z.json`, and their 15:50:21 and
15:52:16 trace companions in the user's Downloads directory.

Generated local summaries and intermediate rankings are under
`artifacts/user-capture-*`. The summary tool now excludes older/partial profile
windows outside the retained frame range and separates mouse-captured normal
frames from menus and deep profiling. This avoids treating samples retained
from earlier in the session as if they described only the current frame window.

No game assets, runtime behaviour, game export or itch.io upload were changed
while analyzing these recordings.
