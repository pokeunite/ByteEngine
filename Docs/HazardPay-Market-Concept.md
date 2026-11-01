# Market mini-game proposal

The goal is to make ore valuable for competing purposes: immediate upgrades, a guaranteed delivery payout, or a higher exchange price. The player should spend a few seconds choosing a buyer, then return to defending the drill. This is a proposal and visual mockup; the current market has not been replaced.

## One terminal, three ways to trade

### 1. Depot buyer: cash when you need it

The depot offers a fixed, modest price—for example 4 credits per refined ore—with a quota of 30 units per completed operation. Sell 20 units and receive 80 credits. It is available even when the exchange price is poor. The quota refreshes after completing an extraction, not by reopening the menu or waiting. This prevents an extended bad market from stopping basic progression while leaving a reason to seek better buyers.

### 2. Live exchange: sell now or set a target

Keep the existing graph. At 5.60 credits, selling 20 ore earns 112 credits. Alternatively, set a target of 6.20: reserve 20 ore and place an order. The order fills at its locked target or better when that price is reached, at the market's configured shipping rate, while the player keeps playing. There is no guarantee that an ambitious order fills. Show reserved quantity, current price, target, partial fills and payout clearly. Allow cancellation of the unsold remainder with no fee; filled units stay sold. Limit active orders so the interface stays small. Reserved ore cannot also be sold or committed to a delivery contract.

### 3. Buyer requests: make the next operation matter

A rotating request might need 40 refined ore and pay 280 credits (7 per unit), due within two **completed** operations. With 28 ore available, the player can accept it, reserve those 28 and see that 12 more are needed. They then choose whether to attempt a riskier, higher-yield extraction, improve processing capacity, or use existing savings for defenses first.

When 40 ore is ready, return to the terminal and deliver for 280 credits. The player could immediately buy the 140-credit press-speed upgrade and keep 140. This links combat, industry and selling rather than creating a separate screen-waiting activity.

## Clear rules and failure handling

- Show quantity, exact total payout, unit price and deadline before acceptance. Offers do not change price after acceptance.
- Deadlines advance only on successful completed extractions. Menu time, pausing, refining, closing the game and a failed round do not consume a deadline step.
- Start with one active delivery request. Failed or cancelled contracts return reserved unsold ore; the penalty is the lost premium opportunity, not deleted inventory or debt.
- Keep depot stock, exchange reservations and contract reservations separate in the UI. A unit of ore has only one owner/reservation at a time.
- Save offers, reservations, partial fills and deadlines. Reopening the terminal or restarting must not reroll offers or duplicate credits.
- Refresh the buyer board after each completed operation, with at least one achievable offer based on the player's current production and operation rewards.
- A bad exchange price should never be the only available route to funding the next operation. The depot is the fallback, not a requirement to wait until the graph recovers.

## Why this can be fun

The useful tension is: "Do I take 80 credits now to buy a needed upgrade, or keep those resources for the 280-credit delivery?" The exchange adds an opportunistic third choice without requiring constant attention. Buyer quantities and deadlines encourage different operation choices. It avoids an unrelated timing-bar or rapid-click task that interrupts the shooter.

First playtest just the depot and three rotating requests using refined ore. Add target-price orders after the reservation and save rules are proven. Judge success by whether players deliberately change what they mine, refine, sell or upgrade—not by how long they stay in the market menu.

## Visual concepts

The generated concepts use the current charcoal/yellow equipment palette. They show the buyer board, exchange order controls and delivery progress, plus an example journey from accepting a request to buying an upgrade. Images are design references, not screenshots of implemented functionality. Prompts were supplied to the built-in image generator; no external game artwork was copied.

Saved concept images: `Docs/Art/HazardPay-Market-Buyer-Board.png` and `Docs/Art/HazardPay-Market-Contract-Flow.png`. Built-in image generation was used. Prompt set: (1) a charcoal/yellow three-panel buyer board, exchange and delivery interface with exact example quantities/prices; (2) a three-stage accept/mine/refine/deliver/upgrade storyboard using the same figures, with operation-based deadlines; (3) a targeted correction identifying the 5.0s-to-4.5s upgrade as the ammo press.
