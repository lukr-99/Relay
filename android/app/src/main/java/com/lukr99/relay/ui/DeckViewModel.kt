package com.lukr99.relay.ui

import android.app.Application
import android.os.Build
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import com.lukr99.relay.net.ConnState
import com.lukr99.relay.net.DeckClient
import com.lukr99.relay.net.NsdDiscovery
import com.lukr99.relay.net.UsbTether
import com.lukr99.relay.settings.PairingStore
import kotlinx.coroutines.launch

/** Owns the [DeckClient] and the remembered pairing. The UI observes the client's state flows. */
class DeckViewModel(app: Application) : AndroidViewModel(app) {
    private val store = PairingStore(app)
    val client = DeckClient()
    val discovery = NsdDiscovery(app)

    val savedHost get() = store.host
    val savedPort get() = store.port
    val savedToken get() = store.token
    val savedAgentId get() = store.agentId

    private val deviceName: String =
        listOf(Build.MANUFACTURER, Build.MODEL).filter { it.isNotBlank() }.joinToString(" ").ifBlank { "Android" }

    init {
        // Reconnect to the last-used agent automatically on launch.
        if (store.host.isNotBlank() && store.token.isNotBlank()) {
            client.connect(store.host, store.port, store.token, deviceName, store.fp,
                onPin = { store.fp = it }, onAgentId = { store.agentId = it })
        }
    }

    fun connect(host: String, port: Int, token: String, fp: String) {
        val h = host.trim(); val t = token.trim(); val f = fp.trim()
        store.save(h, port, t, f)
        client.connect(h, port, t, deviceName, f,
            onPin = { store.fp = it }, onAgentId = { store.agentId = it })
    }

    /** Find the PC over a USB-tethering link and connect to it. [onResult] gets the resolved PC IP,
     *  or null if no USB link / agent was found (the UI shows guidance then). */
    fun connectOverUsb(port: Int, token: String, onResult: (String?) -> Unit) {
        val t = token.trim()
        viewModelScope.launch {
            val peer = UsbTether.discoverPeer(port)
            // The agent is the same already-paired PC, merely reachable on a different interface.
            // Keep its certificate pin instead of silently falling back to trust-on-first-use.
            if (peer != null && t.isNotBlank()) connect(peer, port, t, store.fp)
            onResult(peer)
        }
    }

    fun startDiscovery() = discovery.start()
    fun stopDiscovery() = discovery.stop()

    fun disconnect() = client.disconnect()

    fun press(buttonId: String) = client.press(buttonId)
    fun holdStart(buttonId: String) = client.holdStart(buttonId)
    fun holdEnd(buttonId: String) = client.holdEnd(buttonId)
    fun selectPreset(name: String) = client.selectPreset(name)
    fun setSlider(id: String, value: Float) = client.setSlider(id, value)

    override fun onCleared() {
        discovery.stop()
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
