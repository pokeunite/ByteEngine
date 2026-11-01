# Hazard Pay: game-feel review and next pass

This comparison uses the current scene, enemy blueprints and gameplay source, plus native menu/tutorial previews. It is a system audit, not a claim that a new full combat playtest has established the subjective feel.

The player promise is running a scrappy mining business under attack. The repeating loop is prepare → defend → recover ore → refine → sell → reinvest. Every step should communicate an immediate response, a visible result and a reason to do it again.

## What the research suggests

[Pichlmair and Johansen's survey](https://arxiv.org/abs/2011.09201) separates physical tuning, amplification of important events, and support for player intention. That makes control reliability and fewer interruptions as important as more spectacle. [Lin et al.'s impact-feedback study](https://arxiv.org/abs/2208.06155) identifies sound coherence, camera control and hit stop as potentially important influences on impact feel; these are useful hypotheses, not proof that every hit should shake or freeze the screen. [Valve's description of playtesting](https://www.l4d.com/blog/post.php?id=1849) distinguishes observing unfamiliar players to improve the experience from late stability testing. Hazard Pay needs both.

## Comparison with the current game

| Area | Current evidence | Gap / next experiment | Pass condition |
|---|---|---|---|
| Shooting response | Projectile trails, muzzle effects, weapon recoil, accuracy bloom and actual impact audio exist. | Add a subtle hit-confirm tick at the crosshair, and a distinctly different kill confirmation. Trigger on actual damage, never merely on firing or hitting scenery. | A player can distinguish miss, hit and kill without reading damage numbers. |
| Spider reactions | CombatFeedback3D shows numbers and pushes surviving enemies; scene knockback speed is 4.8, decay 24. Animated spiders support death and explosion knockdown. | Ordinary hits lack a dedicated brief flash on the **skinned spider**. The generic armour flash affects MeshRenderer parts, not the animated skeletal renderer. Test a 50–80 ms flash plus a small local body recoil, preserving attack rules. | Ordinary hits feel connected without hiding silhouettes or repeatedly stunning the whole crowd. |
| Player damage | Hurt sounds and the HP display exist. | Test a short directional edge cue and stronger low-HP emphasis, with sound priority above machinery. | During combat, the player knows immediately that they were hit and roughly from where. |
| Camera | Third-person aim zoom, FOV smoothing, recoil-driven aim point and obstruction feedback exist. | First verify responsiveness and readable aiming. Then test a tiny, bounded shoulder kick for the pistol; stronger distinct impulses for shotgun and explosions. Add a shake intensity setting. | No loss of target tracking or motion discomfort; an unchanged shot spread remains predictable. |
| Attack readability | Authored hunter windup is 0.32s; rig raider windup is 0.5s. Separate recovery/cooldown knobs exist. | Match visible anticipation to the actual damage moment. Use a distinct short cue for an imminent rig strike; avoid identical cues from every spider at once. | A first-time player can identify a threat and act before damage, instead of learning only from HP loss. |
| Machine presence | Layered machine, conveyor and material-impact audio and VFX exist. Drill now has an explicit player-distance envelope that also works for stereo recordings. | Test the mix in a busy wave: shots, danger and hurt must remain readable above the drill. Consider modest ducking rather than turning every source up. | Nearby machines feel substantial; distant machinery recedes; important combat cues remain audible. |
| Ammo reward | Bounded visible ammo flights, walk-up pickup and a pitched reload cue exist. | Show a brief `+N reserve ammo` beside the ammo counter, tied to the actual collected amount. | Pickup is obvious without opening the press menu or counting bullets. |
| Economy reward | Refinery progress, ore movement, price comparison and sale receipts exist. Delivery now has buyer orders, required cargo, acceptance and fulfilment. | On payout, animate only the changed credit amount and show a short receipt. Keep notifications out of buttons and objective text. | The player immediately understands what earned money and how much. |
| First-time understanding | New-company tutorial follows ammo → drill → first win → refinery → company sale. One ground route and one instruction are shown at a time. | Observe a new player without prompting. Existing saves skip onboarding; new games start it. | They finish the first sale without external help and can repeat the loop without the guide. |

## Recommended order

1. **Combat confirmation:** actual-damage hit tick, separate kill cue and skeletal hit flash. This closes the clearest feedback gap without changing balance.
2. **Danger clarity:** directional player-hit cue and a readable, distinct rig-attack tell. Check cue priority with the drill running.
3. **Reward clarity:** `+ammo`, credit delta and one compact round result. Keep automatic collection and payout fast.
4. **Camera tuning:** small recoil impulse only after the above is readable. Do not add a global time freeze on every bullet: that can make a crowded shooter stutter and slow the loop.

These are proposed next changes, not silently installed combat/balance changes. Test each pass against the existing version before keeping it.

## Short playtest

Use a fresh company and the first three operations. Record gameplay and ask the player to narrate only when confused. Note: time to first ammo pickup; whether they need help starting the drill; whether they can tell hits from kills; what they believe damaged the rig; time spent looking for the refinery; whether they understand an accepted order versus a live sale. Also check frame-time spikes with effects active, not only average FPS. The goal is clearer cause and consequence with fewer interruptions, not maximum particles.
