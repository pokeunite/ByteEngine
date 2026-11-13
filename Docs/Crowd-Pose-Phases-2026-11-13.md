# Crowd pose phase buckets — 2026-11-13

## What was wrong

`SkeletalMeshRenderer` caches crowd poses per `(animation, frame, rate)` where `frame` comes from each
actor's own `_currentTime`. Fifty identical looping drones therefore occupy up to fifty different frames
at any instant, so the cache stores up to fifty full copies of deformed vertices (`new
Mesh(runtime.DeformedVertices.ToArray(), …)`, `CrowdPose.cs:47`) plus fifty merged crowd meshes. The pool
caps at 256 poses and then counts `Refused`.

Because every cached pose has its own mesh instance, `RenderBatcher` (which groups on
`(Mesh, Material, Shadows)` by reference) cannot instance them.

## What changed

1. `SkeletalMeshRenderer.AdvanceCrowdClock(Time.DeltaTime)` — one shared clock, advanced once per
   simulation frame from `Scene.UpdateInternal` next to `SkeletalAttachmentService.UpdateScene`.
2. `CrowdPosePhaseBuckets` (per renderer) and `GlobalCrowdPosePhaseBuckets` (process wide, for benchmarks).
   When a value above 1 is active **and** the clip loops, the sample time becomes
   `(crowdClock + bucket * duration / buckets) mod duration`, with `bucket` a stable per-renderer index.
   Result: at most `buckets` distinct pose keys at any instant, instead of one per actor.
3. Nothing else. Instancing needs no new code: identical cached pose meshes share one `Mesh` reference, so
   the existing batcher groups them and `PrepareInstanceStream` uploads per-instance matrices for any batch
   with more than one instance.

## Trade-off

Actors inside one bucket animate in lockstep. That is acceptable for a fleet of identical units and
noticeably wrong for a mixed crowd, which is why the feature is opt-in and defaults to 0 (off). Non-looping
clips are excluded, because a shared loop phase would make a one-shot animation repeat visually.

## Preconditions for it to do anything

- `ShareCrowdPoses` must be true on the renderer. `MiningSpiderAnimation3D.cs:52` sets it;
  `MiningSecurityDrone3D.cs:53` does **not**, so drones currently never enter the crowd path.
- `CrowdEligible` also requires no active blend, ragdoll, hit reaction or physical deformation, so combat
  states fall back to per-actor evaluation by design.

## Measuring it

```powershell
Tools/Performance/Measure-Performance.ps1 -Reps 8 -Modes baseline,crowd-phases
```

`crowd-phases` sets `GlobalCrowdPosePhaseBuckets = 4`. Success shows up as a drop in `renderStats.DrawCalls`
relative to `instancedObjects` (the crowd collapsing into instanced batches), plus lower `animationMeanMs`
and fewer allocations per frame. If `DrawCalls` does not fall, the poses are not sharing — check
`ShareCrowdPoses` first.
