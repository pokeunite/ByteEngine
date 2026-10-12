# Browser frame transport optimization

The previous player serialized full material descriptions and individual UI glyph commands into JSON every frame. The optimized player sends material descriptions only when their complete value changes, reuses them by ID, and retires unused descriptions. Model transforms and draw ordering remain per-frame. Texture content updates retain their existing upload path. Material keys include colors, texture identities, terrain displacement parameters, PBR channels, UV settings, emission, blend/depth/culling and winding state.

UI commands use a little-endian binary buffer with 56-byte records: int32 command kind, int32 texture ID, eight float32 coordinates and four float32 color channels. JavaScript reuses decoded command objects. Clip/unclip ordering is preserved; integer IDs are not converted through float32. Malformed buffers and unknown kinds fail explicitly. This removes repeated text serialization without reducing visual quality, physics frequency or terrain detail.

## Local driving comparison

Both measurements used Chrome on AMD RX Vega 10 at 1280x720, the existing 11-block heavy recovery fixture, default six iterations/two substeps, and the same 20-second held-throttle capture with CPU profiling. Compilation had finished before these paired captures. The new frame timing changes the physical trajectory slightly; draw counts and contact details are not perfectly identical. These are local fixture measurements, not new itch measurements or guaranteed desktop parity.

| Metric | Baseline | Optimized |
|---|---:|---:|
| FPS | 38.24 | 46.79 |
| Mean managed frame work | 21.77 ms | 17.82 ms |
| Mean frame encoding | 3.98 ms | 1.86 ms |
| Mean parsing/decoding | 0.50 ms | 0.27 ms |
| Mean JSON characters/frame | 76,233 | 18,726 |
| p95 frame interval | 33.5 ms | 33.3 ms |
| p99 frame interval | 49.9 ms | 33.5 ms |
| Intervals above 50 ms | 3 | 2 |

The JSON reduction excludes the new binary UI buffer (approximately 7.8 KB for the 140-command final frame). The final frame reused materials with zero material uploads. The vehicle drove at approximately 5 m/s. No JavaScript or WebGL errors occurred, and a screenshot verified the vehicle, sky, terrain and HUD. This is an observed 22% FPS improvement in the paired test, with approximately 53% less encoding time; repeat runs and the user's next itch capture can establish consistency. Initial loading speed was not measured by this driving capture.

Validation includes command coordinates/colors/clipping order, exact large integer texture IDs, subarray offsets, object reuse and malformed packets; renderer material reuse, independent model transforms, changed colors, retirement and missing references; real browser driving; and the native winch approach/attach/extract/single payment/reset/return regression. CI runs the frame transport checks, and the broader browser smoke harness resolves material references for its animation/navigation assertions.

The broader browser AOT smoke test also passed changing skeletal geometry, navigation arrival, scene rendering, clipped UI and four viewport sizes. Installed Dist/Debug boot manifest hashes matched.

The optimized AOT runtime is installed in Dist and the Debug editor BrowserRuntime folders. No game export, upload or Git push was performed for this update. Existing itch uploads require a future user-created export to receive the new runtime.
