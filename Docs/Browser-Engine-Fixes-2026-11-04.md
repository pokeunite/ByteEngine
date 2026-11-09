# Browser engine fixes — 4 November 2026

Implementation is in the maintained checkout configured by `.byteengine-build-source.json`: `C:/Users/codex/.codex/worktrees/52af/ByteEngine`.

Implemented:

- Both browser backends use requestAnimationFrame pacing. Worker completion wakes the scheduler when GPU capacity returns.
- One worker frame is in flight. A nonblocking GPU fence gates completion; polling never blocks the JavaScript thread, and failure/context loss has a bounded timeout. Resource packets remain owned until completion.
- Auto consumes each identified GPU measurement once, rejects measurements at obsolete pixel dimensions, resets on viewport/DPR changes, and responds at a 15 ms GPU threshold.
- Without GPU timing support, Auto uses a conservative 720p world budget while preserving separate UI resolution. Explicit resolution selections retain their meaning.
- Default framebuffer MSAA is disabled; world rendering and postprocessing remain visually consistent in shader/pixel regression checks.
- Canvas copies are created only for UI textures. Managed texture promotion forces a complete upload when an existing world texture first becomes UI-visible. UI residency tracking is released with texture eviction.
- Publication records the configured source root, Git revision, configuration, and build time in `browser-source-identity.json`.

Verification includes a successful browser source build; presentation, binary frame-buffer ownership, packed transfer and uniform-cache tests; headless Chrome shader/color/CPU-vs-GPU skinning parity; worker supported/unsupported/probe-failure paths; and a neutral texture-promotion and GPU-completion fixture (`Tools/TestBrowserEngineFixes.cjs` in the installed engine repository).

Native remains an explicit high-resolution override. The audit's suggested HDR replacement and opaque material reordering were optimization candidates rather than demonstrated defects. HDR is retained to avoid clipping lighting, and authored ordering is retained to avoid rendering regressions. No measured speedup is claimed without a controlled hardware benchmark. The source identity file makes the active checkout explicit; unrelated edits in the two checkouts are not merged automatically.

No game export or itch.io upload is part of this engine refresh.

Delivery completed: optimized Release AOT publication succeeded. The installed runtime passed all 59 boot-resource hashes. The existing full-game smoke test passed real mouse aim/fire, independent button release, Escape pause/resume, resizing, and effects-pixel checks against the refreshed shared runtime. Dist and existing Debug/Release editor copies were synchronized, including source identity and integrity manifests. Full runtime-manifest hashes were verified in all installed copies. These are correctness and installation checks, not a controlled FPS benchmark.
