# Native performance handoff — 9 November 2026

Maintained source: `C:/Users/codex/.codex/worktrees/52af/ByteEngine`; installed root: `C:/Users/codex/ByteEngine`; project: `C:/Users/codex/Documents/palclone/palclone.byteproject`. Source routing verified. Windows-first; no game export/browser refresh.

Read `Docs/Native-Performance-Followup-Results-2026-11-09.md` for the full findings and limitations. Its measurements supersede the first update's claims.

Accepted source defaults: two-pass RGBA16F resolved-colour AA and restricted solid-lit shader. Generic shader/legacy post remain available for controlled A/B. Material binding cache OFF by default. Depth prepass internal/OFF: 4.55% slower and failed image gate. Fixed pre-existing post-target allocation overwriting depth input.

Measurements:
- `.artifacts/post-replay-03`: immutable HDR/depth, post8.70288 ->5.90880ms (-32.11%); control1.80%; game+15 synthetic image gates pass; bypass exact.
- `.artifacts/solid-lit-01`: main13.34352 ->10.77184ms (-19.27%); control0.73%; all three full-output captures bit-identical.
- `.artifacts/native-upgrade-01`: long-block drift, inconclusive.
- `.artifacts/native-upgrade-02`: ~20% paired effect but conservative individual-jitter gate failed; retained as failed.
- `.artifacts/native-upgrade-03`: independent randomized ABBA/BAAB confirmation, 3200frames after60s warm; paired median22.36% reduction, block-bootstrap95%CI21.89–22.76%; all three epochs19.99/22.95/23.32%; controlsCI within1.24%; passed predefined confirmation. Pooled GPU30.8784 ->24.0216ms; CPU8.1487 ->8.1915ms. Audited roles/groups clean.

Workload: Vega10, Balanced, world1506x847/output1920x1080, scale0.7844683,634visible. Paused after600fixed updates. Global app clock also advances. GPU timeline can include CPU idle/preemption; excludes simulation/swap/readback. This is not combat FPS or deterministic game replay. Final execution CoreMVIDa86a73d8-8b4d-4042-a983-a6f3bb095832.

Changed engine areas: PostProcess3D/PostEffects3D resolve+allocation; Shader3D/Renderer3D restricted variant+diagnostic controls; RenderWorld/ByteEngineApplication benchmark hooks. Added PostReplayBenchmark, GeometryAblation, NativePostFrameBenchmark, PostContextTests; extended rendering/skinning tests. Preserve unrelated pre-existing edits and Docs.

Passed checks: --roadmap-render, --post-contexts, --graphics-render, --native-skinning, --render-pipeline, --engine-optimization, portable browser tests, diffcheck. Complete `.artifacts/followup-*.log`. Final game images visually inspected. No need to repeat all suites after documentation-only edits.

Commands (sequential GPU work only):
- Build `dotnet build Tests/ByteEngine.Tests/ByteEngine.Tests.csproj -c Release --no-restore -m:1`; do not use stale Core assemblies.
- Run test EXE at `Tests/ByteEngine.Tests/bin/Release/net9.0-windows/win-x64/ByteEngine.Tests.exe`.
- `--post-bench <fresh-output> [project.byteproject]` (can reuse immutable hdr.bin/depth.bin/snapshot.json).
- `--geometry-ablation <project> <fresh-output>`; `--solid-lit-ablation <project> <fresh-output>`.
- `--native-upgrade-frames <project> <fresh-output>` (combined vs explicit legacy paths); `--native-post-frames` isolates post only.

Publication COMPLETE: updater exit0; VerifyOnly exit0; installed editor/player/bundled runtime matched. Installed CoreMVIDa86a73d8-8b4d-4042-a983-a6f3bb095832 equals final benchmark; SHA256EA12E234197FB2BD1B75554A3E4BE5452304E5C9500CA6374F4C28792F57535C. Identity `.artifacts/followup-build-identity.json`; log `.artifacts/followup-windows-refresh.log`. Final report copied to `C:/Users/codex/Downloads/ByteEngine-Performance-Followup-2026-11-09.md`. Browser skipped; game not exported. No further publication needed for docs-only changes.

Next performance investigation: repeatable live camera/combat route and fresh final-stage profile. Original3.5ms post,5–7ms main,60/90FPS/combat/zeroGC goals remain unmet/unverified. Keep each experiment gated and retain raw evidence.

