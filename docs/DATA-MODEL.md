# DeckForge — Data Model

The **layout** is the PC-owned description of what the deck shows and what each button does. It
is stored as JSON, validated against **JSON Schema (2020-12)** on load, and pushed to phones via
`deck.layout` (see [PROTOCOL.md](PROTOCOL.md)).

## Layout

```jsonc
{
  "version": 1,
  "grid": { "cols": 4, "rows": 3 },        // default page geometry
  "activePage": "p-main",
  "pages": [ Page, … ]
}
```

### Page

```jsonc
{
  "id": "p-main",                          // UUID or slug, stable
  "name": "Main",
  "grid": { "cols": 4, "rows": 3 },        // optional per-page override
  "buttons": [ Button, … ]
}
```

### Button

```jsonc
{
  "id": "b-7a3f",                          // UUID, stable across relabel/reorder
  "row": 0, "col": 1,
  "label": "Mute",
  "icon": "mic-off",                       // Material Symbol name, or "data:image/png;…"
  "color": "#c0392b",                      // idle color
  "action": Action,                        // what happens on tap
  "holdAction": Action,                    // optional: press-and-hold (e.g. push-to-talk)
  "state": StateBinding                    // optional: live feedback source
}
```

### StateBinding (live feedback)

```jsonc
{ "provider": "micforge", "watch": "muted", "onColor": "#c0392b", "offColor": "#2c3e50" }
```

The agent's `StateHub` subscribes to the provider's state and emits `button.state` when
`watch` changes. `badge` can carry a small value (e.g. a level meter, a count).

## Action

An action always names a **provider** and a **verb**, with provider-specific `params`:

```jsonc
{ "provider": "os", "verb": "hotkey", "params": { "keys": ["ctrl","shift","e"] } }
```

### `os` provider verbs

| Verb | Params | Effect |
|---|---|---|
| `hotkey` | `{ keys: ["ctrl","shift","e"] }` | Sends a chord via `SendInput`. |
| `text` | `{ value: "gg wp", method?: "type"\|"paste" }` | Types or clipboard-pastes text. |
| `launch` | `{ path, args?, cwd? }` | `Process.Start`. |
| `open` | `{ url }` | Opens a URL/file with the default handler. |
| `media` | `{ cmd: "playpause"\|"next"\|"prev"\|"stop"\|"volUp"\|"volDown"\|"mute" }` | Standard media `VK_` keys. |
| `macro` | `{ steps: [ Action, … ], gapMs? }` | Runs a sequence with optional inter-step delay. |

### `obs` provider verbs

| Verb | Params | Effect (via obs-websocket v5) |
|---|---|---|
| `setScene` | `{ scene }` | Switch program scene. |
| `toggleSource` | `{ scene, source }` | Show/hide a source. |
| `toggleMute` | `{ input }` | Mute/unmute an audio input. |
| `startStream`/`stopStream`/`startRecord`/`stopRecord` | `{}` | Streaming/recording control. |
| `raw` | `{ requestType, requestData }` | Escape hatch: any obs-websocket request. |

### `micforge` provider verbs

See [INTEGRATIONS.md](INTEGRATIONS.md#micforge). Summary: `toggleMute`, `setMuted`,
`loadPreset {name}`, `setStage {id, enabled}`, `nudge {stage, param, delta}`; watchable
state: `muted`, `preset`, `running`, `inputLevel`.

### `script` provider verbs (opt-in)

| Verb | Params | Effect |
|---|---|---|
| `run` | `{ command, args?[] }` | Runs a whitelisted command. Disabled by default; requires the user to enable Script provider and confirm a command allow-list (no arbitrary strings from the phone). |

## Design rules

- **Stable IDs.** `button.id` / `page.id` are UUIDs so relabeling, recoloring, or reordering
  never breaks a running phone session or state subscriptions.
- **Params are provider-scoped.** The core never parses action semantics — only the provider
  does. Adding a provider adds verbs without touching the schema core.
- **No secrets in the layout.** OBS/MicForge connection secrets live in agent config, never in
  the layout that gets pushed to phones.
- **Schema-validated.** The JSON Schema lives at `agent/schema/layout.schema.json` (to be
  authored with the agent) and is the single source of truth the editor and loader share.

## Example (abridged)

```jsonc
{
  "version": 1,
  "grid": { "cols": 4, "rows": 3 },
  "activePage": "p-main",
  "pages": [{
    "id": "p-main", "name": "Main",
    "buttons": [
      { "id":"b-mute","row":0,"col":0,"label":"Mic","icon":"mic",
        "action":{ "provider":"micforge","verb":"toggleMute" },
        "state":{ "provider":"micforge","watch":"muted","onColor":"#c0392b" } },
      { "id":"b-scene","row":0,"col":1,"label":"Gameplay","icon":"videocam",
        "action":{ "provider":"obs","verb":"setScene","params":{ "scene":"Gameplay" } },
        "state":{ "provider":"obs","watch":"scene=Gameplay","onColor":"#27ae60" } },
      { "id":"b-ptt","row":0,"col":2,"label":"PTT","icon":"record_voice_over",
        "holdAction":{ "provider":"micforge","verb":"setMuted","params":{ "muted":false } } },
      { "id":"b-clip","row":0,"col":3,"label":"Clip","icon":"content_cut",
        "action":{ "provider":"os","verb":"hotkey","params":{ "keys":["alt","f10"] } } }
    ]
  }]
}
```
