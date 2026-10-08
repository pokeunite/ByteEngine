# Export repair validation

Selected identity: B — Open Frame. Engine/player/launcher icons and Windows/browser animated startup installed. Canonical graphics: Resources/Branding.

Project: C:/Users/codex/Documents/DuneCompany/DuneCompany.byteproject. Main engine source: C:/Users/codex/ByteEngine/Plugins/DuneCompany. All four package copies have matching SHA-256 (artifacts, engine PluginLibrary, Documents Plugins, Documents PluginLibrary). Source is copied to Documents PluginSource/DuneCompany. Both editor installations are refreshed.

## Verified

- Separate exported identities start fresh; same identity retains progress across restarts; authoring project untouched.
- Native and actual Chrome New Game confirmation/cancel/confirm; fresh one-block build, zero money, both available jobs; saved blueprints preserved; browser reload/Continue retains the reset campaign. Loader, confirmation and contract UI inspected visually.
- Company balance visible in garage and browser driving screenshots.
- Native launcher works after relocation, forwards quoted arguments and trailing backslashes, selects Runtime/ and returns child exit status.
- Embedded startup GIF has 40 animated frames, opens independently of content loading, and dismisses safely. Native window icons use the selected PNG.
- Binary model geometry, skinning buffers, transforms, material colors, shared textures, corruption rejection, legacy model fallback.
- Actual Chrome late-chunk interruption resumes with HTTP Range; cached reload transfers no game chunks. Node tests cover retries, integrity, cache corruption and blocked storage.
- Vehicle physics: gravity, contact, flat-ground stability, steering, braking/drifting, suspension, terrain, hitch validity, detach/reattach/reset, and two configurations. Normal-rate frames drop no simulation time; stalled-frame catch-up is bounded.
- Actual high-DPI Chrome driving, physical displacement, F8/F10 CSV export, resource hashes, JS/GL checks.

## Local measurements

- Existing fixture content: 126,454,665 -> 67,780,197 bytes; sixteen -> nine chunks; 35 models; duplicate embedded textures reduced from 63.42 MiB to 10.69 MiB unique.
- Loader-only interrupted cold run 6.58 s; verified-cache reload 3.20 s; 559 mounted files.
- Installed browser preparation + startup: 4.74 + 4.79 = 9.53 s. Control: 9.60 + 12.45 = 22.05 s.
- Moving vehicle CPU median: 39.7 ms; rendering 1.7 ms. Control CPU median: 156.1 ms. Mean presentation interval 41.28 ms (~24 FPS).
- Eleven-part vehicle moved >33 m in ten seconds; no JS/GL errors. About 2.28 s simulation time dropped under overload. This is still short of 60 FPS, and overload can slow simulation.

## Limits

Live itch.io/CDN and complete web towing/winch mission were not validated. Browser graphics still lack Windows HDR/shadow/postprocessing parity. A wider legacy test run fails V07RegressionTests character grounding; it is not reported as an engine-wide pass. The remaining browser shared-generic interpreter bottleneck needs further work. No game exports, uploads or pushes were performed. Existing exported builds are not rewritten; exporter/player fixes apply to future builds made by the user.
