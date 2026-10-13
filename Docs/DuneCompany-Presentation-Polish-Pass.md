# Dune Company presentation pass — 0.21.0

The direction is a desert recovery workshop: weathered industrial equipment, clear recovery jobs, warm daylight and readable machinery. This is a substantial polish pass, not a claim that the game now meets a AAA production standard.

## Authored in the game project

- [x] Replace the noisy orange garage wall material with galvanized steel and quieter wear.
- [x] Rework concrete, metal roughness, practical lights, daylight fill and exposure balance.
- [x] Add a painted work bay and workshop branding without restoring the removed coloured start pad.
- [x] Add depot wayfinding, survey/service equipment, drums, mission landmarks and distant scanned outcrops.
- [x] Dress existing equipment obstacles while retaining their collision footprint.
- [x] Make the broken-bridge site read as damaged infrastructure.
- [x] Add restrained field artwork for all five existing jobs.
- [x] Give the main menu a clean in-game garage hero image, readable shading and a clear recovery-game promise; replace the old stack of gradient panels.
- [x] Correct upgrade descriptions, including removing the misleading per-tyre purchase wording.
- [x] Add a small CC0 Freesound desert-wind loop; retain the chosen garage music.

Project: `C:/Users/codex/Documents/DuneCompany`. Art and scenes are backed up under `.byteengine/backups/presentation-*`. The authoring script is `Tools/DuneCompany/presentation_polish.py`; it never exports games or modifies player saves.

## Gameplay presentation

- [x] Two-column contracts: readable job cards beside the briefing, payment, equipment and deployment action.
- [x] Keep the selected briefing open when its card is clicked again.
- [x] Fill the build inspector with selected-part information and vehicle readiness.
- [x] Separate speedometer, recovery controls, wallet and contract exit controls.
- [x] Compact, unobstructive tutorial guidance with the existing starter-blueprint flow.
- [x] Brief reward card communicates automatic garage return.
- [x] Purchase confirmation, hover feedback and mission-start feedback.
- [x] Visible cable tension and attachment state; clearer long-line range failure feedback.
- [x] Replace cube anchor/socket visuals with steel eyes and cube pit stones with scanned rock visuals.
- [x] Pulley motion and subtle engine-casting vibration affect render geometry only.
- [x] Adjustable visual camera motion, damped speed FOV, impact response and orbit sensitivity.
- [x] Save driving key bindings, reject conflicts with fitted winches, and support restoring defaults.
- [x] Live Fast/Balanced graphics selection and audio-bus settings.
- [x] Correct browser sky intensity; compensate ambient lighting and add bounded vehicle contact shading for its lightweight renderer.

Existing tyre dust, sand grooves, layered engine/impact audio, distinct upgrade models, rope geometry, physical opponents and ordered race gates remain part of the presentation. They are not newly invented in this pass.

## Verification and practical limits

- [x] Native layout bounds at 1280×720, 800×600, 1920×1080 and 2560×1080.
- [x] Native renders of garage, builder, contracts, settings, driving and reward presentation.
- [x] Saved rebound forward key drives the vehicle; reward presentation returns to the garage.
- [x] Existing recovery jobs, dual-winch rescue, No Brakes retry/payment and onboarding checks.
- [x] Ordered race gates, physical rival, payment, economy reload and campaign reset.
- [x] Browser menu, settings, actual key rebinding/persistence, deployment and driving checks.
- [x] Browser fresh-player tutorial through starter selection, build, garage and first deployment; tutorial completion persists.
- [x] Final browser lighting/contact-shading render, suspension/sand checks and installation hashes.
- [x] Native main menu, New Game, Continue, settings, credits, contract scene loading and menu return.
- [ ] Sustained browser frame-time target with several spring arms under load. The current local spring fixture still spends substantial time in the physics solver and drops simulation time; this is not marked solved.
- [ ] Fresh export/upload and an uninterrupted tester playthrough on itch.

The native extraction test briefly drives away while reeling before braking on the apron. Holding brakes on loose sloping sand is not equivalent to anchoring the truck. This adjusts the test's driving inputs, not the game's physics or mission difficulty.

The local browser smoke capture includes UI navigation and resolution changes; its FPS is not a controlled performance benchmark. In the separate four-spring-arm fixture, sampled solver work reached about 44 ms and the bounded catch-up loop dropped simulation time. Stability and visible tracks passed, but browser physics performance remains a release concern. Native and browser rendering still have different capabilities. Browser contact shading is a lightweight visual treatment, not full dynamic shadow mapping.

Sound: **Looping Gentle Wind Ambience on an Open Desert Plain** by dhallcomposer, [Freesound](https://freesound.org/people/dhallcomposer/sounds/697217/), CC0. Source and conversion are recorded in `Assets/Audio/Ambience/DESERT-WIND-LICENSE.md`.

## Follow-up fixes � 0.21.1

- Excavation retaining-wall placeholder visuals replaced by scanned stone; collision footprints retained.
- Ladder landmark moved to terrain-sampled open ground with conservative clearance from rocks.
- Functional steel eye transforms fitted to actual scanned triangles, keeping rock parenting and cable targets together.
- Reward card reduced to 340 � 102, with heading and accent stripe fitted to the new size; automatic garage return retained.
- Light knocks and heavier crashes now use separate CC0 Freesound clips. Normal acceleration is excluded from impact detection.
- Native presentation/automatic-return and recovery, anchor descent, No Brakes diagnostics passed; native placement screenshots inspected. Browser UI/deployment/driving smoke passed. Runtime/package hashes verified at 0.21.1. No game export or upload created.

Audio: [Car Crash by squareal](https://freesound.org/people/squareal/sounds/237375/) and [Metal Hit 2 by dslrguide](https://freesound.org/people/dslrguide/sounds/321482/), both CC0. Conversion/license records accompany each WAV.
