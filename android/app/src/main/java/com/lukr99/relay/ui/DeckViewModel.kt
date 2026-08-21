package com.lukr99.relay.ui

import android.app.Application
import android.os.Build
import androidx.lifecycle.AndroidViewModel
import com.lukr99.relay.net.ConnState
import com.lukr99.relay.net.DeckClient
import com.lukr99.relay.settings.PairingStore

/** Owns the [DeckClient] and the remembered pairing. The UI observes the client's state flows. */
class DeckViewModel(app: Application) : AndroidViewModel(app) {
    private val store = PairingStore(app)
    val client = DeckClient()

    val savedHost get() = store.host
    val savedPort get() = store.port
    val savedToken get() = store.token

    private val deviceName: String =
        listOf(Build.MANUFACTURER, Build.MODEL).filter { it.isNotBlank() }.joinToString(" ").ifBlank { "Android" }

    init {
        // Reconnect to the last-used agent automatically on launch.
        if (store.host.isNotBlank() && store.token.isNotBlank()) {
            client.connect(store.host, store.port, store.token, deviceName, store.fp) { store.fp = it }
        }
    }

    fun connect(host: String, port: Int, token: String, fp: String) {
        val h = host.trim(); val t = token.trim(); val f = fp.trim()
        store.save(h, port, t, f)
        client.connect(h, port, t, deviceName, f) { store.fp = it }
    }

    fun disconnect() = client.disconnect()

    fun press(buttonId: String) = client.press(buttonId)
    fun holdStart(buttonId: String) = client.holdStart(buttonId)
    fun holdEnd(buttonId: String) = client.holdEnd(buttonId)

    override fun onCleared() {
        client.disconnect()
        super.onCleared()
    }
}

fun ConnState.label(): String = when (this) {
    ConnState.Disconnected -> "Disconnected"
    ConnState.Connecting -> "Connecting…"
    ConnState.Connected -> "Connected"
    is ConnState.Failed -> reason
}
