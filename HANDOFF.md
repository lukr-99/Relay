# Relay — Handoff & Status

_Last updated: 2026-08-21. Everything below is committed and pushed to `main` on both repos._

A pick-up-anywhere snapshot: what Relay is, what works, how to build/run it on a fresh
machine, and what to do next. For design detail see [ARCHITECTURE.md](ARCHITECTURE.md),
[docs/PROTOCOL.md](docs/PROTOCOL.md), [docs/INTEGRATIONS.md](docs/INTEGRATIONS.md); for the full
idea list see [ROADMAP.md](ROADMAP.md).

---

## TL;DR

Relay turns an Android phone into a **Stream Deck** for Windows. The **phone** is a thin
renderer (Kotlin/Compose); the **Windows agent** (WPF) is the brain. They talk over the LAN with
**WSS + JSON-RPC 2.0**, bearer-token auth, and cert pinning. It works end-to-end today: pair by
QR, tap a button, an action fires on the PC; edit the deck in the agent and the phone updates
live.

The two most recent features — **MicForge control** and **deck presets** — are done, verified,
and shipped.

---

## Two repos (both on GitHub, private, owner `lukr-99`)

| Repo | Local path | What's in it |
|---|---|---|
| **Relay** (this) | `C:\Users\krejci\Code\Relay` | `agent/` (WPF Windows agent), `android/` (Kotlin phone app), `docs/` |
| **MicForge** | `C:\Users\krejci\Code\MicForge` | The mic-DSP app. Hosts the **Deck Control Bridge** the agent talks to. |

The MicForge integration is **cross-repo**: the agent's `micforge` provider is a client of a
loopback pipe server (`Services/DeckBridge.cs`) inside MicForge. Both must be running on the same
PC for mic control to work.

---

## Set up on a new computer

**Prerequisites**
- **.NET 10 SDK** (agent + MicForge). `dotnet --version` ≥ 10.0.4xx.
- **JDK 17+** and the **Android SDK** (phone app). Set `ANDROID_HOME`, or drop
  `android/local.properties` with `sdk.dir=...` (forward slashes). The Gradle **wrapper** fetches
  Gradle itself (8.9). AGP 8.5.2 / Kotlin 2.0.21 / Compose BOM 2024.12.01 / OkHttp 4.12.
- **gh CLI** authed as `lukr-99` (for pushing).
- **Inno Setup 6** — only needed to build the MicForge **installer** (per-user path:
  `%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe`).
- A phone with **USB debugging** to install the debug APK (test device: Samsung Galaxy A56,
  `SM-A566B`, package `com.lukr99.relay`). Or just build the APK and sideload.

**Clone**
```bash
gh repo clone lukr-99/Relay
gh repo clone lukr-99/MicForge   # only if working on the mic bridge
```

**Build & run — the agent**
```powershell
.\tools\run-agent.ps1            # builds Debug + launches; -Release for Release
```
The agent shows a window (Deck editor / Devices / Settings) and a tray icon. Port **8731**.

**Build & install — the phone app**
```powershell
.\tools\build-and-install.ps1 -Launch    # assembleDebug + adb install -r + launch
```

**Build/ship — MicForge** (only for the bridge)
```powershell
dotnet build C:\Users\krejci\Code\MicForge\MicForge.csproj -c Debug   # dev
# release: publish self-contained, compile installer, silent per-user install
dotnet publish MicForge.csproj -c Release -r win-x64 --self-contained true -o publish
& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" installer\MicForge.iss
Start-Process installer\MicForge-Setup-<ver>.exe -ArgumentList "/VERYSILENT","/SUPPRESSMSGBOXES","/NORESTART" -Wait
```

**Pair the phone**: agent → **Devices** tab shows a QR (and host/port/token). Scan it with the
app's "Scan QR code" button. Token lives in `%AppData%\Relay\relay.state.json`.

---

## What's DONE ✅

- **Transport & security:** Kestrel WSS server, JSON-RPC 2.0, bearer-token on handshake,
  self-signed cert with **SHA-256 pinning** (trust-on-first-use, fingerprint carried in the QR).
- **Providers / actions:** `os` (hotkey, media, text, launch, open, keydown/keyup),
  `core` (macro, toggle), `script` (run-command, opt-in), `micforge`.
- **Deck editor** (agent, WPF): visual grid, drag-drop, per-button label/icon/colour/action, all
  action types, multi-page decks, duplicate/export/import.
- **Phone app:** Compose grid (positional, scales to grid size), multi-page pager, QR + deep-link
  pairing, haptics, keep-screen-on, auto-reconnect with backoff, press-and-hold (PTT), toggle
  recolor via `button.state`.
- **MicForge control (v1.6.3):** mute / bypass / start-stop / preset (by name or next/prev) from a
  deck button, **with live two-way state** — a Mute button lights up whenever the mic is muted
  from anywhere. Loopback named-pipe contract (`\\.\pipe\MicForge.DeckControl`, NDJSON). Editor has
  a **MicForge** action type.
- **Deck presets (PC-controlled):** named, switchable **whole decks** in
  `%AppData%\Relay\presets\<name>.json` (one active, pointer in `active.txt`). Editor **preset bar**
  (switch / New / Duplicate / Rename / Delete) + one-click **＋ MicForge preset**. First run migrated
  the old `layout.json` into a **Default** preset.

---

## What's TO DO ⏭️ (roughly prioritized)

### 1. Phone-side preset picker  `high value / med effort`
Presets are PC-controlled today; let the phone switch them too.
- **Agent:** add JSON-RPC methods in `agent/Relay.Agent/Server/RpcDispatcher.cs`:
  `preset.list` → `{ presets: LayoutStore.Presets, active: LayoutStore.ActivePreset }`;
  `preset.select {name}` → `LayoutStore.SetActive(name)` (already pushes the new layout).
  Optionally push a `preset.changed` notification from `DeckServer` when the active preset changes.
- **Phone:** add `Rpc.presetList()/presetSelect()` in `net/DeckClient.kt`, expose a
  `StateFlow<presets/active>`, and a small dropdown/menu in `ui/DeckScreen.kt`.

### 2. MicForge Phase 2 — DSP stages + live meter  `high / med`
Extend the pipe contract (both repos): add `stages` to MicForge's `state` and a
`{"op":"stage","id":…,"enabled":…}` command in `MicForge/Services/DeckBridge.cs`; add a `stage`
verb + a "MicForge: Stage" editor type in Relay. Then a **live input-level** field for a meter
badge on a button.

### 3. mDNS / NSD auto-discovery  `med / low`
Advertise `_relay._tcp` from the agent (`Discovery/MdnsAdvertiser.cs` is currently a stub) so the
phone can find the PC without a QR. Lower priority — QR already carries host/port/token/fp.

### 4. Backlog (see ROADMAP.md)
Per-orientation layouts; a general multi-step macro editor; OBS provider (obs-websocket v5 — was
NU1101-unavailable, revisit); **profiles that auto-switch by foreground app/game** (builds on the
new presets); OSC/MIDI inbound; a configurable "clip" hotkey.

---

## Dev loop & gotchas (hard-won — read before hacking)

- **Launch the agent DETACHED** when scripting (PowerShell `Start-Process`); a bash `&` child dies
  when the launching command returns.
- **Kill `Relay.Agent.exe` before rebuilding** — the running exe locks its output files.
- **`node`/`python` here run in a different FS view and can't open `%APPDATA%` files** — use the
  editor's Read/Edit or PowerShell for those. (Named **pipes** are fine from node — different
  namespace.)
- **Named-pipe deadlock:** MicForge pushes hello+state on connect and the client must go straight
  to reading — if **both** ends write before either reads, the pipe deadlocks. The provider only
  writes on a press. Also the client uses **async** pipe I/O (`WriteLineAsync`); sync `WriteLine`
  on a `PipeOptions.Asynchronous` handle with a pending read also hangs.
- **WPF+WinForms in one project:** the WinForms global usings are dropped (`<Using Remove=…>`), so
  `System.Windows.*` wins; import `System.Drawing`/`System.Windows.Forms` explicitly where needed.
  `Grid` is ambiguous in the editor code-behind — use `Relay.Agent.Layout.Grid`.
- **Testing without the phone:** a Node WS client works (`wss://127.0.0.1:8731/rpc?token=…`,
  `NODE_TLS_REJECT_UNAUTHORIZED=0`). Node 22+ has a built-in `WebSocket`.
- **Live edits:** the agent watches the active preset file; the harness's atomic-rename writes do
  **not** trip `FileSystemWatcher`, but `File.WriteAllText` (what the editor uses) does.

---

## Per-machine data (NOT in git — recreated on first run)

- `%AppData%\Relay\` — `presets\*.json` + `presets\active.txt`, `relay.state.json` (agent id, **token**,
  port), `certs\relay.pfx`, `logs\relay.log`. (`layout.json.migrated` is the pre-presets deck, kept
  as a backup.)
- `%AppData%\MicForge\` — `micforge.json` (settings), `presets\`, `logs\micforge.log`.

Because the token/cert are per-machine, **re-pair the phone** after moving to a new PC (scan the
new QR).

---

## Latest commits

- **Relay:** `feat: deck presets — named switchable whole decks (PC-controlled)` →
  `feat: MicForge provider …` → `feat: WSS + certificate pinning`.
- **MicForge:** `chore: release v1.6.3 (Deck Control Bridge)` → `feat: Deck Control Bridge for Relay`.
