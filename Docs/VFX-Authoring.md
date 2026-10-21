# Reusable VFX authoring

Create or open a .bvfx asset. The editor combines up to eight editable layers, with live preview, moving-emitter preview and Undo. Template replaces the draft with a complete effect; Add layer adds only a layer. Save the result as a reusable asset. VFX Player runs the effect on a scene object, projectile or socket; event sheets decide when to play, stop, spawn or change appearance.

## Quick workflow

Choose a template, adjust layer colors and size, then tune brightness and timing. The built-in library includes Fireball, WaterBolt, LightningStrike, ClawSlash, HealingPulse, PoisonCloud, IceBurst and Shield, plus the original environmental and impact presets. These are editable starting points, not a guarantee of finished art for every project. They contain no attack damage or combat logic.

VFX Player exposes Size, Tint, Brightness, Intensity (emission density) and Playback Speed. Tint white preserves layer colors. Brightness above 1 produces HDR output; visible bloom requires scene post-processing. Event actions vfx.setTint (RGB plus alpha) and vfx.setBrightness adjust these without rebuilding particle textures. Transient pooled effects reset these values when reused.

## Layers

Sprites: SoftDisc, Spark, Ring, Solid, Smoke, Flame, Star, Arc, Lightning and Ripple. The richer procedural shapes animate over particle lifetime. Optional custom PNG/flipbook textures can replace them. Texture detail is 32, 64, 128 or 256 pixels per frame. A compact shared timeline atlas combines texture animation, color and opacity rather than duplicating every source frame at every age. Each atlas has an 8 MiB RGBA pixel budget and at most 4096 pixels per axis; requested detail is reduced when necessary. GPU/CPU copies and many distinct effects still consume additional memory.

Texture breakup and Dissolve over life use deterministic noise baked into the atlas. Pattern scale changes noise size. Random starting rotation can be disabled for directional slash/beam art. Brightness is independent of particle count.

Render modes: Billboard faces the camera; Stretched follows velocity; Trail/Beam connect positions; Horizontal lies in the world XZ plane for ground markers/ripples; EmitterPlane uses the emitter's local XY orientation. Lifetime curves control size, opacity, speed and color blend. Delays coordinate flashes, sparks, smoke and aftermath. Set LocalSpace for effects that follow their object, and world space for trails/smoke left behind.

## Scope

This version improves sprite-based effects and their composition. It does not implement soft-depth intersection fading, mesh particles, refraction/distortion, volumetric fire/smoke, or automatic Unity asset-package conversion. Detailed art may still require authored textures. Avoid describing these unsupported features as available.

Byte Assistant can use create_vfx with these presets and layer properties, create reusable .bvfx assets and attach VFX Player using native tools. Query byte_catalog kind=asset_types for exact names. Keep combat triggers and damage in named editable event sheets.
