# Reviewed upgrade map — 30 October 2026

The installed game uses the submitted review's prices and earliest-operation gates for retained nodes. The canonical gameplay catalog is `Plugins/KarlsonDemo/Mining/CompanyResearchCatalog.json`; the scene has the same definitions in the workshop component. This catalog replaces the old company tree rather than appending another set of bonuses.

- Removed: company-map refinery batch/yield/conveyor nodes, duplicated ammo-press speed, Field Dressing and Emergency Plating. Station upgrades remain at their own machines.
- The free Mining Licence is already owned. Successful rounds restore full HP without a medkit.
- Shotgun and AR licences grant the corresponding carried weapon; both begin locked. Carry capacity is three. Existing number-key event rules select carried weapons.
- Scatter Specialist steers the existing eight-pellet budget across up to three visible enemies; up to three pellets are assigned per target, and unassigned pellets remain ballistic. No additional pellet damage is created. The editable weapon settings expose range, cone and steering speed.
- AR specialisations enable one penetration at half damage and reduced initial-burst inaccuracy. Their prerequisites follow the AR branch.
- Extraction research changes the actual contract forecast and reward. Deep-core extraction applies to higher-pressure contracts.
- Survival research adds maximum HP, reduces the brief hit-slow duration, retains 25% of raw cargo on failure, and grants one 25%-HP emergency drill repair per operation.
- Defence licences use the existing placement, durability, replacement and mine-rearming systems. Tracking Assembly increases sentry projectile speed by 25%.
- A one-time save migration refunds superseded research using the old catalog's rank costs. Production levels and cargo progress remain saved separately. Project scene/effect backups are in the project's `SourceArt/Upgrade-Map-Backups` folder.

The approved delivery/live-market system is now installed in-game. Freight Licence, Demand Scanner and Broker Agreement are active, with their existing prices and operation gates. The HTML review remains available as an independent sample account.

## Selected explosion

Aim Studios: <https://aim-studios.itch.io/explosions-pixel-art>, CC0. The original `Realistic Explosion_default 64 X 64` sheet is 256 × 128: four columns, two rows, eight frames. Mine and mortar effects use one animated billboard with the previous world-size envelope and existing component scale multipliers. Physics knockdowns remain separate from the artwork. Asset and licence references are saved with the project.

## Simplified market review

`HazardPay-Market-Review.html` has a quantity slider, a complete-batch sale button and a guaranteed-delivery comparison. Payout locks on confirmation, finishes in the background and produces a receipt showing credits earned and gain/shortfall against guaranteed delivery. Fees are included in every live payout shown. Target orders, order slots, partial fills and expiry management were removed.

This comparison is not accounting profit after mining costs. There is no ore purchase-price fiction. Current low prices have a direct “Use guaranteed delivery instead” action.

## Verification

The reviewed native gameplay suite passes 69 checks. The native effects suite verifies the external eight-frame sheet, bounded playback, spider knockdown/corpse behaviour and scene preservation. Deterministic HTML-model checks cover locked prices, positive/negative comparisons, guaranteed payouts, stock reservation, one active sale and pause behaviour. Browser interaction checks confirm the progress state and completed-sale receipt.

The tests validate behaviour, not final combat/economy balance. Round gates use the upcoming operation number; successful completions advance that number.

## In-game selling

At the company terminal, open Market and choose Live Market or Guaranteed Delivery. Select raw/refined ore, move the quantity slider and press Sell. Live offers include the fee. Both methods accept the whole batch and lock its payout. Selling takes 30 seconds by default and continues while drilling. Completion pays automatically and plays the notification cue. The receipt shows the last quantity, payout and gain/shortfall versus guaranteed delivery. It does not claim profit after mining costs.

Cargo is deducted and the pending payout is recorded in the same operation-save update. Settlement adds credits, records the receipt and clears the pending sale in the same update. Pending progress and the last receipt survive reloads without a second payment. The workshop save separately retains the demand clock and research. No offline shipping time is simulated.

The workshop component exposes sale duration, guaranteed-price factor (0.9), fee (3%), delivery capacity (40) and demand-cycle duration (90 seconds). Default refined delivery is 4.50 credits per ore. Freight raises capacity to 80; Broker lowers fees to 1%; Scanner reveals real demand-cycle information, not a guaranteed future price.

Event Sheets expose actions to choose a sale method, start a sale, read market values and test whether a sale is shipping. Whole-credit game payouts are rounded down; the Sell button and receipt show the same final total.
