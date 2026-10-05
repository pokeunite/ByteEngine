# VFX — quick start

## The simplest workflow

1. In the Asset Browser, right-click → **Create → VFX Effect** (or **+ Create → VFX Effect**).
2. Pick Fire, Smoke, Sparks, Explosion, MuzzleFlash, Rain, Snow, Magic, Dust, Trail, Beam, Impact, Portal, Heal, Footstep, or EnergyShot.
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
- Spawn VFX at Position: pick a .bvfx, provide a world position and size. This creates a one-shot instance and disables looping for that instance. Asset settings remain unchanged.
- Spawn Impact VFX: supply a world position and hit normal. The effect's local +Y emission axis faces the surface normal.
- Spawn VFX at Last Raycast Hit: place after Cast Ray in the same event execution. Uses the actual hit point/normal automatically; a miss does nothing.
- Spawn VFX on Object / Socket: choose a source object, optional socket name, and whether to follow. A missing socket warns instead of silently spawning at the wrong point. Following does not change the layer's world/local particle-space setting.
- Set VFX Beam Target: supply a world endpoint. Use Authored VFX Beam Endpoint clears the override. Set VFX Playback Speed supports 0–4.

Transient spawns reuse objects, simulation buffers and render batches after finishing. The scene limits active transient effects to 64 and their reserved particles to 65,536. The warm idle cache retains at most 16 objects and 16,384 reserved particles; excess is destroyed. Idle objects are inactive and detached from their previous owner/socket. Different assets/revisions do not reuse stale definitions. Reusable authored VFX Players are separate from this transient cache.

Trigger Play or Spawn on a pressed/hit/once event, not Always, unless repeating every frame is intentional. A child VFX object can use the existing socket-attachment workflow to follow a weapon. World-space smoke/trails stay behind a moving object; **Follow object (local space)** moves live particles with it.

## Making an effect

An effect has up to eight layers. Combine sparks, smoke, rings and flashes rather than wiring a graph.

- **Look:** billboard, velocity-stretched particles, segmented trails or beam; procedural soft disc/spark/ring/solid, custom image, start/end color and size, alpha/additive blending.
- **Emission:** continuous rate, initial burst, delay, lifetime/variation, point/sphere/box/cone/ring spawn, offset and coordinate space.
- **Motion:** direction/spread, velocity, gravity, drag, rotation and beam endpoint.
- **Lifetime curves:** optional size, opacity and speed multipliers, plus color-blend progress between the start/end colors. Tick a curve, choose a quick shape, then drag its points in the visual graph. Up to eight points; Add Point and Remove Last Interior Point keep editing simple. X is normalized age (0–1), Y is the multiplier. Size/speed range 0–4; opacity/color blend range 0–1. Unticked curves preserve old assets' appearance.
- **Forces:** wind acceleration, smooth procedural turbulence, and attraction/repulsion. Attraction uses a world point, or a local point for local-space particles. Speed curves multiply displacement rather than repeatedly multiplying stored velocity. These are inexpensive artistic forces, not fluid simulation.
- **Ground bounce:** cheap flat-plane collision. It does not raycast scene geometry.
- **Performance:** layer capacity and shared effect budget.

Custom sprites should use PNG with alpha. Sprite sheets support up to 8×8 frames. They are sampled into a shared 32-pixel-per-frame atlas with 32 lifetime color/fade samples; this is deliberately lightweight, not a full-resolution cinematic flipbook renderer. HDR sprite inputs fall back to the procedural sprite and show a status warning.

Preview has play/pause/stop/restart, orbit/zoom/frame, particle counters, budget warnings, undo/redo, save/revert, and an unsaved-close prompt. **Moving emitter** and its radius let you test trail continuity or world/local smoke without placing an object in a scene. **Move up / Down** changes layer budget priority; each layer shows its actual reserved capacity and warns when its delay exceeds one-shot duration. **Repeat preview** is editor-only; **Loop in game** is saved to the asset. Inspector Preview/Restart previews the selected component without entering Play.

Trails break when the gap exceeds **Break trail after jump**, avoiding a long ribbon across teleports. Emitter translation is interpolated across simulation substeps for moving emitters; rotation remains the current emitter rotation.

## Performance and scope

Simulation uses fixed 60 Hz steps, seeded randomness, dense bounded arrays and no steady-state managed allocation. Hitches process at most 12 substeps. No GameObject or physics body is created per particle. Each active layer submits one mesh draw; only live triangles are drawn. Matching layers share reference-counted sprite atlases, released when their last user is destroyed.

Each effect is limited to 8,192 particles; the default is 1,024. Layers reserve capacity in list order; later layers receive what remains. Overflow is dropped rather than allocating. Non-additive particles sort back-to-front within their layer; additive layers skip sorting. Draw Distance skips rendering, not simulation. Automatic Distance Quality progressively draws half/third/quarter of billboard and stretched particles at 2×/3×/4× Quality Distance. Selection uses stable particle IDs; beams and connected ribbons are never thinned. It reduces geometry/overdraw, not simulation time. Many large overlapping translucent sprites can still be GPU expensive.

This is a reusable **CPU-simulated 3D VFX foundation**, not a claim of full Niagara feature parity. GPU compute simulation, mesh particles, depth-soft particles, arbitrary shader graphs, volumetric fluids and scene-mesh particle collision are not included. The existing renderer handles the meshes, so no separate graphics backend is required; browser render performance still needs platform testing.

## Manual checks

1. Create Fire and Explosion assets. Preview them, edit colors/sizes, save, close and reopen.
2. Add a smoke layer to Fire; verify both render. Undo/remove/redo a layer.
3. Drag an effect into Scene View. Press Play, then stop. Check the saved scene and Blueprint retain the asset and settings.
4. Give an object a VFX Player without an asset. Choose Sparks; use a pressed-key event → Play VFX.
5. Use Spawn VFX at Position with Explosion. Verify it appears at the requested location and becomes inactive afterward. Repeated hits should reuse the bounded idle cache.
6. Move a Trail effect: it should leave a ribbon behind. Compare world/local space on Smoke.
7. Assign an alpha PNG or sprite sheet; check colors and animation in preview and Play.
8. Set a deliberately low budget and high rate; the counter stays bounded and Dropped increases. Stop/Clear removes all particles.
9. Open Lifetime Curves, tick Size, choose Pulse, drag a point, save/reopen and test Undo/Redo. Tick Opacity with Fade In and compare the preview.
10. Turn on Moving Emitter for Trail. Try Smoke in world/local space. Add wind/turbulence or attraction and confirm the motion changes.
11. Connect Cast Ray → Spawn VFX at Last Raycast Hit. Test wall/floor hits and misses. Use Spawn on Object/Socket for a muzzle flash, with Follow enabled.
12. Create Beam and use Set Beam Target from an event. Clear the target to restore the authored endpoint. Lower Quality Distance and move away from Smoke; Rendered Particles should decrease while Active Particles remains unchanged.

Automated checks: `dotnet run --project Tests/ByteEngine.Tests -c Release -- --vfx` and `--vfx-render` (invisible offscreen GL, not editor UI automation).

Design references: [Unreal Niagara overview](https://dev.epicgames.com/documentation/en-us/unreal-engine/niagara-overview?application_version=4.27) and [Godot 3D particles](https://docs.godotengine.org/en/stable/tutorials/3d/particles/creating_a_3d_particle_system.html). ByteEngine uses preset/layer composition and explicit budgets while keeping basic use graph-free.
