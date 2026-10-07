# Graphics quality: simple setup, bounded costs

## Quick workflow

1. Select the scene's **Sky Environment** (or add one).
2. Choose **Lighting Preset**: Daylight, Overcast, Interior or Night. In the editor this updates the first enabled directional light, or creates Sunlight. It preserves your assigned HDRI. Interior is a starting exposure/ambient setup, not automatic room lighting: add local lights where needed.
3. Choose **Graphics Quality → Balanced**. This enables restrained contact shading and bloom. Fast removes both effects and lowers shadow limits; High increases contact samples and allows more shadow detail.
4. For reflective materials, enable environment lighting and assign an appropriate HDRI. Correct texture maps and UVs matter more than post effects.
5. On a Model component, use **Check Model Graphics**, then **Copy Report** if something looks wrong. The Material editor also shows relevant warnings. These checks never rewrite the model or material.

## Quality budgets

| Setting | Fast | Balanced | High |
|---|---|---|---|
| Contact shading | Off | Half resolution, 8 samples | Half resolution, 12 samples |
| Contact strength | 0 | 0.30 | 0.45 |
| Bloom | Off | Quarter resolution, 0.08 | Quarter resolution, 0.08 |
| Extra fullscreen passes | 0 | 5 | 5 |
| Directional shadow resolution cap | 1024 | 2048 | 4096 |
| Point shadow lights | 0 | At most 1 | At most 2 |

Caps do not increase an authored light's lower shadow resolution. Point lights still illuminate when their shadows are disabled. Smooth Edges uses the existing spatial anti-aliasing; it is not temporal AA. Targets are reused and resized only when dimensions change; disabled effects release their extra targets. No effect simulation or scene asset scan is added to gameplay updates.

Old scenes keep neutral Custom settings: no AO/bloom, neutral colour controls, existing lighting. Selecting a preset is explicit. Subsequent individual adjustments remain adjustable and survive save/load; selecting a preset again resets its relevant settings. Custom keeps the current effect controls but restores the normal shadow caps.

## Controls and material checks

- Contact Shading strengthens visible creases/contacts using depth. Radius is measured in world units. Keep it subtle: this screen-space artistic pass modulates the composited scene, not just physically isolated indirect light. It cannot see off-screen geometry and is not global illumination or a contact-shadow ray tracer. Transparent objects that do not write depth are not reliable occluders.
- Bloom spreads highlights above Bloom Threshold. Use HDR emission or bright lights, not a large bloom value to compensate for weak lighting.
- Saturation, Contrast and Warmth are neutral at 1, 1 and 0. Exposure and ACES tone mapping remain in the existing HDR pipeline.
- Albedo/emission images decode from sRGB; normal/PBR maps remain linear. Packed maps require the correct channels. If bumps are reversed, choose OpenGL/DirectX normal convention. Degenerate UVs now safely skip the normal map rather than produce an invalid tangent frame; mirrored UVs preserve handedness.
- Model audit reports non-finite vertices, missing normals, constant UVs, missing imported material links and unresolved external texture files. Constant UVs may be intentional for palette-based art. Imported materials and overrides should both be inspected.

## Measure, do not assume FPS

Enable **Show Advanced → Measure Post Effects GPU** on Sky Environment, then read **Performance → Graphics / Last View**. GPU queries are asynchronous; normal rendering never waits for completion. Timing includes postprocessing/presentation only, not geometry, animation or shadow rendering, and reports the most recently rendered view (including previews). Disable measurement when finished.

Focused tests: `ByteEngine.Tests.exe --graphics` and `--graphics-render`. The latter uses an invisible GL window, validates output pixels and prints synthetic 1080p postprocess timings. These are not a real-game benchmark or an FPS promise; profile your actual scene and target GPU.

## Platform scope

The new depth-based contact shading, HDR bloom and grading run in the Windows editor and Windows player. The current WebGL browser renderer has its own simpler lighting/material pipeline and does **not** yet render these native post effects. Preset properties still persist in project files, but do not assume Windows/browser visual parity. No browser postprocessing is silently claimed.
