# Hazard Pay Company Systems UI — 2026-11-10

Maintained source: C:/Users/codex/.codex/worktrees/52af/ByteEngine (installed routing verified).
Game: C:/Users/codex/Documents/palclone. Windows plugin 3.85.0.
Reference: Docs/Concepts/HazardPay-UI-Overhaul/02-Company-Systems.png and supplied Downloads/company systens.png; preserved in project SourceArt/Company Systems/Reference.png.

## Completed
Company terminal now opens a Trade landing page with Delivery contracts and Live exchange cards. Trade/Research/Account navigation matches the reference. Company screens use charcoal clipped-corner cards, cream borders/type, yellow selections/actions and the existing Inter font assets.
Live exchange has Refined/Raw, Buy/Sell, a real price-history chart with fitted axes and dashed contract reference, inventory/quantity slider, commission label, actual rounded purchase cost/sale payout, locked shipment payout and receipt split. Buy preview uses the same ceiling rule as PurchaseOre; sale previews preserve SaleTotal's floor rule.
Delivery contracts now select for preview and require explicit Accept before delivery. Acceptance snapshots the existing authored order; existing capacity, freight, payout and shipping rules remain. Current contracts have no deadlines, so the UI states No deadline instead of inventing timers.
Research retains authored catalog, progression, purchase requirements, expanding graph, zoom/pan, rank state, asset credits and scrollable detail text. Its account sidebar shows real target progress/days, deposit split, example and cargo upgrade. Account tab offers the same real controls at full width. RMB pan now uses the UI pointer state so modal pointer ownership cannot block it.
Created six original SVG pictograms and transparent PNGs: delivery-truck, exchange-ingots, camp, company-bank, scatter, lock. Runtime names are company-*.png under Assets/UI/Mining/FieldHud; editable sources under SourceArt/Company Systems. Existing ammo/gear/clock/research icons reused. Artwork was made as repo-native vectors and rasterized with the existing tool, not generated through an image API.

## Source and verification
Main new renderer/input/layout: Plugins/KarlsonDemo/Mining/MiningBusinessUi3D.CompanySystems.cs. Updated Compact, CompanyAccount, Industrial painter, Map, ResearchDetails, Contracts, Market, main company-open routing and plugin version. Preserved unrelated existing changes.
Reusable artwork tools: Tools/MiningPrototype/BuildCompanyArtwork.py, InstallCompanyArtwork.py, RasterizeFieldHud.ps1 -Folder artifacts/company-kit.
Check: Tools/MiningPrototype/bin/Release/net9.0-windows/win-x64/MiningPrototype.exe --verify-company-systems.
33 PASS checks cover landing/cards/tabs, select-vs-accept, delivery debit/payout and repeat guard, Raw/Refined, idempotent Buy/Sell selection, displayed buy total equals real credit debit, actual inventory, graph zoom/RMB pan, research purchase, both deposit sliders, cargo purchase, close/pointer restore and unchanged scene. Harness disables persistence; no player save edits.
Build logs: artifacts/company-systems-build.log, company-systems-preview-build.log. Both succeeded; existing nullable and unavailable vulnerability-feed warnings only.
Interaction log: artifacts/company-systems-interaction.log. Actual native screenshots reviewed for Company, available/accepted delivery, Research/account, standalone Account, Buy at1280x720 and Sell at1920x1080. Previews copied under Docs/Concepts/HazardPay-UI-Overhaul/Company-Systems. Camera framing changes are preview-only; game scene/camera assets unchanged.
A duplicated preview-only Buy toggle was removed from Program.cs; final Buy capture verified its Buy fee, selected Buy segment, buy quantity and You pay action.

## Installed identity and scope
Final DLL SHA256: E927B1DEC1B7AE4315DF37DCFE473F663611909EA164D2FC8EDAE308B1437A1C.
Final package SHA256: BAE1DD5C5B127EA77E20F12332022EA22FF22D269BDF17311552AB5A424BC7ED.
SyncJamPolish.py synchronized canonical/installed PluginLibrary, Dist and Debug/Release editor libraries, palclone Plugins/PluginLibrary and editable PluginSource. Project package DLL matches tested Release assembly. This task changes only Hazard Pay assets/source; MULTIPLAYER TEST package remains untouched.
No game export, browser runtime rebuild, deployment, save edits or git push. Web UI remains at its previous statically linked plugin until an explicitly requested browser refresh. Restart a running editor/game to load the updated Windows package.
