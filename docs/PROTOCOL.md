# Relay — Wire Protocol

Relay speaks **JSON-RPC 2.0** over a single **WebSocket** (RFC 6455) connection, secured
with **WSS** and a **bearer token**. Discovery is **DNS-SD/mDNS**. This document is the
authoritative message catalog. See [STANDARDS.md](STANDARDS.md) for the rationale and
[DATA-MODEL.md](DATA-MODEL.md) for the payload shapes.

## 1. Discovery (mDNS / DNS-SD)

The agent advertises:

```
Service:  _relay._tcp.local
Port:     <wss port, e.g. 8731>
TXT:      v=1                     protocol/record version
          id=<uuid>              stable agent id (survives IP changes)
          name=<PC display name> shown in the phone's device list
          fp=<sha256>            cert fingerprint (also in the QR; lets reconnect verify TLS)
```

The phone browses `_relay._tcp` to list PCs and to re-find a paired agent by `id` after its
IP changes. First-time pairing does **not** rely on mDNS — the QR carries the IP directly.

## 2. Connect & authenticate

The phone opens `wss://<host>:<port>/rpc` and MUST send the token on the WebSocket handshake:

```
GET /rpc HTTP/1.1
Upgrade: websocket
Authorization: Bearer <token>
Sec-WebSocket-Protocol: relay.v1
```

- Invalid/absent token → handshake rejected with **401** (connection never upgrades).
- The agent pins the negotiated subprotocol `relay.v1` for versioning.
- TLS: the phone pins the cert **fingerprint** captured at pairing (trust-on-first-use). A
  mismatch aborts before any RPC. See [SECURITY.md](SECURITY.md).

## 3. JSON-RPC framing

Every WS **text** message is one JSON-RPC 2.0 object (or a batch array).

- **Request** (expects a response): has `id`.
- **Notification** (fire-and-forget, no response): omits `id`. Button presses are notifications.
- **Response**: `result` or `error`, echoing `id`.

```jsonc
// request
{ "jsonrpc": "2.0", "id": 12, "method": "deck.getLayout", "params": {} }
// success
{ "jsonrpc": "2.0", "id": 12, "result": { "pages": [ /* … */ ] } }
// error
{ "jsonrpc": "2.0", "id": 12, "error": { "code": -32004, "message": "unauthorized" } }
// notification (no id, no reply)
{ "jsonrpc": "2.0", "method": "button.press", "params": { "id": "b-7a3f" } }
```

## 4. Methods — phone → agent

| Method | Kind | Params | Result | Notes |
|---|---|---|---|---|
| `session.hello` | request | `{ device: {id,name,model}, appVersion }` | `{ agent:{id,name,version}, capabilities:[…] }` | First call after connect. `capabilities` lists live providers (`os`,`obs`,`micforge`,…). |
| `deck.getLayout` | request | `{}` | `Layout` | Full pages/buttons. See [DATA-MODEL.md](DATA-MODEL.md). |
| `button.press` | **notification** | `{ id, at? }` | — | Fire on tap. `at` = client ISO-8601 timestamp (optional, for latency stats). |
| `button.hold` | **notification** | `{ id, phase }` | — | `phase` ∈ `start`\|`end` for press-and-hold actions (e.g. push-to-talk). |
| `deck.subscribe` | request | `{ ids?:[…] }` | `{ ok:true }` | Subscribe to state for these buttons (or all if omitted). |
| `preset.list` | request | `{}` | `{ presets:[…], active }` | List the named deck presets and which is active. |
| `preset.select` | notification | `{ name }` | — | Switch the active preset. The agent pushes the new `deck.layout` + `preset.changed` to all phones. |
| `slider.set` | **notification** | `{ id, value }` | — | Drag a slider. The agent routes it to the slider's provider (e.g. `micforge/param`) with the value. |
| `ping` | request | `{}` | `{ t }` | App-level RTT check (WS ping/pong also used at transport level). |

## 5. Methods (events) — agent → phone

Sent as **notifications** from the agent:

| Method | Params | Notes |
|---|---|---|
| `deck.layout` | `Layout` | Pushed after `session.hello`, and whenever the layout is edited on the PC. |
| `preset.changed` | `{ presets:[…], active }` | Pushed when the active preset switches or the preset set changes (renamed/created/deleted), so phone-side pickers stay in sync. |
| `button.state` | `{ id, on?, label?, color?, badge?, icon? }` | Live feedback: mute→red, active scene/preset highlight, level badge. Only changed fields are sent. |
| `button.level` | `{ id, level }` | Live 0..1 value for a meter button (e.g. MicForge input level); rendered as a bar. Streamed ~10 Hz while a meter button is on the active deck. |
| `slider.value` | `{ id, value }` | Current value for a slider (e.g. a MicForge param); pushed on connect and when the value changes, so the slider reflects the real state. |
| `agent.notice` | `{ level, text }` | Toast on the phone (`info`\|`warn`\|`error`). E.g. "OBS disconnected". |
| `session.bye` | `{ reason }` | Agent is closing the session (shutdown, unpaired, token revoked). |

## 6. Error codes

Standard JSON-RPC ranges plus a Relay block:

| Code | Meaning |
|---|---|
| `-32700 / -32600 / -32601 / -32602` | Parse / invalid request / method not found / invalid params (per spec). |
| `-32001` | Provider unavailable (e.g. OBS not running). |
| `-32002` | Button id unknown. |
| `-32003` | Action failed (execution error; `message` has detail). |
| `-32004` | Unauthorized (token missing/expired) — usually rejected at handshake instead. |
| `-32005` | Rate limited. |

## 7. Keepalive & reconnect

- Agent sends WebSocket **ping** every ~20 s; the phone must **pong** (RFC 6455).
- The phone reconnects with exponential backoff (1s → 30s cap), re-runs `session.hello`, and
  re-`getLayout` (cheap; layouts are small). Presses issued while disconnected are dropped, not
  queued — a stale macro firing late is worse than not firing.

## 8. Versioning

The WS subprotocol (`relay.v1`) is the version gate. Additive fields don't bump it;
breaking changes ship `relay.v2` and the agent may advertise multiple.
