# Eight-legged spider models

Source: `C:/Users/codex/Documents/palclone/SourceArt/SpiderModels/SpiderKit.blend`.

Upload files: `PlayerHunter-Unrigged.glb` and `DrillBreaker-Unrigged.glb` in the same folder.

Both models reconstruct the corrected concept images as editable low-poly meshes. PlayerHunter uses a lean violet body, red-orange dorsal stripe and joint markings. DrillBreaker has a broader abdomen, thicker legs, ochre mineral armour and heavier crushing mouthparts. Each has **eight walking legs, four per side**, plus compact mouthparts that are not walking limbs. The reference images are packed into the Blender file. Hidden undersides and the depth of overlapping details are reconstructed; this is not a pixel-exact recovery of an original 3D model.

## Blender

The file opens in **Spider Pair**, a comparison scene. Choose **PlayerHunter spider inspection** or **DrillBreaker spider inspection** for an individual asset. Editable source collections are `SPIDER_PlayerHunter` and `SPIDER_DrillBreaker`. Legs are named L1–L4 and R1–R4; their four joint sections are recorded as vertex groups and in the model JSON. The source body, leg meshes, eyes, mineral plates and mouthparts remain editable. The pre-existing defence scenes are preserved.

The source legs are continuous, closed meshes through their joints. The upload copies weld their roots into the body and merge the remaining details into one mesh object. Eyes, mouthparts and mineral details remain separate closed surface islands inside that object; the body plus all eight legs form one connected closed shell.

| Upload | Triangles | Rig |
|---|---:|---|
| PlayerHunter-Unrigged.glb | 7,172 | None |
| DrillBreaker-Unrigged.glb | 7,348 | None |

GLBs contain materials and geometry, without external texture dependencies, armatures, animation clips, lights or studio props. Export is Y-up, at metre scale, with the creature's front toward +Z. The Blender source faces −Y with Z up. Total foot span is approximately 4.4 metres for the hunter and 4.7 metres for the breaker; game scale can be adjusted after rigging.

## Mesh2Motion

Upload one GLB to the creation tool, choose the spider template, align it to the floor and fit all four leg pairs. Use the rest pose supplied here. Match the head, body, abdomen and mouthpart bones before binding. Preview walk, attack, hit and death animations; inspect hips, knees, feet and mouthparts for weighting errors before exporting the rigged file. The models have **not yet been uploaded or rigged in Mesh2Motion**, so successful automatic weights are not claimed.

No in-game enemy replacement was performed in this modelling pass; that should use the completed animated exports.

## Verification

The Blender source checks count eight tagged limbs per asset, verify all four pairs, closed manifold edges, positive volume, finite vertex data and connected topology through every leg. Upload-core checks verify that body and all eight leg roots form one connected closed shell. GLB checks verify a single mesh, eight connected legs in the source/core records, no skeleton or animations, valid bounds and finite vertex coordinates. Top/reference and side renders were inspected and correction passes addressed surface normals, overlapping plates and leg material transitions.

Authoring scripts and verification JSON accompany the models. Back up manual edits before rerunning the generator.
