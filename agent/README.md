# DeckForge Agent (Windows)

The PC-side brain: a **.NET 10** tray app that advertises itself over mDNS, serves a **WSS +
JSON-RPC 2.0** endpoint, authenticates paired phones, and routes button presses to **providers**
that actually do things (hotkeys, launch, OBS, MicForge).

> Design: [../ARCHITECTURE.md](../ARCHITECTURE.md) · Wire: [../docs/PROTOCOL.md](../docs/PROTOCOL.md)
> · Model: [../docs/DATA-MODEL.md](../docs/DATA-MODEL.md) · Security: [../docs/SECURITY.md](../docs/SECURITY.md)

## Planned stack

- **.NET 10**, tray shell (`NotifyIcon` + small WPF/WinForms editor), same pattern as MicForge /
  DL-FOV-Fixer.
- **ASP.NET Core minimal hosting (Kestrel)** for the WSS endpoint (and, later, a web config UI).
- **StreamJsonRpc** for JSON-RPC 2.0 over the WebSocket.
- **SendInput** via P/Invoke (or `H.InputSimulator`) for hotkeys/media.
- **Makaretu.Dns** (or `Zeroconf`) for mDNS advertise/browse.
- **QRCoder** for the pairing QR.
- **obs-websocket-dotnet** for the OBS provider.

## Planned layout (indicative)

```
agent/
  DeckForge.Agent.csproj
  Program.cs                 host builder, DI, tray bootstrap
  Server/                    Kestrel WSS, JSON-RPC dispatch, SessionManager
  Discovery/                 mDNS advertise
  Pairing/                   token store, cert + fingerprint, QR
  Providers/                 OsProvider, ObsProvider, MicForgeProvider, ScriptProvider
  Layout/                    LayoutStore + schema
  StateHub/                  provider state → button.state
  Ui/                        tray + layout editor
  schema/layout.schema.json  JSON Schema for layouts
```

## Build (once code lands)

```bash
dotnet build -c Release
dotnet run   -c Release
dotnet publish -c Release -r win-x64 --self-contained true
```

Status: **spec only** — see [../ROADMAP.md](../ROADMAP.md) Phase 0.
