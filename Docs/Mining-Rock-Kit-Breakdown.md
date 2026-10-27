# Mining camp: separated rock reference kit

The supplied concept has a small number of recurring shape families, placed at different scales and angles. They are separated into fourteen rock references, plus independent crystal and foliage inserts. These are image-based modelling references, not finished 3D assets. Occluded edges are reconstructed; this is not a claim of exact pixel segmentation.

Open `SourceArt/Environment/RockKitReferences/Rock-Kit-References.html` in the game project. Every card opens its own PNG and highlights an approximate source region in the untouched original image. The complete generation prompts, source coordinates and modelling notes are in `catalog.json`.

| ID | Separate shape / cluster | Intended use |
|---|---|---|
| R01 | Tall vertical cliff columns | Continuous tall rear boundary |
| R02 | Stepped lower cliff segment | Side-wall height transitions |
| R03 | Leaning blade-shaped cliff | Slanted side walls / end caps |
| R04 | Closed corner boulder barrier | Curved foreground boundary trim |
| R05 | Tall split monolith | Distant skyline spire |
| R06 | Layered mesa | Distant broad landform |
| R07 | Natural rock bridge | Background silhouette outside the playable area |
| R08 | Single large angular boulder | Individual cover / silhouette variation |
| R09 | Broad low boulder cluster | Wide low cover |
| R10 | Upright boulder cluster | Taller compact cover |
| R11 | Small broken rock pile | Small formation / basal debris |
| R12 | Scattered stone chips | Sparse decorative rubble |
| R13 | Tall cliff with crystal fissure | Crystal-bearing boundary variant |
| R14 | Low boulders with crystal seam | Crystal-bearing cover / edge variant |
| D01 | Standalone cyan crystal tuft | Independent insert for several rocks |
| D02 | Standalone violet plant tuft | Independent foliage at rock bases |

The references preserve broad flat shaded faces, blunt jagged silhouettes, slate/violet basalt, warm highlights, cyan mineral seams and sparse violet plants. Model neutral rock materials and let the accepted game lighting supply the orange highlights. Keep crystals and foliage as separate objects/material slots, including within assembled variants.

When modelling, keep the constituent rocks separate in Blender and provide a reusable cluster root. For background modules, simpler geometry and no individual small-stone collision are sufficient. Avoid making every face a separate material or creating many shadow-casting crystal lights.

The camp's back and both sides must remain physically and visually closed, using overlapping solid tall cliff modules. Low corner trim alone should not imply a climbable exit. Only the broad forward valley admits enemies. Background monoliths, mesas and the bridge do not imply routes behind the camp. Forward boulders should leave readable movement and firing routes.

## Floor update

The accepted orange sand now covers both **Alien worksite floor** and **Ground**. Two editable `.bmat` instances inherit the accepted `OrangeSand.bmat` and retain a common world-aligned texture scale equivalent to the twelve-by-fourteen-metre sample. The isolated test patch is disabled to remove its rectangular outline. The accepted sky and lighting, every object identity/transform, colliders and navigation settings are preserved. The original scene was backed up under `.byteengine/Backups/full-sand-floor-*`.

Floor result was inspected in the actual native renderer. Rock PNGs use the built-in image-generation tool to isolate reference designs, with each family delivered as its own file. No rock geometry has been placed in the game yet, and no game export was performed.
