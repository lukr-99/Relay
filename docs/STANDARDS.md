# DeckForge — Standards

DeckForge deliberately builds on established, documented standards instead of bespoke glue.
This keeps the wire debuggable with off-the-shelf tools, lets other software interoperate, and
avoids reinventing solved problems. This page is the index; each area links to where it's used.

## On the wire

| Concern | Standard | Why / notes |
|---|---|---|
| Service discovery | **DNS-SD + mDNS** (RFC 6763 / RFC 6762) | Phone finds the PC on the LAN with zero config. Service `_deckforge._tcp.local`. Android has **NsdManager** built in; .NET via `Makaretu.Dns`. |
| Transport | **WebSocket** (RFC 6455) | Full-duplex, low-latency, one long-lived connection. Ping/pong keepalive is part of the spec. |
| RPC semantics | **JSON-RPC 2.0** | Requests, responses, **notifications** (fire-and-forget, perfect for `button.press`), errors, and batching — all specified. Libs: **StreamJsonRpc** (.NET), any JSON-RPC client (Kotlin). |
| Serialization | **JSON** (RFC 8259) / **UTF-8** | Human-readable, universal tooling. |
| Config validation | **JSON Schema** (2020-12) | Layout files validated on load; the schema doubles as editor documentation. See [DATA-MODEL.md](DATA-MODEL.md). |
| Identifiers | **UUID** (RFC 4122) | Stable IDs for devices and buttons (survive relabeling/reorder). |
| Time / dates | **ISO 8601** | Timestamps in logs and state. |
| Pairing URI | **URI** (RFC 3986) | QR encodes `deckforge://pair?...` — a normal, parseable URI. |

## Security

| Concern | Standard | Why / notes |
|---|---|---|
| Transport encryption | **TLS** (WSS) | Self-signed cert, **fingerprint pinned at pairing** (trust-on-first-use). LAN traffic isn't in the clear. |
| Auth | **Bearer token** (RFC 6750 style) | `Authorization: Bearer <token>` on the WS handshake. Token minted at pairing, revocable. |
| Cert fingerprint | **SHA-256** | Pinned value carried in the pairing QR. |

See [SECURITY.md](SECURITY.md).

## Integrations & interop

| Target | Standard / protocol | Why / notes |
|---|---|---|
| OBS Studio | **obs-websocket v5** | OBS's own published protocol. DeckForge's OBS provider is just a client — no reinvention. |
| **Inbound** control surfaces | **OSC** (Open Sound Control 1.0) | TouchOSC / other OSC apps can drive the agent; OSC addresses map to synthetic button presses. |
| **Inbound** controllers | **MIDI** (CC / Note, via a virtual MIDI port) | Hardware pads and MIDI-capable apps can trigger actions. |
| Windows "now playing" / media | **SMTC** (System Media Transport Controls) + media **VK_** codes | Read track state for feedback; send standard media keys. |
| MicForge | **Deck Control Contract** (JSON-RPC/mDNS, defined here) | Not an external standard — an *internal* contract reusing the same JSON-RPC-over-WSS + mDNS mechanism, so any of your apps can implement it. See [INTEGRATIONS.md](INTEGRATIONS.md). |

## Deliberately NOT invented here

- **No custom binary framing** — JSON over WebSocket is plenty fast for a button deck and is
  trivially inspectable (browser devtools, `websocat`, Wireshark).
- **No custom RPC scheme** — JSON-RPC 2.0 already defines requests/notifications/errors/batch.
- **No custom OBS control** — obs-websocket v5 exists and is stable.
- **No cloud broker / account system** — DNS-SD + a pinned LAN socket is the whole story.

## The one internal contract

The **Deck Control Contract** (a small JSON-RPC method + event vocabulary an app exposes so a
deck can control it) is the only thing DeckForge defines itself. It intentionally reuses every
transport/security standard above, so implementing it in MicForge (or a future tool) is just
"advertise `_deckctl._tcp`, answer these methods, emit these events." Full spec in
[INTEGRATIONS.md](INTEGRATIONS.md#deck-control-contract).
