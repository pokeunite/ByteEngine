# Delivery contracts and the camp tutorial

At the company tent open **Market → Delivery contracts**. Select a buyer, gather its specified amount of **refined ore**, then return and press **Deliver order**. The accepted order stays saved while you drill and refine. The whole order ships together, locks its payout and pays automatically; no second collect click is required. There is no deadline or financial penalty in this first version.

Initial orders are 20 refined ore for 90 credits, 40 for 200, and 80 for 440. The 80-ore order requires the Freight upgrade. Live selling remains separate and still lets you choose a quantity and accept the displayed market price. In-flight sales from older saves continue to settle normally.

The Workshop's **Delivery orders JSON** exposes buyer names, quantities and prices. **Sale seconds** controls shipment time. **Base delivery capacity** and Freight control available order sizes. Event Sheet actions can accept an order by index (0–2), deliver the accepted order, and check whether its cargo is ready. Contract terms, stock reservation and payout share the operation save to avoid duplicate ore or payment after reload.

Start **New Game** to see onboarding. Existing companies are preserved and do not replay it. It guides the player to the ammo bucket, explains the first collection, directs them to the drill, waits for their first win, then guides refining and the first refined-ore sale. It completes after payout. Losing the first operation guides the player back to retry. Guidance pauses behind business menus and the pause menu.

The route follows baked navigation when available, refreshes once per second, follows moved machines, and stops outside their footprints. Only the nearest 24 metres are drawn. Without a valid baked path it shows a short direction marker rather than pretending a straight route through obstacles is walkable.

On Mining Operation, expose/adjust **Tutorial enabled**, **Show tutorial route**, and **Tutorial starting reserve** (12). New companies begin with space for the first ammo pickup. The tutorial stage is saved. Event Sheets can test its current step and finish it explicitly.

On Mining Audio, **Drill near distance** (3 metres from its collider surface) and **Drill far distance** (65 metres) control a smooth fade. The drill applies distance to the actual player rather than relying on stereo audio spatialization. Its volume still obeys the machine volume, drill multiplier and SFX mixer.
