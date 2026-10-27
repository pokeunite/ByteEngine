# Alien mining canyon

The palclone Main scene now contains an 84 × 168 metre worksite (previously 44 × 96). The camp machines retain their existing identities, offsets, controls and production systems. The accepted sand material covers the larger floor at the original world texture density. The sky image and lighting remain in place; fog distances were extended for the larger environment.

## Art source

Open `C:/Users/codex/Documents/palclone/SourceArt/Environment/MiningRockKit/MiningRockKit.blend`.

- **Mining Canyon Layout** is the assembled environment, with linked collection instances and the camp machines for scale.
- **Mining Rock Kit** contains 16 separately named asset collections in a gallery. Each rock, crystal and leaf is an editable mesh; the original isolated reference images are packed into the file.
- The original Blender **Scene** is retained.

The kit reconstructs the visible silhouettes, broad planar facets and colour palette from the image. Unseen sides are reconstructed geometry. This is a reference-based 3D interpretation, not a recovered original scene.

| ID | Asset |
|---|---|
| R01 | Tall vertical cliff columns |
| R02 | Stepped low cliff |
| R03 | Leaning slabs |
| R04 | Concave corner formation |
| R05 | Split monolith |
| R06 | Mesa |
| R07 | Natural bridge |
| R08 | Single boulder |
| R09 | Broad low cluster |
| R10 | Upright cluster |
| R11 | Small rubble pile |
| R12 | Scattered chips |
| R13 | Crystal fissure cliff |
| R14 | Crystal boulders |
| D01 | Cyan crystal tuft |
| D02 | Violet plant |

Runtime models live in `Assets/Models/Environment/MiningRockKit`. Each GLB shares its materials across instances. The scene contains 336 authored instances, 913 mesh/material parts and 164 simple rock collision proxies. Large rocks and cover block movement and bullets; small chips, plants and distant scenery do not create navigation clutter.

## Level editing

In the hierarchy expand **Alien canyon environment**. Rear/left/right boundaries, upper strata, foothills, arena cover, foliage, rubble and distant landforms are separate groups. Move an instance to reposition a complete formation. The collision objects are children and follow it.

The rear and sides are enclosed by overlapping solid rock formations. The broad forward valley contains five enemy entrances, approximately 110–125 metres from the drill. Enemy detection ranges and path search budgets were increased in the two existing alien blueprints to support the longer route. Combat damage, health and movement balance were not changed.

The floor spans X −32 to 52 and Z −138 to 30. Camp arrangement spans X −25 to 45 and Z −30 to 24. The arrangement camera accommodates the larger site; all camp machines and placed defences still use their existing move/rotate controls.

`level-layout.json` records each rock ID, position, scale, rotation, group and collision intent. `models.json` records mesh bounds, materials, triangle counts and individual rock collision candidates. The included Python sources reproduce the editable kit and Blender assembly. The engine authoring tool uses the same layout plan.

## Validation

- Native model/material loading and 84 × 168 metre floor dimensions checked.
- All five entrances have navigation paths to both the drill and player.
- 193 ray probes at two-metre intervals confirm solid rear and side boundaries.
- Both alien roles travel from the distant entrances, reach attack range and damage their assigned targets; clearing the wave returns to preparation.
- Camp placement/rotation, overlap rejection, save/restore, defence relocation and control restoration passed native checks.
- Installed desktop editor Enter Play, runtime clone, plugin startup, rendering, defence cursor and Stop Play passed.
- Checks disable normal progress persistence and verify that the authored scene is unchanged.

Native screenshots and verification logs accompany the source art. Scene/blueprint backups are under the project's `.byteengine/Backups/alien-canyon-*` and `.byteengine/Backups/full-sand-floor-*` folders. No game build was exported.
