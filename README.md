# Relay

Relay turns an Android phone into a Stream Deck for Windows. The phone shows a grid of buttons.
Each button runs an action on the PC: a hotkey, an app launch, a media key, a text snippet, an OBS
scene switch, or a MicForge control such as mute or a preset. Buttons show live state, such as a
muted mic in red.

The phone is a thin remote. A Windows tray agent is the brain. They talk over your Wi-Fi network
with open standards: mDNS for discovery, WebSocket for transport, and JSON-RPC 2.0 for messages.
There is no cloud service and no account.

## Status

- Windows agent 0.10.1, tagged 2026-10-06 (`agent-v0.10.1`).
- Android app 0.7.0, tagged 2026-10-05 (`app-v0.7.0`).
- Working: pairing by QR code, the button grid on the phone, the deck editor in the agent, the
  `os` and `micforge` providers, live state, and profile switching.
- Planned: the OBS provider and OSC and MIDI input. See [ROADMAP.md](ROADMAP.md).

## Features

- **The PC decides what a button does.** Button definitions live only on the PC. The phone renders
  the layout the PC sends and reports "button X was pressed". The phone cannot run an arbitrary
  action.
- **Deck editor in the agent.** Drag buttons to move or swap them. Save and push the layout to
  the phone.
- **Providers:** `os` (hotkeys, media keys, launch, open, text, and macros), and `micforge`
  (mute, presets, and DSP stages for [MicForge](https://github.com/lukr-99/MicForge)).
- **Live state:** buttons reflect the real state of the mic and show a level meter.
- **Profiles:** the deck can switch to a different layout when the foreground app changes.
- **Screenshots and folder actions** from the deck.
- **Pairing:** scan the QR code shown in the agent's Devices page. The phone then finds the PC
  with mDNS, so you rarely type an address.
- **Themes and updates:** System, Light, or Dark. The agent checks GitHub Releases for updates.

## Requirements

- **Windows agent:** Windows 10 or 11, 64-bit. The installer bundles the .NET runtime. To build,
  you need the .NET 10 SDK.
- **Phone app:** Android 8.0 (API 26) or newer, on the same Wi-Fi network as the PC.
- **Optional:** OBS Studio 28 or newer, for scene control. MicForge, for mic control.

## Quick start

1. Install the Windows agent with `agent/installer/Relay.iss` or run it from source (see below).
2. Install the Android app on the phone. Use the release APK, or build it with the steps below.
3. On the PC, open the agent's Devices page and show the pairing QR code.
4. In the phone app, scan the QR code. The buttons appear. Tap one to run its action.

## Build and test

The agent uses a private package feed for DotNetLib. Restoring the agent needs access to it. Set
`NuGetPackageSourceCredentials_dotnetlib` or your user NuGet configuration first. Do not put a
token in this repository.

Windows agent, from the repository root:

```powershell
dotnet build agent/Relay.slnx -c Release
dotnet test agent/Relay.slnx -c Release
dotnet run --project agent/Relay.Agent
```

Android app, from the `android` folder:

```powershell
cd android
./gradlew assembleDebug
```

To build and install on a phone connected over USB with debugging on, run
`tools/build-and-install.ps1` from the repository root.

## Architecture

The agent runs a WebSocket server (Kestrel) with JSON-RPC 2.0 messages. It uses a self-signed TLS
certificate. The pairing QR code carries the certificate fingerprint, so the phone can check it.
Each connection also needs a bearer token. The agent advertises itself on the LAN with mDNS.

The design is in [ARCHITECTURE.md](ARCHITECTURE.md). The message format is in
[docs/PROTOCOL.md](docs/PROTOCOL.md), the data model is in [docs/DATA-MODEL.md](docs/DATA-MODEL.md),
and the integrations are in [docs/INTEGRATIONS.md](docs/INTEGRATIONS.md). The agent's own README is
[agent/README.md](agent/README.md), and the phone app's is [android/README.md](android/README.md).

## Data safety

- The deck layout and settings are saved under `%AppData%\Relay\` on the PC.
- The pairing token and certificate are created on the PC. The phone stores the host, port, and
  token it was paired with.
- Relay works only on your local network. Do not expose its port to the internet.
- The threat model and controls are in [docs/SECURITY.md](docs/SECURITY.md).

## Delivery

- The agent ships as a self-contained Windows installer. The Inno Setup script is in
  `agent/installer/Relay.iss`. The build script is `tools/build-installer.ps1`.
- The phone app ships as a signed release APK.
- Releases are published with `tools/publish-release.ps1`.

Use `agent-v<version>` tags for the agent and `app-v<version>` tags for the phone app.

## Standards

Every external standard Relay uses, and why it was chosen, is listed in
[docs/STANDARDS.md](docs/STANDARDS.md).

## License

[PolyForm Noncommercial 1.0.0](LICENSE.md). You may use it for noncommercial purposes.
