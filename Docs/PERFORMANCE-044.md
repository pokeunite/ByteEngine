# Battlefield performance pass — 0.4.4

Installed into Goblin Scraper with the rebuilt ByteEngine editor/core.

Changes:
- Directional shadow rendering skips casters outside the light volume, retaining off-camera casters inside that volume.
- Shader programs retain unchanged uniform values instead of uploading identical camera/material/light settings repeatedly.
- Unchanged skin matrices reuse their vertex buffers. Identical physical bone deformations no longer invalidate a completed pose.
- Distant goblins sample animation at 10 Hz beyond 32 m and 4 Hz beyond 65 m. Animation playback time advances continuously; nearby animation, AI, impact detection and physical reactions retain their normal update rate.
- Single-component objects avoid ordered-enumeration allocations. UI navigation filters for actual buttons before walking visibility hierarchies.

Measurements on AMD RX Vega 10 integrated graphics, synchronized 1280×720 rendering, editor panels excluded:
- Earlier update-only measurements: about 8.8–9.3 ms.
- Optimized update-only measurements: 4.2–4.5 ms.
- Earlier combined driving measurements: about 28–33 FPS, with substantial variability.
- Optimized repeated driving measurements: about 35–40 FPS.
- Optimized build mode: about 43–45 FPS. A comparable baseline build-mode number was not captured.

This is an improvement, not a verified 60 FPS result. Rendering remains the largest cost; the editor can cost more at larger view sizes or with multiple visible viewports.

Validation: native shader cache value/program isolation checks, animation pose/socket regressions, asset import, saved scene reload, deployment, driving out into the battlefield and enemy reset passed. The broader engine suite still fails its character-grounding assertion; the same assertion failed with the original scene update/UI-navigation code restored, confirming this pass did not introduce that failure.

Editable map objects, models, game rules and controls remain available in the saved scene. No model reimport is needed. Restart the editor to use the rebuilt core and plugin. Packages are also saved in PluginLibrary and Downloads/ByteEngine Plugins.
