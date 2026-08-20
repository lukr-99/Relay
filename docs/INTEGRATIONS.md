# Relay — Integrations

Integrations are **providers** in the agent (see [ARCHITECTURE.md](../ARCHITECTURE.md#provider-model)).
Each adapts an external target to the same `IProvider` contract: a button's action names a
provider + verb, and the provider can publish state for live feedback.

This doc covers the first-class integrations — **OS**, **OBS**, and **MicForge** — plus the
**OSC/MIDI** interop inputs.

---

## OS provider

No external dependency. Verbs: `hotkey`, `text`, `launch`, `open`, `media`, `macro`
(see [DATA-MODEL.md](DATA-MODEL.md)). Implemented with `SendInput` (chords, media `VK_` keys),
`Process.Start` (launch/open), and clipboard for paste. Reads Windows **SMTC** for
"now playing" so a media button can show the current track as a badge.

---

## OBS provider

A thin client of **obs-websocket v5** (built into OBS Studio 28+). The user enables the
obs-websocket server in OBS and pastes its password into the agent's OBS settings (stored in
agent config, never in the pushed layout). Verbs map to obs-websocket requests: `setScene`,
`toggleSource`, `toggleMute`, stream/record control, and a `raw` escape hatch. State events
(`CurrentProgramSceneChanged`, `InputMuteStateChanged`) drive button feedback so the active
scene / muted source light up. Nothing here is reinvented — Relay just speaks OBS's own
protocol.

---

## MicForge

Goal: a deck button toggles mute, switches a preset, or flips a DSP stage in
[MicForge](https://github.com/lukr-99) **with live feedback** — the Mute button glows red when
muted, the active preset button is highlighted, a button can show the live input level.

This is delivered in two phases so Relay is useful **before** any MicForge change, then
great **after** a small addition to MicForge.

### Phase 0 — zero MicForge changes (works today)

Bind buttons to the **`os` provider** firing MicForge's existing **global hotkeys** (MicForge
already supports global hotkeys + push-to-mute/PTT). A "Mute" button just sends MicForge's
mute hotkey; a "PTT" button uses `holdAction`.

- ✅ Nothing to build in MicForge.
- ⚠️ **No state feedback** (the button can't know if the mic is actually muted), and you're
  limited to whatever hotkeys are bound.

### Phase 1 — MicForge exposes the Deck Control Contract

MicForge adds a small **control server** implementing the [Deck Control Contract](#deck-control-contract)
below. Relay's `MicForgeProvider` discovers it over mDNS and becomes a client. Now:

- **Real toggle + feedback:** `mic.toggleMute` returns/pushes `muted`, so the button reflects
  truth even when muted from MicForge's own UI or hotkey.
- **Presets:** `preset.load {name}`; `preset.changed` event highlights the active preset button.
- **Stages:** `stage.setEnabled {id,enabled}` to flip Gate / Noise Suppression / Voice Changer,
  with `stage.changed` feedback.

### Phase 2 — richer control

- **Parameter nudges:** `param.set {stage,param,value}` / `nudge {…, delta}` for input gain,
  threshold, etc. — deck buttons or a phone slider row.
- **Live meter on a button:** subscribe to `inputLevel`; render it as a badge/bar on a button.
- **Crafting cards:** toggle MicForge "voice character" cards from the deck.

### MicForge-side work (tracked in the MicForge repo)

A minimal, self-contained addition — it reuses the same standards Relay already uses, so
there's little new surface:

1. Host a JSON-RPC-over-WSS endpoint (Kestrel, same as the agent) — or, simplest first cut, a
   loopback-only `ws://127.0.0.1` endpoint since agent+MicForge run on the same PC.
2. Advertise `_deckctl._tcp.local` via mDNS with `app=micforge`.
3. Implement the method + event vocabulary below, mapping to MicForge's existing view-model
   commands (mute, preset load, stage enable, param set).
4. Reuse MicForge's existing token/settings storage for the pairing token.

> Because agent and MicForge are on the same machine, a **loopback endpoint with no TLS** is an
> acceptable Phase-1 shortcut (a remote attacker can't reach loopback). Promote to WSS + token
> if MicForge control is ever exposed off-box.

### Deck Control Contract

A tiny internal contract any of your apps can implement to become deck-controllable. Same
transport as everything else (JSON-RPC 2.0 / WebSocket / mDNS). It is the **only** protocol
Relay defines itself — see [STANDARDS.md](STANDARDS.md#the-one-internal-contract).

**Discovery:** advertise `_deckctl._tcp.local`, TXT `app=<name>; v=1`.

**Methods (controller → app)**

| Method | Params | Result |
|---|---|---|
| `ctl.hello` | `{ controller:{id,name} }` | `{ app:{name,version}, capabilities:[…] }` |
| `ctl.getState` | `{}` | app state object (e.g. `{muted, preset, running, inputLevel, stages:[…]}`) |
| `mic.setMuted` / `mic.toggleMute` | `{muted?}` | `{ muted }` |
| `preset.list` / `preset.load` | `{}` / `{name}` | `[names]` / `{ preset }` |
| `stage.setEnabled` | `{id, enabled}` | `{ id, enabled }` |
| `param.set` | `{stage, param, value}` | `{ ok }` |

**Events (app → controller, notifications)**

| Event | Params |
|---|---|
| `state.changed` | partial state (only changed fields) |
| `mic.muteChanged` | `{ muted }` |
| `preset.changed` | `{ preset }` |
| `level` | `{ inputLevel }` (throttled, for meters) |

Relay maps these onto the `micforge` provider verbs and `StateBinding.watch` keys in
[DATA-MODEL.md](DATA-MODEL.md). Future tools (e.g. a game-config tool, an aim-trainer HUD) can
implement the same contract to appear as new providers with no protocol changes.

---

## OSC / MIDI interop (inbound)

So existing control surfaces work without the Relay app:

- **OSC** (Open Sound Control 1.0): the agent listens on a UDP port; OSC addresses
  (`/relay/press/<buttonId>` or user-mapped addresses) become synthetic `button.press`.
  Drives from TouchOSC, etc.
- **MIDI**: the agent opens a virtual MIDI input; Note-On / CC messages map to button presses
  via a small mapping table. Any hardware pad or MIDI app can trigger actions.

Both feed the **same `ActionRouter`**, so a physical MIDI pad, a TouchOSC layout, and the phone
all trigger identical actions and honor the same providers.
