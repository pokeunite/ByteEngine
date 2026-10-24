# ByteAssistant — AI and weapon system support

24 October 2026

ByteAssistant now includes setup recipes for the native perception/tactics/staged-combat system, weapon projectiles and trails, muzzle points, carried inventory HUD and upper-body weapon animation bindings. Relevant recipes are retrieved on demand; the full guides are not injected into every request.

Its build-matched reference lookup includes the current AI/weapon guides and first-party weapon plugin source. Engine components, properties, conditions and actions remain discoverable from the live native catalogue.

## New native tool operation

`byte_apply` supports `create_ai_behavior` with:

- `name`, `path`: a new `.byteai` under the project's asset directory.
- `preset`: Basic, AttackStages, Melee, Flanker, Ranged, Caster, Charger or Boss.
- `sharedAttack`: an existing `.byteai` path or an asset alias created earlier in the batch. Omit it to inline stages in the role preset.
- `source`: copy an existing behaviour into a new variant.
- `tree`: a custom native definition with nodes, blackboard defaults and background services; this takes precedence over source/preset.

Use `byte_catalog` with `kind=asset_types, search=AI` for the schema and enum choices. `byte_inspect` can read `.byteai` assets. Unknown node settings, invalid child structures, unresolved sub-behaviour paths and out-of-project output paths are rejected. Native size, depth and node limits apply. It does not execute generated code.

The assistant still cannot overwrite an existing AI asset. It can inspect that asset, create a new editable variant, then assign the variant to the selected enemy. Failed batches restore the scene and remove generated files; scene Undo preserves successful new assets for recovery.

## Correct setup knowledge

Staged role presets need the existing combat component enabled with `BehaviorControlled=true`; simply disabling it also disables encounter combat execution. AIBehaviour's perception settings are separate. Navigation Agent starts with Externally Driven off and must reference a baked region. Scene encounter activation rules and existing damage/timing settings should be preserved.

Gunshots use Weapon Definition's Noise Radius. Other hearing stimuli require an explicit noise event; ordinary Audio Sources are not automatically perceived. Shared attack stages, services, tactical positions and interruption policies remain editable in the native WHEN/DO editor.

## Offline verification

- 20 new AI support checks: schema/guide/reference retrieval, preset and custom-tree creation, shared aliases, component binding, inspection, native validation, variants, Undo, rollback and path/overwrite protection.
- All 78 existing assistant regression checks passed.
- No model or API request was made by these tests. They establish tool correctness and available knowledge, not a guarantee about every model response.

Try: “Inspect this enemy and its current behaviour, then create a new ranged variant that searches when it loses sight. Keep my existing damage and encounter rules.”
