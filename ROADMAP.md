# DeckForge — Roadmap

Phased plan from "does the loop feel good?" to a daily-driver deck. Each item is tagged
**impact / effort** (low・med・high). Nothing here is committed; it's a menu ordered roughly by
bang-for-buck. Standards and integrations are specced in [docs/](docs/).

## Phase 0 — Prove the loop (MVP)

Get one real button press from phone to PC with acceptable latency, over the real transport.

- **Agent: WSS server + JSON-RPC dispatch** (Kestrel, `deckforge.v1` subprotocol). `high / med`
- **Agent: `os` provider — `hotkey`** via `SendInput`. `high / low`
- **Agent: QR pairing + bearer-token auth + cert pinning.** `high / med`
- **Agent: mDNS advertise** (`_deckforge._tcp`). `med / low`
- **App: NSD discovery + WSS connect + QR scan pairing.** `high / med`
- **App: single-page Compose grid, static layout, fire `button.press`.** `high / med`
- **Agent: minimal tray shell + a hard-coded layout file.** `med / low`

✅ Exit criterion: tap a button on the phone → a hotkey fires in a foreground game, felt as
instant.

## Phase 1 — A usable deck

- **`os` provider: `launch`, `open`, `media`, `text`, `macro`.** `high / med`
- **Layout editor in the tray app** (add/label/color/icon buttons, assign actions). `high / high`
- **Multi-page + swipe**, per-page grid. `med / med`
- **Icons (Material Symbols) + colors + haptics + keep-screen-awake.** `med / med`
- **Robust reconnect** (backoff, wake lock, WS ping/pong). `high / med`
- **JSON Schema validation** of layouts + friendly errors. `med / low`

## Phase 2 — Feedback & integrations

- **StateHub + `button.state` events**; buttons reflect real state. `high / med`
- **OBS provider** (obs-websocket v5): scenes, source/mute toggles, stream/record, live scene
  highlight. `high / med`
- **MicForge Phase 0** (fire existing global hotkeys — no MicForge change). `high / low`
- **MicForge Phase 1** (Deck Control Contract: mute/preset/stage with feedback). `high / high`
  — needs the [MicForge-side control server](docs/INTEGRATIONS.md#micforge-side-work-tracked-in-the-micforge-repo).
- **Live "value" buttons** — now-playing (SMTC), input level meter badge. `med / med`

## Phase 3 — Power features

- **Profiles that auto-switch by foreground app/game** — absorbs the "per-game preset" and
  "config guardian" ideas; the deck changes with your focused window. `high / high`
- **OSC + virtual MIDI inbound** — TouchOSC / MIDI pads drive the same actions. `med / med`
- **Script provider** (allow-listed commands, opt-in). `med / med`
- **Local web config UI** served by the agent (edit layouts in a browser). `med / med`
- **Multi-PC** (one phone, several agents; pick per page). `low / high`
- **Sliders / dials row** on the phone → `param.set` (MicForge gain, OBS volume). `med / high`

## Nice-to-haves / parking lot

- Android widget / quick-tile for a few top actions. `low / med`
- Elevated-agent option for fullscreen-exclusive games. `med / low`
- Auto-updater (check GitHub Releases). `med / med`
- Import/export layouts; share layout files. `low / low`
- iOS/PWA client (agent already serves a web UI — reuse it). `med / high`
- Themeable button styles (rounded/flat, fonts). `low / low`

## Cross-repo work

- **MicForge:** add the Deck Control Contract server (see
  [docs/INTEGRATIONS.md](docs/INTEGRATIONS.md#micforge)). Loopback `ws://127.0.0.1` first cut is
  fine since agent + MicForge share the PC.
