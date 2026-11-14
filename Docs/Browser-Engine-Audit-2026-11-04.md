# ByteEngine web engine audit — 4 November 2026

The evidence supports targeted changes to browser frame scheduling, resolution feedback, and resource handling. It does not justify rewriting the engine. The architecture already includes AOT WebAssembly, SIMD, GPU skinning, opaque instancing, pooled binary transfers, worker rendering, and separate world/UI resolution.

This is an engine audit, not a game-specific optimization pass. No game, runtime, or plugin was changed or exported. New work consists of this report, a reproducible read-only audit script, and its evidence JSON. Existing game captures are used only as workloads that exercise engine systems; their results are not universal engine benchmarks.

**Scope and source identity**

The folder supplied by the user is not the source checkout used by its default rebuild launcher. [Build configuration](C:/Users/codex/ByteEngine/.byteengine-build-source.json:2) points at `C:/Users/codex/.codex/worktrees/52af/ByteEngine`. The seven browser JavaScript files checked in that checkout exactly match the installed Dist runtime. None of their counterparts in the supplied checkout match; some are absent there. A source-only review of the supplied folder would therefore propose several fixes already present in the installed engine.

Keep one declared source of truth and record source revision, dirty-source fingerprint, compiler configuration, and runtime hashes in every browser build. Existing integrity hashes validate the installed binaries but do not make the two source trees equivalent. The 59 checked boot resources passed integrity verification. This does not identify what any currently uploaded itch.io ZIP contains.

**1. Worker frame scheduling lacks display pacing — highest-priority architecture issue**

In [main.js](C:/Users/codex/ByteEngine/Dist/ByteEngine/BrowserRuntime/main.js:125), the threaded path schedules frames through MessageChannel; only the non-threaded fallback uses requestAnimationFrame. It schedules again at the end of every frame and when the renderer reports capacity. [The client](C:/Users/codex/ByteEngine/Dist/ByteEngine/BrowserRuntime/render-worker-client.js:15) bounds in-flight worker messages to two.

However, [the worker](C:/Users/codex/ByteEngine/Dist/ByteEngine/BrowserRuntime/render-worker.js:18) acknowledges a frame immediately after renderer.draw returns. That means JavaScript finished issuing drawing commands. It does not mean the GPU finished or the browser displayed that image. Consequently, the two-message limit does not bound outstanding GPU work or align rendering to screen refresh. The main simulation also advances once per submitted render packet, which can waste update/preparation work when submission outruns presentation.

Confirmed: scheduling and acknowledgement semantics. Likely consequences requiring controlled A/B measurement: excess CPU/GPU work, queue latency, bursty pacing and reduced input responsiveness. Do not claim this is the proven cause of every reported stall.

Engine correction: pace visual frames using worker requestAnimationFrame or a main-thread animation-frame coordinator; preserve fixed-step simulation and interpolation. Keep resource upload/deletion commands reliable when coalescing visual updates. Add nonblocking GPU completion/backpressure where needed; do not add gl.finish to every frame. Measure simulation, submission, GPU completion, and browser presentation separately. Worker submission FPS must not be labeled verified displayed FPS.

The [HTML canvas standard](https://html.spec.whatwg.org/multipage/canvas.html#the-offscreencanvas-interface) separates OffscreenCanvas bitmap presentation into rendering-update steps. [MDN's worker animation-frame documentation](https://developer.mozilla.org/en-US/docs/Web/API/DedicatedWorkerGlobalScope/requestAnimationFrame) describes callbacks associated with the next repaint. These support the distinction; they do not supply a ByteEngine-specific performance result.

**2. Native resolution bypasses the engine's world pixel budget — confirmed fullscreen multiplier**

[graphics.js](C:/Users/codex/ByteEngine/Dist/ByteEngine/BrowserRuntime/graphics.js:9) fits Auto to a quality budget, but Native uses device pixel density directly, capped at 2× and the GPU texture-size limit. A 1920×1080 CSS viewport at 150% scaling becomes 2880×1620: 4.67 million world pixels, versus 0.92 million at 720p, a 5.06× increase. A 3840×2160 CSS viewport at DPR 2 can become 7680×4320, subject to GPU limits. This behavior remains true at Fast quality. It is an explicit quality policy, not a broken fullscreen API.

Native is intentionally excluded from adaptive scaling. Thus selecting lower quality does not necessarily reduce world resolution. Auto is not unrestricted Native and should not be described that way.

Engine correction: make the web default a clearly bounded world pixel budget independent of UI sharpness, and expose Native as a deliberate expensive override. Include current internal resolution in diagnostics/settings. Do not silently degrade an explicitly selected Native mode. Verify fullscreen and resize at multiple DPR values.

**3. Auto resolution treats repeated old GPU results as new measurements — reproduced defect**

[renderer.js](C:/Users/codex/ByteEngine/Dist/ByteEngine/BrowserRuntime/renderer.js:279) retains the last available gpuMs and identifies its source using gpuRenderFrame. [main.js](C:/Users/codex/ByteEngine/Dist/ByteEngine/BrowserRuntime/main.js:157) passes only gpuMs into the controller on every main frame. [graphics.js](C:/Users/codex/ByteEngine/Dist/ByteEngine/BrowserRuntime/graphics.js:24) therefore cannot distinguish a fresh result from a repeat.

The audit directly reproduced eight repeated reads of one 80 ms result reducing the scale from 1 to 0.75. They need not represent eight independent GPU frames. After a resize or quality change, old results can also describe the previous resolution. The controller key includes quality/resolution mode, but not dimensions or a render-target generation.

An existing Native capture contains 317 worker submissions but only 51 distinct reported GPU sample IDs, with mean sample age 19 frames and maximum 30. Native itself does not adapt, so that capture demonstrates delayed/repeated telemetry, not an observed Auto malfunction. The same producer/consumer interface is used in Auto.

Engine correction: consume each GPU result once, with frame ID, measurement time, pixel dimensions and target generation. Ignore obsolete-generation measurements, bound sample age, and reset adaptation history on resizing. Select thresholds from a target frame budget with headroom; a fixed 20 ms overload threshold does not target 60 Hz's 16.67 ms budget. GPU timers do not measure Canvas UI/compositor cost. Unsupported timers currently leave Auto unchanged; define a conservative fallback rather than pretending a missing measurement means adequate performance. Do not reduce resolution merely because physics is slow.

**4. The low-quality renderer still carries substantial full-resolution overhead**

[Renderer initialization](C:/Users/codex/ByteEngine/Dist/ByteEngine/BrowserRuntime/renderer.js:7) requests antialias:true. Scene geometry is drawn to a separately allocated, single-sample world framebuffer and then a fullscreen post triangle draws to the default framebuffer. Default-framebuffer multisampling does not antialias the original world geometry edges in that intermediate image. If the browser grants it, this can add backing-store/resolve cost with little useful result for this path. Balanced/High also run a post edge filter.

[prepareTargets](C:/Users/codex/ByteEngine/Dist/ByteEngine/BrowserRuntime/renderer.js:161) chooses RGBA16F whenever supported, including Fast. HDR color is 8 bytes per pixel versus RGBA8's 4, before depth and other buffers. Larger fullscreen targets magnify the cost. Fast still performs the fullscreen post pass. Water targets are already lazy in the installed version; allocating water in dry scenes is an old-source issue, not a current finding.

Engine correction: A/B test antialias:false for this presentation path, checking actual context attributes and image quality. Provide an explicitly designed low-cost LDR/tone-mapping path if profiling supports it. Merely changing HDR storage to RGBA8 can clip lighting before tone mapping and is not a visually equivalent fix. Measure GPU stages before deciding which effect to simplify.

These are confirmed implementation choices and optimization candidates; this audit does not quantify their individual speedups. [MDN WebGL guidance](https://developer.mozilla.org/en-US/docs/Web/API/WebGL_API/WebGL_best_practices) recommends explicit back-buffer budgets, batching and avoiding synchronous GPU round trips.

**5. Every non-HDR texture gets a Canvas copy, even if only used in 3D**

[Texture upload](C:/Users/codex/ByteEngine/Dist/ByteEngine/BrowserRuntime/renderer.js:250) creates both a WebGL texture and a Canvas image. Every non-HDR upload is also applied through putImageData. The Canvas copy is needed for UI drawImage, but the upload path has no UI-only usage gate. It duplicates storage and upload work for world-only textures; tint caches can add further copies.

One 2048×2048 RGBA Canvas backing image represents 16 MiB of pixel storage, in addition to the GPU texture and retained CPU asset data. Actual browser process memory may differ. This is a generic resource-design issue affecting any texture-heavy game, not proof of a leak or the main cause in every scene.

Engine correction: distinguish world, UI, and shared texture usage. Create/maintain Canvas copies only for UI/shared resources; promote textures safely if they later become UI-visible. Keep dirty-region consistency and disposal tests. Retain CPU source pixels only where reload, editing or gameplay requires them.

**6. Rendering preparation and material changes are further CPU targets**

The maintained portable renderer retains opaque submission order, groups compatible instances primarily by mesh/material, encodes per-frame draw metadata to JSON, and copies binary buffers into worker-owned transferable storage. Those are real costs, but a worker copy is necessary for the current ownership model; transferring the live WebAssembly heap is not a safe replacement.

In one existing Auto workload, mean managed frame work was 14.97 ms, including 6.01 ms render preparation. It averaged 150 draw calls and 147 material changes. This shows little material-state reuse between consecutive draws in that workload. It does not establish that JSON is the dominant cost: the packed matrices and UI already avoid JSON number arrays.

Engine correction: profile batching/preparation by stage; benchmark state sorting of order-independent opaque batches, stable retained draw records, and generation-based updates for static resources. Preserve transparency, overlays, cutout/depth semantics and authored ordering constraints. Track the sources of dynamic mesh uploads before deciding whether to change animation, procedural geometry, or mesh dirty tracking. Do not rewrite physics from an old game's physics profile.

**Evidence from existing captures, reanalysed today**

These are 3 November local controlled runs already on disk, not new browser runs or the user's current itch session. The workload evolves, so sequential stages are not a perfectly isolated experiment. GPU means below deduplicate by GPU frame ID; main-thread samples are matched to the worker IDs in the stage window.

| Existing stage | World resolution | Worker submissions/s | Mean unique GPU ms | Mean managed ms | Mean render-preparation ms |
|---|---:|---:|---:|---:|---:|
| Embedded Auto | 960×540 | 49.7 | 6.61 | 17.80 | 7.35 |
| Fullscreen fixed 720p | 1280×720 | 53.8 | 8.85 | 16.10 | 6.12 |
| Fullscreen Auto | 1280×720 | 59.6 | 8.87 | 14.97 | 6.01 |
| Fullscreen Native | 2880×1620 | 31.4 | 30.55 | 16.06 | 6.50 |

Similar triangle counts (~367,000) alongside much higher Native GPU time strongly support pixel workload as a fullscreen bottleneck in this example. The managed path remains close to a 60 Hz budget even in Auto. CPU and GPU overlap; do not add these timings to estimate total frame time. Neither these captures nor source inspection establishes an itch.io hosting defect.

**Verification performed and next engine acceptance gate**

Fresh checks: installed boot integrity (59 resources); both checkouts' presentation sizing tests, run from their respective roots; seven-file maintained-source/installed-runtime hash comparison; resolution calculations; repeated-sample and missing-timer probes; raw capture reanalysis. The maintained presentation test initially ran against the wrong working directory and failed module resolution; it passed when rerun against its own checkout. No GPU benchmark or new game export was run in this audit.

Reproduce the audit with `node Tools/AuditBrowserPerformance.mjs` from the supplied engine folder. [Full evidence](C:/Users/codex/ByteEngine/artifacts/browser-engine-audit-2026-11-04/evidence.json) includes file hashes and stage distributions.

Implement in this order: source/build identity and trustworthy telemetry; display-paced worker scheduling and GPU backpressure; fresh-sample resolution adaptation; framebuffer and texture-copy reductions; measured batching/preparation improvements. Changes belong in the maintained engine checkout and should then be built/synchronized deliberately.

Validate with engine-owned neutral fixtures: an empty scene, static instanced geometry, many material groups, animated characters, UI/text, and dynamic texture/mesh updates. Cross fixed world resolution with embedded/fullscreen presentation and DPR 1/1.5/2. Compare main-thread versus worker rendering, timer available/unavailable, Auto versus explicit modes, and normal capture versus capture off. Verify 60/120 Hz pacing, input latency, CPU and GPU p95, memory after repeated resizes, and no lost uploads. A local cross-origin iframe can exercise embedding without requiring an itch game. Confirm on integrated graphics as well as a faster GPU before making performance claims.
