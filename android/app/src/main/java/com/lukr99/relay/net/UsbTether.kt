package com.lukr99.relay.net

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.async
import kotlinx.coroutines.awaitAll
import kotlinx.coroutines.coroutineScope
import kotlinx.coroutines.withContext
import java.io.File
import java.net.Inet4Address
import java.net.InetSocketAddress
import java.net.NetworkInterface
import java.net.Socket

/**
 * Finds the PC's IP over a USB-tethering link so the phone can connect by cable with no typing.
 *
 * When USB tethering is on, the phone exposes a point-to-point interface (rndis0 / usb0 / ncm0) and
 * the PC sits on the same /24. We resolve the PC by reading the tether interface's ARP peers first
 * (fast, exact), then—if that is unavailable—by probing the /24 for the agent's port.
 */
object UsbTether {
    private val TetherIface = Regex("^(rndis|usb|ncm)\\d*", RegexOption.IGNORE_CASE)
    private const val ConnectTimeoutMs = 400

    /** The PC's tether IP with the agent's [port] open, or null if no USB link / agent is found. */
    suspend fun discoverPeer(port: Int): String? = withContext(Dispatchers.IO) {
        val ifaces = runCatching {
            NetworkInterface.getNetworkInterfaces().toList()
                .filter { runCatching { it.isUp }.getOrDefault(false) && TetherIface.containsMatchIn(it.name) }
        }.getOrDefault(emptyList())
        if (ifaces.isEmpty()) return@withContext null

        val ifaceNames = ifaces.map { it.name }.toSet()
        val locals = ifaces.flatMap { nif ->
            nif.interfaceAddresses.filter { it.address is Inet4Address }.map { it }
        }

        // 1) ARP: the tether link is point-to-point, so its one peer is the PC.
        for (ip in readArpPeers(ifaceNames)) if (probe(ip, port)) return@withContext ip

        // 2) Fallback: scan the tether /24 for a host with the agent's port open.
        for (ia in locals) {
            val self = ia.address as Inet4Address
            val hosts = subnetHosts(self, ia.networkPrefixLength.toInt())
            val found = coroutineScope {
                hosts.map { host -> async { if (probe(host, port)) host else null } }.awaitAll()
            }.firstOrNull { it != null }
            if (found != null) return@withContext found
        }
        null
    }

    /** IPs seen on the given tether interfaces in /proc/net/arp (complete entries, no broadcast). */
    private fun readArpPeers(ifaceNames: Set<String>): List<String> {
        val out = ArrayList<String>()
        runCatching {
            File("/proc/net/arp").useLines { lines ->
                for (line in lines.drop(1)) {
                    val f = line.trim().split(Regex("\\s+"))
                    if (f.size < 6) continue
                    val ip = f[0]; val flags = f[2]; val device = f[5]
                    if (device in ifaceNames && flags != "0x0" && !ip.endsWith(".255")) out.add(ip)
                }
            }
        }
        return out
    }

    /** Host addresses of [self]'s subnet (capped to a /24 so the scan stays bounded), minus self/net/broadcast. */
    private fun subnetHosts(self: Inet4Address, prefixLen: Int): List<String> {
        val b = self.address
        val selfLast = b[3].toInt() and 0xFF
        val prefix = "${b[0].toInt() and 0xFF}.${b[1].toInt() and 0xFF}.${b[2].toInt() and 0xFF}"
        if (prefixLen < 24) return (1..254).filter { it != selfLast }.map { "$prefix.$it" }
        return (1..254).filter { it != selfLast }.map { "$prefix.$it" }
    }

    private suspend fun probe(ip: String, port: Int): Boolean = withContext(Dispatchers.IO) {
        runCatching {
            Socket().use { it.connect(InetSocketAddress(ip, port), ConnectTimeoutMs); true }
        }.getOrDefault(false)
    }
}
