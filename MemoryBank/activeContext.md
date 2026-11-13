# Active context

Updated: 2026-11-13 (Cline update review and regression validation)

## Maintained source and branch boundary

Source routing selects the main checkout. Review began at main d91cd2a and
secondary branch codex/event-sheet-ux-and-engine-updates at e128b7f.
Branches remain separate; no merge or game export is authorized by this task.
Main has navigation/physics changes and the editor/registry refactor. The secondary
branch has multiplayer, native GPU skinning, Hazard Pay and performance tooling.

## Reviewed changes

- Navigation region caching now rejects inactive/disabled/detached regions and
  resets paths for replacement regions. A* scratch is retained; queries on one
  grid/mesh must remain serialized. Jitter does not defer first queries.
- Physics candidate reuse checks world transforms; corrections/joints force a
  refresh. Unconditional once-per-substep reuse was unsafe.
- Restored configured fixed delta before component callbacks; custom 20 ms test.
- Removed added crowd frame modulo; playback already wraps looping clips.
  Immutable cached pose geometry uploads remain.
- Secondary adaptive occlusion keeps its prior opt-in default. Packed HDR resolve
  is opt-in via EnablePackedHdrResolve until real performance/image gates pass.
- Measurement runner now executes Reps per mode and handles three or more modes.
- Review details: Docs/Cline-Update-Review-2026-11-13.md.

## Evidence

Main Release build and named engine, physics, navmesh, browser CPU/editor and
full regression suites run against current sources; exact commands/results are
in the review and session log. Raw output stays under .artifacts/cline-review-20261113.
Secondary Release build, AI queries, post-context transitions, render registry,
300-bone native GPU skinning, network session/lobby and native WebRTC checks pass.
Harness dry-run counts pass: 8 per arm/16 total and 4 per mode/12 total.
There is no real repeated FPS A/B acceptance result, overseas network validation,
manual game visual review or browser runtime publication in this task.

## Next steps and limitations

- Choose a tested branch integration plan before assuming main contains multiplayer
  or native GPU skinning. Push both branches separately for backup.
- Run asset-ready repeated A/B sweeps with build identity, quality and actual raster
  dimensions. Earlier single-run FPS conclusions remain inside observed drift.
- Verify stress scene loading before running the new harness. Keep packed HDR off
  until format/fallback, image parity and performance gates pass.
- Validate signaling/ICE on real overseas networks; TURN may still be needed.
- ShareCrowdPoses must be enabled for phase buckets to affect an actor. No gameplay
  plugin source was changed by this review.
- PublicPvpLobby is a separate repository; it and TestPublicPvpLobby.mjs stay outside
  engine commits, as in earlier pushes.
