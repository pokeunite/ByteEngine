# Yellow machine menus — installed

The refinery, ammo press, company terminal, market and drill now use the approved machinery-yellow/charcoal design language. The panels, typography, dividers, progress gauges, focus states and purchase buttons are drawn natively, with purpose-built ore, ingot, timer, hopper and eight-legged spider symbols. Existing monochrome research icons are retained. No game logo or slogan is shown.

## Flow

- **Refinery:** live batch progress, remaining processing time, raw/refined totals, and the three actual upgrade values. Refine & Return starts processing and closes the panel; reopening shows the running batch.
- **Ammo press:** live production cycle and bucket capacity, plus speed, hopper and payload upgrades. Ammunition still ejects into the bucket and uses walk-up collection. There is no Collect-menu button.
- **Company terminal:** approaching the tent opens research directly. Upgrades and Market are immediate tabs. The icon map supports panning, zooming and progressively revealed descendants; selecting a tile shows its real description, prerequisites, rank and cost. All 73 authored upgrades remain available through their prerequisites.
- **Market:** raw/refined selectors, live price history, quantity slider and actual shipment progress. The sale button prevents duplicate orders during an active shipment. Closing the panel does not stop the shipment.
- **Drill:** the three real contracts show difficulty, rewards and hunter/raider counts. Output upgrades, drill health, cargo and expected yield are shown before Start Extraction. An unaffordable upgrade or a contract that exceeds cargo capacity is disabled.

Menus retain native UiWidget hitboxes and their existing Event Sheet actions. Layout and drawing use the same coordinates. The gameplay HUD is hidden while a machine panel is open. Escape, the close icon or the footer close target return to player controls without a recapture click.

## Project and installation

Active project: `C:/Users/codex/Documents/palclone/palclone.byteproject`.

Scene backup: `.byteengine/Backups/yellow-machine-menus-20261027-221143/Main.bytescene`.

Canonical plugin: `Plugins/KarlsonDemo`, version **3.44.0**. Desktop editor/player runtime and project plugin packages are synchronized. No game was exported. A later export from the refreshed editor will include the new interfaces.

## Validation

518 regression checks passed. A native 37-check click-through verifies the actual visible upgrade buttons, refining and return flow, progress updates, resource tabs, quantity slider, shipment completion, company-tab navigation, contract selection, extraction startup and restoration of player controls. Scene and campaign saves are preserved by the checks.

In-game previews are stored in `artifacts/mining-machine-menus` and copied to the project's `SourceArt/Implemented Machine Menus` folder.
