# Dune Company — tester polish

Get the current recovery-and-racing loop into testers' hands. Finish the fixes below before adding contracts, parts or engine features.

## Fixes in this pass

- [x] Web sand: visible tyre grooves, darkened troughs and readable edges; keep real deformation.
- [x] Web performance: use adaptive spring-arm solving, reuse mesh buffers and avoid resending unchanged indices. Preserve the extraction geometry.
- [x] Confirm spring arms still hold heavy tyres upright while turning and landing.
- [x] Confirm recovery, racing, payment and automatic garage return still work.

Installed plugin/runtime: **0.21.0**. The [presentation pass](DuneCompany-Presentation-Polish-Pass.md) adds garage materials/dressing, field scenery, main-menu artwork, contract/HUD layouts, camera/input settings, feedback and browser contact shading. Native checks cover recovery, spring-arm loads, menu flow and racing/economy; Chrome checks cover onboarding, real input rebinding, suspension, track texture data and graphics errors. A previous local comparison cut average mesh upload bytes by about 41%. Browser physics remains expensive with multiple spring arms; sustained frame time on itch still needs work and a clean capture.

## Check before sending the build

- [x] Guard deferred browser uploads against disposed/resized texture data and initialize GPU storage before partial updates. Regression reproduces the old out-of-range exception; three Chrome test-drive/garage return cycles with window resizing pass without graphics errors. The tester's exact post-contract sequence still needs confirmation on itch.

- [ ] Fresh-player run: Blueprints → first car → Build → Garage → first contract, with no tutorial obstruction or dead end.
- [ ] New Game confirmation clears contracts, money and unlocks; Continue restores them. Keep testers' saves isolated from editor saves.
- [ ] Buy a part and tuning once; restart and confirm both stay unlocked, with no repeat charge.
- [ ] Play every existing contract from acceptance to payout. Check failure, retry, recover, quit and garage return too.
- [ ] Browser: first load, refresh, fullscreen, pointer focus and switching tabs. Test cold and warm caches on itch, not just localhost.
- [ ] Record a representative ten-minute browser session on the Vega laptop. Aim for sustained 30 FPS; flag repeated frames over 50 ms. Include garage, two winches, spring arms and the race.
- [ ] Check 1280×720 and a smaller browser window: wallet, part description, tuning panel, tutorial, speedometer and action labels must fit without overlap.
- [ ] Controls are visible where needed: winch attach/reel/release, custom bindings, recover, brake/drift and return. Describe missing equipment without revealing the puzzle solution.
- [ ] Remove remaining grey blockout props, floating hitch markers and blue pads. Anchors must stay on their rocks; start/end areas should read as actual locations.
- [ ] Brief audio pass: audible engine, impact, cable attachment, UI click and victory sound; no doubled sounds or clipping. New SFX must be CC0 from Freesound, with source/license recorded.
- [ ] Confirm the exported build carries the displayed plugin version and newest assets. Existing uploads do not update themselves.

## Improve from tester feedback

- [ ] Clearer mission landmarks, readable rescue targets and hitch contrast in bright sand.
- [ ] Cleaner winch feedback: target, attachment, tension and why an attachment failed.
- [ ] Consistent part thumbnails and exact upgrade/tuning benefits before purchase.
- [ ] Shorten repetitive loading and retry waits; keep contract browsing immediate.
- [ ] Tune the race rival and No Brakes from completion times and failure causes, rather than guessing difficulty.

## Send with the build

- [ ] Mark it as a test build; include version, controls and how to export a performance capture.
- [ ] Ask testers: Where did you get stuck? What felt unfair? Which rescue was most fun? What purchase felt useful? Request a screenshot/video and performance capture for bugs.
- [ ] Collect device/browser, contract, vehicle setup and reproduction steps. Prioritize crashes, lost saves, blocked progress and unreadable controls before cosmetic requests.

Fresh exports/upload and human playthrough items remain unchecked until actually performed. Do not erase player saves to perform these checks.


