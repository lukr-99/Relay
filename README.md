# DeckForge

Turn an old Android phone into a **Stream Deck** for your Windows PC. A grid of customizable
buttons on the phone fires macros on the PC — hotkeys, launch apps, media control, text
snippets, OBS scene switches, and **MicForge** control (mute / preset / DSP stages) with live
state feedback.

The phone is a thin remote; a lightweight **Windows tray agent** is the brain. They talk over
your LAN using **open standards** (mDNS discovery + WebSocket + JSON-RPC 2.0), so there's no
cloud, no account, and nothing proprietary on the wire.

> See [ARCHITECTURE.md](ARCHITECTURE.md) for the design, [docs/PROTOCOL.md](docs/PROTOCOL.md)
> for the wire format, [docs/INTEGRATIONS.md](docs/INTEGRATIONS.md) for the MicForge/OBS
> bridges, and [ROADMAP.md](ROADMAP.md) for where it's going.

## Highlights

- **Phone is a remote, PC is the brain.** Button *definitions* (which key combo, which script)
  live only on the trusted PC. The phone renders a layout the PC pushes to it and sends back
  "button X pressed" — it can never be tricked into executing arbitrary input.
- **Standards on the wire.** Discovery via **DNS-SD/mDNS**, transport over **WebSocket**, RPC
  via **JSON-RPC 2.0**, config validated with **JSON Schema**. See [docs/STANDARDS.md](docs/STANDARDS.md).
- **Providers.** Each button targets a *provider*: `os` (hotkeys, launch, media, text),
  `obs` (via obs-websocket v5), `micforge` (mute/preset/stages), plus your own scripts.
- **Live feedback.** Buttons reflect real state — mic muted glows red, the active OBS scene and
  MicForge preset are highlighted, a button can show a live level meter.
- **Interop in, too.** The agent can also be driven by **OSC** and **virtual MIDI**, so
  TouchOSC layouts and MIDI pad controllers work without the DeckForge app.
- **Secure by construction.** Pair once by scanning a QR code (encodes `wss://host:port` +
  token + cert fingerprint). Bearer token on every connection, TLS pinned at pairing, LAN-only.

## Architecture at a glance

```
┌─────────────────────────┐        Wi-Fi / LAN         ┌───────────────────────────────┐
│   Android app (Kotlin)  │  WebSocket · JSON-RPC 2.0  │   Windows tray agent (.NET)   │
│  • Compose button grid  │ ─────────────────────────▶ │  • Kestrel WSS server         │
│  • QR-scan pairing       │  button.press{id}          │  • Provider adapters:         │
│  • NSD (mDNS) discovery  │ ◀───────────────────────── │      os · obs · micforge      │
│  • reconnect + wake lock│  layout / state events     │  • Layout & macro editor (UI) │
└─────────────────────────┘                            │  • mDNS advertise · token auth│
                                                        └───────────────┬───────────────┘
                        ┌──────────────────────────────────────────────┴───────────┐
                        ▼                         ▼                                  ▼
                  SendInput / SMTC        obs-websocket v5                 Deck Control Contract
                  (hotkeys, media)          (OBS Studio)                 (MicForge, JSON-RPC/mDNS)
```

## Repository layout

```
DeckForge/
  README.md            you are here
  ARCHITECTURE.md      components, provider model, threading, discovery
  ROADMAP.md           phased plan (v0 → v3), impact/effort tagged
  LICENSE.md           PolyForm Noncommercial 1.0.0
  docs/
    STANDARDS.md       every external standard used, and why
    PROTOCOL.md        JSON-RPC 2.0 methods/events over WebSocket, mDNS records
    DATA-MODEL.md      layout / page / button / action model + JSON Schema
    SECURITY.md        pairing, token auth, TLS pinning, threat model
    INTEGRATIONS.md    provider adapters — OS, OBS, MicForge (Deck Control Contract), OSC/MIDI
  agent/               Windows tray agent (.NET) — see agent/README.md
  android/             Android app (Kotlin/Compose) — see android/README.md
```

## Status

Early — this repository currently holds the **design docs and spec**. Code lands per the
[roadmap](ROADMAP.md), starting with the v0 end-to-end loop (agent WSS + a bare grid firing one
real hotkey).

## Requirements (planned)

- **Windows 10/11 (x64)** for the agent. Self-contained build (bundles the .NET runtime).
- **Android 8.0+** for the app, on the **same LAN** as the PC.
- Optional: **OBS Studio 28+** (built-in obs-websocket v5) for scene control; **MicForge** for
  mic control.

## License

[PolyForm Noncommercial 1.0.0](LICENSE.md) — free for noncommercial use.
