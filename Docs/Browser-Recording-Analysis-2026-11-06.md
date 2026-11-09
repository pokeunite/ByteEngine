# Browser recording analysis — 6 November 2026

Source: `ByteEngine-performance-2026-11-06T22-52-36-599Z.json` plus its trace from itch.io.
Machine: AMD Radeon RX Vega 10, 8 logical cores, 4 GiB reported device memory, DPR 1.5.
Runtime: threaded WebGL2, Fast quality, 75% world resolution (1440×810), UI 1920×1080.

Analysis tool: `Tools/AnalyzeBrowserRecording.mjs`. Generated evidence: `artifacts/browser-recording-2026-11-06/analysis.json`.

## Result

The capture contains 2,064 gameplay frames over 120 seconds. Mean timed FPS is 17.2, median frame interval 33.3 ms, and p95 interval 181.7 ms. There are 736 frames over 50 ms and 325 over 100 ms.

This is not one uniform slowdown. The late, low-activity interval reaches 56.5 FPS with 8.5 ms managed work and 9.3 ms GPU work. The machine can approach 60 FPS; content-driven spikes and steady scene work are preventing it.

## Root-cause ranking

### 1. Dynamic texture churn and upload backlog — highest impact on hitches

- 249 generated textures were created: 184 at 1024², 8 at 2048², 20 at 1024×512, and the remainder smaller.
- 218 `graphics-upload` events transferred approximately 1.39 GB of texture data during two minutes.
- The upload queue reached 1,027 pending texture regions.
- Frames containing texture uploads: 108 total, median interval 100.0 ms, mean managed time 43.8 ms, mean render allocation 1.95 MB.
- Frames without uploads: 1,956 total, median interval 33.3 ms, mean managed time 27.0 ms, mean render allocation 0.33 MB.
- The repeated 1024² and 2048² generated textures strongly match font-atlas dimensions in [FontAtlas.cs](../Engine/ByteEngine.Core/Graphics/FontAtlas.cs). UI text currently keys raster atlases by requested size ×2, and browser atlases are 1024² for baked sizes ≤64 and 2048² above that.
- The font cache is bounded at 32 entries in [Program.cs](../Player/ByteEngine.Browser/Program.cs), which is hundreds of MiB if many sizes remain active. Sizes that fall out and return recreate and re-upload multi-megabyte atlases.

Actions:

- Quantize font sizes into a small set of raster sizes and scale glyph quads from the nearest atlas.
- Prefer a 512² atlas for small text; do not bake a separate 1024² atlas for every near-identical size.
- Use a persistent bounded LRU with a realistic working set, not immediate recreation after eviction.
- Add a diagnostic source label such as `font:/path@size` or the VFX cache key; `dynamic/embedded texture` is currently too ambiguous.
- Keep short-lived VFX atlases in an LRU rather than disposing immediately when their user count reaches zero.
- Raise the upload budget only after removing recreation; the 4 MiB/frame cap is masking the allocation source while the backlog grows.

### 2. Steady CPU work is still above the 60 FPS budget

- Managed frame work averages 27.9 ms; p95 is 53.6 ms.
- Update/physics/late-update averages 6.95 / 4.19 / 1.60 ms.
- Render preparation averages 13.0 ms; p95 is 22.2 ms.
- The scene contains about 3,960 objects, 226 colliders, and its 594-candidate broad phase recurs frequently.
- JS submission itself is cheap: 0.3 ms median, 3.1 ms p95. JSON parsing averages 0.15 ms.

Actions:

- Cache static submission preparation independently of dynamic skinned objects.
- Cache unchanged visibility buckets, opaque ordering, material IDs, and matrix packets.
- Profile update callbacks by component; stagger distant AI/navigation and avoid per-frame work for unchanged gameplay state.
- Preserve the current physics quality, but avoid rebuilding query/index state when bounds and membership are unchanged.

### 3. UI work and UI memory are significant

- 240 UI commands per frame, with 289 typical.
- UI CPU averages 3.24 ms, p95 9.3 ms, p99 15.2 ms, max 33.6 ms.
- 761 redraws versus 1,534 skipped frames, so the cache works, but animated HUD sections still trigger frequent full-canvas redraws.
- Tint cache reaches the 64 MiB limit, and UI texture canvases account for about 118 MB in the later sample.

Actions:

- Split static HUD layers from frequently changing text so a small text change does not repaint the complete 1920×1080 overlay.
- Lower the tint-cache budget on 4 GiB systems and release tinted copies aggressively.
- Avoid retaining duplicate Canvas2D copies for world-only textures.
- Quantize font sizes as above; this also reduces UI texture residency.

### 4. GPU cost remains material, especially during combat

- Unique GPU samples: median 18.9 ms, p95 29.5 ms. The 16.7 ms frame budget is already exceeded at the median.
- Draw calls average 82; material changes average 76. This means almost every draw changes material state.
- Triangles average 184k and peak near 513k.
- The capture used 75% world resolution at 1440×810. Fixed 720p (1280×720) reduces world pixels by 21%.

Actions:

- Use the fixed 720p default in the new export.
- Batch opaque work by material/resource state while preserving front-to-back ordering within each material group.
- Reduce overdraw from particles and transparent effects before reducing texture resolution further.
- Keep Fast shadows off; the entire shadow path is already absent in this capture.

### 5. Memory pressure and GC amplify the spikes

- Managed heap grew from 237 MB to 495 MB, peaking at 539 MB.
- 2.33 GB was allocated during the 120-second recording.
- GC0 increased by 77, with 17 additional Gen2 collections.
- Transfer pool retention is about 67 MB.
- JavaScript heap samples also rose substantially; the renderer retained about 154 MB of estimated texture data in one sample.

Actions:

- Eliminate recreate/dispose churn for font and VFX atlases.
- Pool temporary material arrays and generated texture dictionaries.
- Lower retained transfer memory when `deviceMemory <= 4` GiB.
- Add allocation call sites to diagnostics before optimizing blindly; the p99 render allocation is about 6.1 MB and the max is 26.6 MB.

## Load/startup versus gameplay

The 14.6-second `scene-start` long task and the 3.1-second first `frame` happen before steady gameplay and should not be attributed to normal combat. They do indicate that startup remains a separate optimization target. Loading 854 assets and resource downloads also produced large long tasks.

## Recommended order

1. Fix font-atlas and generated-texture churn.
2. Fix UI partial redraw and tint-residency.
3. Cache static render preparation/material/matrix state.
4. Profile and schedule gameplay update callbacks.
5. Tune GPU material batching/particle overdraw.
6. Re-test at fixed 720p with the same machine and scene.

The capture shows substantial room for improvement. The late 56.5 FPS segment proves the platform is not inherently capped near 30 FPS.

