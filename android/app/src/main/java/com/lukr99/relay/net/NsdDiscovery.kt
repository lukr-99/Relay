package com.lukr99.relay.net

import android.content.Context
import android.net.nsd.NsdManager
import android.net.nsd.NsdServiceInfo
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import java.util.concurrent.ConcurrentLinkedQueue
import java.util.concurrent.atomic.AtomicBoolean

/** A Relay agent found on the LAN via mDNS. TXT carries id/fp/name; the token is never advertised. */
data class DiscoveredAgent(
    val serviceName: String,
    val host: String,
    val port: Int,
    val id: String,
    val fp: String,
    val displayName: String,
)

/**
 * Browses `_relay._tcp` with Android's [NsdManager] so the phone can find the PC without a QR.
 * Resolves are serialized through a queue — [NsdManager.resolveService] rejects concurrent calls
 * with "listener already in use" on older APIs.
 */
class NsdDiscovery(context: Context) {
    private val nsd = context.applicationContext.getSystemService(Context.NSD_SERVICE) as NsdManager

    private val _agents = MutableStateFlow<List<DiscoveredAgent>>(emptyList())
    val agents: StateFlow<List<DiscoveredAgent>> = _agents.asStateFlow()

    private var discoveryListener: NsdManager.DiscoveryListener? = null
    private val resolveQueue = ConcurrentLinkedQueue<NsdServiceInfo>()
    private val resolving = AtomicBoolean(false)

    fun start() {
        if (discoveryListener != null) return
        val listener = object : NsdManager.DiscoveryListener {
            override fun onDiscoveryStarted(serviceType: String) {}
            override fun onDiscoveryStopped(serviceType: String) {}
            override fun onStartDiscoveryFailed(serviceType: String, errorCode: Int) {}
            override fun onStopDiscoveryFailed(serviceType: String, errorCode: Int) {}

            override fun onServiceFound(info: NsdServiceInfo) {
                if (info.serviceType.contains("_relay._tcp")) enqueueResolve(info)
            }

            override fun onServiceLost(info: NsdServiceInfo) {
                _agents.value = _agents.value.filterNot { it.serviceName == info.serviceName }
            }
        }
        discoveryListener = listener
        runCatching { nsd.discoverServices(SERVICE_TYPE, NsdManager.PROTOCOL_DNS_SD, listener) }
    }

    fun stop() {
        discoveryListener?.let { runCatching { nsd.stopServiceDiscovery(it) } }
        discoveryListener = null
        resolveQueue.clear()
        resolving.set(false)
        _agents.value = emptyList()
    }

    private fun enqueueResolve(info: NsdServiceInfo) {
        resolveQueue.add(info)
        pumpResolve()
    }

    // resolveService/host are deprecated on API 34+, but their replacements don't exist below it and
    // minSdk is 26 — the deprecated path is the correct back-compatible choice here.
    @Suppress("DEPRECATION")
    private fun pumpResolve() {
        if (!resolving.compareAndSet(false, true)) return
        val next = resolveQueue.poll()
        if (next == null) { resolving.set(false); return }

        nsd.resolveService(next, object : NsdManager.ResolveListener {
            override fun onResolveFailed(info: NsdServiceInfo, errorCode: Int) {
                resolving.set(false); pumpResolve()
            }

            override fun onServiceResolved(info: NsdServiceInfo) {
                addOrUpdate(info)
                resolving.set(false); pumpResolve()
            }
        })
    }

    @Suppress("DEPRECATION")
    private fun addOrUpdate(info: NsdServiceInfo) {
        val host = info.host?.hostAddress ?: return
        val txt = info.attributes ?: emptyMap()
        fun attr(k: String): String = txt[k]?.let { String(it, Charsets.UTF_8) } ?: ""
        val agent = DiscoveredAgent(
            serviceName = info.serviceName ?: host,
            host = host,
            port = info.port,
            id = attr("id"),
            fp = attr("fp"),
            displayName = attr("name").ifBlank { info.serviceName ?: host },
        )
        _agents.value = (_agents.value.filterNot { it.serviceName == agent.serviceName } + agent)
            .sortedBy { it.displayName.lowercase() }
    }

    private companion object {
        const val SERVICE_TYPE = "_relay._tcp."
    }
}
