# DeckForge App (Android)

The phone-side remote: a **Kotlin / Jetpack Compose** app that discovers the PC agent over
mDNS, pairs by scanning a QR code, and renders the button grid the agent pushes to it. Tapping a
button sends a `button.press` notification; the agent pushes `button.state` back for live
feedback.

> Design: [../ARCHITECTURE.md](../ARCHITECTURE.md) · Wire: [../docs/PROTOCOL.md](../docs/PROTOCOL.md)
> · Security: [../docs/SECURITY.md](../docs/SECURITY.md)

## Planned stack

- **Kotlin + Jetpack Compose** (`LazyVerticalGrid` for the deck).
- **OkHttp** WebSocket client (JSON-RPC 2.0 over WSS).
- **NsdManager** (built-in DNS-SD) for discovery.
- **CameraX + ML Kit Barcode** (or ZXing) for QR pairing.
- **Android Keystore** for the pairing token; **kotlinx.serialization** for JSON.
- Foreground service / wake lock to keep the socket alive; haptics + keep-screen-on.

## Responsibilities (thin client)

- Discover + pair; store `{host, port, token, fp, id}` securely; pin the TLS fingerprint.
- Render `deck.layout`; send `button.press` / `button.hold`; apply `button.state` updates.
- Reconnect with backoff; never queue stale presses.
- **No action definitions live here** — the phone only knows button IDs (see
  [../docs/SECURITY.md](../docs/SECURITY.md)).

## Planned layout (indicative)

```
android/
  settings.gradle.kts
  app/
    build.gradle.kts
    src/main/java/…/deckforge/
      net/         OkHttp WS, JSON-RPC, NSD discovery
      pairing/     QR scan, keystore token store, cert pinning
      ui/          Compose grid, pages, button state
      model/       Layout/Button/Action DTOs (kotlinx.serialization)
```

Status: **spec only** — see [../ROADMAP.md](../ROADMAP.md) Phase 0.
