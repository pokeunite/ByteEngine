# Active context

## 2026-11-16 — MCP focused context queries

- Tools/ByteEngine.Mcp adds catalog/help (overview/scenes/gameplay/debugging), scene/find_objects (name/component filters, stable ID pagination), asset/find pagination and project/batch_read (1–16 allowlisted reads, per-request errors).
- Release MCP build passed; full --self-test and stdio client help passed. Evidence: .artifacts/mcp-efficiency-build.log, mcp-efficiency-self-test.log, mcp-efficiency-protocol.json.
- Synthetic 160-object inspection: 15768 UTF-8 bytes; one camera match: 173 bytes. Payload comparison only; no billing/token or gameplay performance claim.
- Activation pending: three running published MCP processes retain the old binary. Publish with Tools/PublishMcp.ps1 after disconnecting them, then reconnect. No export, browser refresh or whole-editor publish performed; unrelated engine/plugin edits remain in the checkout.


## 2026-11-16 — Event authoring controls

Condition/action popup search is restored through the existing shared search helper. Built-in mouse Held/Pressed/Released arguments use the enum dropdown before generic registry metadata rendering. Release build and --event-authoring-controls pass with real ImGui popup checks. Windows distribution refresh succeeded and -VerifyOnly confirmed matching editor/player runtimes. Installed editor DLL matches the Release build. Evidence is .artifacts/event-authoring-fix/. Preserve unrelated existing working-tree changes.

Updated: 2026-11-14 — consolidated engine and Hazard Pay repaired

- Maintained source: `C:/Users/codex/ByteEngine`, branch `main`. Never publish from the retired 52af integration workspace or recreate ByteEngine-Palclone.
- Active game: `C:/Users/codex/Documents/palclone/palclone.byteproject` (Hazard Pay). Engine 0.10.0; canonical plugin `Plugins/KarlsonDemo` v3.94.0.
- Main engine/export/editor/network updates and the Hazard Pay gameplay history are integrated. Project Plugins, PluginLibrary and PluginSource match canonical source/package.
- Run `Rebuild-ByteEngine-And-Refresh-Dist.ps1` from canonical root for Windows. `-VerifyOnly` checks editor/loose/bundled Core identity. MSBuild workers must not be reused across restricted sessions/checkouts.
- Windows-only workflow: old browser runtimes archived; explicit `-BrowserOnly` refresh required before web export. No game exports or Git push performed in this session.
- Evidence/recovery: `C:/Users/codex/Documents/testing/ByteEngine-Recovery-20261114`. Includes complete Git branch archive, raw checks and old installs/backups. Do not delete authored assets or saves.
- Verification: solution build 0 errors; default/engine-optimization/physics/integration/web CPU tests; MCP self-test/missing-component regression; native Hazard Pay field/company checks; installed editor normal open/close; player fullscreen/windowed/borderless; runtime identity match; project validation 4 scenes/8 blueprints/14 event modules/3890 startup objects/no missing components.
- Known non-startup warnings: unavailable models in old tree-study scenes, retained for user recovery. No new FPS claims.
- Full report: `Documentation/Hazard-Pay-Engine-Repair.html`.
