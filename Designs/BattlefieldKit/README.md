# Quarry Siege: first playable battlefield

Installed into Goblin Scraper's saved Scenes/Main.bytescene. Reopen the project and Main scene to see it. Do not overwrite it by saving an older scene still open in the editor.

## What was built

18 reusable Blender assets: 6-metre palisade, green/red banners, green/red watchtowers, fortress gate, open departure arch, barricade, ramp, crate, barrel, workbench, workshop shed, brazier, three quarry rocks and a pine tree. Materials and timber/stone/dirt textures were authored locally. No new goblin models were made.

136 placed props form a broad quarry arena. Ground is 190 by 180 metres. Green workshop starts near the player; red fort is about 118 metres downfield. The centre stays clear; six encounters and two ramps sit around the sides and fort approach. Existing 18 goblins are reused in six groups of three. The existing battle timer is set to 180 seconds.

This is a stylized, lightweight interpretation of the approved reference, not a pixel-identical reproduction. The floor is flat to support the current vehicle physics. Rocks define the perimeter instead of a sculpted collision terrain.

## Manual editing

Select QUARRY SIEGE - editable battlefield in Main. Its child groups are Green workshop, Red fortress, Arena encounters and Quarry perimeter. Props are individually saved and editable. Their collision child objects have native BoxCollider3D components. Move, rotate or scale the prop parent to move its collision with it. Ramp collisions are sloped boxes matching their decks. Collision-only perimeter walls stop vehicles driving off the ground.

GOBLINS - duplicate or move these spawns remains outside the map group. Keep GoblinEnemySpawn true on enemy spawn objects. HUD and native control event sheets were retained.

Assets are installed in Assets/Environments/QuarrySiege. The old scene is backed up in project Backups/Before-Quarry-Siege/Main.bytescene.

The fortress gate, crates, barricades and walls are static scenery/collision at this stage. They do not yet break, open or create a siege objective. Existing combat still wins by clearing all red goblins. Brazier flame shapes are static emissive-style art, not animated fire effects.

## Blender source and exports

GoblinBattlefieldKit.blend: editable multi-material original prop library; original geometry is kept separate by material.
QuarrySiege-Assembled.blend: assembled arena reference with repeated mesh instances, daylight and overview camera.
Both files pack their texture images.
exports: original PBR GLBs.
exports-optimized: one texture atlas/material per prop; these are the files installed in the game. Source UVs are retained during baking, then the atlas becomes the exported primary UV.
layout.json: source placements and enemy locations.
kit.json: local collision dimensions in Blender axes.

Built using installed Blender 5.0 command-line Python. Blender MCP was unavailable. Verified through Blender rendering and native engine rendering.

## Verification and performance

Native checks passed: model import, visible saved scene in Edit, save/reload, deployment, driving through the departure arch into the open centre, and existing enemy reset after returning to build. Native compilation: zero errors/warnings.

Single-material exports improved the local synchronized 1280x720 scene update/render measurement from about 28 FPS to about 35 FPS on the AMD Vega 10 integrated GPU. This excludes editor panels and is not a 60 FPS guarantee. Further engine/render optimization is needed on this hardware; art remains intentionally lightweight (about 17,000 triangles across all 18 prototype models, before instancing).

The subsequent 0.4.4 engine/plugin optimization pass measured about 43–45 FPS in build mode and 35–40 FPS driving at synchronized 720p. Update work fell to about 4.2–4.5 ms. See Docs/PERFORMANCE-044.md for details and remaining limits.
