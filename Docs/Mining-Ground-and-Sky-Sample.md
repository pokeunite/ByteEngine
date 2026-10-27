# Ground and sky art sample

Installed into `C:/Users/codex/Documents/palclone/Scenes/Main.bytescene`.

## Inspect in the editor

The **Orange sand ground sample** is a 12 × 14 metre static surface centred at **(10, 0, -4)**, beside the drill. It has 336 triangles and approximately 8–26 mm of visual relief. The existing floor supplies flat gameplay collision. This sample is for approving colour, shape scale and lighting before replacing/sculpting the whole level.

The current camp floor, object positions and game systems remain in place. Scene backups precede each authoring pass in `.byteengine/Backups/ground-sky-sample-*`.

Project assets:

- `Assets/Environment/MiningSandbox/OrangeSand-BaseColor.png` — generated 1254 × 1254 colour texture.
- `Assets/Environment/MiningSandbox/OrangeSand.bmat` — reusable matte material, metallic 0, roughness .96, no normal map. Edit this in the material editor.
- `Assets/Environment/MiningSandbox/OrangeSand-Sample.glb` — shallow faceted mesh with the texture embedded as a fallback.
- `Assets/Environment/MiningSandbox/RingedPlanet-Sunset.png` — generated 1774 × 887 equirectangular image. This is an LDR sky image, not a captured high-dynamic-range HDRI.

Select **Sky Environment** to change the map, its rotation (90°), environment intensity (.85), exposure (1.05), ambient (.20) and distant fog. The ringed planet faces the front approach direction. Look upward to see its full disc/rings.

The main directional light has warm neutral light, intensity 1.85, 2048 shadows and 55 metre shadow range. **Alien sky soft fill** adds a weaker cool light without another shadow map. These are scene settings, not hidden runtime overrides. The generated texture importers use Linear filtering. Native PNG/JPEG sky rendering now decodes sRGB before exposure/tone mapping, matching the existing IBL path; HDR skies retain their linear path.

The sample does not add a live sand simulation, footprints or dunes. The remaining teal/grey floor and prototype boundaries are intentionally visible for comparison. The full enclosure and terrain are a later pass.

## Asset provenance and generation prompts

Both images were made using the built-in image-generation tool and the user's `ChatGPT Image Oct 26, 2026, 09_25_30 PM.png` as a style reference. Generated image pixels were copied unchanged. `Tools/MiningPrototype/BuildGroundSample.py` creates geometry only.

### Ground prompt

Create a production-ready seamless tileable square BASE COLOUR texture for a stylized low-poly alien desert floor, matching ONLY the sandy orange ground material in the reference image. Strict orthographic directly overhead view, surface fills entire image, perfectly even flat diffuse lighting with NO directional cast shadows, NO perspective, NO horizon, NO objects, NO rocks, NO vegetation, NO text. Broad irregular contiguous flat polygon colour patches, rust orange, terracotta, apricot and muted burnt ochre with restrained contrast. Organic broken polygon mosaic, varied patch sizes and soft sparse wind-smoothed scuffs, matte compacted sand, very little fine grain. NOT realistic photo sand, NOT cracked mud with dark outlines, NOT cobblestones, NO obvious triangular checkerboard, NO high contrast noise. Same premium clean low-poly ground visual grammar as reference. Texture covers approximately 8 metres of terrain. Ensure all four edges repeat seamlessly. Square texture asset, no frame.

The sample uses one texture across its surface; large-area repeated tiling has not yet been approved.

### Sky prompt

Create a production-ready 360-degree EQUIRECTANGULAR latitude-longitude SKY ENVIRONMENT TEXTURE, exactly 2:1 landscape panorama. Match the low-poly stylized alien sunset SKY ONLY in reference. Panorama projected for surrounding a game camera, horizon precisely at middle height y=50%, zenith at top, nadir at bottom, horizontal edges perfectly seamless. SKY ONLY, no landscape, no mountains, no ground objects, no buildings, no terrain silhouette. Upper hemisphere: dusty periwinkle blue-violet zenith transitioning to peach salmon orange near horizon, sparse elongated angular faceted low-poly cloud bands with coral-lit undersides. ONE large lavender ringed planet at longitude centre (x=50%) and elevation about 28 degrees above horizon (y=34%); planet disc spans about 14% panorama width and 22% height with pale warm thin rings angled gently across it and stretching wider; respect spherical equirectangular projection. Small warm sun near horizon at x=66%, y=46%, not blown-out giant sun. Quiet backside twilight gradient at left/right seam, no second planet. Lower hemisphere below horizon is a simple muted rust/dusky violet gradient suitable for ambient environment lighting with NO objects. Premium clean stylized game sky, restrained detail and saturated pastel colour, NOT photoreal, no stars, no text, no UI, no frame. This is a usable wraparound texture map, NOT a normal perspective concept illustration.

### Final sky refinement prompt

Edit this production equirectangular 360 sky texture. Keep the exact 2:1 panorama projection, faceted low-poly cloud style, dusty violet/peach sunset palette, sun position, horizon at middle height and lower hemisphere gradient. Change ONLY the ringed planet: reduce the complete planet AND rings to HALF their current width and height, move their centre to x=50% and y=40% of panorama, so the planet sits lower above the horizon and is much smaller in the actual game camera. Planet disc diameter should be about 7% of total panorama width and 14% of panorama height; rings should extend about 17% of total panorama width. Its angular diameter in a 360 sky should be roughly 25 degrees rather than huge 55 degrees. Seamlessly fill the old planet area with matching empty violet sky and retain surrounding clouds. No second planet, no text, no terrain. Preserve all other sky content. Deliver a clean high-resolution 3840x1920 texture if possible, with smooth gradients and crisp stylized cloud edges. This is a wraparound texture map, not a perspective scene.

The selected refined output is 1774 × 887; the tool did not return the larger requested resolution. The unused initial large-planet map is retained alongside the source art for comparison.

## Verification

The native renderer loads the ground texture and static model, renders the sky with filtering, and captures both at 1920 × 1080. The sample and a third-person view are inspected visually. No game export is made.
