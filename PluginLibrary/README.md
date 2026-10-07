# Saved plugin library

- **GoblinScrapper.byteplugin**: Goblin builder, physics and native event actions; version 0.3.4.
- **SpaceScraper.byteplugin**: starter/example plugin; version 0.1.0.
- **GoblinControls.byteevents**: editable native control sheet. Copy to the project's Assets/Events folder and attach through EventModuleComponent on its builder object.
- **GOBLIN-MANUAL-CONTROLS.md**: controls, event setup and resume instructions.

Import packages through Project Settings > Plugins and reopen the project. Game art/catalogues are separate project assets. Editable plugin source is in ../Plugins. Rebuild the solution to regenerate packages under artifacts/plugins, then refresh this library when publishing a new version.


Latest Goblin Scraper package: 0.3.5. Matched cogs and build performance changes are documented in COGS-PERFORMANCE-035.md. The model revision also needs the matching cog GLBs and catalogue in the game; editable source and export copies are saved in Downloads/parts/refined/matched-cogs-035.

Current package: 0.3.6. See BUILD-BRACES-036.md for floor clearance, camera framing and moving-part brace fixes. No model reimport required.

Current package: 0.4.0. See BATTLE-PROTOTYPE-040.md for the playable red-goblin combat test, rigged assets, ragdolls and dismemberment. Source assets and preparation scripts are in Designs/BattlePrototype.

Current package: 0.4.1. See BLOCK-TUNING-041.md for per-block settings, native tuning events and the land-combat palette.

Current package: 0.4.2. See SCENE-AUTHORING-042.md for the saved editable level/HUD, native toolbar event sheet and restored steering parts.


Current package: 0.4.3. See TUNING-STABILITY-043.md for draggable tuning bars, typed numeric fields, and suspension/brace stability fixes.

Current package: 0.4.4. See PERFORMANCE-044.md for battlefield animation, scene update and native renderer optimizations. This release also requires the rebuilt ByteEngine editor/core.

Current package: 0.4.5. See CRASH-COMPATIBILITY-045.md. Corrects the mismatched-runtime crash, removes game-specific animation throttling, and accompanies general engine allocation fixes. Engine research/benchmarks are in Docs/EnginePerformance.

## Desert Terrain / Dune Company

`DesertTerrain.byteplugin` version 0.2.0 adds 8/16-bit PNG heightmap landscapes, terrain collision, tyre ruts, compaction and persistent track saves. Digging and sculpting are removed. Requires ByteEngine 0.7.1+. Editable project: `C:/Users/codex/Documents/DuneCompany/DuneCompany.byteproject`; controls and heightmap workflow: `C:/Users/codex/Documents/DuneCompany/README.md`. See `DESERT-TERRAIN.md` for integration details.
