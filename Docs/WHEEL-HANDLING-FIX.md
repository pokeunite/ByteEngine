# Wheel handling correction — 2026-10-03

Gameplay evidence: user's 2026-10-03 16-47-50.mp4. The vehicle uses two large powered wheels and two unpowered wheels, then adds steering hinges.

Reproduced without hinges: front-powered vehicle veered 2.94 m sideways over 9.42 m forward, without steering input. Previous validation used four powered wheels and did not catch this configuration.

Correction in GoblinScrapper plugin 0.2.1:
- Lateral tire grip applies to powered and unpowered wheels, with or without steering hinges.
- Wheel motor force reduced from 120 to 60; other mechanism motors retain their force settings. Wheels still propel individual rigid bodies through axle torque.
- During fixed-axle differential steering, passive wheel scrub resistance is reduced so it does not oppose the driven axle. Straight-line grip remains unchanged. Shift drift remains available.
- Steering hinges retain their physical joint constraints and stops.

Verification: front-powered, rear-powered and four-powered layouts at 30/60/120 Hz drive straight, turn right without rolling, reverse, and brake below 0.3 m/s after four seconds. Mixed layouts cover about 7.3 m in four seconds with negligible sideways drift. Contraption regression covers hinges, attachment continuity, gears, passive wheels, projectiles, flight and every selected block.

The editor is running and must be fully restarted to unload its existing plugin assembly. Installed canonical package: C:/Users/codex/Documents/Goblin Scraper/Plugins/bytebard.goblinscrapper.byteplugin.
