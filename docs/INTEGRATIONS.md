# Relay — Integrations

Integrations are **providers** in the agent (see [ARCHITECTURE.md](../ARCHITECTURE.md#provider-model)).
Each adapts an external target to the same `IProvider` contract: a button's action names a
provider + verb, and the provider can publish state for live feedback.

This doc covers the first-class integrations — **OS**, **OBS**, and **MicForge** — plus the
**OSC/MIDI** interop inputs.

---

## OS provider

No external dependency. Verbs: `hotkey`, `text`, `launch`, `open`, `media`, `macro`
(see [DATA-MODEL.md](DATA-MODEL.md)). Implemented with `SendInput` (chords, media `VK_` keys),
`Process.Start` (launch/open), and clipboard for paste. Reads Windows **SMTC** for
"now playing" so a media button can show the current track as a badge.

---

## OBS provider

A thin client of **obs-websocket v5** (built into OBS Studio 28+). The user enables the
obs-websocket server in OBS and pastes its password into the agent's OBS settings (stored in
agent config, never in the pushed layout). Verbs map to obs-websocket requests: `setScene`,
`toggleSource`, `toggleMute`, stream/record control, and a `raw` escape hatch. State events
(`CurrentProgramSceneChanged`, `InputMuteStateChanged`) drive button feedback so the active
scene / muted source light up. Nothing here is reinvented — Relay just speaks OBS's own
protocol.

---

## MicForge

Goal: a deck button toggles mute, switches a preset, or flips a DSP stage in
[MicForge](https://github.com/lukr-99) **with live feedback** — the Mute button glows red when
muted, the active preset button is highlighted, a button can show the live input level.

This is delivered in two phases so Relay is useful **before** any MicForge change, then
great **after** a small addition to MicForge.

### Phase 0 — zero MicForge changes (works today)

Bind buttons to the **`os` provider** firing MicForge's existing **global hotkeys** (MicForge
already supports global hotkeys + push-to-mute/PTT). A "Mute" button just sends MicForge's
mute hotkey; a "PTT" button uses `holdAction`.

- ✅ Nothing to build in MicForge.
- ⚠️ **No state feedback** (the button can't know if the mic is actually muted), and you're
  limited to whatever hotkeys are bound.

### Phase 1 — MicForge exposes the Deck Control Contract ✅ (done 2026-08-21)

MicForge hosts a small **control endpoint** (`DeckBridge`) and Relay's **`micforge` provider** is
its client. Now:

- **Real toggle + feedback:** pressing Mute toggles the mic *and* the button reflects the truth —
  it also lights up when muted from MicForge's own UI, hotkey, or push-to-talk.
- **Bypass & Start/Stop:** same two-way behavior.
- **Presets:** load a preset **by name** or cycle **next/prev**; the active-preset button highlights
  and follows changes made anywhere.

**Transport — loopback named pipe, not a socket.** Agent + MicForge share the PC, so instead of a
network endpoint the contract runs over `\\.\pipe\MicForge.DeckControl` — unreachable from the LAN
by construction, no token/TLS needed, no port to collide. Messages are **newline-delimited JSON**
("Deck Control Contract v1"). MicForge pushes `hello` + `state` on connect and re-broadcasts `state`
on every change; the client only ever writes in response to a press (writing before both ends start
reading would deadlock the pipe).

**Client → MicForge**

| Message | Meaning |
|---|---|
| `{"op":"getState"}` | request a `state` reply |
| `{"op":"set","target":"mute\|bypass\|running","value":true}` | set an explicit state |
| `{"op":"toggle","target":"mute\|bypass\|running"}` | flip it |
| `{"op":"preset","name":"…"}` | load a preset by name |
| `{"op":"preset","dir":"next\|prev"}` | cycle presets |
| `{"op":"stage","id":"…","value":true}` | enable/disable a DSP stage by id _(Phase 2)_ |
| `{"op":"stage","id":"…"}` | toggle a DSP stage _(Phase 2)_ |
| `{"op":"meter","enabled":true\|false}` | subscribe/unsubscribe to the input-level stream _(Phase 2)_ |
| `{"op":"param","key":"<stageId>\|<label>","value":12.5}` | set a DSP parameter to an absolute value _(Phase 2b)_ |

**MicForge → client**

| Message | Meaning |
|---|---|
| `{"type":"hello","app":"MicForge","version":"…","protocol":1}` | sent once on connect |
| `{"type":"state",…,"stages":[{id,title,enabled,canToggle}],"params":[{key,stage,label,value,min,max,step,unit}]}` | current state; pushed on connect and on every change. `stages` added in Phase 2, `params` in Phase 2b. |
| `{"type":"meter","in":0.0..1.0}` | input level, pushed ~10 Hz while a client is subscribed via `{"op":"meter"}` _(Phase 2)_ |

The provider maps each `state` onto `button.state` for any deck button bound to `micforge` (mute /
bypass / startstop, or a preset button whose name matches `preset`), so the phone mirrors MicForge
live. On disconnect it clears those buttons. Authoring: the agent's deck editor has a **MicForge**
action type (Mute / Bypass / Start-Stop / Next preset / Previous preset / Preset by name).

### Phase 2 — richer control ✅ (DSP stages + live meter, done 2026-08-21)

- ✅ **DSP stages:** flip any MicForge stage (Gate / Noise Suppression / Voice Changer / …) from a
  deck button **with live feedback** — the button lights up when the stage is enabled, and follows
  toggles made in MicForge's own UI or by a preset load. State carries a `stages` array
  (`{id,title,enabled,canToggle}`); the command is `{"op":"stage","id":…}` (toggle) or
  `…,"value":…` (set). The agent editor's **MicForge** action type gains **"DSP stage"** (its picker
  is populated live from the stages MicForge reports).
- ✅ **Live meter on a button:** a **"Input meter"** MicForge button streams MicForge's input level and
  renders it as a bottom bar (green→amber→red). The agent subscribes (`{"op":"meter","enabled":true}`)
  only while a meter button is on the active deck; MicForge then pushes `{"type":"meter","in":…}`
  ~10 Hz, forwarded to phones as `button.level`.

### Phase 2b — parameter control ✅ (phone slider row, done 2026-08-22)

- ✅ **DSP parameters:** MicForge's `state` carries a `params` catalog (`{key,stage,label,value,min,max,
  step,unit}` for every tunable), and `{"op":"param","key":"<stageId>|<label>","value":…}` sets one
  (clamped). Relay exposes these as a **`SliderDef`** deck element; a **slider row** on the phone drives
  the param live (`slider.set` → `micforge/param`), and the agent pushes `slider.value` back so the
  slider reflects the real value (synced on connect). Authored in the Deck editor's **Sliders** panel.

> Any of the user's other apps can become deck-controllable by hosting the same tiny NDJSON pipe
> contract — it's the one protocol Relay defines itself (see
> [STANDARDS.md](STANDARDS.md#the-one-internal-contract)). A future tool appears as a new provider
> with no protocol changes.

---

## OSC / MIDI interop (inbound)

So existing control surfaces work without the Relay app:

- **OSC** (Open Sound Control 1.0): the agent listens on a UDP port; OSC addresses
  (`/relay/press/<buttonId>` or user-mapped addresses) become synthetic `button.press`.
  Drives from TouchOSC, etc.
- **MIDI**: the agent opens a virtual MIDI input; Note-On / CC messages map to button presses
  via a small mapping table. Any hardware pad or MIDI app can trigger actions.

Both feed the **same `ActionRouter`**, so a physical MIDI pad, a TouchOSC layout, and the phone
all trigger identical actions and honor the same providers.
