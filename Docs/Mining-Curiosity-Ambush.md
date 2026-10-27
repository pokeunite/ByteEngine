# Far-end spider nest

The Main scene now contains **Far canyon spider nest**, centred at `(10, 0, -131)`. Its trigger spans 70 metres across the approach and begins at Z −125, shortly before the far end of the navigable floor.

Entering the zone releases exactly **50 Alien Hunter spiders**, aimed at the player. They emerge around the disturbance at reachable navigation positions, nine to eighteen metres away. Releases are staggered over approximately six seconds to spread the cost of creating fifty enemies. Retreating does not cancel the releases. It fires once per play session; leaving and returning does not repeat it.

This is a roaming threat, separate from the drill contract's quota and concurrency limit. Starting drilling cannot delete it. Defences can target these enemies even between operations. If the swarm kills the operator, the existing failure/recovery flow clears it; retrying does not automatically release another fifty. No ambush state or extra rewards are written to the company save.

## Editing

Select **Far canyon spider nest → Curiosity Spider Ambush**. The player, operation, spider blueprint, trigger dimensions, enemy count, interval, radii and detection distance are editable. Moving/rotating the object moves/rotates its trigger volume. Live values show whether it fired, how many have been released and its status.

Event Sheet menu **Modular Weapon Gameplay → Curiosity Spider Ambush** exposes:

- Release curiosity spider ambush.
- Rearm curiosity spider ambush.
- Stop releasing ambush spiders.
- Curiosity ambush has been triggered.
- Curiosity ambush is releasing spiders.
- Player is inside curiosity ambush area.

Rearming is explicit; it can release another wave if the player is still inside. Stopping releases does not remove already spawned spiders. The live Mining Operation value **Roaming Enemies Alive** counts the surviving bonus enemies independently of contract enemies.

## Verification

Native checks cover the safe camp, the edge of the trigger, exactly fifty live enemies, valid AI targets, movement, defences firing between rounds, no repeat on re-entry, retaining the swarm when drilling starts, death cleanup and retry. The complete weapon/gameplay regression executable and installed editor play check were also run. Checks disable normal persistence and preserve the scene file.

Plugin version: **3.34.0**. No game export was made. Before/after packages and the scene backup are in the project's `.byteengine/Backups` folders.
