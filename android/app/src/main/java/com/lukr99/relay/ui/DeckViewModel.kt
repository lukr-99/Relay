package com.lukr99.relay.ui

import android.app.Application
import android.os.Build
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import com.lukr99.relay.net.ConnState
import com.lukr99.relay.net.DeckClient
import com.lukr99.relay.net.NsdDiscovery
import com.lukr99.relay.net.UsbTether
import com.lukr99.relay.settings.ActiveLink
import com.lukr99.relay.settings.ConnectionLink
import com.lukr99.relay.settings.PairingStore
import kotlinx.coroutines.Job
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

/** Owns the [DeckClient] and the remembered pairing. The UI observes the client's state flows. */
class DeckViewModel(app: Application) : AndroidViewModel(app) {
    private val store = PairingStore(app)
    val client = DeckClient()
    val discovery = NsdDiscovery(app)

    val savedHost get() = store.lanHost
    val savedPort get() = store.port
    val savedToken get() = store.token
    val savedAgentId get() = store.agentId

    /** Whether a PC was ever paired, so the page can offer Reconnect. */
    val isPaired get() = store.token.isNotBlank() && (store.lanHost.isNotBlank() || store.host.isNotBlank())

    private val _link = MutableStateFlow(store.link)
    /** The chosen way to reach the PC. */
    val link: StateFlow<ConnectionLink> = _link.asStateFlow()

    private val _activeLink = MutableStateFlow<ActiveLink?>(null)
    /** The link the current connection uses, or null before one is picked. */
    val activeLink: StateFlow<ActiveLink?> = _activeLink.asStateFlow()

    private val _linkError = MutableStateFlow<String?>(null)
    /** Why the chosen link could not even start (no USB link, no address yet). */
    val linkError: StateFlow<String?> = _linkError.asStateFlow()

    private val _searchingCable = MutableStateFlow(false)
    /** True while looking for the PC on the USB tether. */
    val searchingCable: StateFlow<Boolean> = _searchingCable.asStateFlow()

    private var resolveJob: Job? = null

    // Auto found tethering on but no PC on it and went to Wi-Fi. Don't search the cable again on
    // every retry while tethering stays on.
    private var cableFailedWhileTethered = false

    private val deviceName: String =
        listOf(Build.MANUFACTURER, Build.MODEL).filter { it.isNotBlank() }.joinToString(" ").ifBlank { "Android" }

    init {
        store.migrateLanHost()
        // Reconnect to the last-used agent automatically on launch, the way the link setting says.
        if (isPaired) reconnect()

        // Auto follows the cable: when a retry starts and tethering came or went since the link was
        // picked, pick again instead of retrying the address that no longer fits.
        viewModelScope.launch {
            client.state.collect { s ->
                if (s !is ConnState.Retrying || _link.value != ConnectionLink.Auto) return@collect
                val cableNow = UsbTether.isTetherUp()
                if (!cableNow) cableFailedWhileTethered = false
                val onCable = _activeLink.value == ActiveLink.Cable
                if (cableNow != onCable && !(cableNow && cableFailedWhileTethered)) reconnect()
            }
        }
    }

    /** Picks the link and reconnects that way at once, keeping the pairing. */
    fun setLink(link: ConnectionLink) {
        cableFailedWhileTethered = false
        store.link = link
        _link.value = link
        if (isPaired) reconnect()
    }

    /**
     * Connects to the paired PC over the chosen link: the cable finds the PC on the USB tether, Wi-Fi
     * uses its network address, and Auto takes the cable when tethering is up.
     */
    fun reconnect() {
        resolveJob?.cancel()
        _linkError.value = null
        val token = store.token
        if (token.isBlank()) return
        val useCable = when (_link.value) {
            ConnectionLink.Cable -> true
            ConnectionLink.WiFi -> false
            ConnectionLink.Auto -> UsbTether.isTetherUp()
        }
        if (!useCable) {
            val host = store.lanHost
            if (host.isBlank()) {
                _linkError.value = "No network address yet. Scan the PC's QR code once over Wi-Fi."
                return
            }
            open(host, ActiveLink.WiFi)
            return
        }
        client.disconnect()
        _activeLink.value = ActiveLink.Cable
        _searchingCable.value = true
        resolveJob = viewModelScope.launch {
            val peer = UsbTether.discoverPeer(store.port)
            _searchingCable.value = false
            if (peer == null && _link.value == ConnectionLink.Auto && store.lanHost.isNotBlank()) {
                // Auto: tethering is on but the PC isn't there, so use the network instead.
                cableFailedWhileTethered = true
                open(store.lanHost, ActiveLink.WiFi)
                return@launch
            }
            if (peer == null) {
                _linkError.value = if (UsbTether.isTetherUp())
                    "USB tethering is on, but Relay isn't answering on the PC. Check that it runs."
                else
                    "No USB link. Plug in the cable and turn on USB tethering (Settings › Connections › Mobile Hotspot and Tethering)."
                return@launch
            }
            open(peer, ActiveLink.Cable)
        }
    }

    /** Connects with details from the QR code, a found PC or the manual fields. They are always network addresses. */
    fun connect(host: String, port: Int, token: String, fp: String) {
        resolveJob?.cancel()
        _linkError.value = null
        val h = host.trim(); val t = token.trim(); val f = fp.trim()
        store.save(h, port, t, f)
        store.lanHost = h
        // Pairing by address means the network, so a forced cable link would undo it at once.
        if (_link.value == ConnectionLink.Cable) setLinkQuietly(ConnectionLink.WiFi)
        _activeLink.value = ActiveLink.WiFi
        client.connect(h, port, t, deviceName, f,
            onPin = { store.fp = it }, onAgentId = { store.agentId = it })
    }

    /** First pairing over the cable, with a token typed by hand. */
    fun connectOverUsb(port: Int, token: String, onResult: (String?) -> Unit) {
        val t = token.trim()
        resolveJob?.cancel()
        _linkError.value = null
        _searchingCable.value = true
        resolveJob = viewModelScope.launch {
            val peer = UsbTether.discoverPeer(port)
            _searchingCable.value = false
            if (peer != null && t.isNotBlank()) {
                // Keep the network address; only the address in use changes.
                store.save(peer, port, t, store.fp)
                setLinkQuietly(ConnectionLink.Cable)
                open(peer, ActiveLink.Cable)
            }
            onResult(peer)
        }
    }

    /** Stops trying. The pairing stays, so Reconnect works later. */
    fun cancel() {
        resolveJob?.cancel()
        _searchingCable.value = false
        client.disconnect()
    }

    private fun setLinkQuietly(link: ConnectionLink) {
        store.link = link
        _link.value = link
    }

    private fun open(host: String, via: ActiveLink) {
        // The agent is the same paired PC, merely reachable on another interface, so keep its
        // certificate pin instead of falling back to trust on first use.
        store.host = host
        _activeLink.value = via
        client.connect(host, store.port, store.token, deviceName, store.fp,
            onPin = { store.fp = it }, onAgentId = { store.agentId = it })
    }

    fun startDiscovery() = discovery.start()
    fun stopDiscovery() = discovery.stop()

    fun disconnect() = cancel()

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
    is ConnState.Retrying -> "Retrying…"
    is ConnState.Failed -> reason
}
