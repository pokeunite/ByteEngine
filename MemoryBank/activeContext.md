# Active context

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
