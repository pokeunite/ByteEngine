# Quick mining interactions

The reference recording was reviewed for its short menu visits, world-visible production and enemy arrival direction. The mining prototype now uses this flow:

- Credits stay at the top left. Refined and raw ore are labelled separately immediately below.
- **Ammo press:** production visibly ejects bounded visual rounds into the collection bin. Walk to the bin to transfer available ammo into the equipped gun's reserve, including during a wave. Full reserves leave the remaining rounds in the bin. Press **E** near the machine to open its upgrade panel; collecting no longer requires a panel or Collect button.
- **Refinery:** a compact side panel shows batch size, processing time, yield, exact current/next values and prices. **Refine & Return** begins the batch and immediately returns to play. The batch continues in the world. Upgrade purchases keep the panel open for further purchases.
- **Company tent:** **E** opens the upgrade graph directly. The selected upgrade's effect, prerequisite and buy button remain beside the graph. **Market** switches directly to trading; **Upgrades** returns directly. The intermediate company information page is bypassed. Select a node and press **Enter**, or click Buy. Enter does not duplicate a focused button's purchase.
- **Escape:** when a menu owns the pointer, the application lets the menu consume Escape. Closing restores mouse look immediately; the user does not have to click the viewport again. The usual Escape-to-release-input behaviour remains when no menu is open.
- **Enemy arrival:** all five spawn lanes are beyond the front of the camp, more than 30 metres from the drill. The floor, perimeter and navigation region have been extended to make that approach traversable. The old camp-side emergence pads are disabled. Current enemy population limits remain unchanged.

## Editing

The authored spawn points and navigation region are editable in the scene. **Ammo Collection Bucket** on the press exposes its workshop reference, bucket/ejection offsets, collection radius, visual budget, rounds represented by each visual and automatic-pickup switch. The existing workshop balance is the single source of ammunition stock, so the bucket cannot duplicate ammunition. The pool is capped at 60 simple visual objects and uses no per-round rigid-body simulation.

The Event Sheet action **Collect produced ammunition** is available for custom interactions. Existing purchases, refinement, opening/closing and terminal transactions remain Event Sheet actions. Current/next previews and compact hitboxes use the same screen layout, including changed aspect ratios.

## Verification and backups

`Tools/MiningPrototype --verify-cursor` posts Escape to its own native test window and checks that the menu closes and grabbed mouse look returns without a click. `--verify-ui` checks real button clicks, automatic bucket collection, direct terminal tabs, purchase accounting and refinery output. `--verify-flow` checks navigation from every front lane and actual enemy progress toward the camp. The authoring tool saves a backup under `.byteengine/Backups/compact-flow-*`; tests disable company save writes. No game export is performed.
