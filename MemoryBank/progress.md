# Progress

Updated: 2026-11-13

## Works

- Editor shell, Project Browser and templates (Clean Project, 3D Starter,
  ByteArena, Last Stand, Interactive Sand).
- Model pipeline: FBX/OBJ/GLB import with node hierarchy, sub-assets, reimport,
  scale normalization and per-instance markers.
- Rendering: indexed geometry, PBR-style shading, shadows, instancing, static
  mesh LODs, water and terrain.
- Editor tooling: scene view navigation/gizmos, Game View, UI authoring with
  resolution previews, profiling by viewport/pass.
- Characters: `CharacterController3D` (acceleration, air control, slope limits,
  step tolerance, coyote time, buffered jumps), capsule collider, animation
  controller, skeletons, sockets, blending, crowd pose sharing.
- Blueprints and variants with three-way inheritance; ByteGraph `.byteevents`
  logic with tracing and Play-mode hot reload.
- Variables across Global/Scene/Self/Component scopes with persistent GUID refs
  and writable nested fields.
- Navigation: grid and triangle-navmesh baking with clearance/slope constraints,
  links, debug paths, agent path following.
- Physics: rigid bodies, box/capsule/heightfield/static-mesh/convex-hull
  colliders, BVH acceleration, angular contacts, sleeping, swept collision,
  distance/hinge joints, plus the separate construction-vehicle solver.
- Audio: master/music/sfx/ui buses, ducking, voice priorities, bounded native
  WAV/Ogg streaming; browser uses media playback.
- Runtime: Windows player publish, WebGL2 browser runtime with crash reporting,
  plugin loading and browser plugin package sync.

## In progress

- Branch integration and real repeated performance acceptance remain open.
- Cline navigation/physics/crowd updates are reviewed with correctness fixes; see
  Docs/Cline-Update-Review-2026-11-13.md for exact checks and branch boundaries.

## Known gaps

- Main uses CPU crowd deformation. Native GPU skinning exists and passes automated
  main/shadow parity tests on the secondary branch; integration remains open.
- Crowd sharing opts out for hidden mesh parts, blends, advanced pose layers,
  IK/reactions, physical bone deformation and per-slot material overrides.
- Root motion and socket followers are still evaluated per renderer on a crowd
  pose hit.
- Batched spawning is missing: `AddGameObject` is O(n) per call, so mass spawning
  is quadratic.
- Dynamic avoidance and skinned LOD generation are separate, unfinished work.
- Fixed-step simulation is opt-in for compatibility; scene callbacks must migrate
  before enabling it.
- Importer/rig compatibility still needs per-asset validation.
- OBJ import is intentionally basic; GLB is the recommended exchange format.
