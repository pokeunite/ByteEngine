# Fullscreen company hub — 2026-11-11

Maintained source verified via C:/Users/codex/ByteEngine/.byteengine-build-source.json: C:/Users/codex/.codex/worktrees/52af/ByteEngine. Active game C:/Users/codex/Documents/palclone. Plugin3.93.0.

Company/Trade/Market/Research/Account share a fullscreen dark shell with20px horizontal and12px vertical safe margins at1280x720. Existing typography/icons/yellow active states retained. Trade landing uses side-by-side delivery/exchange cards and three business-summary cards. Market and Account retain functional controls on left and gain a freight/storage/shipment summary on right. Research has a substantially larger graph and dedicated right-hand effect/rank/prerequisite/price/purchase panel; account controls only in Account tab. Detail text wraps/scrolls independently; graph keeps pan/zoom and gains Centre Map action. Fullscreen combat/preparation status in header. Floating field shipment card suppressed inside the fullscreen hub to avoid overlays.

No gameplay economy, combat balancing, drone logic, research prerequisites, save schema or simulation pause changes. Current combat restrictions on permanent research remain. No source-art generation needed: uses existing shared assets.

Changed Compact/CompanySystems/Map/ResearchDetails/FieldStatus partials, new FullscreenCompany partial, plugin version; updated company tests to current fullscreen hit areas.40 company interaction checks passed (research purchase, map pan/zoom, delivery/live buying/selling, account allocation/storage, close/cursor, combat payout). No save/scene authoring changes. Build logs artifacts/fullscreen-hub-build.log and fullscreen-hub-tests-build.log. Native runner isolated under artifacts/fullscreen-hub-harness because the normal harness DLL was in use; copied existing engine dependencies, did not terminate another process.

Desktop package and editable project source synchronized with SyncJamPolish.py, SHA2565B295E35D952AEFD25D1A44AA956AE48CD9FDDD03759EFEEFEA2B57E753373F8. No browser refresh, export or push. Existing exported games need a fresh export to receive the plugin.

Visual QA: native1280x720 Research/Trade/Exchange screenshots inspected, clear/readable, no panel overlap. artifacts/fullscreen-hub-research.png, fullscreen-hub-trade.png, fullscreen-hub-market.png. Account and1080p Research final checks in corresponding fullscreen-hub logs.
Final Account screenshot inspected: fullscreen-hub-account.png; allocation slider/cargo action readable with no overlap. Larger research capture inspected: mining-research-ui-1080.png actual raster1920x1050 (window client area), not an exact1080p raster. All previews exited0.40 company assertions passed. No balance changes.
