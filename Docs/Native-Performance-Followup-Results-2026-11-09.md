# ByteEngine Windows performance follow-up — 9 November 2026

## Outcome

The corrected work targets the two expensive GPU stages identified in the profile. The final independent Hazard Pay comparison passed its predefined confirmation gate: **22.36% lower paired median GPU rendering time** (95% block-bootstrap interval **21.89–22.76%**). Pooled per-frame GPU medians were **30.8784 -> 24.0216 ms**. This is a paused-render result, not a measured combat FPS increase.

The accepted changes are enabled in the maintained source. Windows publication and identity verification are recorded at the end of this report. No game export or browser-runtime refresh is part of this update.

## Why the first update did not establish a major FPS gain

DeepSeek was right that I missed the largest measured opportunities. I concentrated on small CPU/GL-call reductions while post-processing and the main material shader dominated the GPU profile. Discovering that some recommendations already existed did not justify concluding there was little substantial work left.

The earlier material-cache comparison isolated a few texture bindings, not the complete update. Its startup workload and machine-state drift were too weakly controlled to establish an FPS improvement or regression. The preview resolution cap can reduce editor preview cost, but it does not optimize a packaged 1080p player's shading. Those distinctions should have driven the implementation sooner.

The shader work below is the substantive correction. It does not establish every estimate or proposed gate in DeepSeek's document.

## Accepted changes and measurements

| Change | Legacy path | Accepted path | Reduction | Evidence |
| --- | ---: | ---: | ---: | --- |
| Post-processing, immutable Hazard Pay HDR/depth input | 8.70288 ms | 5.90880 ms | 32.11% | `.artifacts/post-replay-03` |
| Main geometry, restricted solid-lit program | 13.34352 ms | 10.77184 ms | 19.27% | `.artifacts/solid-lit-01` |
| Combined paused native renderer, pooled GPU medians | 30.87840 ms | 24.02160 ms | 22.21% | `.artifacts/native-upgrade-03` |
| Combined renderer, paired quartet statistic | — | — | 22.36% | same run; CI 21.89–22.76% |

Stage measurements are independent experiments; do not add their percentages. CPU submission medians in the combined run were **8.1487 -> 8.1915 ms**, essentially unchanged. GPU p95 was **42.06192 -> 33.70016 ms** in this run; individual timing spikes still occurred.

### Post-processing

The old edge filter repeatedly evaluated tone mapping, gamma, grading, AO and bloom for neighbouring samples. The accepted path grades once into an **RGBA16F** target, then uses a separate lightweight AA program to sample the resolved image. It preserves the source-pixel AA footprint and applies final dithering once. Targets are reused and resized per PostProcess instance.

RGBA16F was chosen to preserve gradients before final dithering. An RGBA8 experiment was faster (45.8% post reduction) but is not the shipping result. Raw-copy, AA-disabled and pixel-art paths bypass the new resolve.

The real-frame difference gate passed: RGB mean absolute difference **0.5219/255**, with **0.546%** of pixels differing by more than 16 in any RGB channel; maximum isolated difference was 95. The predefined limits were MAE <=2 and changed fraction <=3%. Fifteen synthetic HDR edge fixtures (five patterns at three offsets) also passed; bypass comparisons were exact. Final full-size real baseline and candidate images were inspected visually. These are not five authored game scenes or a real moving-camera shimmer certification.

### Restricted solid-lit program

Materials without textures, emission, wetness, water, sand or heightfield features use a compiled program with those absent features constant-folded out. The same PBR, lighting, shadows, fog, IBL, palette and GPU skinning logic remains. Other materials use the generic program. Eligibility is checked from current material state, so live edits fall back immediately. The variant is precompiled during renderer initialization.

All three solid-lit comparison captures were bit-identical to the generic shader. Additional tests cover live feature changes and animated meshes. The main shader is still expensive; this is a restricted variant, not a completed redesign into every proposed material family.

### Existing post-effect allocation bug fixed

A real shared-context/resize test exposed an existing input-binding defect. Creating an AO/bloom target bound its texture on the active unit and could overwrite the depth source before the first draw. All required targets are now allocated before source color/depth bindings are restored. Four actual shared-window transitions and resizes then passed with identical returning pixels and no GL errors.

## Rejected and inconclusive experiments

- **Depth prepass:** 13.45968 -> 14.07184 ms, **4.55% slower**; its strict image gate also failed (0.241% of RGB channels changed by >1 against a 0.1% limit). It remains internal and disabled. This result applies to this prototype/workload, not every possible depth prepass.
- **Flat-fragment ablation:** 13.37824 -> 4.85776 ms. This confirms expensive material shading contributes substantial cost. A grey diagnostic shader is not a usable optimization.
- **Material binding cache:** retained as opt-in diagnostic code and disabled by default. No FPS claim is made for it.
- **First combined run (`native-upgrade-01`):** large absolute drift across long blocks; not used for acceptance.
- **Second combined run (`native-upgrade-02`):** three paired epochs improved about 20%, but the preselected conservative gate comparing a median effect to twice individual-quartet jitter failed. That failed gate is retained. It was not silently relabelled a pass.
- **Independent confirmation (`native-upgrade-03`):** a fresh run used a protocol defined before execution: seeded randomized ABBA/BAAB quartets; same-path AAAA/BBBB controls; confidence intervals for paired medians, rather than individual observation tails. It passed as detailed below.

## Final comparison protocol and raw evidence

- Maintained source: `C:/Users/codex/.codex/worktrees/52af/ByteEngine`.
- Installation: `C:/Users/codex/ByteEngine`; project: `C:/Users/codex/Documents/palclone/palclone.byteproject`.
- Git base: `0158bd81052d367b474a0ed97883f92627d924a6` plus recorded working-tree changes; no new commit was created.
- Final replay execution and combined benchmark Core MVID: `a86a73d8-8b4d-4042-a983-a6f3bb095832`. Solid-stage experiment MVID: `f08f78f9-36ac-4494-aa57-28290e2538ce`. The immutable input snapshot has its own older capture identity; it is distinct from replay execution identity.
- GPU: AMD Radeon RX Vega 10. Balanced quality; actual world **1506x847**; output **1920x1080**; render scale **0.7844683**; **634** visible submissions. No raster/quality reduction between A and B.
- Scene initialized through 600 explicit 1/60 updates, then paused. Camera matrices, raster and visible count were guarded. Both renderer paths warmed for 60 seconds. The application's global clock also advances; this is not a fully deterministic game replay.
- GPU profiler disabled. No competing benchmark/build was run during measurement. JobRunner parallelism disabled for the harness and restored afterwards. VSync off; test audio backend unavailable.
- 100 AAAA controls, 100 BBBB controls, and three epochs of 200 paired quartets: **3200 frames**, including **1200 baseline +1200 candidate** measured comparison frames. All raw roles and four-frame groups audited: zero mismatches; 295 ABBA and 305 BAAB quartets.
- Acceptance defined before the confirmation run: control median 95% CIs inside +/-3%; effect lower CI >max(5%, control bound+3%); all epoch medians >3%. Bootstrap: circular blocks of 10 quartets, 2000 resamples, fixed seeds.
- Control CIs: A **0.0008–0.7901%**, B **-0.1021–1.2366%**. Effect CI **21.89–22.76%**. Epoch median reductions **19.99%, 22.95%, 23.32%**. Both ordering groups showed a benefit.
- GPU timestamps surround native rendering including sky, shadows, geometry, post and UI, excluding simulation, swap and query readback. The GPU timeline may include CPU-induced idle or scheduling stalls. CPU submission is recorded separately before the query wait.

Raw JSON, per-frame samples, images and complete logs live under the maintained checkout's `.artifacts` directories listed above. `native-frame-summary.json` contains the final gate; `native-frame-timings.json` contains metadata and all 3200 samples. The complete timing tail remains available; a positive median does not mean every quartet improved.

## Validation

Passed on the final engine code: `--roadmap-render` (including 24 randomized GPU image comparisons and material fallback), `--post-contexts`, `--graphics-render`, `--native-skinning` (including lit generic/variant comparison), `--render-pipeline`, `--engine-optimization`, and the portable browser runtime checks. Full logs are `.artifacts/followup-<command>.log`; portable log is `.artifacts/followup-portable.log`. Final test build: zero errors, with existing nullable/package-audit warnings. `git diff --check` passed. Only statistical test-harness code changed after those engine checks.

The earlier hierarchy search remains included: case-insensitive object-name matching, matching descendants with ancestor paths, Escape to clear, and typing protection against Delete removing scene objects.

## Remaining performance work

DeepSeek's absolute post <=3.5 ms and main-pass 5–7 ms estimates remain unmet on the representative game frame. The 60/90 FPS, combat-wave, whole-frame allocation, loading-hitch and all-layout editor/player goals remain unverified. The portable checks preserve source compatibility; the installed browser package was intentionally not rebuilt.

The next useful experiment is a repeatable live gameplay/camera route with an asset-ready gate, measuring update/physics, rendering, allocations and frame-time tails together. Further shader variants or shadow/overdraw changes should follow a fresh stage profile and independent acceptance, rather than another batch of unmeasured GL-call changes.

## Windows publication — verified complete

Published on 2026-11-09 at 12:43 UTC using the installed updater with explicit maintained source, install root, Hazard Pay project and `-SkipBrowser`. The updater exited 0; `-VerifyOnly` exited 0 and verified matching editor/player runtime and bundled-player identity.

Installed Core MVID: **a86a73d8-8b4d-4042-a983-a6f3bb095832**, identical to the final benchmark Core. Installed Core SHA256: **EA12E234197FB2BD1B75554A3E4BE5452304E5C9500CA6374F4C28792F57535C**. The engine build identity is `C:/Users/codex/ByteEngine/Dist/ByteEngine/build-identity.json`; its copy and source SHA256 manifest are retained in `.artifacts/followup-build-identity.json` and `.artifacts/followup-source-manifest.json`. Complete Windows build output: `.artifacts/followup-windows-refresh.log`.

The Windows editor/player and desktop gameplay package were synchronized. Gameplay source/version was not changed. No game was exported and the browser runtime was not refreshed. The hierarchy search from the earlier update is included.
