# Relay — Architecture

Relay turns an Android phone into a control surface for a Windows PC. The phone shows a
grid of buttons; tapping one sends a message to a Windows tray **agent**, which looks the
button up and executes its action through a **provider**. State flows back so the phone can
reflect reality (mute lit, active scene highlighted).

The guiding rule: **the PC is the source of truth.** Action definitions never live on the
phone — the phone only knows button IDs and how to render a layout the PC pushes to it.

## Stack

- **Agent (PC):** C# / .NET 10, tray shell (`NotifyIcon` + a small WPF/WinForms editor, same
  pattern as MicForge/DL-FOV-Fixer). WebSocket server via **ASP.NET Core minimal hosting
  (Kestrel)** — it terminates WSS and can also serve the layout-editor web UI later. Input via
  `SendInput` P/Invoke (or `H.InputSimulator`). mDNS via `Makaretu.Dns`/`Zeroconf`.
- **App (Android):** Kotlin + Jetpack Compose (`LazyVerticalGrid`), **OkHttp** WebSocket,
  **NsdManager** (built-in DNS-SD) for discovery, CameraX + ML Kit Barcode for QR pairing.
- **Wire:** WebSocket (RFC 6455) carrying **JSON-RPC 2.0**. See [docs/PROTOCOL.md](docs/PROTOCOL.md).

Everything external is an **open standard** — see [docs/STANDARDS.md](docs/STANDARDS.md).

## Components

```
                          Windows tray agent (.NET)
  ┌───────────────────────────────────────────────────────────────────────────┐
  │  DiscoveryService      advertise _relay._tcp via mDNS (DNS-SD)          │
  │  PairingService        QR generation, token store, TLS cert + fingerprint   │
  │  WsServer (Kestrel)    WSS endpoint, bearer-token auth, JSON-RPC dispatch   │
  │  SessionManager        connected phones, per-session send/subscribe         │
  │  LayoutStore           pages/buttons/actions (JSON + JSON Schema validated)  │
  │  ActionRouter          button.press → resolve button → provider.invoke      │
  │  StateHub              collects provider state → pushes button.state events  │
  │  Providers ───────────────────────────────────────────────────────────────┐│
  │     OsProvider         hotkeys, launch, open url, media keys, type text     ││
  │     ObsProvider        client of obs-websocket v5 (scenes, sources, mute)   ││
  │     MicForgeProvider   client of the Deck Control Contract (see below)      ││
  │     ScriptProvider     run a command / .bat / .ps1 (opt-in, sandboxed args) ││
  │     OscProvider*       inbound OSC → synthetic button.press (interop)        ││
  │     MidiProvider*      virtual MIDI in → synthetic button.press (interop)    ││
  └──────────────────────────────────────────────────────────────────────────┘│
  └───────────────────────────────────────────────────────────────────────────┘
        * optional interop inputs; they feed the same ActionRouter.
```

## Provider model

An **action** names a provider and a verb: `{ "provider": "obs", "verb": "setScene",
"params": { "scene": "Gameplay" } }`. The `ActionRouter` hands it to the matching provider
adapter. Providers implement a tiny contract:

```csharp
public interface IProvider {
    string Id { get; }                                  // "os" | "obs" | "micforge" | ...
    Task<InvokeResult> InvokeAsync(string verb, JsonElement p, CancellationToken ct);
    IObservable<StateChange> State { get; }             // pushed to StateHub → button.state
    Task<ProviderStatus> ProbeAsync();                  // connected? available?
}
```

New capabilities are new providers — the wire protocol, layout model, and app don't change.
This is how OBS and MicForge plug in without special-casing, and how future targets
(Spotify, Home Assistant, your own tools) get added later.

## State feedback

Each button may declare a `state` binding: `{ "provider": "micforge", "watch": "muted" }`.
The `StateHub` subscribes to provider state and emits `button.state` events (`{ id, on,
label?, color?, badge? }`) to every session bound to that button. The phone re-renders that
one button — mic mute goes red, the live OBS scene lights up, a button can show a level badge.

## Discovery & pairing (happy path)

1. Agent starts → generates a self-signed cert (once), advertises `_relay._tcp.local`
   with TXT `v=1; id=<uuid>; name=<PC name>`.
2. User opens the agent's **Pair** window → it shows a QR encoding
   `relay://pair?host=<ip>&port=<p>&token=<t>&fp=<sha256-of-cert>`.
3. Phone scans → stores `{host, port, token, fp}`, opens `wss://host:port` pinning `fp`,
   sends `Authorization: Bearer <token>` on the handshake.
4. Agent validates token + pins the same session → sends the current `layout`. Done.

mDNS lets the phone reconnect after IP changes without re-pairing (it matches on the stored
device `id` from TXT). QR carries the IP so first contact works even if mDNS is blocked.

Full details: [docs/SECURITY.md](docs/SECURITY.md) and [docs/PROTOCOL.md](docs/PROTOCOL.md).

## Threading (agent)

- Kestrel handles WS I/O on the thread pool; JSON-RPC dispatch is `async`.
- `SendInput` and window/foreground calls run on a dedicated STA "input" pump to keep UI-thread
  affinity sane and avoid racing focus changes.
- Provider clients (OBS, MicForge) own their own sockets and surface state via `IObservable`;
  the `StateHub` marshals to session sends.

## Known Windows gotchas (designed-for)

- **Fullscreen-exclusive / elevated games** may ignore `SendInput` unless the agent runs
  elevated → offer an optional "run as admin" launch (relevant for gaming decks).
- **Wi-Fi radio sleep on Android** drops idle sockets → app holds a wake lock + auto-reconnects
  with backoff; agent sends WS pings.
- **Firewall** prompts on first bind of the WSS port (expected; document the port).
- **mDNS flakiness** on some routers → QR carries the IP as the reliable first-contact path;
  manual host:port entry is the fallback.

## Related repos

- **MicForge** — exposes the [Deck Control Contract](docs/INTEGRATIONS.md#micforge) so a deck
  button can mute / switch preset / toggle DSP stages with feedback.
