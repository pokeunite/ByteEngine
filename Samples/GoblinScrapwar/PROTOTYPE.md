# Goblin Scrapwar — first playable gate

The pitch is a slapstick war machine that the player builds and drives through
a crowd, earning scrap from impacts and using it to bolt on one new part.
The target is Windows desktop, mouse/keyboard, one short arena session.

## Player promise

- **Fantasy:** be a reckless goblin engineer who tips a battle with a machine.
- **Feeling:** heavy, surprising, hilariously fragile.
- **Core loop:** spot an opening → steer the contraption → squish infantry →
  collect scrap → attach a part → risk a bigger impact.
- **Primary verb:** smash. Supporting verbs: build, steer.
- **Signature moment:** a heavier machine plows through a line of enemies,
  then loses a wheel and spins apart.

## What is implemented

The construction foundation in `Engine/ByteEngine.Core/Construction` supplies
socket/channel topology, connected-component traversal, socket alignment math,
a measured-load integrity boundary, shared 2D flow fields, pooled infantry,
and a swept vehicle impact zone. The focused test command is:

`dotnet run --project Tests/ByteEngine.Tests/ByteEngine.Tests.csproj -- --construction`

This is **headless logic**, not a playable game. The test with 500 agents
establishes functional correctness only; it is not a 60 FPS benchmark.

## Blocking engine gate

`PhysicsWorld3D` is a linear-only rigid-body solver. It has no angular inertia,
torque, hinge/fixed/spring joints, or measured joint reaction forces. The
`IAssemblyConstraintAdapter` interface intentionally has no fake ByteEngine
implementation. A driveable breakable vehicle needs a real rotational
constraint solver or an integrated physics library before this pitch can be
honestly claimed as playable.

## Smallest playable proof

1. Add real rigid-body rotation and one measured fixed joint.
2. Build a cockpit, axle, and wheel from placeholder primitives. Drive and
   collide with a wall; verify a joint breaks at a repeatable threshold.
3. Put 50 pooled enemy markers in a small arena. A swept impact removes only
   enemy units and emits a scrap reward.
4. Let the player attach one replacement wheel using the same socket graph.
5. Record a 10-second unedited clip and test whether fresh players can explain
   why the machine changed and why it failed.

Pass: a new player can steer, smash, earn, attach, and suffer one comprehensible
breakage without developer explanation. Fail: the vehicle cannot be controlled
or impacts/breaks look arbitrary. Only after that gate should the team scale
unit counts or add catapults, watchtowers, a save/load workshop, and particles.

## Not now

500 visible units, per-goblin rigid bodies, many materials/part types, campaign
progression, complex audio/particle polish, generic public editor tooling, and
cross-engine portability. None rescues an unproven vehicle toy.
