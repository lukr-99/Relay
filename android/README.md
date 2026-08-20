# Relay App (Android)

The phone-side remote: a **Kotlin / Jetpack Compose** app that connects to the PC agent over a
**WebSocket** (JSON-RPC 2.0), renders the button grid the agent pushes, and sends
`button.press` on tap.

> Design: [../ARCHITECTURE.md](../ARCHITECTURE.md) · Wire: [../docs/PROTOCOL.md](../docs/PROTOCOL.md)
> · Security: [../docs/SECURITY.md](../docs/SECURITY.md)

## Status — Phase 0

- **Pair screen** — enter host / port / token (remembered in `SharedPreferences`); the values
  come from the agent's tray **Pairing info…** dialog.
- **DeckClient** — OkHttp WebSocket speaking JSON-RPC 2.0; bearer token on the handshake. On
  connect it says `session.hello`, fetches `deck.getLayout`, and exposes the layout as a flow.
- **Deck grid** — `LazyVerticalGrid` of buttons (icon + label + color); tap sends `button.press`.
- Connection state surfaced (Connecting / Connected / rejected-wrong-token).

Deferred: NsdManager (mDNS) auto-discovery, QR-scan pairing (CameraX + ML Kit), WSS + cert
pinning, live `button.state` feedback, multi-page swipe, haptics/keep-awake.

## Stack

Baselined on the workout-tracker / QRingSet reference apps so it reuses cached artifacts:
AGP 8.5.2 · Kotlin 2.0.21 · Compose BOM 2024.12.01 · OkHttp 4.12 · kotlinx.serialization ·
coroutines. `minSdk 26`, `compileSdk 35`, JDK 17. Package `com.lukr99.relay`.

## Layout

```
android/
  settings.gradle.kts · build.gradle.kts · gradle.properties
  gradle/libs.versions.toml            version catalog
  app/
    build.gradle.kts
    src/main/AndroidManifest.xml       INTERNET + cleartext (ws:// for Phase 0)
    src/main/java/com/lukr99/relay/
      MainActivity.kt                  single-activity Compose host
      net/Rpc.kt                       JSON-RPC builders + Layout DTOs (kotlinx.serialization)
      net/DeckClient.kt                OkHttp WebSocket client + connection/layout flows
      settings/PairingStore.kt         host/port/token persistence
      ui/App.kt                        Pair ⇄ Deck switch
      ui/DeckViewModel.kt              owns the client + pairing
      ui/PairScreen.kt · DeckScreen.kt · DeckIcons.kt
      ui/theme/Theme.kt                dark-first Material3
```

## Build & install (USB-tethered phone)

```powershell
# from the repo root, phone connected with USB debugging authorized
.\tools\build-and-install.ps1 -Launch
```

or directly:

```bash
cd android && ./gradlew assembleDebug
adb install -r app/build/outputs/apk/debug/app-debug.apk
```
