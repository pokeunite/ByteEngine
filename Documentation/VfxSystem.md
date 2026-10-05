# VFX — quick start

## The simplest workflow

1. In the Asset Browser, right-click → **Create → VFX Effect** (or **+ Create → VFX Effect**).
2. Pick Fire, Smoke, Sparks, Explosion, MuzzleFlash, Rain, Snow, Magic, Dust, Trail, or Beam.
3. The VFX window opens. The middle is a live preview; choose a layer on the left and edit its settings on the right.
4. Click **Save**, then drag the .bvfx asset into Scene View.
5. Press Play. The placed **VFX Player** plays automatically.

For an even quicker effect, **GameObject → VFX Effect** creates a VFX Player. Choose a built-in preset in its Inspector; no asset is required.

## Gameplay

For a reusable effect on a gun, engine, or torch, add VFX Player to an object, choose its effect asset, and untick **Play on Start** if it should wait for gameplay.

Event Sheet's **VFX** category provides:

- Play VFX, Stop VFX (Let Particles Finish), Stop and Clear VFX.
- Pause VFX, Resume VFX.
- Emit VFX Burst (particle count per layer).
- Set VFX Intensity and Set VFX Size.
- VFX Is Playing and VFX Has Finished. Finished is false until the effect has played.
- Spawn VFX at Position: pick a .bvfx, provide a world position and size. This creates a one-shot instance, disables looping for that instance, and removes it once all particles finish. Asset settings remain unchanged. At most 64 disposable effects may coexist per scene.

Trigger Play or Spawn on a pressed/hit/once event, not Always, unless repeating every frame is intentional. A child VFX object can use the existing socket-attachment workflow to follow a weapon. World-space smoke/trails stay behind a moving object; **Follow object (local space)** moves live particles with it.

## Making an effect

An effect has up to eight layers. Combine sparks, smoke, rings and flashes rather than wiring a graph.

- **Look:** billboard, velocity-stretched particles, segmented trails or beam; procedural soft disc/spark/ring/solid, custom image, start/end color and size, alpha/additive blending.
- **Emission:** continuous rate, initial burst, delay, lifetime/variation, point/sphere/box/cone/ring spawn, offset and coordinate space.
- **Motion:** direction/spread, velocity, gravity, drag, rotation and beam endpoint.
- **Ground bounce:** cheap flat-plane collision. It does not raycast scene geometry.
- **Performance:** layer capacity and shared effect budget.

Custom sprites should use PNG with alpha. Sprite sheets support up to 8×8 frames. They are sampled into a shared 32-pixel-per-frame atlas with 32 lifetime color/fade samples; this is deliberately lightweight, not a full-resolution cinematic flipbook renderer. HDR sprite inputs fall back to the procedural sprite and show a status warning.

Preview has play/pause/stop/restart, orbit/zoom/frame, particle counters, budget warnings, undo/redo, save/revert, and an unsaved-close prompt. **Repeat preview** is editor-only; **Loop in game** is saved to the asset. Inspector Preview/Restart previews the selected component without entering Play.

## Performance and scope

Simulation uses fixed 60 Hz steps, seeded randomness, dense bounded arrays and no steady-state managed allocation. Hitches process at most 12 substeps. No GameObject or physics body is created per particle. Each active layer submits one mesh draw; only live triangles are drawn. Matching layers share reference-counted sprite atlases, released when their last user is destroyed.

Each effect is limited to 8,192 particles; the default is 1,024. Layers reserve capacity in list order; later layers receive what remains. Overflow is dropped rather than allocating. Non-additive particles sort back-to-front within their layer; additive layers skip sorting. Draw Distance skips rendering, not simulation. Many large overlapping translucent sprites can still be GPU expensive.

This is a reusable **CPU-simulated 3D VFX foundation**, not a claim of full Niagara feature parity. GPU compute simulation, mesh particles, depth-soft particles, arbitrary shader graphs, volumetric fluids and scene-mesh particle collision are not included. The existing renderer handles the meshes, so no separate graphics backend is required; browser render performance still needs platform testing.

## Manual checks

1. Create Fire and Explosion assets. Preview them, edit colors/sizes, save, close and reopen.
2. Add a smoke layer to Fire; verify both render. Undo/remove/redo a layer.
3. Drag an effect into Scene View. Press Play, then stop. Check the saved scene and Blueprint retain the asset and settings.
4. Give an object a VFX Player without an asset. Choose Sparks; use a pressed-key event → Play VFX.
5. Use Spawn VFX at Position with Explosion. Verify it appears at the requested location and its object is removed afterward.
6. Move a Trail effect: it should leave a ribbon behind. Compare world/local space on Smoke.
7. Assign an alpha PNG or sprite sheet; check colors and animation in preview and Play.
8. Set a deliberately low budget and high rate; the counter stays bounded and Dropped increases. Stop/Clear removes all particles.

Automated checks: `dotnet run --project Tests/ByteEngine.Tests -c Release -- --vfx` and `--vfx-render` (invisible offscreen GL, not editor UI automation).

Design references: [Unreal Niagara overview](https://dev.epicgames.com/documentation/en-us/unreal-engine/niagara-overview?application_version=4.27) and [Godot 3D particles](https://docs.godotengine.org/en/stable/tutorials/3d/particles/creating_a_3d_particle_system.html). ByteEngine uses preset/layer composition and explicit budgets while keeping basic use graph-free.
