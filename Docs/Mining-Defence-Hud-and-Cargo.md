# Defence HUD, cargo capacity and balance — 3.29.0

## Audio

The pistol and defence shot bindings use the original `Assets/Audio/Weapons/First Hunt/pistol-shot.wav` again at the original 0.48 volume. Reload recordings and bindings are unchanged. Purchase confirmation and shipment reward now use the approved notification recording instead of the rejected confirm/coin clips.

The Mining Audio component exposes Drill Volume Multiplier (2.5), Press Volume Multiplier (3), Machine Hearing Distance (8 m) and Machine Rolloff (0.7). These lift the machines above the previous very quiet mix without changing notification volume. They remain positional and stop with their operating state.

## Discoverable defences

Unlocked defences appear below the drill-health display on the right, with an icon, name and available quantity. Unresearched items remain hidden. Newly available stock highlights briefly; fully deployed items show that status rather than disappearing.

Press F1 for sentry, F2 rotary gun, F3 mortar, F4 mine or F5 supply drop. Alternatively, hold Left Alt to release the pointer and click a ready slot. Selecting a slot returns to placement: aim at valid nearby ground, left click to deploy, right click to cancel. B still cycles available types. Pressing a slot only selects placement; it never spends a deployment or fires the gun. All quantities use the planner's actual researched allowance and placed/consumed ledger.

The hotbar uses native UI buttons connected to Select defence placement actions in the Mining interface Event Sheet. Hotbar widgets cannot intercept firing with a hidden captured pointer. HUD pointer key is editable. The existing planner exposes placement range, spacing and build key.

## Readability and cargo

Operation status and nearby E interaction prompts have opaque dark backing cards. Cargo appears below the ore totals with stored quantity / capacity, a fill bar and remaining space. Amber marks reserved incoming extraction or extra refinery yield; the caption identifies reserved space. At 80% committed capacity it turns amber, and at capacity it turns red. Shipments lower actual stored cargo as their units are processed, so this bar updates during combat too.

Mining Operation exposes Cargo Used, Cargo Reserved Space, Cargo Fill Fraction and Cargo Full; a Cargo hold is full or reserved condition is available in Event Sheets. Raw input already inside the hold is not double-counted while refining: only the output's net extra space is reserved.

## Difficulty

Base hunters: 50 HP and 5.0 m/s. Base rig raiders: 75 HP and 3.8 m/s. Deep vein multiplies HP by 1.2; Overdrive by 1.45. Enemy health also gains 6% of base per completed operation, capped at twice base before the contract multiplier. Contract cards show the actual health and movement values for the next wave. These values are captured at launch and applied to actual spawned actors, alongside the existing attack and dodge settings. All growth, health and speed fields are editable on Mining Operation and in Contracts JSON.

## Background shipments

An order queued at the terminal continues selling while the next extraction runs. The launch button no longer waits on the shipment. Queued quantities and the locked quote remain unchanged; only that reserved quantity is paid, exactly once. Physical cargo capacity still limits the next haul. New shop orders and manual instant trades remain between operations; this change applies to fulfilling an order already placed.

Shipments of previously banked cargo also finish after a failed wave. Current-wave ore is never awarded on failure. Quitting still leaves the unprocessed remainder in storage, with processed units and credits saved together.

## Validation

Regression checks cover spawn health/speed agreement, cargo reservations, background shipment conservation, duplicate-payment prevention and existing mining systems. Native UI checks unlock a real sentry licence, verify the new slot, select it by Alt-click and F1, and confirm that selecting the HUD never fires the weapon. Native audio checks verify the restored source and mix, and the gameplay check exercises navigation and actual attacks. Scene authoring creates backups and does not modify company save files. No game export is performed.
