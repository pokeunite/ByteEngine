# First hunt — combat feedback and pressure

## Controls and editing

Automatic reload is enabled for all three guns. When an equipped magazine runs empty, its final-shot cooldown finishes, then the normal reload starts if reserve ammunition remains. R still reloads manually. An empty reserve does not cause a reload loop. Unequipped guns do not auto reload.

On each weapon, **Weapon Definition → Auto Reload** exposes the option. `Assets/Events/Weapon Audio and Auto Reload.byteevents` also enables it through a named startup rule; disable that rule if you want to control the component setting independently. It contains separate shooting, reload-start and reload-cancel rules for every gun. Shooting uses native overlapping one-shot voices with slight pitch variation. Reloading uses a separate stoppable source.

The sound template objects are under **First Hunt - encounter director**, named `<weapon> shot/reload sound - editable clip and mix`. Change the Audio Source clip or volume there. All six files are in `Assets/Audio/Weapons/First Hunt`. See `Weapon-Audio-Sources.md` for source recordings and CC0 licences. The short shotgun mechanism cue is a prototype substitute for a shell-insertion recording.

**Combat feedback - damage numbers and knockback** contains the native **Combat Hit Feedback** component. Edit number size, height, rise, lifetime and colours, knockback speed/decay, and display/knockback toggles there. `Configure damage numbers and hit knockback` is also available in Event Sheets.

Damage numbers show actual health removed, aggregate same-frame shotgun pellets per victim, and remain visible on killing hits. Recovery damage appears gold. Knockback follows the hit direction and stops at cover. Each enemy's **Health → Knockback Multiplier** controls resistance: normal enemies 1, charging brute .25, Crownback .08.

## Pressure changes

- Trail rushers: speed 3.7; other rushers: 4.3. Melee windup .38 sec, cooldown .85 sec.
- Blue spitters: pursuit 2.7, preferred range 5.5, projectile speed 9, shot cooldown 1.35 sec, damage 10.
- Violet ground caster: pursuit 2.4, warning .85 sec, strike radius 2.3, attack cooldown 1.65 sec.
- Chargers: pursuit 2.8; charge speed 10, boss 12; warning .95 sec; recovery 2 sec; cooldown 1.8 sec.

Enemy health and the maximum 10 normal enemies plus one boss remain unchanged. All pressure values are saved in the enemy components rather than hidden in the hunt script. Timed manual playtests are still needed to judge pressure and feel.

## Verification

Tests cover auto-reload reserve transfer and guards, pellet popup aggregation, knockback direction and wall blocking, and killing-hit popups. The native weapon regression suite also passes. A saved-game rendering check exercises an actual gun hit, popup projection and the sound event.
