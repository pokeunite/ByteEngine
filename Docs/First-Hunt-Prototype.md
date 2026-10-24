# First hunt prototype

The existing simple level and Ground material are preserved. Ten normal enemies and one boss are authored as ordinary scene objects.

## Play

Enter the first court, clear the trail, then use the green preparation shrine with E. It restores health, energy and reserve ammunition. Cross into the ambush court, clear the four enemies, and approach the hunter test at the rear. After the test, climb to the gold checkpoint and press E. Enter the boss arena when ready.

SPACE dashes. E also retains the normal weapon pickup action. ENTER after death restarts the hunt before the checkpoint, or restores you at the Crownback entrance after the checkpoint. Boss retries restore boss health and reset its attack; earlier encounters remain cleared. The checkpoint lasts for the current play session.

## Enemy colours and initial tuning

| Capsule | Role | HP | Attack |
|---|---|---:|---|
| Red | Rusher | 75–100 | Short, telegraphed melee attack; movement 2.6 |
| Blue | Spitter | 100–125 | Slow projectile, speed 7; 9 damage |
| Violet | Hexer | 150 | 2.1-radius ground strike; 1.1-second warning; 14 damage |
| Amber | Charger | 350 | Locked charge; 1.25-second warning; 16 damage |
| Dark crimson | Crownback | 1600 | Longer charge; 1.15-second warning; 24 damage |

Chargers stop on cover and recover for 2.8 seconds. The gold marker indicates recovery: incoming damage doubles during this window. Ground attacks lock the player's position when their warning starts; move out of the disk before it resolves. Blue enemies' projectiles can be dodged or blocked by cover.

## Edit without code

Open **Assets/Events/First Hunt.byteevents**, attached to **First Hunt - encounter director**. Named rules control encounter activation, barriers, supplies, objective text, checkpoint reset and victory. `HuntPhase` is the director's progression variable. Encounter groups are children of the director.

Select an enemy and edit **Health** and **Simple Enemy AI** in the inspector. Attack Style, movement, detection and attack ranges, damage, windup, recovery, charge speed/distance, hazard radius and recovery damage multiplier are saved in the scene. Blue enemies additionally use **Projectile Launcher 3D** for projectile speed and damage. Colour materials are `Assets/Environment/Prototype Dungeon/Materials/Enemy <type>.bmat`.

The native Event Sheet additions are **All enemies in group are defeated**, **Enemy attack phase is**, **Reset enemy attack**, and **Stop character velocity**. Existing Set Variable actions can modify component properties as well as the progression variable. No project-specific runtime hunt script is used.

## Pacing and limits

6–10 minutes to the boss is a playtest target, not a measured completion time or a forced timer. This small arena may play faster for an experienced shooter. Adjust encounters after timed playtests; do not stretch it with idle waits or excessive enemy health. The supply stop is a single resource cache rather than a choice menu. Capsules are combat stand-ins, not final monsters. Movement uses simple obstacle avoidance, not a full navigation-mesh pathfinder.

## Verification

Native combat tests cover ground-strike warning and dodge, one-hit damage, charge collision with cover, recovery damage multiplier and clearing inactive defeated enemies. The saved project's Event Sheet was simulated through every encounter, supply use, checkpoint death/retry and victory with no event warnings. A native render verified enemy colours, HUD and entry floor position. Manual gunplay feel and encounter duration still need playtesting.
