# Engine platform capability matrix

| Feature | Windows editor/player | Browser player |
|---|---|---|
| Scenes, fixed callbacks, general box/capsule/heightfield/static-mesh/convex physics | Supported | Supported |
| Specialized managed plugins | Loaded packages | Only built-in plugins listed in the browser manifest |
| Static meshes, opaque instancing, static/generated LOD | Supported | Supported through WebGL2 |
| Skeletal animation and imported animation metadata | Supported | Supported; browser import uses cooked assets |
| PBR, environment lighting | Native renderer | Reduced browser lighting; parity is not promised |
| Directional/point shadow maps, desktop post-processing | Supported | Reduced/unsupported desktop effects |
| SpriteRenderer / legacy Arena HUD | Supported | Export-blocking capability warning |
| Grid / static triangle navmesh, authored links | Supported | Supported |
| UI containers, clipping, focus, text, palette assets | Supported | Supported through Canvas UI |
| Audio buses, priorities, ducking | Supported | Supported; autoplay needs user activation |
| Long music | Bounded native WAV/Ogg decoder | Browser-managed media playback; encoded files remain packaged |
| GPU measurements | Delayed timestamp queries, per viewport/pass | Optional disjoint timer query; Canvas UI CPU timing |
| Save persistence | Platform save directory | Browser origin storage; private/incognito/quota restrictions apply |

The Inspector's Browser Compatibility check and export policy share core component checks. Plugin support is separately validated against the browser runtime manifest. A blank warning list is not a guarantee of native rendering parity or a substitute for testing the actual host.

Local browser smoke uses Chrome/SwiftShader, an animated skinned mesh, static geometry, collider serialization and four viewport sizes. Separate browser audio checks exercise streaming, pause/resume, gains and cache eviction. Remote CI and hosted itch validation remain separate. Test unsupported content before shipping; warnings describing reduced effects are not errors.
