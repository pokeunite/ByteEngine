# Complete upgrade reference and implementation map

The searchable [reference catalog](Reference/Zombillionaire-Upgrade-Catalog.html), [CSV](Reference/Zombillionaire-Upgrade-Catalog.csv) and [structured data](Reference/Zombillionaire-Upgrade-Catalog.json) contain the installed demo's **98 authored nodes / 219 rank purchases**, their prerequisites, currencies, rank costs and effects, plus **69 ammo-press ladder rows, three shop items and 153 stat definitions**.

The stat registry describes capabilities the code supports. It is not a list of 153 purchasable upgrades. Duplicate node titles are intentional separate purchases with different prerequisite paths and currencies. Some expensive demo-locked entries contain no serialized gameplay effect at all. They are identified in the catalog rather than treated as working features.

Three ancillary configuration assets (airdrop, atomic bomb and wave map) could not be fully decoded. The complete skill-tree nodes and press/shop upgrade tables above were decoded and checked. Base authored prices may be modified by runtime balancing code. This audit covers the user's installed demo, not another release.

## Original equivalents for the mining game

| Upgrade family found | Mining-game equivalent | Required implementation |
|---|---|---|
| Per-weapon flat/percentage damage | Weapon machining and ammunition research | Apply modifiers to selected weapon definitions; preserve authored baselines |
| Fire rate, magazine capacity, pellet count | Feed motors, extended magazines, multi-shot conversions | Ranked upgrades with exact current/next weapon values; account for increased ammunition demand |
| Projectile piercing and blast radius | Penetrator rounds and explosive payloads | Swept multi-hit projectiles and explosion damage; not merely larger trails |
| Weapon unlock flags | Purchase equipment licences | Spawn/award actual weapon inventory items, correct sockets, animation profile and production family |
| Small/heavy/rocket ammunition capacity | Separate ammunition stockpiles | Shared stock per ammunition family; weapon reloads consume the correct shelf |
| Press speed, hopper size, payload | Independent machine production tracks | Authored rank ladders with base values and costs, plus production/collection feedback |
| Health, speed and base durability | Operator armour, movement tuning, drill reinforcement | Apply real player/machine values; avoid restoring removed dash/energy features |
| Sale rate, tank capacity, price floor and peak | Cargo storage, shipping throughput, market contracts | Storage limits and timed sales, quote locking, bounded market changes |
| Resource extraction and crafting yield | Drill yield, vein capacity and refinery recovery | Resource accounting, remaining deposit, throughput and processing costs |
| Autogun and other emplacement grants | Deployable mining-site defences | Inventory grants, placement limits, target selection, damage and persistence |
| Mortar, mine, bait and control devices | Area defences and alien lures | Real projectiles, triggers, threat attraction, crowd control and friendly-fire rules |
| Aircraft, bomb, drone and airdrop unlocks | Late company support contracts | Actual support systems; several reference entries are only demo placeholders |
| Chips/wrench mastery purchases | Operation permits and engineering tokens | Award through explicit operation milestones; persist alongside credits |

The reference's existing names, prices and graph are documented for comparison. The implementation should use original names, a graph that suits our mining operation, and balance around our wave lengths and earnings. Proprietary code, prefabs, UI art and models are not included in the game.

## What is currently playable

The current company tree remains a small functional tree: damage, magazine capacity, reload speed, health, ore/refining yield, automatic refining and ammunition delivery. Machine upgrades affect production. All are backed by gameplay effects. This audit **does not install 98 fully working upgrades**.

The next full implementation should first establish ranked purchases, currency rewards, typed modifiers and a scrollable tree that shows prerequisites and exact current/next effects. Then wire weapon families, ammunition shelves and machine ladders. Add defences/support branches only with their working systems. Do not expose an affordable button for an unimplemented reward.

## Requested simplification and crash correction

Turn-in-place animation playback is disabled on the player; mouse-facing body rotation remains. Dash and its Event Sheet rules are disabled. The energy HUD is removed and the old energy/dash research node becomes health plating, so old ownership remains useful.

Machine menus now explicitly request a visible cursor; the standalone application releases and restores cursor grabbing while keeping UI input active. The exported-player crash was traced from the supplied log to AI attack leases retaining detached components. AI destruction now releases leases, and pruning safely rejects detached owners. Runtime files must be refreshed before a later user-authorized export. Existing exported executables are unchanged by editor/runtime installation alone.
