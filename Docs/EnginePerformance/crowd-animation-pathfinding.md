# Crowd animation and pathfinding review

2026-11-13. Reviewed Cline changes on main. These are correctness and allocation
results, not an FPS comparison. The two branches carry different renderers.

## Accepted changes

- Agents cache navigation regions with activation, component-membership and
  selection invalidation. Replacement regions force a new path.
- Grid/navmesh A* retain search scratch and return private paths. Queries on
  one grid or mesh must be serialized; scratch is not reentrant/thread-safe.
- Optional jitter preserves the immediate first query and spreads retries or
  changed-destination requests. Successful unchanged paths are not continuously
  replanned by the existing condition.
- Frozen cached crowd geometry uses immutable/static uploads.
- Physics reuses broad-phase candidates only while transforms are unchanged.
  Contact corrections and joints invalidate candidates before the next iteration.

## Corrections made during review

The unconditional once-per-substep candidate set missed solver-created overlaps.
A regression places a body between a wall and an initially separated trigger;
the correction must discover that trigger within the same step.

The added crowd-key modulo was removed. Playback already wraps looping clip time
through AdvanceTime. Modulo by a rounded quantized frame count mapped one-shot
endings back to the first pose and truncated fractional clip endings. The claimed
unbounded-key failure was not demonstrated.

A prior optimization omitted Time.FixedDeltaTime assignment before fixed callbacks.
This is restored and tested with a custom 20 ms simulation step.

## Evidence and limits

EngineAllocationRegressionTests checks warmed grid queries below 4096 bytes/query,
private path ownership, navigation region removal/activation, solver-created
contacts and configured fixed callback timing. Run --engine-optimization,
--roadmap-physics, --roadmap-integration, --web-cpu-regressions and the default
console suite after building Tests/ByteEngine.Tests in Release.

Cline's earlier single-run timings are historical observations, not accepted
speedups. Correct invalidation may rebuild candidates after each solver correction;
do not claim an eightfold reduction for arbitrary dynamic stacks.

Main uses CPU crowd deformation. The secondary branch already has native GPU
skinning; it must not be described as missing across the entire repository.
See Docs/Cline-Update-Review-2026-11-13.md for branch boundaries and outstanding work.
