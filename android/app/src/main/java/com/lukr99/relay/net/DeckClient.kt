package com.lukr99.relay.net

import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import kotlinx.serialization.json.JsonArray
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive
import kotlinx.serialization.json.boolean
import kotlinx.serialization.json.float
import kotlinx.serialization.json.int
import kotlinx.serialization.json.jsonObject
import kotlinx.serialization.json.jsonPrimitive
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.Response
import okhttp3.WebSocket
import okhttp3.WebSocketListener
import java.security.MessageDigest
import java.security.SecureRandom
import java.security.cert.CertificateException
import java.security.cert.X509Certificate
import java.util.concurrent.TimeUnit
import javax.net.ssl.SSLContext
import javax.net.ssl.TrustManager
import javax.net.ssl.X509TrustManager
import kotlin.math.min

sealed interface ConnState {
    data object Disconnected : ConnState
    data object Connecting : ConnState
    data object Connected : ConnState
    data class Failed(val reason: String) : ConnState
}

/** The agent's deck presets and which one is active. Drives the phone-side preset picker. */
data class PresetState(val names: List<String> = emptyList(), val active: String? = null)

/**
 * OkHttp WebSocket client speaking JSON-RPC 2.0 to the Relay agent. Bearer token on the handshake.
 * Auto-reconnects with backoff when the socket drops (e.g. the agent restarts), so the deck comes
 * back on its own without re-pairing.
 */
class DeckClient {
    private val scope = CoroutineScope(Dispatchers.IO + SupervisorJob())

    private var http: OkHttpClient? = null
    private var socket: WebSocket? = null
    private var reconnectJob: Job? = null
    private var wantConnected = false
    private var attempt = 0

    private var host = ""
    private var port = 8731
    private var token = ""
    private var deviceName = "Android"
    private var pinnedFp = ""
    private var onPin: (String) -> Unit = {}
    private var onAgentId: (String) -> Unit = {}

    private val helloId = 1
    private val layoutId = 2
    private val presetId = 3

    private val _state = MutableStateFlow<ConnState>(ConnState.Disconnected)
    val state: StateFlow<ConnState> = _state.asStateFlow()

    private val _layout = MutableStateFlow<Layout?>(null)
    val layout: StateFlow<Layout?> = _layout.asStateFlow()

    private val _agentName = MutableStateFlow<String?>(null)
    val agentName: StateFlow<String?> = _agentName.asStateFlow()

    // Toggle states pushed by the agent (button id -> on).
    private val _states = MutableStateFlow<Map<String, Boolean>>(emptyMap())
    val states: StateFlow<Map<String, Boolean>> = _states.asStateFlow()

    // Deck presets + active, for the phone-side picker.
    private val _presets = MutableStateFlow(PresetState())
    val presets: StateFlow<PresetState> = _presets.asStateFlow()

    // Live 0..1 levels pushed by the agent (button id -> level), for meter buttons.
    private val _levels = MutableStateFlow<Map<String, Float>>(emptyMap())
    val levels: StateFlow<Map<String, Float>> = _levels.asStateFlow()

    fun connect(host: String, port: Int, token: String, deviceName: String, pinnedFp: String,
                onPin: (String) -> Unit, onAgentId: (String) -> Unit = {}) {
        this.host = host; this.port = port; this.token = token; this.deviceName = deviceName
        this.pinnedFp = pinnedFp; this.onPin = onPin; this.onAgentId = onAgentId
        wantConnected = true
        attempt = 0
        reconnectJob?.cancel()
        open()
    }

    private fun open() {
        socket?.cancel()
        _state.value = ConnState.Connecting
        val client = buildClient(pinnedFp, onPin)
        http = client
        val request = Request.Builder()
            .url("wss://$host:$port/rpc")
            .header("Authorization", "Bearer $token")
            .build()
        socket = client.newWebSocket(request, Listener())
    }

    /** OkHttp client that pins the agent's self-signed cert by SHA-256. If [pinned] is blank it
     *  trusts on first use and reports the fingerprint via [onPin] so it can be saved and pinned next time. */
    private fun buildClient(pinned: String, onPin: (String) -> Unit): OkHttpClient {
        val tm = object : X509TrustManager {
            override fun checkServerTrusted(chain: Array<out X509Certificate>?, authType: String?) {
                val cert = chain?.firstOrNull() ?: throw CertificateException("no server certificate")
                val sha = MessageDigest.getInstance("SHA-256").digest(cert.encoded)
                    .joinToString("") { "%02x".format(it) }
                if (pinned.isBlank()) onPin(sha)
                else if (!sha.equals(pinned, ignoreCase = true))
                    throw CertificateException("certificate fingerprint mismatch")
            }
            override fun checkClientTrusted(chain: Array<out X509Certificate>?, authType: String?) {}
            override fun getAcceptedIssuers(): Array<X509Certificate> = arrayOf()
        }
        val ctx = SSLContext.getInstance("TLS")
        ctx.init(null, arrayOf<TrustManager>(tm), SecureRandom())
        return OkHttpClient.Builder()
            .pingInterval(20, TimeUnit.SECONDS)
            .sslSocketFactory(ctx.socketFactory, tm)
            .hostnameVerifier { _, _ -> true } // self-signed; security is the pinned fingerprint
            .build()
    }

    fun disconnect() {
        wantConnected = false
        reconnectJob?.cancel()
        socket?.close(1000, "bye")
        socket = null
        _state.value = ConnState.Disconnected
        _layout.value = null
    }

    fun press(buttonId: String) { socket?.send(Rpc.press(buttonId)) }
    fun holdStart(buttonId: String) { socket?.send(Rpc.hold(buttonId, "start")) }
    fun holdEnd(buttonId: String) { socket?.send(Rpc.hold(buttonId, "end")) }
    fun selectPreset(name: String) { socket?.send(Rpc.presetSelect(name)) }

    private fun scheduleReconnect() {
        if (!wantConnected || reconnectJob?.isActive == true) return
        attempt++
        val delayMs = min(30_000L, 1_000L * (1L shl min(attempt, 5)))  // 2s,4s,8s,16s,32s→cap 30s
        reconnectJob = scope.launch {
            _state.value = ConnState.Connecting
            delay(delayMs)
            if (wantConnected) open()
        }
    }

    private inner class Listener : WebSocketListener() {
        override fun onOpen(webSocket: WebSocket, response: Response) {
            attempt = 0
            _state.value = ConnState.Connected
            _states.value = emptyMap()
            _levels.value = emptyMap()
            webSocket.send(Rpc.hello(helloId, deviceName, "android"))
            webSocket.send(Rpc.getLayout(layoutId))
            webSocket.send(Rpc.presetList(presetId))
        }

        override fun onMessage(webSocket: WebSocket, text: String) {
            val msg = runCatching { DeckJson.parseToJsonElement(text).jsonObject }.getOrNull() ?: return

            // Server-initiated notifications (no id) — e.g. the agent pushing an edited layout live.
            val method = (msg["method"] as? kotlinx.serialization.json.JsonPrimitive)?.contentOrNull()
            if (method == "deck.layout") {
                (msg["params"] as? JsonObject)?.let { params ->
                    runCatching { DeckJson.decodeFromJsonElement(Layout.serializer(), params) }
                        .getOrNull()?.let { _layout.value = it }
                }
                return
            }
            if (method == "button.state") {
                val prm = msg["params"] as? JsonObject ?: return
                val bid = (prm["id"] as? JsonPrimitive)?.contentOrNull() ?: return
                val on = (prm["on"] as? JsonPrimitive)?.let { runCatching { it.boolean }.getOrNull() } ?: false
                _states.value = _states.value + (bid to on)
                return
            }
            if (method == "preset.changed") {
                (msg["params"] as? JsonObject)?.let { _presets.value = parsePresets(it) }
                return
            }
            if (method == "button.level") {
                val prm = msg["params"] as? JsonObject ?: return
                val bid = (prm["id"] as? JsonPrimitive)?.contentOrNull() ?: return
                val lvl = (prm["level"] as? JsonPrimitive)?.let { runCatching { it.float }.getOrNull() } ?: 0f
                _levels.value = _levels.value + (bid to lvl.coerceIn(0f, 1f))
                return
            }

            val id = (msg["id"] as? kotlinx.serialization.json.JsonPrimitive)?.intOrNull()
            val result = msg["result"] as? JsonObject ?: return
            when (id) {
                helloId -> (result["agent"] as? JsonObject)?.let { agent ->
                    _agentName.value = agent["name"]?.jsonPrimitive?.contentOrNull()
                    agent["id"]?.jsonPrimitive?.contentOrNull()?.takeIf { it.isNotBlank() }?.let(onAgentId)
                }
                layoutId -> _layout.value = runCatching {
                    DeckJson.decodeFromJsonElement(Layout.serializer(), result)
                }.getOrNull()
                presetId -> _presets.value = parsePresets(result)
            }
        }

        private fun parsePresets(obj: JsonObject): PresetState {
            val names = (obj["presets"] as? JsonArray)
                ?.mapNotNull { (it as? JsonPrimitive)?.contentOrNull() } ?: emptyList()
            val active = (obj["active"] as? JsonPrimitive)?.contentOrNull()
            return PresetState(names, active)
        }

        override fun onFailure(webSocket: WebSocket, t: Throwable, response: Response?) {
            if (response?.code == 401) {
                wantConnected = false
                _state.value = ConnState.Failed("Rejected: wrong token")
                return
            }
            if (wantConnected) scheduleReconnect()
            else _state.value = ConnState.Failed(t.message ?: "connection failed")
        }

        override fun onClosed(webSocket: WebSocket, code: Int, reason: String) {
            if (wantConnected) scheduleReconnect()
        }
    }
}

private fun kotlinx.serialization.json.JsonPrimitive.intOrNull(): Int? = runCatching { int }.getOrNull()
private fun kotlinx.serialization.json.JsonPrimitive.contentOrNull(): String? =
    runCatching { content }.getOrNull()
