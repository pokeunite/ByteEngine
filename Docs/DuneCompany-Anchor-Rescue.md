# Down With the Patient

Third contract: `Scenes/AnchorRescue.bytescene`, reward $850. The existing tow and well contracts retain their own history. New history uses `anchor-progress-v1.json`, included in New Game reset. Selecting a contract shows details; only deployment changes scene.

The brief states the recovery request and unstable access bank, without prescribing a vehicle or explaining the two-cable solution. An eight-metre excavation, narrow loose access cut, lower bend and steel eye on the upper rock provide the problem. The casualty cannot be pulled directly from the staging area through terrain.

Powered winches now have independent physical cables. Each can attach to a casualty or a scene-authored `DuneWinchAnchor3D`. The hook includes the anchor connection; no additional inventory part is required. Connection picks the closest valid, unobstructed attachment within the standard 12m hook reach. Cable can then be extended to 24m. Released input holds the selected cable length. Anchor and casualty cables apply separate forces, including torque at the mount.

Per-block Attach, Reel in and Pay out keys are edited by clicking their buttons in the winch tuning panel. Defaults: first H/E/Q, second J/U/O. Intentional shared keys are labelled SHARED. Driving keys are reserved. Bindings live in each block's ControlKeys and survive blueprint/session saves, undo and load. Apply-to-same copies physical tuning, not bindings.

A diagnostic Anchor rescue truck is kept under `.byteengine/Diagnostics`, outside the player's blueprint library. The player must devise their own build. There is no two-winch inventory gate: completion is based on recovered vehicle delivery, not a prescribed loadout. R resets both vehicles for a retry at the staging area. The access cut includes a halfway resting ledge; loose grip is .3, packed grip 1.1.

Validation: isolated native diagnostic physically attaches the rear winch to the rock, lowers the truck down the access slope, attaches the front cable to the casualty, reels the casualty into line, and pulls both vehicles uphill. It verifies independent controls, cable holding and save/load. Depot poses are then reset to isolate one payment, history classification and automatic garage return; the driving journey to the depot is not automated. Existing two-contract regression checks also run. No game export is generated.

Authoring: `Tools/DuneCompany/author_anchor_rescue.py` edits only the actual Documents DuneCompany project, creates backups and preserves saved campaign progress.
