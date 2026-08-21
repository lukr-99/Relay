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
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive
import kotlinx.serialization.json.boolean
import kotlinx.serialization.json.int
import kotlinx.serialization.json.jsonObject
import kotlinx.serialization.json.jsonPrimitive
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.Response
import okhttp3.WebSocket
import okhttp3.WebSocketListener
import java.util.concurrent.TimeUnit
import kotlin.math.min

sealed interface ConnState {
    data object Disconnected : ConnState
    data object Connecting : ConnState
    data object Connected : ConnState
    data class Failed(val reason: String) : ConnState
}

/**
 * OkHttp WebSocket client speaking JSON-RPC 2.0 to the Relay agent. Bearer token on the handshake.
 * Auto-reconnects with backoff when the socket drops (e.g. the agent restarts), so the deck comes
 * back on its own without re-pairing.
 */
class DeckClient {
    private val http = OkHttpClient.Builder()
        .pingInterval(20, TimeUnit.SECONDS)
        .build()

    private val scope = CoroutineScope(Dispatchers.IO + SupervisorJob())

    private var socket: WebSocket? = null
    private var reconnectJob: Job? = null
    private var wantConnected = false
    private var attempt = 0

    private var host = ""
    private var port = 8731
    private var token = ""
    private var deviceName = "Android"

    private val helloId = 1
    private val layoutId = 2

    private val _state = MutableStateFlow<ConnState>(ConnState.Disconnected)
    val state: StateFlow<ConnState> = _state.asStateFlow()

    private val _layout = MutableStateFlow<Layout?>(null)
    val layout: StateFlow<Layout?> = _layout.asStateFlow()

    private val _agentName = MutableStateFlow<String?>(null)
    val agentName: StateFlow<String?> = _agentName.asStateFlow()

    // Toggle states pushed by the agent (button id -> on).
    private val _states = MutableStateFlow<Map<String, Boolean>>(emptyMap())
    val states: StateFlow<Map<String, Boolean>> = _states.asStateFlow()

    fun connect(host: String, port: Int, token: String, deviceName: String) {
        this.host = host; this.port = port; this.token = token; this.deviceName = deviceName
        wantConnected = true
        attempt = 0
        reconnectJob?.cancel()
        open()
    }

    private fun open() {
        socket?.cancel()
        _state.value = ConnState.Connecting
        val request = Request.Builder()
            .url("ws://$host:$port/rpc")
            .header("Authorization", "Bearer $token")
            .build()
        socket = http.newWebSocket(request, Listener())
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
            webSocket.send(Rpc.hello(helloId, deviceName, "android"))
            webSocket.send(Rpc.getLayout(layoutId))
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

            val id = (msg["id"] as? kotlinx.serialization.json.JsonPrimitive)?.intOrNull()
            val result = msg["result"] as? JsonObject ?: return
            when (id) {
                helloId -> _agentName.value = (result["agent"] as? JsonObject)
                    ?.get("name")?.jsonPrimitive?.contentOrNull()
                layoutId -> _layout.value = runCatching {
                    DeckJson.decodeFromJsonElement(Layout.serializer(), result)
                }.getOrNull()
            }
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
