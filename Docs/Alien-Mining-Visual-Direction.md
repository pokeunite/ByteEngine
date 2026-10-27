# Alien mining — reference study and next visual pass

Reference: user-supplied `2026-10-25 11-07-30.mp4`, 2:01 long. These are observations from sampled frames and short sequences, not measurements of the reference game's internal simulation.

## What the reference actually shows

- 0–6s: small persistent balances upper left, two slim bars upper right, weapons/ammo along the bottom. A short interaction prompt appears over the nearby object.
- 6–10s: approaching a laptop opens the selling screen. A large price-history graph dominates; current price and floor/peak sit above it. Storage, selling rate, claim, buy and deposit are explicit controls. The graph is a commodity price display; the footage does not establish a full equity stock exchange.
- 12–20s: a separate large upgrade tree opens only when requested. Costs/details appear on selection or hover; it does not occupy normal gameplay.
- 30–40s: bullet presses use small contextual panels. Each row gives a stat, present value, next value and cost, or MAXED. Output is visibly collected from the machine.
- 46–112s: a horde counter appears at top centre. Many enemies advance toward the defended area, creating crowd pressure. Several attack lanes are visible, with occasional brighter threats. Damage numbers communicate sustained weapon throughput. The exact count and AI rules cannot be established from the recording alone.

## Interface direction for our top-down game

The combat screen's job is to make survival and the current operation readable. First read: player health and immediate threats. Second: ammunition and drill condition. Third: wave progress. Business management belongs at machines between operations.

Persistent regions:

- Upper left: credits with one compact ore balance beneath it; icons plus numbers, no instructions paragraph.
- Upper centre: operation number and remaining attackers during a wave. A short preparation label between waves.
- Upper right: a compact drill-health meter while defending it.
- Bottom: retain readable player health/energy and one equipped weapon/ammo display. No empty ability slots or hypothetical weapons.
- Near an interactable: one prompt, for example `[E] Ammo press`; only the closest eligible machine owns the prompt.

Remove the permanent upgrade sidebar, long status prose, enemy role legend and global buying shortcuts from the normal play view. Show tutorial instructions briefly on first use. Pressing Escape closes a panel before affecting any other menu.

Machine panel, approximately 460px wide at 1280×720:

1. Machine name and available credits.
2. Three short rows at most: stat, current → next, purchase price.
3. Primary action appropriate to that machine, then Close.

Every purchase needs affordable, insufficient funds, maximum level and purchased feedback. Exact benefits must remain visible. Keep essential labels around 18–22 rendered pixels at 720p. Validate in the actual game view, not only concept artwork.

The tent terminal has two clear entry points: **Company upgrades** and **Market**. Orders show their exact cost, effect, delivery state and availability. Machine-specific tuning can still be accessed at each machine.

## Market behavior to implement

Replace the current fixed-price sale with a real price simulation and saved price history. The graph must represent the same current quote used for the transaction.

- Select resource; show inventory, unit price, change, recent low/high and a line graph.
- Choose quantity with an explicit Sell total. Insufficient stock and zero quantity disable selling.
- Price moves within exposed bounds at a configurable interval. Persist history and simulation state to prevent reopening the panel rerolling the price.
- Freeze the displayed quote for confirmation or explicitly refresh it before a sale; never silently charge a different price.
- No real-world finance connection. This is a fictional resource market.
- Keep buying commodities optional for a later pass; the first version needs selling and the decision to hold cargo.
- Expose price bounds, volatility, update interval, resource identity, inventory and buy/sell actions through components and Event Sheets.
- Do not allow endless risk-free waiting to become the best way to progress. First test prices advance during operations, with trading between them; clearly label this rule.

This is new work: the current prototype still sells at fixed raw/refined prices. A drawn graph alone would not implement a market.

## Combat direction

Our current small mixed-role encounter produces a skirmish. The desired loop emphasizes thinning a growing crowd before it reaches the operation.

- Start by testing 20–30 inexpensive ordinary aliens per wave, with a separate exposed cap on simultaneously active enemies. These are proposed test values, not a claimed laptop performance budget.
- Spawn from readable approach lanes outside the immediate player view, with a visible emergence cue. No appearing directly beside the player.
- Ordinary aliens approach predictably, die quickly, and use a short readable melee attack. They do not all dodge or perform elaborate charge sequences.
- A minority of faster hunters create priority decisions. Reserve complex behavior for special enemies.
- Crowd separation prevents stacking; stagger arrival slightly and distribute approach positions around the target.
- Maintain a simple drill-attacker versus player-hunter distinction. Use different silhouettes, not a permanent text legend.
- Benchmark navigation, collision, projectile and damage-number costs before increasing the live cap. Pool transient effects and bound active numbers.
- Playtest whether the player creates breathing room by aiming and moving well, and whether upgrades visibly change how quickly they clear the crowd.

## Asset family

Original frontier mining equipment: worn mustard paint, charcoal frames, cream service panels, small cyan screens. Large shapes must remain distinct from the top-down camera. Use restrained grime; avoid machinery covered in tiny unreadable details.

| Asset | Silhouette | Separate moving parts | Interaction |
|---|---|---|---|
| Drill | Tall open gantry, visible auger, four feet | Auger, motor, hopper | Start operation, drill upgrades |
| Refinery | Broad low furnace body, two short stacks | Drum, output tray | Process ore, upgrade yield/throughput |
| Ammo press | Compact C-frame press and flywheel | Ram, wheel, short conveyor | Collect ammunition, upgrade output/storage |
| Company tent | Open front, sloping canvas roof and antenna | Screen content; optional flap | Company orders/upgrades and market |

Keep collider geometry simple and separate from visuals. Define named interaction points and output/effect anchors in each asset. Machine logic remains in components/Event Sheets rather than embedded in model geometry. Generated concept sheets are modeling references, not finished meshes.

## Deliverable status

This pass studies the supplied video and establishes model/UI concepts. It does not replace the installed gameplay, implement a stock market, or deliver finished 3D models. The previous working prototype is preserved.
