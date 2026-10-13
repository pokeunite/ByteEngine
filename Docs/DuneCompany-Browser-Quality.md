# Browser quality and race feedback — 0.22.0

The UI has a display-resolution canvas, capped at 2× display density. TrueType glyphs are baked at twice their logical size. World resolution changes preserve UI layout, pointer coordinates, simulation timing and text resolution. Small build/drive instructions are larger, with regular Barlow for body text.

Garage Settings has separate Graphics and World Resolution controls. Both are saved in menu-settings.json. Older Fast/Balanced-only saves migrate to Balanced. New games default to Balanced. Main-menu Graphics also cycles the three tiers.

| Quality | Auto world budget | Sun shadows | Texture filtering |
|---|---|---|---|
| Low | 1280 × 720 | Contact shade only | Trilinear |
| Balanced | 1920 × 1080 | 1024 px, PCF | Up to 4× anisotropy |
| High | 2560 × 1440 | 2048 px, PCF | Up to 8× anisotropy |

Auto fits the budget while retaining aspect ratio, without supersampling beyond display density. Native uses display density (up to 2×). 75% and 50% explicitly scale the world. GPU texture limits cap allocations. Auto adjusts world scale gradually after sustained GPU measurements, between 60% and 100% of its budget. Startup uploads and unsupported/disjoint GPU timers do not lower quality. Physics/frame stalls are not treated as evidence of a GPU limit.

Browser world materials now apply authored saturation/contrast/warmth, distance fog and sun shadow maps. Full native-renderer parity, point-light shadow maps and native postprocessing are outside this change.

Race audio follows countdown integers, the transition to Racing and actual ordered player checkpoint changes. Retry resets the trackers and stops outstanding cues. Winning retains the existing contract victory cue; rival finishes have a notification. Sources and conversions are recorded beside each WAV.

CC0 sources: [Race countdown C octave — Sadiquecat](https://freesound.org/people/Sadiquecat/sounds/845657/), [Race start D4 D5 — Sadiquecat](https://freesound.org/people/Sadiquecat/sounds/845658/), [Retro Bonus Pickup — suntemple](https://freesound.org/people/suntemple/sounds/253172/).

Verification covers resolution/aspect-ratio and adaptation safeguards; WebGL shaders, depth framebuffer and scaled UI pixels; native settings/layout/garage return; actual native race countdown/launch/checkpoint audio. Browser integration checks cover quality/resolution controls, persistence, driving, shadow draws and multiple viewports. Runtime and packages must have matching plugin hashes.

Final checks passed in `.artifacts/quality-browser-smoke.log`, `.artifacts/quality-race-test.log`, `.artifacts/quality-native-presentation.log` and `.artifacts/quality-native-menu.log`. The 1080p High/50% screenshot was visually checked: all labels remain visible and sharp while the world is rendered at 960 × 540. A font-cache correction and smaller atlases prevent repeated uploads from making labels disappear after resizing. Installed plugin/runtime hashes match at 0.22.0, and the installed Dist core matches the compiled Release core.

No distributable game export or itch upload is created. Desktop/laptop defaults still need hardware playtesting; headless Chrome does not replace that comparison.
