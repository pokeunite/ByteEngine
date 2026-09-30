# Goblin Scrapwar — playable whitebox

Goblin Scrapwar is a small arena prototype built inside ByteEngine's Sandbox.
The player drives a breakable four-wheel machine through a 500-goblin
Red-vs-Green tug-of-war, harvests pooled scrap pickups, and installs armor,
a powered saw, or a catapult while the battle continues.

## Launch

From the repository root:

```powershell
dotnet run --project Sandbox/ByteEngine.Sandbox/ByteEngine.Sandbox.csproj -- --goblin-scrapwar
```

Or run `Samples/GoblinScrapwar/Launch-GoblinScrapwar.ps1`.

The first run restores BEPU Physics 2 for the Sandbox. The main engine remains
independent of BEPU; the adapter is confined to the Sandbox game.

## Controls and loop

- WASD: drive and steer. The camera follows the cockpit.
- Tab: enter/exit build mode. The machine freezes while the battle keeps moving.
- Build mode: 1 selects iron armor (5 scrap), 2 selects powered saw (12 scrap,
  mounted to armor), 3 selects catapult (15 scrap, mounted on top). Point at the
  glowing socket, inspect the translucent part ghost, then left-click to install.
- Space: launch a physical catapult stone when the catapult is attached.
- F5: save the current machine topology to
  `%LOCALAPPDATA%\ByteEngine\GoblinScrapwar\machine.json`.
- F9: restore the supported armor/saw/catapult build configuration from that
  blueprint. The generic engine blueprint round-trip preserves part/socket/link
  IDs; the game instantiates fresh runtime IDs.
- Esc: release the mouse.

Run over Red goblins or use the saw/catapult to make scrap drops; drive through
the gold drops to harvest them. Ram or shoot the iron watchtower to scatter 20
more. Green and Red infantry advance toward opposing bases; a base at zero
health ends the match. Detached machine parts remain as physical debris.

## Architecture

- `Engine/ByteEngine.Core/Construction/AssemblyGraph.cs`: generic,
  physics-independent socket/channel graph with connected components.
- `AssemblyBlueprint.cs`: versioned JSON topology persistence and validation.
- `AssemblyPlacementRules.cs`: mass-budget and conservative overlap gate.
- `AssemblyEventBus.cs` and `AssemblyIntegrity.cs`: typed part/link/break events,
  measured joint overload, and detached-component traversal.
- `SwarmFlowField.cs`, `SwarmHorde.cs`, `SwarmRenderer3D.cs`: shared 2D
  navigation, pooled packed agents/pickups, and two mesh submissions per horde.
- `ImpactParticles3D.cs`: fixed-capacity impact debris in one dynamic mesh.
- `Sandbox/ByteEngine.Sandbox/Goblin/GoblinPhysicsWorld.cs`: BEPU-only bridge
  for dynamic boxes/spheres, fixed, hinge, and spring constraints.
- `GoblinScrapwarGame.cs`: arena, camera, HUD, input, upgrades, impacts, match.

## Automated checks

```powershell
dotnet run --project Tests/ByteEngine.Tests/ByteEngine.Tests.csproj -- --construction
dotnet run --project Sandbox/ByteEngine.Sandbox/ByteEngine.Sandbox.csproj -- --goblin-physics-test
dotnet run --project Sandbox/ByteEngine.Sandbox/ByteEngine.Sandbox.csproj -- --goblin-benchmark
```

The benchmark measures only the 500-unit CPU swarm step; it does not prove
rendered frame rate. On the development machine, two 600-frame runs averaged
0.26–0.45 ms per swarm step and about 0.1 B/frame allocated. GPU rendering and the
playable scene still require human testing.

## Manual playtest gate

1. Launch the game. Confirm 500 red/green goblins and the machine are visible,
   the HUD reads clearly, and WASD drives the vehicle without explosive wobble.
2. Ram Red goblins, then turn back through the gold pickups. Scrap must increase
   only on collection.
3. Press Tab. Select 1; point at the front socket. Confirm the translucent
   armor ghost aligns, then click with at least 5 scrap. Drive again.
4. Repeat with 2 for the saw on the armor. Verify it spins and kills at its
   physical position. Select 3 for the top catapult and fire with Space.
5. Smash the watchtower and collect its debris. Hit the machine hard enough to
   lose a part; detached debris should fall, not remain in the cockpit graph.
6. Press F5, alter the build, then F9. Confirm the three supported upgrades
   match the saved connected build.
7. Let either base reach zero; verify a clear winner and no more reinforcements.

## Known limits

This is a playable whitebox, not an art-complete jam submission. A fresh
manual playtest has not yet verified rendering, camera, cursor picking, frame
rate, or game feel. The in-game catalog contains three upgrades, not arbitrary
user-authored part types. The blueprint loader restores those upgrade kinds,
not arbitrary authored transforms. Scrap pickups use pooled lightweight
visuals rather than separate rigid bodies; tower and projectile gameplay
hits use conservative body-overlap checks. Impact debris is a simple
single-color particle effect. There are no authored sounds, menus, or
polished arena assets yet.
