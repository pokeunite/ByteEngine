# AI Behaviour workshop

Current upgrade: see **AI-Perception-Tactics-and-Combat.md** for perception, staged attacks, services, tactical queries and sub-behaviours. This workshop remains an introduction to the basic tree and event tools.

ByteEngine first version · 24 October 2026

## What you now have

A native `.byteai` asset, **AI Behaviour** component, per-enemy blackboard, readable tree editor and live debugger. This is an independent ByteEngine implementation. Unreal source was consulted for architecture; no Epic source is compiled into ByteEngine.

This first version uses a configurable decision/perception interval (default 0.1 seconds). It does not implement Unreal's full event-driven observer scheduler, EQS, StateTree, networking or its complete task library. Movement continues every simulation update while decisions run at the chosen interval. The existing grid navigator remains the pathfinding backend; the Unity A* package is a reference, not a linked ByteEngine dependency.

## Make your first enemy

1. Save your scene. Pick an enemy with a collider and Health.
2. For a new custom tree, disable **Simple Enemy AI**. For an existing combat role, keep it enabled and turn on **Behaviour Controlled**: the behaviour asset then drives that component as a combat executor, so its normal autonomous update is skipped.
3. Ensure there is a **Navigation Region** covering the floor and bake it. Put **Navigation Agent** on the enemy and bind its Region Object. Enable it; leave Externally Driven off initially. AI Behaviour owns and drives that agent while a movement task is active, then releases it on completion, interruption or pause. It does not add crowd avoidance.
4. In Assets choose **+ Create → AI Behaviour**. This creates a working chase / windup / melee / recovery example and opens its editor. Rename the asset in the asset browser as desired.
5. Add **AI Behaviour** to the enemy and select the `.byteai` asset in Behavior.
6. Assign **Target Object** to your player. Alternatively enable Find Nearest Character to discover a nearby actor with an enabled Character Controller. Detection Range limits either route.
7. Check Eye Height and Sight Mask Bits. Sight rays must include cover and target collision layers. The same eye height is used for the target sight point in this version.
8. Enter Play and open the behaviour asset. Choose the enemy in the live inspector.
9. Tune task distances, speeds, windup, damage and recovery in the editor. **Save**, then use **Restart from saved asset** in the live debugger to apply saved changes to that actor. Runtime state and memory reset on restart.

Palclone uses role assets under `Assets/AI/Enemies`: Rusher, Spitter, Hexer, Charger and Crownback. Each of the ten ordinary enemies and the boss has an AI Behaviour binding. Existing combat values and encounter activation rules are retained. The generic examples remain under `Assets/AI`.

## Read the tree

The default editor shows rule cards with **WHEN** conditions and **DO** steps. Click an item to change its settings on the right. Use **+ Add rule** and the local **+ Add step** menu; the full task palette is hidden. Rule Up/Down buttons change priority. Right-click a step to reorder/remove it. Choose **Advanced tree** only when you need deeper structural editing. Memory and Test / Live view are separate tabs.

In Advanced tree, children run in visible top-to-bottom order. Groups are indented. Use the palette's search to find a task, select a group, then click the task to add a child. Drag a row onto a group to nest it; Up / Down changes sibling order and therefore priority. Delete subtree removes the selected node and its children. Undo / Redo are available. Invalid/incomplete structures show validation errors and cannot be saved.

| Group | Meaning |
| --- | --- |
| Choose first available | Try children from the top; stop at the first running or successful one. Reconsider higher priorities each decision. |
| Do in order | Complete children in order. A running child resumes without rerunning earlier completed children. Failure fails the group. |
| If condition | Run its single child while the condition is true. When false, the branch is cancelled. |
| Cooldown | Run one child; after success, block it for the configured simulation duration. |

Conditions: known target, target alive, inside/outside distance, line of sight, own health below a fraction, memory boolean true and memory number at least a threshold. Invert enables NOT. Put an If condition around a group when it should be monitored throughout that group's execution. A condition inside a sequence only runs when the sequence reaches it.

Example:

```
Choose first available
  If target is close
    Do in order
      Stop moving
      Send event signal: AttackWindup
      Wait: 0.45 seconds
      Damage target: 10, reach 1.8, require line of sight
      Wait: 0.7 seconds
  If target is known
    Move to target: speed 3, stop distance 1.3
  Wait: 0.5 seconds
```

If the target leaves the close-range gate during windup, this example aborts it. If you want a committed attack that continues after the target dodges, author the range check before the attack sequence rather than wrapping the whole attack in a continuously monitored range gate. Keep the damage task's final reach/visibility check enabled so dodging can avoid the hit.

## Tasks

| Task | Purpose / settings |
| --- | --- |
| Use enemy combat pattern | Continuously run this actor's existing attacks, dodge, cover, movement and boss pattern. Requires Simple Enemy AI with Behaviour Controlled enabled. Stops/cancels when its rule is interrupted. |
| Wait | Remain active for Duration simulation seconds. |
| Move to target | Use Navigation Agent toward the sensed target; Speed, Stop Distance, Timeout. |
| Move to memory position | Navigate toward a Vector3 memory key. |
| Retreat | Request a destination away from the target until desired range is reached. This is not a cover-search or flank allocator. |
| Face target | Rotate horizontally toward the target once. |
| Stop moving | Stop navigation owned by this behaviour. |
| Set memory | Store a boolean, number, text or position in this enemy's blackboard. |
| Send event signal | Emit a named signal once when the sequence reaches it. |
| Wait for memory | Wait until a chosen boolean is true; optional Timeout fails the task. |
| Damage target | Apply Amount once if the target remains within Reach and, optionally, line of sight. |

A movement task fails on timeout, unreachable navigation or missing bindings. Use a lower-priority fallback and a wait to avoid repeated expensive retries. Externally driven navigation owned by another system is rejected. Damage Target performs contact damage, not a gun shot or area attack.

## Blackboard / memory

Use the **Blackboard / Memory** tab to add named defaults. Each actor receives its own copy; enemies sharing one asset do not share mutable values. Runtime memory is reset when loading/restarting the tree and is not saved as permanent progression.

Perception refreshes these reserved keys:

- `HasTarget`: boolean.
- `TargetDistance`: horizontal distance; Detection Range when no target is sensed.
- `HasLineOfSight`: boolean from a cover-aware ray.
- `HealthFraction`: own health / maximum, from 0 to 1.
- `TargetPosition`: last sensed target position; remains last-known when the target is lost.

Avoid using those names for your own writable flags. Custom names such as `AttackDone`, `Alerted`, `Destination` and `CanCast` are suitable. Position tasks require a position value; boolean waits require booleans. Memory names are case-insensitive.

## Connect Event Sheets

Search **AI / Behaviour** in the condition/action menus. On a sheet attached to the enemy, target Self. On a director sheet, explicitly choose the enemy.

| Entry | Use |
| --- | --- |
| AI sent signal | Condition that fires once for a matching new named signal per event/actor. |
| AI task is running | Check the current leaf task's authored name. |
| AI memory is true / AI memory number is at least | Read custom flags or perception values. |
| Set AI memory boolean / number / text / position | Update memory from gameplay logic. |
| Copy AI memory to object variable | Copy a memory value to a variable on that AI actor for other event tools. |
| Set AI target | Bind a scene object as the explicit target. |
| Use AI target as Picked object | Make the current target available to actions supporting Picked. |
| Pause or resume AI behaviour | Stop its owned movement and suspend task timing. |
| Restart AI behaviour and reset memory | Reload the saved asset and begin again with fresh state. |

### Custom attack example

Use `Assets/AI/Custom attack through Event Sheets.byteai`:

1. Its attack branch clears `AttackDone`, sends `CustomAttack`, waits for `AttackDone`, then recovers.
2. Add an enemy Event Sheet event named **Custom attack — start**. Condition: AI sent signal, name `CustomAttack`, target this enemy.
3. Add your own named animation, sound, VFX or projectile actions. Use AI target as Picked object if the chosen action needs that binding.
4. When the actual custom attack completes, use **Set AI memory boolean**, key `AttackDone`, value true, targeting the same enemy. For an immediate attack you can set it in the start event; for an animation wait for its completion/signal window.
5. Adjust the Wait for memory timeout so a missing completion event cannot stall the enemy forever.
6. If the attack is interruptible, use AI task is running / memory guards in your presentation rules, and stop any independent long-running audio/VFX/animation on interruption. The behaviour runtime cancels its own waits/navigation; it cannot automatically undo arbitrary effects launched by your event.

Do not combine a Damage Target task with an Event Sheet that also applies the same damage. Signals request presentation/custom gameplay; they do not themselves deal damage.

This first version does not supply a plugin-defined task registration API. Event signals and memory completion are the supported no-code extension path.

## Debugging and performance

Open **Live debugger** and select the actor. It shows Current Task, tree status, errors, current blackboard and recent signals. Running nodes are blue, successful nodes green, failed nodes red; abandoned running nodes are recorded as Aborted for the latest decision. Multiple enemies can use one tree and be inspected individually.

Start at a 0.1-second decision interval. Perception includes a line-of-sight ray, so lowering the interval for every enemy increases work. The current maximum of about 11 enemies is a suitable small playtest; no crowd-scale performance claim is made. Trees are limited to 512 nodes and 48 nesting levels. Signals retain a bounded history of 64; use memory for durable state instead of relying on old pulses.

The role migration preserves existing attack logic rather than converting every internal attack phase into separate tree nodes. Tune role combat settings on the enemy's Simple Enemy AI component. Encounter events can still disable that component; the behaviour then waits quietly until it is enabled again.

The first version does not fix steering, flanking or enemy crowd spacing by itself. Those remain navigation/tactical tasks to add next. Keep behaviour decisions separate from the movement implementation.

## Save and standalone games

The component has a native serializer and `.byteai` files are normal project assets. Keep them inside the game's asset folder. The Windows player runtime must match the installed editor before exporting. Updating the editor does not rewrite an existing exported game; re-export when ready. No game export was performed for this feature.

## Reference and provenance

Authorised reference checkout: engine workspace `artifacts/UnrealEngineReference`, EpicGames/UnrealEngine release commit `f45729c0b6f097038e72b52d366a62baeca3275b`. Only the BehaviorTree class/implementation directories were selected, plus root files included by Git sparse checkout. Consulted areas: composites, task lifecycle, component execution state and blackboard observers. This checkout is ignored scratch reference material and must not be copied into the reusable engine distribution.

Public architecture reference: [Epic's Behaviour Tree overview](https://dev.epicgames.com/documentation/en-us/unreal-engine/behavior-tree-in-unreal-engine---overview). ByteEngine's interval-based first version differs from Epic's event-driven implementation.

Unity navigation reference supplied by the user: `C:/Users/codex/Desktop/game dev/New folder/New Unity Project/Packages/com.arongranberg.astar`. No Unity addon source is linked into ByteEngine.
