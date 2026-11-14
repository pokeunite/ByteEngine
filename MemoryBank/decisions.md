# Decisions

Append-only. Newest first. One entry per choice that would be expensive to
rediscover. Record what was decided, why, and what was rejected.

## 2026-11-13 — MCP wraps shared Core authoring, not editor automation

Decision: `Tools/ByteEngine.Mcp` edits projects through `ProjectSerializer`,
`AssetDatabase`/`AssetManager`, `SceneSerializer`, `BlueprintSerializer`,
`EventModuleSerializer`, the shared `ByteEngine.Core.Authoring.ProjectTemplateFactory`,
`BytePlugin.targets` and the runtime exporters. Starter scenes moved into Core so
the editor and MCP cannot drift. MCP always installs into the same project package
folders the editor reads.

Why: agents need deterministic, reviewable edits with revision tokens. UI mouse
automation and duplicated template/import code would drift and be unsafe. The only
process MCP starts is `dotnet build` for plugin source, with node reuse disabled
(`-nodeReuse:false`, `MSBUILDDISABLENODEREUSE=1`, closed stdin, event-based output)
because reused MSBuild nodes otherwise keep the stdio pipes open and the call hangs.

Rejected: referencing `ByteEngine.Editor` from the MCP host (net9.0-windows, ImGui,
OpenTK, editor-only asset factories); duplicating starter scenes inside the MCP;
shelling out to the editor for imports; running exports implicitly.

## 2026-11-13 — Keep agent memory in a tracked `MemoryBank/` folder

Decision: durable cross-session state lives in `MemoryBank/`, loaded through
`.clinerules/memory-bank.md`, and is committed with the code it describes.

Why: conversation history is lost between chats, and this repository is too large
to rediscover per task. A rules file alone is not enough; the facts have to live
somewhere reviewable.

Rejected: relying on a single growing `AGENTS.md` (unreviewable, mixes standing
instructions with volatile state); gitignoring the memory files (loses continuity
across machines and clones).

## 2026-11-13 — Cache navigation region per agent

Decision: `NavigationAgent3D` caches the resolved `NavigationRegion3D` and
re-resolves only when `RegionObject` changes or after a failed lookup (0.5 s
retry).

Why: the previous implementation projected every component in the scene with
LINQ per agent per frame, producing ~74 bytes of garbage per agent per frame.

Rejected: a scene-level region registry. Correct long term, but a larger change
than the regression required.

## 2026-11-13 — Jitter repaths, keep the first query immediate

Decision: `StaggerRepaths` multiplies the next interval by 0.85–1.15 after the
first query.

Why: `SetDestination` sets the timer to zero, so a spawn wave replanned on one
frame and stayed phase-locked, producing a sawtooth spike proportional to wave
size.

Rejected: randomising the initial destination or delaying the first query, both
of which change gameplay-visible behavior.

## 2026-11-13 — Retain A* scratch, return private path copies

Decision: `NavigationGrid` and `NavigationMesh` keep search dictionaries, parent
maps, closed sets and priority queues between queries, clearing them per query.
Each returned path is still a fresh copy.

Why: per-query allocation scaled with crowd size and produced steady garbage.

Rejected: returning the internal path buffer (aliasing bugs between agents).

## 2026-11-13 — Split physics broad phase out of the solver loop

Decision (corrected during review): prepare candidates at substep start and reuse
them only while collider transforms remain unchanged. Solver position corrections
and joints require a refresh before the next iteration.

Why: the broad phase (gather colliders, refit both bounds trees, query both trees,
sort candidates) was re-run once per solver iteration — eight times per substep at
the default iteration count.

Rejected: lowering iteration count, or unconditional once-per-substep reuse.
Detection results can change during an iteration; a solver-created trigger
overlap is covered by a regression.

## 2026-11-13 — Preserve playback-time crowd quantization

Decision (corrected during review): keep the quantized playback-time key, and
upload immutable cached meshes as static data. Playback already wraps loops.

Why: a second modulo by rounded frame count corrupts non-looping terminal poses
and fractional clip endings. The unbounded-key failure was not demonstrated.

Rejected: the added frame-count modulo. Pool capacity remains a separate limit.

---

## Established before 2026-11-13

- **One version source.** `ByteEngineVersion` in `Directory.Build.props` feeds
  assembly, file and informational versions everywhere. Rejected: per-project
  version literals, which drifted.
- **Game projects live outside the engine repository**, under
  `C:/Users/codex/Documents`. Rejected: sample content inside the repo, which
  blurred engine versus game ownership.
- **`Dist/ByteEngine/BrowserRuntime` is the canonical web runtime**, mirrored into
  editor `bin/Debug` and `bin/Release` by `Tools/Sync-BrowserRuntime.ps1`.
  Rejected: building the browser runtime independently per configuration, which
  produced divergent copies.
- **Any plugin change requires `Tools/PublishBrowser.ps1`** because the browser
  runtime is statically linked. Rejected: runtime plugin loading on web.
- **No game exports without explicit user authorization.** Rebuilding the browser
  runtime is not an export.
- **Fixed-step simulation is opt-in per scene.** Rejected: enabling it globally
  while authored scene callbacks still assume variable steps.
- **Tests are a console runner with flag-selected suites**, not xUnit, and hot
  paths assert allocation budgets. Rejected: a test framework migration mid-flight.

- **Performance claims require interleaved repeats and a bootstrap CI.** Two
  identical baseline runs measured 31.77 ms and 49.97 ms, so a single-run A/B is
  indistinguishable from drift. Rejected: one run per condition, which is what the
  2026-11-11 crowd experiments did.
- **Crowd pose sharing stays opt-in and collapses phases onto a shared clock.**
  Buckets above one make actors inside a bucket animate in lockstep. Rejected:
  quantising each actor's own clock, which turns animation into stop-motion, and
  sharing unconditionally, which fixes arbitrary crowds to one phase.
- **`Scene.InvalidateQueryBounds` must keep bumping `QueryStructureRevision`.** A
  bounds-only split breaks collider removal because the incremental refit cannot
  drop an index entry; `AIQueryOptimizationTests` fails on "disabled collider
  disappears immediately". Rejected: the one-line split, reverted after the gate
  caught it.
- **Crowd instancing needs no new code.** `RenderBatcher` groups on
  `(Mesh, Material, Shadows)` by mesh reference and `PrepareInstanceStream`
  instances any batch above one, so shared cached pose meshes instance for free.
  Rejected: a second instancing path for crowd draws.
