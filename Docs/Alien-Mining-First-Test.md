# Alien mining — first playable test

Open `C:/Users/codex/Documents/palclone/palclone.byteproject`. The prototype uses the existing top-down movement, pistol, dash, HUD and physical bullet/trail systems. The test is one small mining worksite with one resource, three machines and two alien roles. The galactic market uses fixed editable prices for this first test.

## Play
1. Click the game view to focus it. Start an operation with **E** or the **Start drill** button.
2. Move with WASD, aim with the cursor, fire with left mouse, reload with R and dash with Space. Auto reload remains enabled.
3. Protect the drill. Green Raiders approach the machinery; pink Hunters chase the player.
4. The first operation extracts a fixed 18 ore over 16 seconds and schedules six attackers. Finishing extraction does not generate endless extra ore. Clear the last attacker to bank the quota and return to preparation.
5. Press **F** / Refine to process a batch, or **X** / Sell to sell available cargo. Raw ore sells for 2 credits per unit; refined ore for 5. Processing reserves its batch until it finishes, preventing duplicate sales.
6. Spend earnings on the four shop upgrades. Start another operation to feel the change. Each completed operation adds two attackers, up to the authored cap; no more than 12 attackers are alive at once by default.
7. If the player or drill dies, recover through the Start/Recover button or E. Unbanked extraction is lost; previous cargo, credits and upgrades remain. There is no fee that can permanently block another attempt.

## Upgrades
- **Drill:** +7 ore per operation, +30 drill HP, faster extraction per level. Starts at 40 credits.
- **Ammo press:** +24 reserve rounds available at the next launch per level. The magazine is refilled on launch. Starts at 35 credits.
- **Refinery:** +12 units per batch and faster processing per level. Starts at 30 credits.
- **Company research:** +10% actual weapon damage per level, saved across sessions. Starts at 90 credits.
Costs grow by 1.65x per level, rounded upward. Default maximum is five levels. Every amount is a component setting rather than dispatch by scene-object names.

## Where to edit
Select **Mining company operation** under **Mining first test**. Its Mining Operation component exposes actor links, alien Blueprint assets, fixed haul, duration, health, enemy counts/cap/cadence, prices, batches, ammo allowance, costs, research bonus and save slot.

Open `Assets/Events/Mining/Mining business controls.byteevents` for all keyboard/button bindings and operation actions. No interaction keys are embedded in the simulation component. Keys 1–4 buy drill, press, refinery and research upgrades during preparation. The existing weapon-controls sheet remains independent.

Alien assets are in `Assets/Blueprints/Mining`. Their editable AI assets are in `Assets/AI/Mining`. Raiders and Hunters are deliberately simple primitive placeholders without animations. They share the staged melee asset. Spawned actors receive their explicit drill/player target from the operation component, not an enemy-name rule.

The five **Alien emergence point** objects have Mining Enemy Spawn Point components linked to the operation. Move or duplicate them to change entry pressure. The Navigation Region automatically samples the authored worksite floor on layer 20. Re-bake when changing obstacles.

The drill uses layer 19. The starter pistol's Hit Mask excludes that layer, preventing accidental damage to the player's own drill. AI melee damage still affects it. To intentionally enable friendly fire, include that layer again.

UI is native and editable: **Mining HUD** contains panels, text, buttons and extraction progress. Mining HUD component only presents state; Event Sheets execute purchases. The original health/energy/weapon HUD is preserved.

## Saved company progress
`Persist Progress` is enabled by default. Save filename: `mining-business-v1.json`, in ByteEngine's project save storage. Saved fields are credits, raw/refined cargo, completed rounds and upgrade levels. A running operation is not resumed; restart returns to preparation with banked company progress. A partially processed batch is not lost because its raw input is deducted only when processing completes. Tests use temporary save slots or disable persistence and do not alter the real company save.

## Prototype boundary
One site, one resource in raw/refined forms, one pistol, two roles, no stock speculation, prestige reset, multiple planets or automated factories yet. The playtest question is whether selling a haul creates an immediate desire to buy an upgrade and drill again. Costs, timings and pressure are initial tuning values, not a finished balance pass.
