# Built-in scene shaders

Select the scene's **Sky Environment** and open **Built-in Shader**.
Set **Scene Shader** to **Pixel Art**. The default is Standard, preserving
existing scenes. This is a scene-wide 3D presentation effect, not a material
shader or an automatic hand-painted sprite generator.

- **Pixel Size:** square blocks in output pixels (1–32). Start at 3 or 4.
- **Colour Levels:** levels per RGB channel (2–32). Start at 8. This is colour
  quantization, not a fixed indexed palette.
- **Ordered Dithering:** pattern strength (0–1). Start at 0.35 or use 0 for
  clean bands.

Pixel Art samples one source texel per block after scene composition, applies
tone mapping/colour controls, then quantizes. Spatial edge smoothing and the
normal fine-grained display dither are bypassed while this style is active.
UI is composed afterwards and stays sharp. Render Scale changes the 3D source
resolution; Pixel Size remains anchored to output pixels. This final-pass
effect does not reduce geometry or shading cost by itself.

Event Sheet → **Graphics / Built-in Shaders** provides **Set Pixel Art Shader**
(enable, pixel size, colour levels, dithering) and **Pixel Art Shader Is Enabled**.
It uses the selected object's Sky Environment, or the scene's first active one.
The environment must already exist; actions do not silently create it.

Desktop and WebGL have the same pixel-grid and ordered-dither algorithm, on top
of their existing presentation pipelines. Continuous camera motion can still
shimmer as geometry crosses screen pixels; this effect does not snap cameras
or vertices. UI/text is intentionally excluded.

Verification: `ByteEngine.Tests --pixel-art-shader <separate-project-directory>`
authors a native demo and captures a standard/pixel-art comparison. It checks
GPU block uniformity, colour levels, partial edge blocks, disabled and 2D-copy
identity, serialization, bounded settings and event actions. Use a project
directory under Documents, not inside the engine repository. The demo's 1 and 2
keys switch Standard and Pixel Art through an editable event sheet.
