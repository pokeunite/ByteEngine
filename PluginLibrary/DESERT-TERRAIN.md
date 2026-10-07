# Desert Terrain 0.2.0 - Heightmaps

Requires ByteEngine 0.7.1 or newer. Import DesertTerrain.byteplugin and reopen the project. Add Desert Terrain 3D, assign a project-relative Heightmap Path and tune Heightmap Height, Base Height, Cells and Spacing.

Non-interlaced 8/16-bit grayscale/RGB PNG heightmaps are loaded on the CPU without gamma conversion; RGB uses red. 16-bit precision is preserved. Images are bilinearly resampled into the shared render/collision heightfield. PNG dimensions are limited to 4096 x 4096; indexed/interlaced PNGs and RAW/TIFF/JPEG are unsupported.

Desert Terrain now has wheel ruts and compaction only. Digging, terrain raising, brush controls and sculpting events are removed. Maximum Rut Depth defaults to 0.22 metres. Event Sheet actions reload terrain, reset tracks and save/load track state. Attach Sand Contact 3D to true grounded tyre contacts and consume TryGetSand/StampTrack in a vehicle solver.

Source changes reload at most once per second during rendering, reset existing ruts and invalidate stale saves using the source fingerprint. Invalid replacement maps report errors while retaining the last good loaded landscape.

Complete editable project: C:/Users/codex/Documents/DuneCompany/DuneCompany.byteproject. See its README for controls, supported input, performance measurements and limitations.

Engine heightfield support remains general; the PNG loading, sand behavior and demo live in this plugin. This version adds no game-specific engine changes. The implementation uses original code, not proprietary game code.

PNG byte order, filtering and integrity handling follow the [W3C PNG specification](https://www.w3.org/TR/png/).

Heightmap/rut CPU regressions and native import/render/drive/save checks passed. This does not claim that the full historical engine test suite passed.
