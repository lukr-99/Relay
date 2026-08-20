package com.lukr99.deckforge.net

import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.int
import kotlinx.serialization.json.jsonObject
import kotlinx.serialization.json.jsonPrimitive
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.Response
import okhttp3.WebSocket
import okhttp3.WebSocketListener
import java.util.concurrent.TimeUnit

sealed interface ConnState {
    data object Disconnected : ConnState
    data object Connecting : ConnState
    data object Connected : ConnState
    data class Failed(val reason: String) : ConnState
}

/**
 * OkHttp WebSocket client speaking JSON-RPC 2.0 to the DeckForge agent. Bearer token is sent on the
 * handshake (Authorization header). On connect it says hello, fetches the layout, and exposes it.
 */
class DeckClient {
    private val http = OkHttpClient.Builder()
        .pingInterval(20, TimeUnit.SECONDS)
        .build()

    private var socket: WebSocket? = null
    private var helloId = 1
    private var layoutId = 2

    private val _state = MutableStateFlow<ConnState>(ConnState.Disconnected)
    val state: StateFlow<ConnState> = _state.asStateFlow()

    private val _layout = MutableStateFlow<Layout?>(null)
    val layout: StateFlow<Layout?> = _layout.asStateFlow()

    private val _agentName = MutableStateFlow<String?>(null)
    val agentName: StateFlow<String?> = _agentName.asStateFlow()

    fun connect(host: String, port: Int, token: String, deviceName: String) {
        disconnect()
        _state.value = ConnState.Connecting
        val request = Request.Builder()
            .url("ws://$host:$port/rpc")
            .header("Authorization", "Bearer $token")
            .build()
        socket = http.newWebSocket(request, Listener(deviceName))
    }

    fun disconnect() {
        socket?.close(1000, "bye")
        socket = null
        _state.value = ConnState.Disconnected
        _layout.value = null
    }

    fun press(buttonId: String) { socket?.send(Rpc.press(buttonId)) }
    fun holdStart(buttonId: String) { socket?.send(Rpc.hold(buttonId, "start")) }
    fun holdEnd(buttonId: String) { socket?.send(Rpc.hold(buttonId, "end")) }

    private inner class Listener(val deviceName: String) : WebSocketListener() {
        override fun onOpen(webSocket: WebSocket, response: Response) {
            _state.value = ConnState.Connected
            webSocket.send(Rpc.hello(helloId, deviceName, "android"))
            webSocket.send(Rpc.getLayout(layoutId))
        }

        override fun onMessage(webSocket: WebSocket, text: String) {
            val msg = runCatching { DeckJson.parseToJsonElement(text).jsonObject }.getOrNull() ?: return
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
            val code = response?.code
            _state.value = ConnState.Failed(
                if (code == 401) "Rejected: wrong token" else (t.message ?: "connection failed")
            )
        }

        override fun onClosed(webSocket: WebSocket, code: Int, reason: String) {
            if (_state.value is ConnState.Connected) _state.value = ConnState.Disconnected
        }
    }
}

private fun kotlinx.serialization.json.JsonPrimitive.intOrNull(): Int? = runCatching { int }.getOrNull()
private fun kotlinx.serialization.json.JsonPrimitive.contentOrNull(): String? =
    runCatching { content }.getOrNull()
