# Relay Agent (Windows)

The PC-side brain: a **.NET 10** tray app that serves a **WebSocket + JSON-RPC 2.0** endpoint,
authenticates paired phones with a bearer token, and routes button presses to **providers** that
do things (hotkeys, media keys, launch, open URLs — OBS and MicForge next).

> Design: [../ARCHITECTURE.md](../ARCHITECTURE.md) · Wire: [../docs/PROTOCOL.md](../docs/PROTOCOL.md)
> · Model: [../docs/DATA-MODEL.md](../docs/DATA-MODEL.md) · Security: [../docs/SECURITY.md](../docs/SECURITY.md)

## Status — Phase 0 (working)

Verified end to end: a `button.press` from a client fires the mapped action on Windows.

- **Kestrel WebSocket server** at `/rpc`, bearer-token auth on the handshake (binds all
  interfaces, no admin/urlacl needed).
- **JSON-RPC 2.0** dispatch: `session.hello`, `deck.getLayout`, `ping`, `button.press`,
  `button.hold`.
- **`os` provider**: `hotkey` (SendInput), `media` keys, `launch`, `open`, `text`.
- **LayoutStore** seeded from the bundled [default deck](Relay.Agent/assets/layout.default.json)
  into `%AppData%\Relay\layout.json`.
- **Tray** (WinForms) with a **Pairing info…** dialog: host / port / token + a QR (via QRCoder).

Deferred: WSS + cert-fingerprint pinning (Phase 0 is `ws://`), real mDNS advertising (stubbed —
pair via the tray QR / manual host:port for now), OBS + MicForge providers, state feedback.

## Stack

- **.NET 10**, `net10.0-windows`, WinForms tray shell (MicForge / DL-FOV-Fixer pattern).
- **Kestrel** (`Microsoft.AspNetCore.App` framework reference) for the WebSocket host.
- **QRCoder** for the pairing QR. JSON via `System.Text.Json`.

## Layout

```
agent/
  Relay.slnx
  Directory.Build.props           Nullable + ImplicitUsings enable (dotnetlib convention)
  Relay.Agent/
    Program.cs                    entry point — wires server + providers + tray
    AppConfig.cs                  %AppData% paths, persisted agent id + token + port
    Log.cs
    Layout/                       DeckLayout / Page / ButtonDef / ActionDef + LayoutStore
    Providers/                    IProvider, OsProvider, NativeInput (P/Invoke), ProviderRegistry
    Server/                       DeckServer (Kestrel), RpcDispatcher, SessionManager, WsSession
    Discovery/                    MdnsAdvertiser (stubbed)
    Pairing/                      LAN IP, pairing URI, QR bitmap
    TrayApp.cs                    NotifyIcon + pairing dialog
    ActionRouter.cs               button -> provider dispatch
    assets/layout.default.json    bundled starter deck
```

## Build & run

```powershell
# from the repo root
.\tools\run-agent.ps1            # build (Debug) + launch the tray agent
```

or directly:

```bash
dotnet build agent/Relay.Agent/Relay.Agent.csproj -c Debug
dotnet run   --project agent/Relay.Agent
```

Right-click the tray icon → **Pairing info…** for the host / port / token to enter on the phone.
