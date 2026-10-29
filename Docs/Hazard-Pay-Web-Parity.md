# Hazard Pay: browser asset and runtime repairs

The mining project remains `C:/Users/codex/Documents/palclone/palclone.byteproject`.
Its display name and main-menu title are Hazard Pay; its project GUID and source saves are preserved.
The canonical gameplay plugin is KarlsonDemo (Modular Weapon Gameplay), version 3.47.0.

## Confirmed failures

- The floor material arrived in WASM with near-zero corrupted colour channels. Generated typed JSON setters now handle material parameters and imported model data, avoiding reflective SIMD property invocation.
- The Synty character had 71.67 MiB of indented animation metadata. The portable reader rejected metadata above 64 MiB, and AnimationController silently discarded the load failure. Metadata is now compact (22.13 MiB for this character), the bounded reader accepts up to 256 MiB, and required animation-model failures are surfaced. Every cooked model is read back by the portable loader during export, before a distributable ZIP is completed.
- Browser HUD outlines expanded into thousands of small quads. A binary line command now draws each line as one Canvas stroke, matching the native straight-line primitive. The tested idle HUD fell from 5,661 commands to 217.
- The frame encoder could force a full collection every frame above a memory threshold. That forced collection and unconditional per-frame upload logging were removed; normal managed collection and bounded resource caches remain.

## Further CPU reductions

Portable frames reuse RenderWorld storage and the browser's batch builder. Material state hashes are cached only within one batching pass and reset before the next pass, preserving live material edits. Scene GUID lookups use an invalidated identity index. Machine proximity queries traverse that machine's hierarchy rather than scanning the complete level. Validated contract definitions are parsed again only when their authored JSON changes; hidden contract menus no longer request previews every frame.

## Validation and repeatable checks

- `Tests/ByteEngine.GltfSkin.Tests`: model matrices, inverse bind data, animation keys, colour and signed UV tiling round trips.
- `Tests/BrowserRuntime/FrameWire.test.mjs`: binary UI, material state reuse and a single Canvas stroke per line.
- `Tests/ByteEngine.Browser.Tests`: shared portable lifecycle, input, drawing and GUID index add/remove invalidation.
- `Tests/ByteEngine.WeaponEvents.Tests`: 522 gameplay checks, including invalid/edited contract definitions.
- `Tests/ByteEngine.Tests --roadmap-render`: real GPU material-state, transparency, shadow, text, occlusion and instancing checks, including mutation between reusable batching passes.
- `Tools/TestMiningCombatBrowser.cjs <exported-site> <results>`: a fresh browser context with the complete real level, new-game flow, contract selection and 32 seconds of simulated spider combat. Checks actor presence, finite animated bounds, low HUD command count and runtime errors. Saves screenshots and frame/animation diagnostics. It uses no user browser profile or existing saves.
- `Tools/TestFullMiningBrowser.cjs <exported-site> <results>`: real-time browser presentation measurements after startup. This complements deterministic simulation; it is not interchangeable with the small linked-plugin smoke test.
- `Tests/BrowserRuntime/PublishedRuntime.test.mjs <runtime>`: all boot resource SHA-256 hashes and obsolete framework-file rejection.

Tests used the laptop's AMD RX Vega 10 browser graphics backend, not software-rendered SwiftShader. Asset correctness and timing are separate checks. WebGL and native OpenGL still have different renderer costs; a functional test is not evidence of desktop-speed performance. Record real-time measurements after compilation and texture uploads have finished.

## Installation/export discipline

Refresh desktop and statically linked browser runtimes when changing gameplay code. Run publishers sequentially, or finish with `Tools/MiningParity/InstallRuntime.ps1` after both have completed: publishing the editor distribution can replace its BrowserRuntime directory. Keep project Plugins, PluginLibrary and PluginSource synchronized. Plugin fingerprints must match before export.

Do not distribute a build merely because the linked-plugin fixture passes. Test the complete exported level, visible player, ground material, animated enemies and menus. The new export read-back check catches portable asset size/format failures but does not replace browser gameplay testing.

The itch.io page text was renamed and rewritten for the mining defence game. The user chose to keep the tested ZIP locally rather than enable the Chrome extension's file-upload permission, so no replacement web build is uploaded in this pass. Existing page visibility and URL are preserved.
