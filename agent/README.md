# Relay Agent (Windows)

The PC-side brain: a **.NET 10 / WPF** desktop app (hand-rolled dark theme, MicForge-style) that
serves a **WebSocket + JSON-RPC 2.0** endpoint, authenticates paired phones with a bearer token,
and routes button presses to **providers** that do things (hotkeys, media keys, launch, open
URLs). Minimises to the tray.

> Design: [../ARCHITECTURE.md](../ARCHITECTURE.md) · Wire: [../docs/PROTOCOL.md](../docs/PROTOCOL.md)
> · Model: [../docs/DATA-MODEL.md](../docs/DATA-MODEL.md) · Security: [../docs/SECURITY.md](../docs/SECURITY.md)

## What works

A `button.press` from a paired phone fires the mapped action on Windows; the deck is designed and
edited on the PC and pushed to the phone live.

- **Kestrel WebSocket server** at `/rpc`, bearer-token auth on the handshake (binds all
  interfaces — no admin/urlacl needed).
- **JSON-RPC 2.0** dispatch: `session.hello`, `deck.getLayout`, `ping`, `button.press`,
  `button.hold`; pushes `deck.layout` when the layout changes.
- **`os` provider**: `hotkey` (SendInput), `media` keys, `launch`, `open`, `text`; plus the
  `core.macro` verb (ordered steps → in-game chat macros).
- **LayoutStore** seeded from the bundled [default deck](Relay.Agent/assets/layout.default.json)
  into `%AppData%\Relay\layout.json`, watched for live edits and re-pushed to phones.
- **WPF UI** — one window, nav rail:
  - **Deck editor** — a WYSIWYG grid mirroring the phone, drag-and-drop to move/swap buttons, a
    properties panel (Label / Action / Appearance), and **Save & Push** (live).
  - **Devices** — pairing QR + host/port/token + a connected-phones list (green/red status dots).
  - **Settings** — files, regenerate token, about.
- **Icon** drawn at runtime (`IconFactory`) for the tray + window; a matching `Relay.ico` is the
  exe/taskbar icon.

Deferred: WSS + cert-fingerprint pinning (currently `ws://`), real mDNS advertising (stubbed —
pair via the Devices QR / manual host:port), OBS + MicForge providers, live `button.state` feedback.

## Stack

- **.NET 10**, `net10.0-windows`, **WPF** UI + **WinForms** only for the tray `NotifyIcon`
  (MicForge pattern). The WinForms/`System.Drawing` global usings are removed in the csproj so WPF
  types win; the tray file imports them explicitly.
- **Kestrel** (`Microsoft.AspNetCore.App` framework reference) for the WebSocket host.
- **QRCoder** for the pairing QR. JSON via `System.Text.Json`.

## Layout

```
agent/
  Relay.slnx
  Directory.Build.props           Nullable + ImplicitUsings enable (dotnetlib convention)
  Relay.Agent/
    App.xaml(.cs)                 startup — wires server + providers + tray, shows MainWindow
    AppServices.cs                composition root shared with the views
    MainWindow.xaml(.cs)          nav-rail shell (Deck / Devices / Settings), dark title bar, tray
    IconFactory.cs                runtime-drawn Relay icon (tray + window)
    TrayIcon.cs                   NotifyIcon + Open / Quit
    Relay.ico                     exe / taskbar icon
    Themes/Dark.xaml              hand-rolled dark theme + control styles
    Views/
      DeckEditorView.xaml(.cs)    visual drag-drop deck editor + properties + Save & Push
      DevicesView.xaml(.cs)       pairing QR + connected phones (status dots)
      SettingsView.xaml(.cs)      files, regenerate token, about
    AppConfig.cs                  %AppData%\Relay paths, persisted agent id + token + port
    Log.cs
    Layout/                       DeckLayout / Page / ButtonDef / ActionDef, LayoutStore, IconCatalog
    Providers/                    IProvider, OsProvider, NativeInput (P/Invoke), ProviderRegistry
    Server/                       DeckServer (Kestrel), RpcDispatcher, SessionManager, WsSession
    Discovery/                    MdnsAdvertiser (stubbed)
    Pairing/                      LAN IP, pairing URI, QR bitmap
    ActionRouter.cs               button -> provider dispatch (+ macro)
    assets/layout.default.json    bundled starter deck
```

## Build & run

```powershell
# from the repo root
.\tools\run-agent.ps1            # build (Debug) + launch
```

or directly:

```bash
dotnet build agent/Relay.Agent/Relay.Agent.csproj -c Debug
dotnet run   --project agent/Relay.Agent
```

Open **Devices** for the host / port / token to pair a phone. Closing the window minimises to the
tray; **Quit Relay** from the tray icon exits.
