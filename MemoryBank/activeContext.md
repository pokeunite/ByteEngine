# Active context

## 2026-11-16 — Recorder quality presets and full-resolution Game View capture

Canonical source/source-routing verified at C:/Users/codex/ByteEngine. Recorder settings in both editor and Windows player now persist Low (720p/4 Mbps), Standard (1080p/12 Mbps), High (1080p/25 Mbps, default) and Ultra (1080p/50 Mbps). Missing/invalid quality values migrate to High; folder/audio options remain intact. Profiles are snapshotted per session. Native H.264 encoder parameters explicitly set CBR and mean bitrate via SetInputMediaType, in addition to the output media type bitrate. Settings are disabled until recording/saving stops.

Root quality issue also included the editor's normal 720p/small-panel render target. While recording, Game View now renders at project output resolution fitted to the chosen preset, independently of panel size; the preview fits that aspect ratio and input/overlays use the fitted image bounds. Normal preview behavior returns afterward. Player capture follows window resolution, capped by the same preset. Sources are never artificially upscaled.

Release tests/player builds pass. --video-recorder passes saved presets, legacy/invalid preference handling, actual H.264 encoding at every preset, complete decode and quality comparison, existing audio/sync/resize/error/shutdown checks, and native Game View recording at 1920x1080 (High) / 1280x720 (Low) inside a 700x500 panel. A 640x360, 60-frame moving grayscale detail fixture produced decoded MSE Low=3798.378736472801, High=46.807123046875 (19.09 dB PSNR gain). Synthetic encoding-quality evidence only, not gameplay/FPS measurements. The initial eight-frame bitrate-only comparison showed no difference; explicit encoder controls and a longer fixture validate the final change. Editor and player quality selectors visually inspected. Logs, comparison, four clips and screenshots preserved in .artifacts/video-quality/.

Windows editor/player refreshed through Tools/PublishEditor.ps1 -Configuration Release -SkipBrowser; -VerifyOnly confirms paired editor/loose/bundled Core identity, and SHA256 checks match installed editor to Release source output and editor/player Core files. Evidence: refresh.log, verify.log and identity.json in .artifacts/video-quality/. Preserved pre-existing Wildlings edits and plugin_build logs; the focused existing publish script avoided building that unfinished plugin. No browser rebuild or game export.


## 2026-11-16 — Built-in Windows video/audio recorder

Maintained source C:/Users/codex/ByteEngine on main; source routing verified. F9 toggles recording and Shift+F9 opens settings in the editor and Windows player. Editor Recorder menu/panel controls Game View capture; selecting Start opens Game View and hiding it stops recording. Windows player records the final game window before its recording-status overlay. Future Windows exports inherit it from the player runtime; no game was exported.

VideoRecorder/VideoFrameCapture use a fixed even-sized LDR capture target up to 1920x1080, 30 fps capture scheduling, pooled BGRA frames, three queued frames and a dedicated encoding thread. Resize letterboxes into the original dimensions; GL framebuffer, read/draw buffer, pixel-pack and scissor state is restored. Held images advance while rendering pauses so video/audio streams keep progressing. Stop and normal shutdown drain/finalize the MP4; encoder/filesystem errors surface as visible status. Encoder uses native Windows Media Foundation H.264/AAC, no external executable/package. Windows N may require the Media Feature Pack.

Audio is enabled by default: WASAPI loopback of the default Windows playback device at recording start (48 kHz stereo AAC, 192 kb/s). Includes other apps' playback, no microphone. Silence fills endpoint gaps. An observed virtual-device QPC timestamp about 450 ms in the future is calibrated against first packet receipt to prevent dropping all playback samples; valid device timestamps retain their original clock. Audio can be disabled in settings. Default output follows the Windows Videos known folder; validated folder/audio preferences live in LocalApplicationData/ByteEngine/Recorder/settings.json, outside projects/exports.

Release tests build and --video-recorder, --ui-scene-editing, --engine-foundations pass. Recorder suite encodes and decodes real H.264/AAC via Windows APIs, checks colour/orientation, monotonic video timestamps, captured quiet tone, A/V duration within 200 ms including AAC padding, an 800 ms render pause, resize letterboxing, window back-buffer/video-only capture, pixel-pack restoration, cancel/restart/shutdown saving, bad-folder errors and preference persistence. Editor settings GL image and Windows player dialog bitmap visually inspected. Raw logs, MP4s, quiet tone and screenshots: .artifacts/video-recorder/. No FPS improvement claim; capture has render/readback overhead.

Windows editor/player refreshed successfully through Tools/PublishEditor.ps1 -Configuration Release -SkipBrowser. -VerifyOnly confirms editor/loose/bundled Core pairing; SHA256 checks confirm installed editor DLL matches the Release source output and editor/player Core files match. Evidence: refresh.log, verify.log and identity.json in .artifacts/video-recorder/. Preserved pre-existing Plugins/Wildlings edits/new hub ambience source and plugin_build logs. Full updater would build that unfinished plugin, so the existing focused publish script was used as in the preceding UI update. Browser not rebuilt. No game export.


## 2026-11-16 — Hand-emitter projectile ownership

Canonical source C:/Users/codex/ByteEngine. ProjectileLauncher3D now attributes a child muzzle's shot to its nearest HealthComponent/CharacterController3D ancestor (standalone emitters retain their own identity). GameplayQuery3D excludes child colliders of the secondary ignored owner as well as the owner itself. This prevents the moving Monsters & Magic player and hand hitboxes from destroying their own projectiles.

Evidence in C:/Users/codex/Documents/monsters and magic/tools: projectile-owner-check.log and hud-final-runtime-check.log cover spawn inside the owner capsule, a child hitbox, player speeds 0/10/16, unchanged owner HP and exact enemy damage; existing navigation/animation checks still pass. Native OpenGL renders of the game HUD at 1280x720, 1920x1080, 1280x800 and 1920x810, plus low HP/mana, pass bounds and GL checks (game-hud-render-final.log, hud-qa/). Project assets/events implement the supplied-font HUD; game-specific changes remain outside the engine repository. Tools/PublishEditor.ps1 -Configuration Release -SkipBrowser completed; VerifyOnly confirms paired editor/player runtime. No browser rebuild, game export, or push. Preserve unrelated Wildlings changes.

## 2026-11-16 — Navigation object pickers and Monsters & Magic repair

Canonical source verified at C:/Users/codex/ByteEngine. NavigationRegion3D.SurfaceObject now has a MeshCollider3D-filtered SceneObjectReference picker and a tooltip explaining that Grid mode needs no surface. NavigationAgent3D.RegionObject now filters its picker to navigation regions. Uses the existing shared Inspector picker; no GUID/serialization changes.

Monsters & Magic game assets remain in C:/Users/codex/Documents/monsters and magic. Repaired missing agents, region origin, supported capsule silhouettes, arms-only casting profile and shared cooldown-gated animation/projectile rules through project-local tools and ByteEngine MCP. Project harness tools/CombatCheck passes actual 1880-cell bake, all six agent paths, real-model repeated casts while moving/standing, exact projectile count and blocked cooldown repeats. Raw evidence: tools/combat-runtime-check.log and tools/combat-repair.log in the game project. Windows editor/player refreshed through Tools/PublishEditor.ps1 -Configuration Release -SkipBrowser; no browser rebuild, export or Git push. Preserve unrelated Wildlings work.

## 2026-11-16 — Scene UI selection and hierarchy icons

Canonical source: C:/Users/codex/ByteEngine, verified against .byteengine-build-source.json. Scene View now has an Edit UI toggle and shares a UiSelectionTool with UI Authoring. Text/widgets use actual canvas/text layout and scroll clipping, topmost picking, selected/hover outlines, Alt-click overlap cycling, Ctrl multi-selection, scale-aware offset dragging, Escape cancellation and one object-delta undo transaction. Selected widget ancestors move children once. World transforms and anchors are preserved. UiContainer-managed widget placement remains container-controlled and is explained by a hover tooltip; move the container or change its layout settings instead.

Hierarchy uses native vector type icons (mesh, sprite, canvas, text, panels, images, buttons, sliders, progress, camera, light, audio and generic objects) and expands the selected object's ancestors when selection changes. No generated raster icon dependencies. UiText shares layout calculation between runtime rendering and editor hit bounds.

Validation: Release tests build passes; --ui-scene-editing, --ui-text and --engine-foundations pass. Native ImGui mouse checks cover text picking, dragging and overlap cycling. Bounds, canvas scaling, hidden ancestors, scroll clipping, parent/child selection, undo/redo and cancel checks pass. Actual GL preview and hierarchy screenshot visually inspected at .artifacts/ui-scene-editing/selection-and-icons.png; raw logs in the same folder. Existing warnings include unavailable NuGet vulnerability metadata. Windows distribution refreshed successfully with Tools/PublishEditor.ps1 -Configuration Release -SkipBrowser; the standard full updater was deliberately stopped before distribution publishing when concurrent Wildlings source edits appeared. Only its three generated package changes were reverted, preserving the unfinished plugin work. -VerifyOnly confirms matching editor/player Core and bundled player identity, and installed editor DLL matches the Release source output. Evidence: editor-refresh.log, verify.log and identity.json in .artifacts/ui-scene-editing/. No browser rebuild or game export.


## 2026-11-16 — Wildlings, UI artwork and plugin packages

Wildlings gameplay is contained in Plugins/Wildlings, plugin version 0.4.4. Release build passes; PluginLibrary/Wildlings.byteplugin matches DLL SHA256 7e407eee66ab01db724b22f332ab8c9b5c57b01846175ff36b5111caf75181dc. This task verifies compilation and package identity; gameplay playthrough is unverified.

UiWidget normal/hover/pressed artwork, Inspector fields and serialization are committed with EngineFoundationTests coverage. Release test build and --engine-foundations pass state precedence, reference roundtrip and legacy fallback. All ten requested PluginLibrary package archives were audited. Evidence .artifacts/git-gameplay-update/.

Windows distribution refresh stopped because ByteEngine is open. Source build and installed Core hashes differ. Close/save the editor before running Rebuild-ByteEngine-And-Refresh-Dist.ps1 again. Git publishing is authorized by the user.

## 2026-11-16 — MCP focused context queries

- Tools/ByteEngine.Mcp adds catalog/help (overview/scenes/gameplay/debugging), scene/find_objects (name/component filters, stable ID pagination), asset/find pagination and project/batch_read (1–16 allowlisted reads, per-request errors).
- Release MCP build passed; full --self-test and stdio client help passed. Evidence: .artifacts/mcp-efficiency-build.log, mcp-efficiency-self-test.log, mcp-efficiency-protocol.json.
- Synthetic 160-object inspection: 15768 UTF-8 bytes; one camera match: 173 bytes. Payload comparison only; no billing/token or gameplay performance claim.
- Activation pending: three running published MCP processes retain the old binary. Publish with Tools/PublishMcp.ps1 after disconnecting them, then reconnect. No export, browser refresh or whole-editor publish performed; unrelated engine/plugin edits remain in the checkout.


## 2026-11-16 — Event authoring controls

Condition/action popup search is restored through the existing shared search helper. Built-in mouse Held/Pressed/Released arguments use the enum dropdown before generic registry metadata rendering. Release build and --event-authoring-controls pass with real ImGui popup checks. Windows distribution refresh succeeded and -VerifyOnly confirmed matching editor/player runtimes. Installed editor DLL matches the Release build. Evidence is .artifacts/event-authoring-fix/. Preserve unrelated existing working-tree changes.

Updated: 2026-11-14 — Wildlings plugin startup repair

- Maintained source: `C:/Users/codex/ByteEngine`, branch `main`. Never publish from the retired 52af integration workspace or recreate ByteEngine-Palclone.
- Active game: `C:/Users/codex/Documents/palclone/palclone.byteproject` (Hazard Pay). Engine 0.10.0; canonical plugin `Plugins/KarlsonDemo` v3.94.0.
- Main engine/export/editor/network updates and the Hazard Pay gameplay history are integrated. Project Plugins, PluginLibrary and PluginSource match canonical source/package.
- Run `Rebuild-ByteEngine-And-Refresh-Dist.ps1` from canonical root for Windows. `-VerifyOnly` checks editor/loose/bundled Core identity. MSBuild workers must not be reused across restricted sessions/checkouts.
- Windows-only workflow: old browser runtimes archived; explicit `-BrowserOnly` refresh required before web export. No game exports or Git push performed in this session.
- Evidence/recovery: `C:/Users/codex/Documents/testing/ByteEngine-Recovery-20261114`. Includes complete Git branch archive, raw checks and old installs/backups. Do not delete authored assets or saves.
- Verification: solution build 0 errors; default/engine-optimization/physics/integration/web CPU tests; MCP self-test/missing-component regression; native Hazard Pay field/company checks; installed editor normal open/close; player fullscreen/windowed/borderless; runtime identity match; project validation 4 scenes/8 blueprints/14 event modules/3890 startup objects/no missing components.
- Known non-startup warnings: unavailable models in old tree-study scenes, retained for user recovery. No new FPS claims.
- Full report: `Documentation/Hazard-Pay-Engine-Repair.html`.

## Wildlings startup repair

- Project: C:/Users/codex/Documents/wildlings; canonical source: Plugins/Wildlings.
- Updated slot-marker Submit call for current API and rebuilt/installed plugin 0.1.1. Engine API and installed runtime unchanged; user explicitly prefers updating the plugin over restoring old methods.
- Release plugin build: 0 errors, one existing SkeletalMeshRenderer nullable warning. JIT prepared all 391 plugin methods against installed Core; editor loaded v0.1.1 and opened project, alive past 20 seconds without logged exceptions. No visual/gameplay verification.
- Raw evidence and previous 0.1.0 package: .artifacts/wildlings-compat/. No export or browser refresh.
