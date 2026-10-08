# Browser export support

Dune Company and Desert Terrain are statically linked into the .NET browser player. The desktop plugin DLLs are not dynamically loaded in WebAssembly. Unsupported plugins remain blocked explicitly.

To add another plugin: reference its project in ByteEngine.Browser.csproj, register its ID before opening the project, add that ID to the capability list in Tools/PublishBrowser.ps1, and republish the browser runtime. Updating desktop plugins alone does not update the browser player.

Export Web Game now cooks the project and produces an itch.io-ready ZIP with index.html at its root. Cooked model assets retain metadata; duplicated source model bytes are omitted only from the exported copy. The original project is unchanged.

The player requires WebGL 2 and a click to start audio. Blueprint saves persist in localStorage under the project ID. Clearing browser storage deletes those browser saves. Desktop saves remain separate.

Browser rendering uses reduced graphics: no desktop shadow, IBL or post-processing parity; decoded image textures are limited to 1024 pixels. Sand has real shader displacement and contact deformation, with a 0.25m near render mesh and unchanged 0.125m simulation samples. Physics uses one worker. Texture uploads are bounded and dirty-region based.

Validation: actual Chrome browser loaded the garage, selected the winch starter, entered driving with physical movement, returned to the garage, opened the winch contract with visible selectors, and restored the nine-part vehicle after reload. No managed or GL errors occurred. Portable export checks and sand regressions passed. Full winch mission completion and live itch.io hosting were not validated in the browser; this is not a browser performance benchmark.

## itch.io download resilience

Web manifest version 3 divides Game.bytepak into sequential 8 MiB .bin chunks, each with its own size and SHA-256. The browser assembles and validates the complete package before mounting. Each interrupted, truncated or corrupt download retries up to three times, shows progress and has a 30-second inactivity timeout. Legacy formats 1 and 2 remain supported.

The supplied itch ZIP contains a 126,454,665-byte single package. The CDN returns HTTP 200 and the matching length; Chrome obtained an HTTP 206 prefix whose SHA-256 matches the uploaded ZIP. The screenshot therefore shows a body-transfer failure rather than a missing asset. The original full-transfer failure was not reproduced, so its particular network cause is unknown.

Validation: C# chunk boundary/reassembly test; Node truncated/corrupt/terminal-failure and legacy compatibility tests; Chrome interrupted HTTP response and corrupt-hash retries, verified reassembly and mounting. The complete updated game has not been uploaded or tested on itch.io. No game exports were made. Browser runtime and editor core were rebuilt and installed for the next user export.

Hosting limits reference: https://itch.io/docs/creators/html5
