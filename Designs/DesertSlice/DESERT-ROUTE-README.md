# Dune Company — audio and first desert route

Open `C:/Users/codex/Documents/DuneCompany/DuneCompany.byteproject` with the refreshed ByteEngine editor.

## Play

Build/load a rover with an engine, seat and tyres. Press **B** to drive. **W/S** throttle/reverse, **A/D** steering, **Space** brake, **R** recover to the garage, **B** return to build. Follow the roadside drums to the dismantling gantry about 400m from the garage. The haul road is 448m long. The navigation HUD confirms arrival. This is the first landscape/route milestone; cargo collection and payment are not implemented by this pass.

The drivable heightfield is 1,024 × 1,024m. Distant dunes extend the view; the route component recovers the rover before it leaves the collision area. Sand keeps shallow wheel compaction/tracks and traction changes; no digging was added.

## Sound and effects

Six CC0 recordings cover ignition, idle, loaded engine, tyre rolling, gravel braking and a quiet skid layer. Engine load changes blend/pitch; surface-contact speed and slip drive tyre sound and sand. Airborne wheels do not emit contact dust. Exhaust follows the compact engine stack and both heavy-engine outlets. Returning to build stops/releases vehicle audio and effects.

Select the **Dune Vehicle Workshop** component to edit `VehicleSfxVolume`, `DustAmount`, `ExhaustAmount`. Edit `Assets/VFX/tyre-sand.bvfx` and `engine-exhaust.bvfx` with the VFX editor. The test rover's vehicle effects have 420 allocated particles; wheel emitters are capped at eight, exhaust engines at four.

## Manual world editing

The scene contains the garage, road, salvage gantry, scanned props, static collision proxies, sun, sky and route as native objects/components. Move the props directly in Scene View. `Dune World Obstacle 3D` supplies editable size/centre collision to the vehicle solver; keep its proxy aligned when editing a prop.

Use terrain brushes on **Desert - Editable Dunes**. The 16-bit source is `Assets/DesertSlice/garage-salvage-heightmap.png`; `packed-road-mask.png` marks the haul road and pads. Resetting wheel tracks preserves the authored packed road. The road component's points/width are editable and the ribbon follows terrain edits.

The route component exposes its salvage destination, arrival radius and boundary margin. Its HUD is navigation/arrival feedback, separate from the older combat proving contract.

`SourceArt/DesertSlice` contains the Blender sources, generation scripts and original source downloads. `PluginSource/DuneCompany` and `PluginSource/DesertTerrain` contain the editable gameplay/terrain plugin code. Backups are in `Backups/audio-terrain-20261006`.

## Graphics

CC0 photographic sand/metal materials, scanned rocks/crates/drums and the `quarry_01_puresky` HDR replace the old mountain-horizon lighting. The default uses Fast quality, contact shadows and colour grading for this laptop; Balanced/High are available on the sky component at greater cost. Terrain has distance LODs for rendering while full-resolution collision remains unchanged.

This is a first environment pass, not a claim that the entire concept map or a finished AAA photorealistic world has been completed. The first route is intentionally limited; the fort/racing/bounty regions remain future map work.

## Windows export fix

The old exported build used a stale `ByteEngine.Core.dll` which lacked `ITerrainSculptSurface`; that stopped the terrain plugin loading, removing both ground rendering and collision. The editor and PlayerRuntime now use matching engine builds. Export rejects a different player core before creating a build, and runtime refuses active required components that failed to load. Package validation now creates a hidden graphics context so HDR/model components can deserialize correctly.

Old exports remain old builds. Use the new build or export again from the refreshed editor. Keep the entire exported folder together. `--validate` checks package content and scene loading; `--smoke-test` renders 150 hidden native frames and logs results. Logs are in `%LOCALAPPDATA%/ByteEngine/Games/Logs`.

## Source quality/licences

See `ASSET-NOTICE.txt` and `Design/DesertRoute/asset-sources.json`. Each Freesound licence was checked individually. Audio uses public HQ previews converted to WAV, not original full-resolution WAV downloads. All newly downloaded props/materials/HDR/audio in this pass are CC0.
