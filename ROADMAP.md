# Relay — Roadmap

Phased plan from "does the loop feel good?" to a daily-driver deck. Each item is tagged
**impact / effort** (low・med・high). Nothing here is committed; it's a menu ordered roughly by
bang-for-buck. Standards and integrations are specced in [docs/](docs/).

## Phase 0 — Prove the loop (MVP)

Get one real button press from phone to PC with acceptable latency, over the real transport.

- **Agent: WSS server + JSON-RPC dispatch** (Kestrel, `relay.v1` subprotocol). `high / med`
- **Agent: `os` provider — `hotkey`** via `SendInput`. `high / low`
- **Agent: QR pairing + bearer-token auth + cert pinning.** `high / med`
- **Agent: mDNS advertise** (`_relay._tcp`). `med / low`
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

- ✅ **StateHub + `button.state` events**; buttons reflect real state (done — toggles + MicForge
  mirror their real state to the deck).
- **OBS provider** (obs-websocket v5): scenes, source/mute toggles, stream/record, live scene
  highlight. `high / med`
- **MicForge Phase 0** (fire existing global hotkeys — no MicForge change). `high / low`
- ✅ **MicForge Phase 1** (Deck Control Contract: mute / bypass / start-stop / preset **with live
  feedback**) — done 2026-08-21. Loopback **named pipe** `\\.\pipe\MicForge.DeckControl`, NDJSON
  contract; MicForge hosts `DeckBridge`, the agent's `micforge` provider is the client and mirrors
  MicForge state onto the deck via `button.state`. See
  [docs/INTEGRATIONS.md](docs/INTEGRATIONS.md#micforge).
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

## Idea backlog — user dump (2026-08-20)

Raw ideas captured after seeing Phase 0 on-device. Not yet slotted into phases. More to come.

### Visuals & layout
- **Keep the current look** — the user likes it; don't regress the card style.
- **Portrait mode:** square cards are *too big* when the phone is vertical. Explore responsive
  sizing — `GridCells.Adaptive(minCardSize)` so more/smaller cards fit, and/or a separate column
  count per orientation, and/or a user-set card size. Fill the screen instead of a few huge
  squares. `high / med`
- **Custom layouting:** user-defined grid (rows × cols), which buttons appear and where, and card
  size. Possibly independent layouts per orientation. `high / high`

### Settings
- A real **Settings menu/screen** in the app (gear entry). Home for: layout config,
  connection/pairing, orientation prefs, and the button/macro editor below. `high / med`

### Button & macro editor ("creation mode")
- On-device (and/or agent-side) editor to **create/edit buttons** without hand-editing JSON.
  `high / high`
- **Custom text snippets** beyond the demo "type hi" — make and save your own. `med / med`
- **In-game chat macro:** typing in a game needs opening chat first. Model as a `macro`:
  `[open-chat key: Enter / Alt+Enter / T / Y]` → (small delay) → `type message` →
  `[send: Enter]`. Make the open-chat key, the message, and the send key configurable, with
  per-game presets. Already expressible via the `macro` action in the data model — needs the
  `os.macro` verb wired up + an editor UI. `high / med`
- More action types will follow.

### Reliability (discovered while testing)
- ✅ **App auto-reconnect** (done 2026-08-20) — the phone now retries with backoff when the socket
  drops; verified it comes back on its own after an agent restart.

### Deck presets / profiles (user idea 2026-08-21)
- ✅ **PC-controlled presets — done 2026-08-21.** Named, switchable *whole decks* stored one-per-file
  in `%AppData%\Relay\presets\<name>.json` with one **active** (pointer in `presets\active.txt`); the
  active preset is what's watched, edited, and pushed. First run migrates the old `layout.json` into a
  **Default** preset. Editor gets a **preset bar** (switcher + New / Duplicate / Rename / Delete) and a
  one-click **＋ MicForge preset** (`PresetTemplates.MicForge()` — mute/bypass/start-stop + prev/next
  preset). Switching pushes the new deck to phones live.
- ⏭️ **Next:** a **phone-side preset picker** (needs a small protocol addition: list presets +
  select). Then Phase-3 **auto-switch by foreground app/game**.

### Clip button
- **How it works:** the button fires an `os.hotkey` chord (currently `Alt+F10`); the agent
  synthesizes that keypress and the user's capture tool (ShadowPlay/**Medal**/etc.) catches it and
  saves the clip. The deck doesn't record — it just presses the capture tool's hotkey.
- **TODO:** make the clip hotkey configurable to the user's tool. User uses **Medal** (default
  "Clip That" = `Ctrl+Alt+G`) → offer a preset. Note the fullscreen-exclusive / run-as-admin
  caveat. `med / low`
