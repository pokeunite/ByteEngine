# Performance measurement harness — 2026-11-13

Supersedes the ad-hoc run strategy behind the `.artifacts/crowd-*` comparisons. The engine-side
instrumentation described in `Native-Performance-Followup-Verdict-2026-11-09.md` is unchanged; what is new
is a repeatable way to tell an effect from run-to-run drift.

## Why this exists

Two runs labelled **baseline**, same build, same scene, same 1.351M triangles, differ by 1.57x:

| run | mean | fps | draw calls |
| --- | ---: | ---: | ---: |
| `crowd-geometry/baseline-88e8…` | 31.77 ms | 31.5 | 640 |
| `crowd-geometry/baseline-0d88…` | 49.97 ms | 20.0 | 511 |

Every 11-11 crowd conclusion was drawn from **one run per condition** (`1.log`, `2.log`, `3.log`). The
`geometry-lod` change cut triangles 18.6 % and landed at 37.64 ms, which is inside that spread. It is not
evidence of anything. Do not ship or revert on single runs again.

## Running a comparison

```powershell
# Prerequisite: the benchmark references a prebuilt plugin DLL.
dotnet build Plugins/KarlsonDemo/KarlsonDemo.Plugin.csproj -c Release
dotnet build Tools/MiningPrototype/MiningPrototype.csproj -c Release

# Interleaved ABBA/BAAB, five reps per arm (rounded to 8 so quartets complete).
Tools/Performance/Measure-Performance.ps1 -Reps 8 -Modes baseline,no-occlusion

# Preview the schedule without launching anything.
Tools/Performance/Measure-Performance.ps1 -Reps 8 -Modes baseline,shadow-1024 -DryRun
```

The runner launches `MiningPrototype.exe --stress-spiders --probe=<mode> --count=120
--skip-stress-screenshot`, alternates modes per quartet, copies each run's artifact into its own
directory, and calls `Aggregate-Performance.ps1`.

The benchmark must run with its scene present in the working directory (`-Work`, default: repository
root). It logs `Loading scene: Main` when it has one and fails with a clear exception when it does not;
the runner turns that into a message rather than a silent zero.

## What the gate means

`Aggregate-Performance.ps1` computes per-run medians per mode, a relative improvement for each
baseline/candidate **pair**, a 4000-seed percentile bootstrap over those pairs, and passes only when the
95 % CI lower bound is above zero. Verified against synthetic data with a known 10 % effect:
`mean 9.54 %, CI 9.23–9.84 %, gate PASS`.

A result inside the CI is not a result. Report the CI, not the point estimate.

## Benchmark modes

| Mode | Sets | Use |
| --- | --- | --- |
| `baseline` | nothing (shipping defaults) | reference arm |
| `no-occlusion` | `EnableOcclusionCulling=false` | item 1 A/B: occlusion cost with zero culled meshes |
| `adaptive-open` | `AdaptiveOcclusion=true` | occlusion with backoff instead of probing every frame |
| `shadow-1024` | `Renderer3D.ShadowResolutionCap=1024` | item 6 A/B: shadow map area cost |
| `geometry-lod`, `shadow-lod`, `frozen`, `hidden-spiders` | existing crowd switches | unchanged |

## New fields in every artifact

`queryFullIndexRefreshes`, `queryIncrementalBoundsUpdates`, `updateAllocatedBytes`,
`physicsAllocatedBytes`, `lateAllocatedBytes`, `allocatedPerFrame`.

The refresh split is the one that decides item 3: `queryIndexRefreshes` counts both the cheap incremental
refit and the full regather, and before this change the artifact could not distinguish them.

## Status of the 2026-11-13 fix list

| # | Item | State |
| --- | --- | --- |
| 0 | 5-rep median/CI harness | **done** — runner + aggregator, statistics verified |
| 1 | Occlusion A/B | **done** — `AdaptiveOcclusion` now defaults on and `no-occlusion` exists; measurement pending |
| 2 | Allocation attribution | **done** — update/physics/late bytes per frame now recorded |
| 3 | Query refresh counters + invalidation split | **counters done, split reverted** — see below |
| 4 | Material variant table | **not started** — needs `#define` prepends and a bit-identical capture gate |
| 5 | R11G11B10F resolve | **done** — with an RGBA16F fallback; MAE gate still to run |
| 6 | Shadow resolution tier | **tiering done**, static/dynamic split not started |

### Why the item 3 split was reverted

Making `InvalidateQueryBounds` bump only `QueryBoundsRevision` looked like a one-line win. The existing
gate in `AIQueryOptimizationTests` (`--ai-query`) caught the defect immediately:
`disabled collider disappears immediately` failed, because the incremental path only refits bounds of
already-indexed colliders and cannot *remove* one. Callers of `InvalidateQueryBounds` are not all
bounds-only. Any future attempt must split the call sites first, then the revision, and must keep
`disabled collider`, `reactivated parent` and `enabled collider` green.

The counters are now in the artifact, so the next run can say whether the 90 refreshes per frame are
incremental refits (harmless) or full regathers (the 3–7 ms).
