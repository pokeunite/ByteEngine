# Wildlings plugin

Gameplay systems for the Wildling Adventures prototype (GDD v0.2).

- Manifest id: `studio.wildlings`, assembly `Wildlings.Plugin.dll`
- Source files live beside this README; each component registers a
  `studio.wildlings.*` codec.

## Components

| Component | Purpose |
| --- | --- |
| `TeamManager3D` | three-slot party, 0.60 s switch throttle, camera handover, auto-switch on death |
| `WildlingStats3D` | species + quality multiplier, applies HP / speed / damage |
| `WildlingCombat3D` | LMB basic, RMB special, Q utility; player input or companion AI with the 0.75 factor |
| `WildlingCompanion3D` | formation spacing, retreat trigger and facing for non-controlled members |
| `WildlingStatus3D` | Burning, Root, Slow |
| `WildlingProjectile3D` | projectile with splash, Burning, Root and chain jumps |
| `WildlingHud3D` | party HP, cooldown readout, control hint (needs a `UiCanvas` in the scene) |
| `TrainerFollower3D` | noncombat trainer that trails the active member |

Species numbers live in `Species.cs` (`WildlingSpeciesDatabase`). Update the GDD
table there, not in the components. `WildlingStats3D.species` is the key
(`fire_lizard`, `spriglet`, `zappet`).

## Event Sheet nodes

`WildlingsEvents.cs` registers the systems with the ByteGraph Event Sheet, so
`.byteevents` modules can read and command them without C#:

| Node | Kind |
| --- | --- |
| `studio.wildlings.team.enterCombat` / `leaveCombat` / `toggleCombat` | action |
| `studio.wildlings.team.commandSlot` (`slot`) | action |
| `studio.wildlings.team.inCombat` / `isExploring` | condition |
| `studio.wildlings.team.activeSlotIs` (`slot`) | condition |
| `studio.wildlings.team.livingCountAtLeast` (`count`) | condition |
| `studio.wildlings.creature.setCombatEnabled` (`enabled`) | action |
| `studio.wildlings.creature.setSpecies` (`species`) | action |
| `studio.wildlings.creature.speciesIs` (`species`) | condition |
| `studio.wildlings.creature.isAiControlled` | condition |

All nodes take `target` (`Self`, object name, or `id:<guid>`).


Cross-object links are scene-object **names**, never GUIDs, so the inspector reads
`Wildling Manager` / `Fire Lizard` instead of raw ids:

- `TeamManager3D.slotOneName` / `slotTwoName` / `slotThreeName`
- `WildlingCombat3D.teamObjectName`, `WildlingCompanion3D.teamObjectName`,
  `WildlingHud3D.teamObjectName`, `TrainerFollower3D.teamObjectName`

## Build and install

```powershell
dotnet build Plugins/Wildlings/Wildlings.Plugin.csproj -c Release
```

The build writes `artifacts/plugins/Wildlings.byteplugin`. With MCP:

```json
{"plugin":"Wildlings","install":true}
```

`be_plugin(op:"build")` compiles and packages; `install:true` copies the package
into the active project and requires reopening the project so the assembly can
register its components.
