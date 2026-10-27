# Camp interaction and rearranging

Installed in palclone with Modular Weapon Gameplay 3.33.0.

The drill prompt measures distance to its compound body colliders, rather than one control-panel anchor. E works from any reachable side. The press and refinery use the same surface-distance interaction. The company terminal retains its computer interaction point.

Between operations, close menus and press **Tab** to arrange the camp. Alternatively, hold **Alt** and click **Arrange Camp**. The overhead view releases the cursor and freezes player movement and shooting.

- Click the drill, ammo press, refinery, company tent or an existing placed defence.
- Move the mouse to position its translucent preview. The original stays put until confirmed.
- **Q / E** rotate by the component's Rotation Step, initially 15 degrees.
- Left click places. Right click or Escape cancels the current preview; with no preview selected, it exits.
- **Tab** or **Done** returns to gameplay.

Moves preserve object identity, child transforms, interaction anchors, collision, inventories, machine processing and defence allowances. There is no relocation charge. Overlapping objects, reserved player arrival space, static obstacles and positions outside camp bounds are rejected. Navigation rebakes after placement; moves that remove an enemy route to the drill are rolled back.

Machine poses save independently in `mining-camp-layout-v1.json` in the game's normal save directory. Defence positions and yaw use the existing defence save. The scene file and company progress are not rewritten by rearranging. Before scene setup, an original scene backup was created under `.byteengine/Backups/camp-arrangement-*`.

Editable components:

- **Camp Arrangement** on the player: operation/interface/navigation bindings, build bounds, grid size, rotation step, camera width, arrival clearance and save slot.
- **Movable Camp Object** on each machine root: display name, allow relocation and clearance. Put it on the root that owns all moving children.
- Event Sheet actions under **Camp Arrangement**: open/close, finish, select object, confirm/cancel and rotate. The **Camp arrangement is open** condition is also available.

Native verification covers four-side drill interaction, mouse picking, rotation, every machine, defence relocation, no-cost licence retention, saved layouts, invalid placement, ghost cancellation, control restoration and prevention during operations. Existing market/machine/defence UI checks and installed-editor play/cursor checks passed. The full weapon/event regression suite passed. No game export was made.

## Environment direction

The approved concept remains a reference, not an implemented level: faceted low-poly rocks and clean materials, continuous impassable cliffs/boulders across the back and both sides, and one broad forward valley. Enemies approach from far in front. Avoid gaps, climbing steps or attractive routes behind the physical camp boundary. Keep the camp floor spacious enough to rearrange its machines.
