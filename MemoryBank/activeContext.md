# Active context

Updated: 2026-11-13 — Cline update review.

This is the secondary codex/event-sheet-ux-and-engine-updates branch. Maintained
source routing selects main. Keep the two branches separate until an explicit,
validated integration plan; main lacks this branch's networking and GPU skinning.

Per-phase allocations, opt-in crowd phase buckets, diagnostic shadow cap and
multiplayer signaling changes are reviewed. Adaptive occlusion keeps its former
opt-in default; packed HDR resolve is opt-in via EnablePackedHdrResolve (false).
The measurement runner now executes Reps per mode and handles three or more modes.

Release build and --ai-query, --post-contexts, --render-pipeline, --native-skinning
pass. Network unit/recovery and three real local native WebRTC checks pass.
Dry-run schedules and a synthetic 10% bootstrap effect pass. Full logs remain
under .artifacts/cline-review-20261113. See Docs/Cline-Update-Review-2026-11-13.md.

Next: actual overseas ICE/TURN validation, an asset-ready repeated FPS gate, packed
HDR image/fallback validation, and a branch integration plan. No FPS improvement,
game export, runtime publication or overseas connectivity claim is made here.
