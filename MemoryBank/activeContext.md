# Active context

Updated: 2026-11-13 (Memory Bank initialized)

## Current focus

Crowd performance. Navigation agents, physics broad phase and crowd animation
caching are being fixed together because a spawned enemy crowd exercises all
three at once. The work is **uncommitted** on `main` at `2137bf2`.

## Uncommitted changes

| File | Change |
| --- | --- |
| `Engine/ByteEngine.Core/Navigation/NavigationAgent3D.cs` | Region lookup cached and re-resolved only on `RegionObject` change or retry; repath interval jittered (`StaggerRepaths`, ±15%) |
| `Engine/ByteEngine.Core/Navigation/NavigationGrid.cs` | A* scratch retained per grid, cleared per query |
| `Engine/ByteEngine.Core/Navigation/NavigationMesh.cs` | Same retained-scratch treatment for the triangle navmesh |
| `Engine/ByteEngine.Core/Graphics/3D/SkeletalMeshRenderer.Crowd.cs` | Crowd pose key wrapped to the clip's quantized frame count; cached pose meshes uploaded as static data |
| `Engine/ByteEngine.Core/Physics/PhysicsWorld3D.cs` | Broad-phase candidate generation moved to `PrepareContacts`, called once per substep |
| `Tests/ByteEngine.Tests/EngineAllocationRegressionTests.cs` | `CheckNavigationAllocation` budget: 4096 bytes/query |
| `Tests/ByteEngine.Tests/EngineArchitectureBenchmarks.cs` | New navigation/crowd scenarios |

Untracked:

- `Docs/EnginePerformance/crowd-animation-pathfinding.md` — diagnosis, before/after
  measurements and the remaining ceilings. This is the reference for the work.
- `PublicPvpLobby/`, `TestPublicPvpLobby.mjs` — unrelated to the crowd work;
  intent not recorded yet.

## Verified state

The uncommitted edits have **not** been rebuilt in the session that wrote this
memory. Before trusting them, run:

```powershell
dotnet build ByteEngine.sln
dotnet run --project Tests/ByteEngine.Tests/ByteEngine.Tests.csproj
dotnet run -c Release --project Tests/ByteEngine.Tests -- --engine-performance .artifacts/engine-performance.json
```

## Next step

1. Build and run the suite; confirm `CheckNavigationAllocation` and the new
   benchmark scenarios pass.
2. Commit the crowd work together with `Docs/EnginePerformance/crowd-animation-pathfinding.md`
   and the `MemoryBank/` updates.
3. Then continue with the ceilings named in that doc, in this order: vertex-shader
   bone palette (removes CPU skinning and per-frame upload), wider crowd
   eligibility, per-renderer root motion/socket evaluation, batched spawning.

## Open questions

- Is `PublicPvpLobby/` supposed to be committed, and does it belong under
  `Plugins/`? Nothing in `AGENTS.md` mentions it.
- `README.md` still advertises "ByteEngine v0.7" while `ByteEngineVersion` is
  `0.10.0`. Decide whether to make the README version-free or update it.
- Shadow and skinned-LOD work listed as separate in `README.md` has no owner task.
