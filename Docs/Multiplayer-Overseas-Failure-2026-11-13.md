# Overseas match failure — 2026-11-13

## Symptom

Two native logs, identical build (`Source: 870fbb5d…`, so not a compatibility reject):

- Host: `Match scene loaded: Main`, `Hosting 4979B710` — and nothing else, no `Peer joined`.
- Guest: `Joining 4979B710`, then **exactly 15.0 s later** `Connection ended: connection timed out`.

An earlier attempt (`1E831285`) shows the same 15.0 s pattern, and the guest retried three times
(14:45:44, 14:46:56, 14:46:56).

## Root cause: the handshake could not finish inside the 15 s pre-accept window

Three facts combine:

1. `NetworkSession.Update` removed every unaccepted link after **15 s of silence**, and silence was reset
   only by data-channel packets or by `accept`/`welcome` — **never by signaling**. So the entire SDP +
   candidate exchange had to complete, ICE included, inside 15 s or the link was deleted.
2. `NetworkLobbyClient.Poll` kept **exactly one HTTP request in flight**: either a `/signal` POST or a
   `/poll`, never both. Every trickled ICE candidate is a separate POST
   (`WebRtcNetworkLink.OnCandidateSafe` → `NetworkRuntime.Signal` → lobby `/signal`), so a two-peer
   handshake is roughly 12–20 serialized round trips through the Cloudflare Worker and its D1
   compare-and-swap (up to 8 retries per write).
3. Each round trip also has to be *delivered* by a poll, which is the same single slot.

At 300–600 ms per intercontinental round trip that is comfortably more than 15 s, so the session deleted
the link before the answer was even applied. `PvpMatch3D`'s own 25 s "Connecting… Ns remaining" budget
never got the chance to run, which is why the log shows the session's message and not the friendlier one.

The host log confirms it never accepted a peer: acceptance requires the guest's `hello`, which requires
an open data channel, which requires a completed ICE handshake.

## Changes made

1. `NetworkSession`: pre-accept timeout 15 s → 60 s.
2. `NetworkSession.NoteSignal` + `NetworkRuntime.Accept`: every inbound signal resets the handshake
   window, so a slow but progressing exchange is no longer killed mid-flight.
3. `NetworkLobbyClient`: polling and signaling now use **independent in-flight slots**; queued signals
   flush every frame instead of waiting behind a poll; polling backs off 0.25–4 s after a failure
   instead of retrying every frame.
4. Diagnostics (all through the existing exportable `NetworkDiagnostics` log): ICE server list with
   credentials masked, every gathered candidate type, inline candidate count in the local SDP, all
   connection state transitions, `Signal out`/`Signal in` kinds, `Peer joined`/`Peer left`.
5. Signal and poll failures are recorded as **non-fatal**, so `PvpMatch3D`'s `LastError` gate no longer
   aborts a join after one transient 503 or a full signaling queue.

## Verification

- `ByteEngine.Network.Tests`: 4/4 pass (codec, lossy session, four-peer, lobby recovery).
- `ByteEngine.Network.Integration`: 3/3 real WebRTC runs pass (ICE, DTLS, SCTP, both channels).
- `ByteEngine.Network.Native` builds clean (0 errors).

## Re-test, then read the new log

Expected on success: `WebRTC link created; ICE servers: stun:…`, several `Candidate host`/`srflx`,
`Connection state connected`, `Peer joined: N`.

If it still fails:

| Log shows | Meaning | Action |
| --- | --- | --- |
| `ICE servers: none` | authored `NetworkSettings.IceServers` is empty | set `stun:stun.l.google.com:19302` |
| only `Candidate host` | STUN unreachable (UDP 3478 blocked) | try TURN, and `?transport=tcp` on 443 |
| `Candidate srflx` but state stays connecting/failed | symmetric NAT or CGNAT pair | **TURN required** |
| `Signal out` repeats with no `Signal in` | relay not delivering | check Worker/D1 and both log `Source` lines |

`NetworkSettings.IceServers` is a `;`-separated list passed straight to libdatachannel, so TURN needs no
code change, e.g.:

```
stun:stun.l.google.com:19302;turn:user:pass@turn.example.com:3478?transport=udp;turn:user:pass@turn.example.com:443?transport=tcp
```

The browser bridge already parses that form into `{urls, username, credential}` for `RTCPeerConnection`.

## Still open

- TURN credentials currently must be authored into the scene. Serving short-lived credentials from the
  lobby Worker (`/ice`) is the proper fix and is not implemented.
- Two independent handshake timers now exist (session 60 s, plugin 25 s). The plugin's is the
  user-facing one; worth documenting together.
- The room-code lobby is native-only. The browser bridge still uses manual offer/answer copy-paste, so a
  browser peer cannot join a room code, and browser↔native is manual exchange only.
