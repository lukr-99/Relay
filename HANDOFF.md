# Relay — Handoff & Status

_Last updated: 2026-08-21 (v0.3.0: phone preset picker, MicForge Phase 2, mDNS, versioned installers)._

> **Paths are machine-specific.** This handoff was first written on a machine with the repos under
> `C:\Users\krejci\Code\…`; they currently also live at `F:\Code\Relay` and `F:\Code\MicForge`.
> Substitute your own clone paths throughout.

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

As of **v0.3.0** the deck also has a **phone-side preset picker**, **MicForge Phase 2** (DSP-stage
toggles + a live input-level meter), and **mDNS auto-discovery**. Both apps now build **versioned
installers** — a self-contained Windows installer for the agent and a signed release APK.

---

## Two repos (both on GitHub, private, owner `lukr-99`)

| Repo | Local path | What's in it |
|---|---|---|
| **Relay** (this) | `…\Code\Relay` | `agent/` (WPF Windows agent), `android/` (Kotlin phone app), `docs/` |
| **MicForge** | `…\Code\MicForge` | The mic-DSP app. Hosts the **Deck Control Bridge** the agent talks to. |

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

**Build the versioned installers** (v0.3.0+)
```powershell
.\tools\build-installer.ps1        # -> agent\installer\Relay-Setup-<ver>.exe (self-contained, per-user)
.\tools\build-apk.ps1              # -> android\dist\Relay-<ver>.apk (signed release)
```
- The agent installer bundles the .NET 10 runtime — end users need nothing installed. Version comes
  from the csproj `<Version>`. Needs **Inno Setup 6**.
- The APK is signed from `android\keystore.properties` (gitignored) → `android\relay-release.keystore`
  (also gitignored — **back this up**; losing it means you can't ship signed updates). Without those,
  `build-apk.ps1` falls back to the debug key (runnable, not distributable). `build-apk.ps1`
  auto-detects a **JDK 17+** if `JAVA_HOME` points at an older one.

**Pair the phone**: agent → **Devices** tab shows a QR (and host/port/token), or (v0.3.0+) tap the PC
under **"Found on your network"** on the pair screen. Token lives in `%AppData%\Relay\relay.state.json`.

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
- **Phone-side preset picker (v0.3.0):** `preset.list` / `preset.select` RPC + a `preset.changed`
  push (`RpcDispatcher.cs`, `DeckServer.cs`); the phone header shows a dropdown that lists presets and
  switches the active deck live (`DeckClient.kt`, `DeckScreen.kt`).
- **MicForge Phase 2 (v0.3.0):** toggle any of MicForge's ~23 **DSP stages** from a button (verb
  `stage`, live feedback) and an **"Input meter"** button that streams the live level as a bar (verb
  `meter` → `button.level`, ~10 Hz). Contract extended in both repos: `stages` in state +
  `{"op":"stage"}` / `{"op":"meter"}` (`MicForge/Services/DeckBridge.cs`, agent `MicForgeProvider.cs`).
  Editor **MicForge** action type gains "DSP stage" (live stage picker) + "Input meter".
- **mDNS auto-discovery (v0.3.0):** the agent advertises `_relay._tcp` via **Makaretu**
  (`Discovery/MdnsAdvertiser.cs`); the phone browses with **`NsdManager`** (`net/NsdDiscovery.kt`) and
  shows found agents on the pair screen — a match on the saved agent `id` offers one-tap **Reconnect**
  (survives the PC's IP changing). The token is never in mDNS; pairing still needs the QR/manual token.
- **Versioned installers (v0.3.0):** `tools\build-installer.ps1` → self-contained agent installer
  `agent\installer\Relay-Setup-<ver>.exe` (Inno Setup); `tools\build-apk.ps1` → signed
  `android\dist\Relay-<ver>.apk`. Agent version = csproj `<Version>` (surfaced via `AppInfo.Version`);
  app version = `versionName`/`versionCode` in `android/app/build.gradle.kts`.

---

## What's TO DO ⏭️ (roughly prioritized)

The three items that headlined the last handoff — **phone preset picker**, **MicForge Phase 2**, and
**mDNS** — are all **done** (see What's DONE). Next up:

### 1. Profiles that auto-switch by foreground app/game  `high / high`
Phase-3 headliner; builds directly on presets. Watch the focused window (Win32 event hook) and call
`LayoutStore.SetActive` when it matches a rule. The whole push/mirror path already exists.

### 2. MicForge Phase 2b — parameter nudges  `med / med`
`param.set {stage,param,value}` / `nudge {…, delta}` for input gain / threshold, driving a phone
slider row. Extends the same pipe contract (stages + meter already landed).

### 3. OBS provider  `high / med`
obs-websocket v5 (scenes / mute / stream-record) with live scene highlight. Was NU1101-unavailable
before — revisit the package (Makaretu restored fine now, so the feed may be healthy again).

### 4. Backlog (see ROADMAP.md)
Per-orientation layouts; a general multi-step macro editor; OSC/MIDI inbound; a configurable "clip"
hotkey; auto-updater (check GitHub Releases — the installers make this easy now).

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

- **v0.3.0 work is implemented and verified but not yet committed** (both repos). It spans the phone
  preset picker, MicForge Phase 2 (stages + meter, cross-repo), mDNS, and the versioned installers.
  Commit both repos when ready.
- Prior **Relay:** `feat: deck presets — named switchable whole decks (PC-controlled)` →
  `feat: MicForge provider …` → `feat: WSS + certificate pinning`.
- Prior **MicForge:** `chore: release v1.6.3 (Deck Control Bridge)` → `feat: Deck Control Bridge for Relay`.
