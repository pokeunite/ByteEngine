# Ranked company upgrades, defences and shipments

## Play controls

The company map uses connected icon tiles, rank counters, wheel zoom and right-drag panning. Select a tile to inspect its exact effect, prerequisite and next-rank price. Enter or Buy purchases one rank. Existing company ownership is preserved.

After researching a defence licence, press **B** to enter placement. B cycles available types. Aim at clear ground within eight metres; a green preview means the point is in range. Left click places the defence, right click cancels. Placement spacing and solid-obstacle checks still apply. Placing does not fire or spend a pistol round. The build key and all base damage, interval, range and blast settings are editable on **Defence Placement**.

- **Watchpost:** autonomous aimed sentry, with damage, cycle-speed and piercing research.
- **Rotary emplacement:** faster autonomous gun, with its own damage, cycle-speed and piercing research.
- **Lobber:** visible arcing mortar rounds; salvo count, payload and loading-cycle research.
- **Seismic charges:** proximity-triggered area damage. Research controls available charges, damage and radius. Spent charges are replenished after completing an operation; unexploded placed mines still count against the allowance.
- **Emergency freight:** a delivered supply crate restores up to 24 reserve rounds and 15 health when approached, then is consumed. Its allowance refreshes after completing an operation.

Turret and mortar placement allowances are simultaneous deployed limits. Purchased ranks and placed defences are saved separately. Bypassing deployment limits by reloading is prevented by the saved charge/placement ledger. Defences currently use original prototype geometry; they are functional gameplay units rather than final production models.

## Scope of the upgrade port

There are **63 new renamed nodes / 154 new rank purchases**, plus the ten existing company nodes: **73 nodes / 164 ranks total**. All 29 included effect types have consumers in gameplay. This covers active reference upgrades for the current pistol, ammunition logistics, health, movement, drill protection, market price/throughput/storage, hazard contracts, placed sentries/rotary guns/mines/mortars and supply deliveries.

Handheld SMG, rifle, shotgun, launcher and melee branches are excluded because those would require more player weapons. Aircraft, atomic-bomb and other demo-locked placeholders are excluded. Original names, copied UI artwork and proprietary code are not shipped. Credits replace the reference's additional progression currencies; prices, health bonuses and ammunition increments are adapted for this prototype. The mapping is in `Docs/Reference/Company-Upgrade-Mapping.json` in the engine source.

The authored catalog is `Plugins/KarlsonDemo/Mining/CompanyResearchCatalog.json`. The complete current tree is also serialized in **Mining Trade → Research Definitions Json**, so effects, prices, ranks, prerequisites and positions can be adjusted. Stable IDs are save keys; preserve them when renaming or repositioning an upgrade. Unsupported effect names should not be added without their gameplay implementation.

## Ammunition and icon

The pistol starts with 12 loaded rounds and 36 reserve rounds. Its base reserve limit is **72**, replacing 300. Reserve research expands that earned capacity. Magazine upgrades increase capacity without granting free ammunition. The HUD uses an original white pistol silhouette with a transparent background; it is assigned through the weapon's **HUD Icon** reference.

## Refining and selling

Refining shows a percentage bar on the HUD while the player is back in the world, plus a bar and time remaining when its panel is open. Progress uses the duration captured at batch start, so buying a speed upgrade cannot make an active bar jump backwards.

Selling creates a shipment instead of instantly converting a whole batch. The selected cargo is reserved, and units leave storage over time. Throughput is the base rate plus transfer-pump research, scaled by current market demand. The unit quote is locked when the order starts. Integer-credit payments use differences between cumulative totals, so selling in small chunks pays exactly the same quoted total as the full batch. Raw ore reserved for refining or shipping cannot also be consumed by the other system.

Both the market and world HUD show shipment progress and a completion notice. Starting another operation waits for active refining; shipments continue in the background during combat. Cargo-capacity checks still apply. Closing a menu does not stop the work. Quitting cancels the unfinished part of a shipment: already sold units/credits remain saved together, while unsold ore stays in storage. An incomplete order is not silently resumed at a changed price.

## Authoring and verification

The registered Event Sheet actions include buying upgrade ranks, collecting produced ammo, cycling/cancelling defence placement and placing at the preview. Mining Trade exposes shipment progress/rate/quote and research rank/cost queries; the operation exposes refining progress. New components and fields remain configurable in the editor.

The progression regression test purchases every reachable rank, verifies exact shipment accounting and intermediate progress, checks refining progress, exercises sentry/mine/mortar/supply effects and reloads all ranks. Native UI checks exercise map selection, shipment completion, refinery output and the existing machine interactions. Authoring backs up the scene under `.byteengine/Backups/ranked-defences-*`. No game export is performed by these tools.

## Upgrade map presentation (3.26.0)

The company research screen is now a full-screen olive overlay with clipped-corner icon tiles and a compact bottom selection strip. There is no background grid or side details panel. White transparent pictograms are authored by `Tools/MiningPrototype/build_research_icons.py`; the pistol uses `company-pistol.png`.

Right-drag pans; the wheel zooms around the pointer; selecting a tile displays its name, effect, rank and missing prerequisites. Enter purchases the selected upgrade. Escape returns to play. Gold tiles are complete, olive tiles available, grey locked. Rank pips stay inside each tile.

The map lays out a two-sided dependency tree with shared thin trunks. Only the current choices and their next locked steps are revealed. The first prerequisite is the visual tree parent; all actual prerequisites are still required and any missing ones are listed in the selected details. Research IDs, rank costs, saved purchases and effects are unchanged.
