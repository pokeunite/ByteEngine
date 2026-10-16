# Direct Unreal water source review

Local Git access to https://github.com/EpicGames/UnrealEngine.git was verified on 2026-10-16. The initial GitHub connector 404 was specific to that connector, not evidence that the user lacked access. A filtered research checkout is at `C:/Users/codex/.cache/UnrealWaterResearch`; selected source is in its WaterSourceReview directory.

Reviewed revision: `396c9f059903aed5fec78ecd3d437a40c6415368`. Engine/Build/Build.version identifies UE 5.8.3.

Directly inspected:

- Engine/Shaders/Private/SingleLayerWaterCommon.ush: safe viewport bounds and depth-aware bilateral sampling reject foreground objects when reconstructing the scene behind water.
- Engine/Shaders/Private/SingleLayerWaterShading.ush: absorption and scattering contribute jointly to extinction; integrated scattering is normalized by extinction. Submerged illumination also accounts for the path from the light to the bottom. Distorted scene samples can fall back to undistorted samples.
- Engine/Source/Runtime/Renderer/Private/SingleLayerWaterRendering.cpp: separate reflection modes, sky/capture fallback, resolution scaling, reconstruction/denoising and tiled composition are distinct rendering systems.
- Engine/Plugins/Experimental/Water/Source/Runtime/Private/GerstnerWaterWaves.cpp and GerstnerWaveEvaluation.cpp: deterministic wave generation, gravity dispersion, CPU wave queries, depth attenuation and agreement with shader conventions.

The first ByteEngine upgrade used public documentation; the subsequent water pass implements findings from this direct-source review. It already has native ocean waves, optical depth/refraction, an environment reflection fallback, bounded screen-space rays and separate finite hydraulic dynamics. It does not reproduce Unreal's complete pipeline.

Implementation status after the direct-source pass:

1. Implemented: separate absorption/scattering coefficients in inverse metres, total extinction, scattering albedo/integration, and light attenuation on the path to the submerged surface. Native CPU reference equations have energy and wavelength-order regression checks.
2. Implemented: depth-aware four-neighbour reconstruction for refraction, with foreground sample rejection and fallback to the undistorted background.
3. Implemented: native WaterDepthField3D rasterizes the actual static landscape into a shared CPU/GPU bathymetry field. Gerstner amplitudes attenuate over shallow water using the same field in GPU rendering and CPU queries.
4. Pending: a separate lower-resolution reflection buffer with temporal reconstruction/denoising. Current reflections remain bounded screen-space rays with an environment fallback.

These are independent ByteEngine implementations of standard rendering/physical principles, not a port of Unreal's render graph or proprietary shader source. No Unreal source was copied into the ByteEngine repository.
