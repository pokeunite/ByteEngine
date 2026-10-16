# Native water components

Water simulation lives in `Engine/ByteEngine.Core/Gameplay/Hydraulics`. These components contain no Headwater island names, controls, level positions or victory rules. They are serialized native components and appear in the editor's Add Component search. Game Event Sheets command gates and selectors; scene/Blueprint properties define the plumbing and geometry.

## Basin and local surface

Add a Water Network and Water Basins. Each basin owns an authoritative volume, floor, rim, footprint and water level; immersed rigid bodies displace water. Add Water Surface Dynamics to a basin for a staggered height/velocity grid. Small waves propagate, reflect at solid boundaries, advect with the current and dissipate. Wind ripples start immediately; incoming jets, outgoing suction and rigid-body motion disturb the same grid. The rendered Water Surface samples that grid for vertex height and normals; buoyancy samples its local height with a configurable influence. The grid's mean height perturbation is removed, so it cannot manufacture water.

Water Surface hides its surface and volume below Dry Depth. A zero-depth basin is dry, rather than a blue sheet laid on the floor. Refraction, depth absorption, Fresnel reflection and small optical ripples use the same material during idle and flowing states.

## Streams with real travel time

Water Stream Transport (`WaterFlowPath3D`) stores water in 97 transport reaches. Gravity changes downstream velocity; discharge determines width and depth. Input is advected conservatively to a receiving basin instead of appearing there instantly. After an inlet closes, water already in the stream continues to arrive until the profile empties.

Set Conserve Volume on the stream and set its Flow Gate and End Basin. Set that Water Gate's Transit Path Name to the stream object's name. The native network includes stream storage and pending discharge in Total Volume. Arrival flux drives local waves, foam and receiving currents. Initial State preserves the transport profile when saving a running scene.

For a retained level channel, regular native Water Basins and Water Gates provide the conserved reaches; a stream surface can interpolate their levels through Basin Prefix / Basin Count. This does not count the water a second time.

## Controls and scope

Screw Sluice Actuator and Rotary Water Diverter read configurable scene variables. Apertures, wheel rotation and path selection share measured travel. Use these components from Event Sheets; no game-specific C# controller is necessary.

This is a bounded-basin, small-amplitude shallow-water system with conservative channel/stream transport. It does not resolve overturning liquid volumes, volumetric bubbles or full 3D fluid turbulence; fine spray is rendered as flux-driven particles. The staggered height/velocity formulation follows standard shallow-water equations; background reference: https://matthias-research.github.io/pages/publications/hfFluid.pdf. This implementation is not a port of that paper's full CUDA/particle system.

Validation in Headwater covers idle wave motion, visible mesh deformation, dry starts, impulse propagation, damping, zero-mean waves, delayed stream arrival, stream drain-down, total volume, pause, valve reversal, sealed float retention, physical gate blocking and successful cargo delivery.

## Coastal sea and shared optics

`WaterOcean3D` is a native editor component, not a Headwater plugin. Add it to any scene for open sea. Size, sea level, wavelength, amplitude, wind direction, steepness, absorption, scattering, caustics, roughness, shoreline foam and screen-space reflections are serialized properties. Its three gravity-wave bands use GPU Gerstner displacement and analytic normals. `Sample(worldXZ, time)` supplies the equivalent CPU displacement and normal for floating scenery without changing finite basin volumes. Extreme steepness and amplitude are bounded to prevent wave folding. The decorative ocean never registers as a puzzle basin.

Both Water Surface and Water Ocean use the native renderer's opaque HDR colour/depth capture. Water reconstructs the submerged scene, distorts refraction without pulling foreground pixels underwater, applies wavelength-dependent Beer–Lambert extinction and directional scattering, and blends reflections with the dielectric water Fresnel term. Caustic patterns follow reconstructed submerged positions. Ocean shoreline foam follows actual scene-depth intersections; a sloping seabed and submerged rocks provide the shallows. Screen-space reflection rays fall back to the environment when their hit is off screen or unavailable. There is no full off-screen reflection trace or underwater camera/postprocess volume yet.

The sea has a stationary tessellated mesh, with broad waves evaluated by the vertex shader. No per-frame CPU ocean mesh rebuild is needed. Use Screen Space Reflections selectively on slower integrated GPUs. Macro waves and fine optical ripples remain separate from basin transport and water conservation.

Research: Epic's public [Single Layer Water](https://dev.epicgames.com/documentation/en-us/unreal-engine/single-layer-water-shading-model-in-unreal-engine), [Water Waves](https://dev.epicgames.com/documentation/en-us/unreal-engine/simulating-waves-using-the-water-waves-asset-in-unreal-engine) and [Water Meshing](https://dev.epicgames.com/documentation/unreal-engine/water-meshing-system-and-surface-rendering-in-unreal-engine) documentation informed this separation of optical depth, macro waves and micro detail. The GitHub connector initially returned 404 on 2026-10-16; that did not represent the user's local Git access. Local Git access was subsequently verified and the actual UE 5.8.3 water source was inspected at commit 396c9f059903aed5fec78ecd3d437a40c6415368. See UE-Water-Source-Review.md. The existing ByteEngine rendering upgrade predates that direct-source review. No Unreal code was copied into ByteEngine.

Further wave reference: NVIDIA GPU Gems, Mark Finch, Effective Water Simulation from Physical Models: https://developer.nvidia.com/gpugems/gpugems/part-i-natural-effects/chapter-1-effective-water-simulation-physical-models . The native wave derivative and steepness bounds follow standard analytic Gerstner mathematics; no sample implementation was copied.


## Coefficient optics and bathymetry follow-up

Absorption and Scattering on Water Surface/Water Ocean now mean reciprocal-metre medium coefficients. Total extinction is their sum. The shared shader integrates the scattered fraction and attenuates both the view path and the incident light path to the submerged scene. WaterOptics supplies native CPU reference equations used by regression checks. Four-neighbour depth-aware refraction rejects foreground wall/cargo samples, and caustics modulate lit submerged surfaces instead of adding white surface lines.

Add Water Bathymetry (WaterDepthField3D), choose a static landscape root and world bounds, and reference that object's name from Water Ocean's Depth Field Name. The component rasterizes opaque mesh triangles into a 16-bit height field. The ocean's CPU Sample and GPU vertex waves use the same bilinear bathymetry and Wave Mask Depth. Rebuild the field after changing the static landscape. It is static bathymetry, not a moving-object fluid obstacle solver.

Water Flow Audio (WaterFlowAudio3D) drives an assigned looping Audio Source from actual source flux or stream outflow, including smoothing and pause handling. Headwater's lever, winch, latch and delivery cues are scene Audio Sources triggered by Event Sheets, not game-specific code in the native water components.

This remains a finite-volume channel/basin simulation plus local shallow-water surface dynamics and analytic open-sea waves. It does not simulate overturning 3D liquid volumes. Temporal reflection denoising remains future work.

## Connected channels and surface update cost

WaterFlowPath3D.AppearanceSurface optionally references a WaterSurface3D object and shares its extinction, scattering, roughness, normal strength, caustics and reflection settings. Different actual depths still produce different colours. ConfinedChannel spreads the transported cross-sectional area across the authored Width (depth remains area/width); free streams retain the narrowing-jet mode. FoamStrength is independently tunable. Channel micro-normal perturbations follow the downstream surface frame, including slopes, rather than assuming world X/Z.

WaterSurface3D.MatchSimulationResolution caps the visual mesh at the local solver grid dimensions by default. Mesh height is sampled once per vertex and normals come from neighbouring visible vertices; the previous five solver samples per vertex are unnecessary. Fine optical ripples remain in the shader. Neither change alters the fixed-step solver, stored volume, buoyancy, or transit timing.
