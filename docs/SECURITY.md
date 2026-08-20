# Relay — Security & Pairing

Relay lets a phone **press keys and run actions on your PC**. That power demands the network
side be locked down. The model is intentionally small: **LAN-only, TLS with a pinned
fingerprint, and a bearer token minted at pairing.** No cloud, no account.

## Threat model

**In scope**
- Another device on the same LAN trying to connect and issue actions.
- Passive eavesdropping on Wi-Fi.
- A stolen/lost phone that still holds a pairing.
- A malicious layout or action payload trying to escalate (e.g. run arbitrary shell).

**Out of scope**
- An attacker with local admin on the PC (they already own the machine).
- Physical coercion of an unlocked, paired phone.
- Nation-state TLS breaks. This is a hobby LAN tool.

## Controls

### 1. Transport — WSS with pinned fingerprint (TOFU)
- The agent generates a self-signed cert on first run (stored in `certs/`, git-ignored).
- The cert **SHA-256 fingerprint** is embedded in the pairing QR and in the mDNS TXT record.
- The phone pins that fingerprint at pairing and verifies it on every reconnect. A mismatch
  aborts before any RPC — this defeats a LAN attacker impersonating the agent's IP.

### 2. Authentication — bearer token
- Pairing mints a high-entropy random token (≥256-bit). Stored hashed on the agent, plaintext
  in the phone's secure storage (Android Keystore-backed).
- Sent as `Authorization: Bearer <token>` on the **WebSocket handshake**; a bad/absent token is
  rejected with 401 and never upgrades to a socket.
- Tokens are per-device. The agent's Pair window lists paired devices and can **revoke** any one
  (next connect fails; a live session gets `session.bye`).

### 3. Network scope
- Server binds to LAN interfaces only; loopback always allowed. An optional setting restricts
  accepted clients to the agent's own subnet.
- Default port documented so the user can firewall it deliberately (Windows will prompt on
  first bind).

### 4. Action safety
- **Phone sends button IDs, never action definitions.** All "what does this do" logic lives on
  the PC. A compromised/rogue phone can only trigger buttons that already exist in your layout.
- **Script provider is off by default.** When enabled, it runs only commands from a user-defined
  allow-list — the phone can't send arbitrary command strings.
- **Rate limiting** on `button.press` guards against a flooding client (`-32005`).

### 5. Pairing flow (details)

```
relay://pair?host=192.168.1.20&port=8731&token=<b64url>&fp=<sha256-hex>&id=<uuid>
```

1. Agent → shows QR (above) in the Pair window. Token is single-use for pairing and rotates if
   the window is reopened (prevents a stale QR photo from pairing later).
2. Phone scans → stores `{host, port, token, fp, id}` in secure storage.
3. Phone connects `wss://host:port/rpc`, pins `fp`, sends the bearer token, runs
   `session.hello`.
4. Agent verifies token, records the device, streams the layout.

Manual fallback: the Pair window also shows `host:port` + a short code to type, for when the
camera/QR path isn't available.

## Operational notes

- **Lost phone:** revoke its pairing from the agent. Because the token is per-device, other
  phones are unaffected.
- **Rotating trust:** "Reset pairings" regenerates the cert + invalidates all tokens; every
  device must re-pair.
- **Logs** never contain the token or cert private key. Pairing artifacts (`pairings.json`,
  `certs/`, `*.pfx`) are git-ignored — see [.gitignore](../.gitignore).
