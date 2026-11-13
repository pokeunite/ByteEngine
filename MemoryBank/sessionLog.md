# Session log

Newest first. One entry per work session or hand-off. Keep each entry short:
what changed, what was verified, what is left.

## 2026-11-13 — Reviewed Cline engine updates before Git publication

Changed: navigation cache lifetime and path invalidation; safe physics candidate
reuse after solver/joint movement; configured fixed callback delta; removed unsafe
crowd frame modulo; corrected README, memory and performance claims. Secondary
render experiments stay opt-in; harness repetitions/multi-mode scheduling fixed.
See Docs/Cline-Update-Review-2026-11-13.md.

Main checks: dotnet build Tests/ByteEngine.Tests -c Release --no-restore -m:1
-nr:false -p:UseSharedCompilation=false; then dotnet run --no-build -c Release
--project Tests/ByteEngine.Tests with --engine-optimization, --roadmap-physics,
--roadmap-integration, --web-cpu-regressions and no flag for the default suite.
Secondary: same test-project build flags; --ai-query, --post-contexts,
--render-pipeline, --native-skinning; dotnet run -c Release --project
Tests/ByteEngine.Network.Tests and Tests/ByteEngine.Network.Integration.
All successful validation uses current assemblies. Build logs retain warnings.
Harness dry runs verify 16 launches for 8 repetitions per arm, and 12 for 4
repetitions across three modes. Raw logs: .artifacts/cline-review-20261113.

Limitations: no controlled FPS speedup claim, actual overseas test, game export,
runtime publication or branch merge. Keep both branches separate when pushing.

## 2026-11-13 - Performance harness, crowd phase buckets, multiplayer triage

Changed (worktree `52af`, branch `codex/event-sheet-ux-and-engine-updates`):
- Measurement: `Tools/Performance/Measure-Performance.ps1` (interleaved ABBA/BAAB)
  and `Aggregate-Performance.ps1` (paired medians, 4000-seed bootstrap, CI gate).
- Instrumentation: per-phase allocation bytes in `RuntimeFrameMetrics` and `Scene`;
  `queryFullIndexRefreshes`, `queryIncrementalBoundsUpdates` and `allocatedPerFrame`
  now appear in the stress artifact.
- Crowd: shared crowd clock plus `CrowdPosePhaseBuckets` collapses pose keys, and the
  existing `RenderBatcher`/`PrepareInstanceStream` then instances the draw batches.
- Rendering: `AdaptiveOcclusion` defaults on, R11G11B10F resolve, opt-in shadow
  resolution cap.
- Multiplayer: pre-accept timeout 15 to 60 s, inbound signals re-arm it, lobby polls
  and signals use independent in-flight slots, ICE diagnostics added.

Verified: `MiningPrototype` builds with 0 errors and the DLL contains the new
symbols; `--ai-query` exit 0; network suites 4/4 and 3/3; harness statistics checked
against synthetic data with a known effect. No real repeat sweep ran, because the
stress scene does not load in that worktree.

Left: commit with the three docs; enable `ShareCrowdPoses` on drones; settle the
merge direction between main and the multiplayer branch.

## 2026-11-13 — Memory Bank initialized

Changed:
- Added `.clinerules/memory-bank.md` (the protocol Cline follows each session).
- Added `MemoryBank/` with `projectBrief.md`, `productContext.md`,
  `systemPatterns.md`, `techContext.md`, `activeContext.md`, `progress.md`,
  `decisions.md`, `sessionLog.md`.
- Seeded content from `README.md`, `AGENTS.md`, `Docs/`, the solution layout and
  the uncommitted crowd-performance work.
- Added a memory pointer to `AGENTS.md` so non-Cline agents use the same files.

Verified: repository structure and script parameters read directly
(`Directory.Build.props`, `Tools/*.ps1`, `Tests/ByteEngine.Tests/Program.cs`,
`Plugins/DuneCompany/plugin.json`); `git status` shows the uncommitted crowd work.
No build or test run was performed as part of this setup.

Left: confirm the uncommitted crowd-performance edits build and pass
`CheckNavigationAllocation`; then commit them with
`Docs/EnginePerformance/crowd-animation-pathfinding.md`.
