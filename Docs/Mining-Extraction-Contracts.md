# Extraction contracts and enemy pressure — 3.27.0

Walk to the drill and press E. Select one of three contracts and press Start. Selection itself does not start the encounter. The card shows exact rig raider and player hunter totals, spawn interval and raw ore reward. The selected contract's movement/damage modifiers are shown at the bottom. Upgrading drill output refreshes all three rewards before launch.

Default first operation:

| Contract | Rig raiders | Player hunters | Raw ore | Spawn gap | Enemy movement / damage |
|---|---:|---:|---:|---:|---|
| Shallow survey | 4 | 2 | 18 | 0.90s | standard |
| Deep vein | 5 | 4 | 30 | 0.70s | +6% / +10% |
| Overdrive | 6 | 7 | 45 | 0.52s | +12% / +20% |

Counts grow with completed operations, respecting Maximum Enemies Per Round and Maximum Alive. The spawn gap is a scheduling interval, not a promise of simultaneous enemies: the concurrency cap can delay the next arrival. Existing front approach lanes are retained. Completing extraction alone never pays; all scheduled enemies must be spawned and defeated while the operator and drill survive. Failing pays none of the current operation's ore and keeps earlier company progress.

Counts, role assignments, ore reward, spawn gap and contract speed/damage multipliers are snapshotted at launch. The spawner uses that snapshot. Changing contracts during combat is rejected. Cargo capacity, active refining is checked before launch; an existing shipment continues in the background during drilling. No ammo is granted for starting an operation.

## Enemy roles

- Player hunters now approach at 4.4 m/s (previously 3.1), wind up for 0.38s, recover for 0.42s and have a 0.9s attack cooldown. Squad attack opportunities remain capped at three hunters. Damage stays at the authored blueprint value, modified by the chosen contract.
- Rig raiders now approach at 3.2 m/s (previously 1.9). Wind-up 0.65s, recovery 0.6s, cooldown 1.1s. They retain the drill target when evading.
- Raiders can react to nearby gunfire in their firing lane: default 30% chance, 0.18s reaction delay, 4.5s cooldown. Native evasions use a brief 0.18s sidestep at 5 m/s, checking world collision/navigation. The existing native damage reaction can also request a dodge when its cooldown is ready. No invulnerability, teleport, guaranteed projectile avoidance or shared synchronised dodge timer is added.
- Faster motion applies to the actual Behaviour movement tasks via the reusable AI Behaviour Movement Speed Multiplier. Merely changing the combat component would have left approach speeds unchanged.

## Editing and Event Sheets

On the Mining Operation component, edit Contracts JSON for the three names, IDs, ore/enemy multipliers, hunter fraction, spawn interval, movement and damage multipliers. Exactly three valid unique contracts are supported by this interface. Invalid configuration rejects launch. The role speeds, timings, evasion chance, reaction delay and cooldown are separate Enemy Pressure properties.

Existing blueprints and Behaviour assets remain the source for models, health, attack style, damage and tactical structure. The operation applies the listed wave-specific pressure settings when spawning. Hunter Every is a legacy hidden field; contract Hunter Fraction now controls the role mix.

Event Sheet actions under Mining include Select extraction contract 1/2/3 (interface), Choose extraction difficulty 1/2/3 (operation), Open drill panel and the existing Start drilling action. Selected extraction contract is provides an explicit condition. The generated button events use these actions. Native enemy dodge can also be requested through the existing AI / Tactics action.

Source: Plugins/KarlsonDemo/Mining/MiningContracts.cs, MiningEnemyEvasion3D.cs and MiningBusinessUi3D.Contracts.cs. Public Evasion component properties permit independent use on other enemies.

## Verification

Regression checks cover exact forecast/spawn agreement, active-wave snapshot isolation, role targets, real role tuning, fixed payout, invalid configuration, threat-lane filtering, native sidestep movement and taking damage during a dodge. Native UI checks click each difficulty, launch the chosen wave and compare its count/reward to the preview. The native gameplay check exercises navigation, attack wind-ups and damage against both assigned targets. Test previews disable persistence.

This is an initial difficulty tuning pass, not a claim that survival balance is final. Play all three contracts with the current pistol and compare drill damage, player damage, ammunition spent and completion/failure. Adjust the exposed values from that evidence.
