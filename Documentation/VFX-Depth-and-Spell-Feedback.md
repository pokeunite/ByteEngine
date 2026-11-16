# Mesh VFX and spell feedback

The Firebolt presets demonstrate layered cast, flight and impact effects. In a VFX asset, select Mesh render mode for primitive geometry with editable scale, deformation, pulse, emission and surface flow. Surface flow animates the mesh colour and brightness; it is a stylized surface treatment, not volumetric fire. SurfacePlane lays a particle across the emitter X/Z plane, useful for impact rings.

Enable the effect light to illuminate nearby surfaces. Its colour, intensity, range and lifetime curve are editable. At most four unshadowed effect lights are selected per view, sharing the existing eight point-light slots. Mesh layers are capped at 64 particles; use small counts for cores and sprites/ribbons for trails.

Assign Release Effect and Release Size on Projectile Launcher for successful-shot feedback. Assign Impact Effect and Impact Size on the projectile Blueprint for hit feedback aligned to the surface normal. Let Effects Fade On Hit detaches child VFX and stops emission so existing trail particles can finish after the projectile is destroyed. These settings do not alter damage, collision or cooldowns.

One-shot Sound and Sound Volume play through spatial audio on pooled release/impact effects. The pool retains an effect until its sound finishes. The public VfxRuntime.Spawn helper uses the same bounded pool. Author audio in the project; no external service is required.

The VFX workspace reference floor makes depth and light spill easier to inspect. Check spells head-on, side-on, moving and against surfaces in the game as well. A glowing mesh alone does not replace layered motion, audio or good timing.

Monsters & Magic uses editable Assets/VFX/Firebolt presets and Assets/Attacks/Firebolt/Firebolt.byteblueprint on both muzzle launchers. Original Anime/Shooting assets remain available. Its FIREBOLT_VFX_GUIDE.md documents editing and the scene backup. Windows editor/player runtimes were refreshed; browser publication and game export are separate steps. This change does not implement soft-particle depth fading, decals, imported-mesh emitters or a volumetric renderer.
