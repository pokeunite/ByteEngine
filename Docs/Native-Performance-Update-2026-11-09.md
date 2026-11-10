# Windows performance update and hierarchy search

The supplied DeepSeek brief is retained in `Native-Performance-Brief.md` as reference material. Its findings are hypotheses, not measurements of this checkout. Inspection found several recommendations already implemented: uniform value caching, fixed-extent texel-snapped directional shadows, point-light quality limits, static point-shadow reuse, and a frozen secondary Scene view during Play. The engine currently has Custom/Fast/Balanced/High tiers, rather than the brief's proposed Ultra tier.

Changes in this pass:

- Hierarchy name search, case-insensitive, including matching descendants and their ancestor paths. Search branches expand independently of the normal hierarchy. Escape clears the query; Delete while typing does not delete selected scene objects.
- Main-pass material texture/sampler deduplication for units 0, 1, 8–12. Cache ownership ends at each pass boundary and on a context identity change. Dynamic texture content versions prevent stale mipmaps. Shadow, environment, water, post and UI units retain their existing behavior.
- Directional shadow reuse depends on the exact light projection, context and caster inputs, without an additional camera equality restriction. A changed projection still rebuilds the map; this does not promise persistent reuse during arbitrary camera motion.
- Game preview caps its target at 1280×720 by default, preserving panel aspect ratio. “Match window resolution” restores the full panel target. Packaged game output resolution is unaffected.
- Detached editor windows honor the editor VSync preference at creation.
- Renderer2D avoids re-uploading viewport projection for unchanged dimensions. A water-material check uses an indexed loop.

Benchmark command (real native player path, startup scene, 120 warmup and 300 measured frames at each of 720p/1080p):

```powershell
dotnet run --project Tests/ByteEngine.Tests -c Release -- --native-bench Samples/3DStarter/3DStarter.byteproject .artifacts/native-before.csv --no-material-cache
dotnet run --project Tests/ByteEngine.Tests -c Release -- --native-bench Samples/3DStarter/3DStarter.byteproject .artifacts/native-after.csv
./Tools/CompareNativeBench.ps1 -Before .artifacts/native-before.csv -After .artifacts/native-after.csv
```

Use fresh output paths per run. The cache switch isolates material binding deduplication; other existing render state optimizations stay enabled in both runs. CPU submission includes the recorded update/physics/late-update durations and excludes swap. GPU values are delayed nonblocking timestamps. Allocations are explicitly the scene update's main-thread allocations, not whole-frame/process allocations. This initial harness does not automate combat waves, camera pans, editor layout combinations, post/shadow toggles, all GL calls, or the brief's complete acceptance matrix.

The focused sampling fixture checks a synthetic 150-draw, seven-map workload: 1,050 material binds become seven binds, with 1,043 skipped. That is a call-count result, not a claim of a measured FPS gain on the user's game.

Remaining work uses the representative Hazard Pay scene: moving-camera shadow coverage/cache policy, adaptive presentation, allocation sources, loading hitches and physics. The brief's 60/90 FPS, combat allocations and editor/player parity targets remain unverified until that workload is available and measured.

## Validation and representative measurements

Active source: `C:/Users/codex/.codex/worktrees/52af/ByteEngine`, explicitly selected by the user. The older `C:/Users/codex/ByteEngine` checkout cannot load the current Hazard Pay plugin; its source edits were removed after porting to the active checkout.

Passed in the active checkout: Release build, `--roadmap-render` (material-cache invalidation, exact directional/point depth comparisons, instancing, text, and 24 randomized GPU image comparisons), `--engine-optimization`, and portable browser runtime checks. Live editor inspection could not be completed: Windows capture returned `FrameArrived timed out` and then `window capture timed out` on the retry.

Hazard Pay was measured on AMD Radeon RX Vega 10 using its authored startup view and runtime settings, without exporting the game. These runs do not constitute Balanced-quality combat or a controlled camera-pan comparison. Audio was unavailable in the test executable, so this is not an audio-inclusive benchmark.

| Resolution | Material cache | CPU p50 ms | GPU p50 ms | Material binds/frame |
| --- | --- | --- | --- | --- |
| 1280x720 | off | 13.950 | 20.514 | 4 |
| 1280x720 | on | 17.651 | 39.671 | 3 |
| 1920x1080 | off | 14.679 | 34.383 | 4 |
| 1920x1080 | on | 15.572 | 34.894 | 3 |

Use the CSV files under `.artifacts/native-hazard-{before,after}.csv` for exact figures. `CompareNativeBench.ps1` correctly rejected this pair at its 3% threshold. Startup allocation levels differed in the 720p runs, so they are not equivalent steady workloads; no FPS improvement is established. Binding deduplication is retained as the provable removal of redundant GL calls, not as a claimed game-frame gain.

A later profiler capture at 1080p recorded 1,707 submitted meshes, 634 visible meshes, 143 total draws, 326,524 main-pass triangles, about 3.51 ms directional shadows, 12.61 ms main geometry, and 9.80 ms post. These are delayed per-pass samples, not percentile aggregates. They direct subsequent work toward GPU geometry/shadows/post, rather than uniform calls already cached in this engine.

A fully quantized XYZ shadow-center experiment passed pixel comparisons but did not reduce Hazard Pay's 33 shadow draws or improve overall timing. It was reverted and is not part of this update. The retained shadow change only permits reuse when the existing exact cache inputs match.

The brief's overall performance targets remain unachieved/unverified. This change supplies hierarchy search, a capped editor preview, safe deduplication and a working measurement path; it does not certify the entire performance backlog as complete. Browser packages are intentionally not rebuilt under the active checkout's Windows-first workflow.
Windows publication completed through Rebuild-ByteEngine-And-Refresh-Dist.ps1 with SourceRoot=52af, InstallRoot=C:/Users/codex/ByteEngine, and SkipBrowser. Debug/Release player pairs and the installed editor were refreshed; no game was exported. The render-pipeline regression check, benchmark invalid-input exit-code check, and identical-CSV comparison also passed.
