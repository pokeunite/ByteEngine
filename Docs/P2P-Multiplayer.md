> **Playable PvP update (2026-11-09):** The active game is now `C:/Users/codex/Documents/MULTIPLAYER TEST`. Use its `Start-Multiplayer.ps1` (Windows default, `-Mode Web` for browser) and `MULTIPLAYER-PLAY.md`. Its editable PvP Match component supplies automatic HTTP room discovery/signaling and the actual imported character/weapon/health systems; the manual-signaling planar demo documented below remains a transport reference.
>
> Local signaling service: `node Tools/NetworkLobby/server.cjs` on port8099. The game uses host-authoritative movement and damage, client movement prediction, four authored avatars, health/ammo/weapon/score/time replication, death/respawn and a win condition. First to10 kills or5minutes by default. Host/Find/Join and log download are available in both runtimes. Restart the game for a fresh match after the result screen.
>
> Internet discovery is **not deployed**: the user has no public hosting account. A reachable HTTPS lobby and, for restrictive NATs, TURN service are still needed. The asset preview binds localhost only. Same-computer Windows/browser testing does not prove cross-Internet NAT traversal. Host migration and ranked anti-cheat are not implemented.
# P2P multiplayer — engine usage and acceptance

This update adds a four-peer host-authoritative networking foundation to ByteEngine. Windows and browsers use compatible WebRTC data channels; either can host. It includes a reference planar character motor, local prediction/reconciliation, interpolated remote avatars, compatibility checks, late joins/departures, authoritative events and client intents. Existing games require explicit network authoring and host/client gameplay rules; adding transport does not automatically synchronize a game's AI, economy, inventories or combat.

## Demo and host/join

Open `C:/Users/codex/Documents/ByteEngineMultiplayerDemo/MultiplayerDemo.byteproject` in the Windows editor and enter Play. The scene has four avatar slots and a fixed camera. Movement uses WASD. A browser client must run the same scene through the updated runtime.

1. Windows editor: open the **Network** menu during Play. Windows player: press **F10**. Browser: expand **Multiplayer** in the top-right corner (available only for scenes with Network Settings).
2. On the hosting peer, select **Host**, then **New invite**. Wait for ICE gathering to populate the outgoing connection data (typically a second). Copy the complete connection data to the guest.
3. Guest selects **Join**, pastes host connection data into the incoming field, then selects **Apply incoming data**. Wait for the answer/candidates to populate its outgoing field; copy these back to the host.
4. Host pastes the guest data and selects **Apply incoming data**. The player count should become2. Each peer controls its own avatar immediately; the host validates and simulates received inputs.
5. For each additional guest, the host creates a separate new invite and applies that guest's answer to the matching connection. Browser/editor controls identify individual links. Maximum four peers including the host.
6. Leave ends your participation. Host departure ends clients gracefully. Foreground game ticks are required; a suspended browser tab can time out rather than continue running a hidden game simulation.

Manual data exchange is signaling, not a game server. No accounts, hosted lobby service, short room-code service or paid relay were provisioned. Use a private channel to exchange connection data. Connection data has no long-term account credential.

## Internet configuration

`NetworkSettings.IceServers` accepts semicolon-separated STUN/TURN URLs. Default: `stun:stun.l.google.com:19302`. Native and browser clients use the same WebRTC transport and ICE candidates.

TURN with credentials can be supplied as `turn:USERNAME:PASSWORD@HOST:PORT` (percent-encode reserved characters in username/password). The browser bridge converts this into standard WebRTC username/credential fields. Configure the same authored scene on all peers, or override `NetworkRuntime.IceServers` after starting a session and before opening its connections. Games should obtain short-lived TURN credentials at runtime rather than ship reusable private credentials in public content. The engine does not create a TURN service.

STUN alone cannot connect every internet pair. An ICE failure is reported as a connection failure, not a guaranteed diagnosis of a particular NAT type. Verification here used local Windows/Edge peers; a real session between two internet connections still requires deployment and testing with the chosen ICE configuration.

## Authoring

- Add exactly one **Network Settings** component to the scene. Set game/plugin/scene version labels and movement speed.
- Add **Network Identity** and **Network Transform** to each pre-authored avatar. Assign unique peer slots0–3 (host0). Initial positions become spawn points. Unassigned avatar renderers/colliders are hidden/disabled during a session.
- Network Transform disables the original CharacterController3D/PlayerController3D while the session motor owns position, sets an attached rigid body kinematic, and restores authored settings when the session ends. Cameras under remote avatar hierarchies are disabled. Local predicted movement is direct; remote positions are smoothed.
- The reference motor is planar, with conservative capsule collision constraints. Use `NetworkSession.SimulatePlayer` for a game's replayable jump/gravity/acceleration motor. The same function must operate from explicit state/input on host and predicting client. Avoid applying an ordinary controller to the same avatar simultaneously.
- Engine-source fingerprints, canonical initial scene data and enabled runtime-plugin binaries participate in compatibility. Browser exports carry the plugin identity already validated by the exporter. Version labels supplement these hashes. Different source/content rejects with a specific engine/plugin/scene reason.

## Gameplay APIs and authority

`NetworkRuntime.Start(host, compatibility)` creates a session. A host transport factory is installed automatically by the Windows editor/player and browser runtime. `NetworkRuntime.OpenConnection(offerer)` creates a link; exchange `NetworkRuntime.Signal` and `NetworkRuntime.Accept` messages through your UI/service. Session polling runs before scene updates.

`NetworkSession.ReadInput` supplies player intent. The wire message contains tick/sequence, bounded finite movement/look values and button bits; it contains no client position, hit, damage or economy result. Host identity comes from the accepted link, never a claimed peer id inside the packet. The host consumes at most one latest input per peer per simulation tick, preventing input flooding from increasing movement speed.

`AuthoritativeInput(peerId,input)` is raised on the host after movement. Use host collision/raycast/weapon cooldown rules to decide combat results. `SendIntent(kind,payload)` sends a client request (maximum512bytes; host accepts at most32persecond per peer). `IntentReceived` runs on the host: game code must validate the operation, ownership, distance, cooldown and resources. It must not treat a request as an authoritative result.

`BroadcastEvent(kind,payload)` is host-only (maximum40000bytes). `EventReceived` delivers ordered deduplicated authoritative events on clients. Games can encode spawn/despawn, health, inventory, VFX and other results using these events and existing blueprint/game APIs. Generic world spawning and arbitrary component/property replication are not automatic. Every client receives complete avatar snapshots, including when it joins late.

A player-host is trusted and can cheat. This is suitable as a PvP networking architecture, but it does not establish neutral authority, anti-cheat, lag-compensated hit validation, ranked matchmaking or host migration. Those require game-specific systems or dedicated authority.

## Diagnostics and checks

Editor/browser lobby displays peer count, total sent/received bytes and RTT. `NetworkSession` also exposes packet totals and reconciliation count. Input snapshots use binary frame-buffer views in WASM; per-tick packet payloads do not become JSON/base64 strings. Signaling and small frame metadata use JSON.

Run from the maintained checkout:

```powershell
dotnet run --project Tests/ByteEngine.Network.Tests -c Release --no-restore
dotnet run --project Tests/ByteEngine.Network.Integration -c Release --no-restore
node Tools/TestBrowserNetworking.cjs
$env:BYTEENGINE_NATIVE_CONFIG='Release'
node Tools/TestNativeBrowserSession.cjs .artifacts/network-browser-release/wwwroot
node Tools/TestBrowserNetworkSession.cjs .artifacts/network-browser-release/wwwroot
```

Browser tests use locally available Playwright (`BYTEENGINE_PLAYWRIGHT` can override its location) and headless Edge. Full logs and captured images are `.artifacts/network-*.log` and `.artifacts/network-browser-session.png`. Test fixtures mount the demo content directly and do not export a user game.

Passed Debug acceptance already includes actual native/native transport, actual native/browser transport both offer directions, full native-C#/WASM sessions both host directions, and two rendered managed WASM peers with movement convergence. Release acceptance and installed build identities are recorded in the active handoff after publication.

No Hazard Pay gameplay plugin code was changed. It is not automatically a multiplayer game as a consequence of this engine update. No game was exported.

## Installed release validation — 9 November 2026

Windows editor/player and browser runtime installed successfully. Full release-AOT Windows/browser sessions passed with either host; two browser peers passed actual lobby controls, optimized frame-buffer transfer and final position convergence. Four-player late join/departure, compatibility rejection, authority/rate limits, native cleanup and shared portable/physics/render-pipeline regressions passed. Every installed browser integrity entry matched; installed native WASM was byte-identical to the accepted release build. See `.artifacts/network-installed-verification.json` for identities and `.artifacts/network-*.log` for raw evidence.

The installed build is the validated release; a subsequent EOF-only source cleanup changed no behavior. No game was exported. Use the demo project and network authoring instructions above; Hazard Pay gameplay remains unchanged.
