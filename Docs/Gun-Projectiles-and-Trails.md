# Gun projectiles and trails

Palclone's pistol, SCAR and shotgun now use physical projectiles. Damage happens at impact rather than immediately when the trigger is pressed. Fast bullets sweep their flight path so they cannot step through thin cover. The Crownback's back core must still be open when a bullet arrives.

## Change a gun

Select its weapon object and open **Weapon Definition**. Under **Projectiles**, choose Shot Mode, Projectile Speed, radius, visual size, lifetime and maximum active projectiles. Existing damage, spread, range, ammunition and reload settings still apply. Range also bounds flight lifetime.

Under **Projectile trail**, adjust trail length and width, core/glow colours, glow scale, brightness and impact fade. Show Shot Trace toggles the trail. These trails use cached primitive geometry with depth testing, a bright core and three tapered glow segments; they do not build particle atlases or add lights per bullet.

| Gun | Speed | Trail length | Width |
| --- | ---: | ---: | ---: |
| Pistol | 75 m/s | 2 m | 0.035 m |
| SCAR | 100 m/s | 2.6 m | 0.030 m |
| Shotgun | 60 m/s | 1.4 m | 0.025 m |

The impact fade is 0.07 seconds. Bullets originate at the existing model muzzle point; camera aiming still chooses their direction. Shotgun pellets retain individual spread and collision.

## Event Sheets

Weapon actions include **Set projectile speed and trail** and **Set bullet trail appearance**. Definition settings are also available as component properties. Hit feedback and damage events occur on arrival; firing, muzzle flash and audio still occur at launch. Existing hitscan mode remains available on each definition.

## Verification

Regression tests cover delayed damage, owner exclusion, thin cover, one hit per projectile, range limits on long frames, bounded projectile counts, impact event data and Crownback armour/core routing. Native rendering is checked separately in a runtime scene copy. No project combat logic is embedded in a temporary test script.

Re-export the game to include the updated plugin and weapon definitions in a standalone build.
