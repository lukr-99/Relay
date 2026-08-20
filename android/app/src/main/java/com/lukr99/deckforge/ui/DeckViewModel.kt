package com.lukr99.deckforge.ui

import android.app.Application
import android.os.Build
import androidx.lifecycle.AndroidViewModel
import com.lukr99.deckforge.net.ConnState
import com.lukr99.deckforge.net.DeckClient
import com.lukr99.deckforge.settings.PairingStore
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow

/** Owns the [DeckClient] and the remembered pairing. The UI observes the client's state flows. */
class DeckViewModel(app: Application) : AndroidViewModel(app) {
    private val store = PairingStore(app)
    val client = DeckClient()

    val savedHost get() = store.host
    val savedPort get() = store.port
    val savedToken get() = store.token

    private val _cardMinDp = MutableStateFlow(store.cardMinDp)
    val cardMinDp: StateFlow<Int> = _cardMinDp.asStateFlow()

    fun setCardMinDp(dp: Int) {
        store.cardMinDp = dp
        _cardMinDp.value = dp
    }

    private val deviceName: String =
        listOf(Build.MANUFACTURER, Build.MODEL).filter { it.isNotBlank() }.joinToString(" ").ifBlank { "Android" }

    init {
        // Reconnect to the last-used agent automatically on launch.
        if (store.host.isNotBlank() && store.token.isNotBlank()) {
            client.connect(store.host, store.port, store.token, deviceName)
        }
    }

    fun connect(host: String, port: Int, token: String) {
        store.save(host.trim(), port, token.trim())
        client.connect(host.trim(), port, token.trim(), deviceName)
    }

    fun disconnect() = client.disconnect()

    fun press(buttonId: String) = client.press(buttonId)

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
