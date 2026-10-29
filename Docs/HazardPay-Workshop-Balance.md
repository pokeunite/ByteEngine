# Workshop balance and recovery

Hazard Pay's authored scene uses refinery queues: 3 raw ore per 3-second batch, +1 batch capacity per rank, and -0.25 seconds per speed rank (minimum 1 second). Recovery yield carries fractional units across completed batches, so small starting batches do not erase yield upgrades. Input and output are reserved per batch, not for the entire cargo. One click queues available ore; processing continues during an operation. The generic component retains opt-in switches for legacy projects.

Ammo production uses actual cycles: 5 rounds every 5 seconds, 35-round bucket, +15 bucket capacity per rank. Speed ranks subtract 0.5 seconds (minimum 1 second), while payload ranks add 1 round per cycle. The menu displays both quantities directly. Permanent production research multiplies payload; the average rounds-per-second property remains available to existing logic. A full bucket banks no production credit. Walk-up pickup credits only available reserve capacity; bounded, short bullet flights provide visual collection feedback without per-round physics.

The operation captures the player's initial spawn. Automatic defeat recovery fades to black, returns the player to spawn, revives the actors, then fades back in and restores controls. Custom spawn position and fade timings are editable on MiningOperation3D. Existing event actions still start refining, purchase upgrades, collect ammo and retry; live component properties expose cycle progress and recovery opacity. No saves are deleted by the scene migration.

## Market proposal (not implemented)

Make selling a choice between three buyers instead of waiting for a random graph:

- **Depot contract:** an always-available modest fixed price, with limited volume per operation. This covers operating costs when the exchange is weak.
- **Live exchange:** the existing fluctuating price for opportunistic sales. Allow a target-price sell order, reserving quantity until filled or cancelled, so players can return to combat.
- **Buyer request:** a clearly displayed order for a quantity and premium, expiring after two or three completed operations rather than real-time waiting. Fulfilling it trades flexibility for a guaranteed reward.

Refresh offers after extraction. Show price, quantity, deadline and total payout before commitment. Avoid a mandatory timing bar or rapid-click challenge: the interesting decision is whether to sell now to afford an upgrade, hold for an order, or take a riskier operation to fulfill a contract. Test this with three offers and the existing ore commodity before adding more resources.
