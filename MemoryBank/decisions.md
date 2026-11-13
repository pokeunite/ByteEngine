# Decisions

Append-only. Newest first. One entry per choice that would be expensive to
rediscover. Record what was decided, why, and what was rejected.

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

Decision: `PhysicsWorld3D.Step` calls `PrepareContacts` once per substep;
`SolveContacts` only runs narrow-phase work over that candidate set.

Why: the broad phase (gather colliders, refit both bounds trees, query both trees,
sort candidates) was re-run once per solver iteration — eight times per substep at
the default iteration count.

Rejected: lowering iteration count (changes solver quality) or caching per
iteration (detection results do not change between iterations).

## 2026-11-13 — Crowd pose cache keys on the wrapped clip frame

Decision: the crowd pose key uses the playback frame modulo the clip's quantized
frame count, and cached pose meshes upload as static data.

Why: an absolute frame number produced a new key every loop, filling the
256-entry pool within seconds and silently disabling sharing for the rest of the
session.

Rejected: enlarging the pool (delays the failure and wastes memory).

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
