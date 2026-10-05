package com.lukr99.relay.ui

import android.net.Uri
import androidx.compose.animation.AnimatedVisibility
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.CheckCircle
import androidx.compose.material.icons.filled.ErrorOutline
import androidx.compose.material.icons.filled.ExpandLess
import androidx.compose.material.icons.filled.ExpandMore
import androidx.compose.material.icons.filled.LinkOff
import androidx.compose.material.icons.filled.QrCodeScanner
import androidx.compose.material.icons.filled.Settings
import androidx.compose.material.icons.filled.Usb
import androidx.compose.material.icons.filled.Wifi
import androidx.compose.material3.Button
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableLongStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.unit.dp
import com.google.mlkit.vision.barcode.common.Barcode
import com.google.mlkit.vision.codescanner.GmsBarcodeScannerOptions
import com.google.mlkit.vision.codescanner.GmsBarcodeScanning
import com.lukr99.relay.net.ConnState
import com.lukr99.relay.net.DiscoveredAgent
import com.lukr99.relay.settings.ActiveLink
import com.lukr99.relay.settings.ConnectionLink
import kotlinx.coroutines.delay

/**
 * The connection page, shown whenever there is no deck. From the top: what is happening right now
 * (with Cancel, Try now or Reconnect), how to connect (Auto, Wi-Fi or Cable), then the ways to pair
 * a PC: the QR code, PCs found on the network, and the details by hand.
 */
@Composable
fun ConnectScreen(
    state: ConnState,
    target: String,
    isPaired: Boolean,
    link: ConnectionLink,
    activeLink: ActiveLink?,
    linkError: String?,
    searchingCable: Boolean,
    initialHost: String,
    initialPort: Int,
    initialToken: String,
    discovered: List<DiscoveredAgent>,
    savedAgentId: String,
    savedToken: String,
    onLink: (ConnectionLink) -> Unit,
    onReconnect: () -> Unit,
    onRetryNow: () -> Unit,
    onCancel: () -> Unit,
    onConnect: (host: String, port: Int, token: String, fp: String) -> Unit,
    onConnectUsb: (port: Int, token: String, onResult: (String?) -> Unit) -> Unit,
    onOpenSettings: () -> Unit,
) {
    val context = LocalContext.current
    // Saveable, so what the user typed survives a trip to Settings (App keeps this screen's state).
    var host by rememberSaveable { mutableStateOf(initialHost) }
    var port by rememberSaveable { mutableStateOf(if (initialPort > 0) initialPort.toString() else "8731") }
    var token by rememberSaveable { mutableStateOf(initialToken) }
    var manualOpen by rememberSaveable { mutableStateOf(!isPaired) }
    var scanError by remember { mutableStateOf<String?>(null) }
    var usbError by remember { mutableStateOf<String?>(null) }

    fun scan() {
        scanError = null
        val options = GmsBarcodeScannerOptions.Builder()
            .setBarcodeFormats(Barcode.FORMAT_QR_CODE)
            .enableAutoZoom()
            .build()
        GmsBarcodeScanning.getClient(context, options).startScan()
            .addOnSuccessListener { barcode ->
                val uri = barcode.rawValue?.let { runCatching { Uri.parse(it) }.getOrNull() }
                val h = uri?.getQueryParameter("host")
                val p = uri?.getQueryParameter("port")?.toIntOrNull() ?: 8731
                val t = uri?.getQueryParameter("token")
                val f = uri?.getQueryParameter("fp") ?: ""
                if (!h.isNullOrBlank() && !t.isNullOrBlank()) {
                    host = h; port = p.toString(); token = t
                    onConnect(h, p, t, f)
                } else {
                    scanError = "That isn't a Relay pairing code."
                }
            }
            .addOnCanceledListener { /* user backed out */ }
            .addOnFailureListener { scanError = it.message ?: "Couldn't open the scanner." }
    }

    Column(Modifier.fillMaxSize()) {
        Row(
            Modifier.fillMaxWidth().padding(start = 20.dp, top = 8.dp, end = 4.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            Column(Modifier.weight(1f)) {
                Text("Relay", style = MaterialTheme.typography.headlineSmall)
                Text(
                    "Connect to your PC",
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
            // Settings is reachable before any PC is paired (updates, version, about).
            IconButton(onClick = onOpenSettings) {
                Icon(Icons.Filled.Settings, contentDescription = "Settings")
            }
        }

        Column(
            Modifier
                .fillMaxSize()
                .verticalScroll(rememberScrollState())
                .padding(horizontal = 16.dp, vertical = 12.dp),
            horizontalAlignment = Alignment.CenterHorizontally,
        ) {
            Column(
                Modifier.widthIn(max = 760.dp).fillMaxWidth(),
                verticalArrangement = Arrangement.spacedBy(12.dp),
            ) {
                StatusCard(
                    state = state,
                    target = target,
                    isPaired = isPaired,
                    activeLink = activeLink,
                    linkError = linkError,
                    searchingCable = searchingCable,
                    onReconnect = onReconnect,
                    onRetryNow = onRetryNow,
                    onCancel = onCancel,
                )

                if (isPaired) {
                    SettingsCard("Connect via", "Force the cable or Wi-Fi, or let Relay pick.") {
                        LinkPicker(link, onLink, Modifier.padding(top = 8.dp, bottom = 4.dp))
                    }
                }

                SettingsCard(
                    if (isPaired) "Pair another PC" else "Pair your PC",
                    "Open Devices in Relay on the PC.",
                ) {
                    Spacer(Modifier.height(8.dp))
                    Button(onClick = { scan() }, modifier = Modifier.fillMaxWidth()) {
                        Icon(Icons.Filled.QrCodeScanner, contentDescription = null)
                        Spacer(Modifier.width(8.dp))
                        Text("Scan the QR code")
                    }
                    scanError?.let { ErrorText(it) }

                    if (discovered.isNotEmpty()) {
                        Spacer(Modifier.height(12.dp))
                        Text(
                            "Found on your network",
                            style = MaterialTheme.typography.labelLarge,
                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                        )
                        discovered.forEach { agent ->
                            val known = agent.id.isNotBlank() && agent.id == savedAgentId && savedToken.isNotBlank()
                            FoundAgentRow(agent, known) {
                                host = agent.host
                                port = agent.port.toString()
                                if (known) onConnect(agent.host, agent.port, savedToken, agent.fp)
                                else manualOpen = true
                            }
                        }
                    }

                    Spacer(Modifier.height(4.dp))
                    TextButton(onClick = { manualOpen = !manualOpen }, modifier = Modifier.fillMaxWidth()) {
                        Text("Enter the details by hand", Modifier.weight(1f))
                        Icon(if (manualOpen) Icons.Filled.ExpandLess else Icons.Filled.ExpandMore, contentDescription = null)
                    }
                    AnimatedVisibility(manualOpen) {
                        Column(verticalArrangement = Arrangement.spacedBy(10.dp)) {
                            OutlinedTextField(
                                value = host, onValueChange = { host = it },
                                label = { Text("PC address (IP)") }, singleLine = true,
                                keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Uri),
                                modifier = Modifier.fillMaxWidth(),
                            )
                            OutlinedTextField(
                                value = port, onValueChange = { port = it.filter(Char::isDigit) },
                                label = { Text("Port") }, singleLine = true,
                                keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                                modifier = Modifier.fillMaxWidth(),
                            )
                            OutlinedTextField(
                                value = token, onValueChange = { token = it },
                                label = { Text("Token") }, singleLine = true,
                                modifier = Modifier.fillMaxWidth(),
                            )
                            Button(
                                onClick = { onConnect(host.trim(), port.toIntOrNull() ?: 8731, token.trim(), "") },
                                enabled = host.isNotBlank() && token.isNotBlank(),
                                modifier = Modifier.fillMaxWidth(),
                            ) {
                                Icon(Icons.Filled.Wifi, contentDescription = null)
                                Spacer(Modifier.width(8.dp))
                                Text("Connect over Wi-Fi")
                            }
                            OutlinedButton(
                                onClick = {
                                    usbError = null
                                    onConnectUsb(port.toIntOrNull() ?: 8731, token.trim()) { peer ->
                                        if (peer == null) usbError =
                                            "No USB link found. Turn on USB tethering (Settings › Connections › Mobile Hotspot and Tethering › USB tethering), then try again."
                                    }
                                },
                                enabled = token.isNotBlank() && !searchingCable,
                                modifier = Modifier.fillMaxWidth(),
                            ) {
                                Icon(Icons.Filled.Usb, contentDescription = null)
                                Spacer(Modifier.width(8.dp))
                                Text("Connect over the USB cable")
                            }
                            Text(
                                "The cable needs only the token. Plug it in and turn on USB tethering first.",
                                style = MaterialTheme.typography.bodySmall,
                                color = MaterialTheme.colorScheme.onSurfaceVariant,
                            )
                            usbError?.let { ErrorText(it) }
                        }
                    }
                }
            }
        }
    }
}

/** What is happening now, in words, with the one or two actions that fit. */
@Composable
private fun StatusCard(
    state: ConnState,
    target: String,
    isPaired: Boolean,
    activeLink: ActiveLink?,
    linkError: String?,
    searchingCable: Boolean,
    onReconnect: () -> Unit,
    onRetryNow: () -> Unit,
    onCancel: () -> Unit,
) {
    val via = activeLink?.label ?: "Wi-Fi"
    when {
        searchingCable -> Status(
            title = "Looking for your PC on the cable",
            detail = "Checking the USB tether for Relay.",
            busy = true,
        ) { OutlinedButton(onClick = onCancel) { Text("Cancel") } }

        linkError != null -> Status(
            title = "Can't connect over ${if (activeLink == ActiveLink.Cable) "the cable" else "Wi-Fi"}",
            detail = linkError,
            icon = Icons.Filled.ErrorOutline,
            isError = true,
        ) { Button(onClick = onReconnect) { Text("Try again") } }

        state is ConnState.Connecting -> Status(
            title = "Connecting over $via",
            detail = target,
            busy = true,
        ) { OutlinedButton(onClick = onCancel) { Text("Cancel") } }

        state is ConnState.Retrying -> {
            // Count down to the next try once a second.
            var now by remember { mutableLongStateOf(System.currentTimeMillis()) }
            LaunchedEffect(state) {
                while (true) { now = System.currentTimeMillis(); delay(1000) }
            }
            val seconds = ((state.retryAtMs - now + 999) / 1000).coerceAtLeast(0)
            Status(
                title = "Can't reach your PC over $via",
                detail = "${state.reason}\nTrying $target again in ${seconds}s (try ${state.attempt}).",
                icon = Icons.Filled.ErrorOutline,
                isError = true,
            ) {
                Button(onClick = onRetryNow) { Text("Try now") }
                OutlinedButton(onClick = onCancel) { Text("Stop") }
            }
        }

        state is ConnState.Failed -> Status(
            title = "Couldn't connect",
            detail = state.reason,
            icon = Icons.Filled.ErrorOutline,
            isError = true,
        ) { if (isPaired) Button(onClick = onReconnect) { Text("Try again") } }

        state is ConnState.Connected -> Status(
            title = "Connected over $via",
            detail = "Loading the deck...",
            icon = Icons.Filled.CheckCircle,
        ) {}

        isPaired -> Status(
            title = "Not connected",
            detail = if (target.isNotBlank()) "Last PC: $target" else "Your PC is paired.",
            icon = Icons.Filled.LinkOff,
        ) { Button(onClick = onReconnect) { Text("Reconnect") } }

        else -> Status(
            title = "No PC paired yet",
            detail = "Start Relay on your PC, then scan the code on its Devices page.",
            icon = Icons.Filled.LinkOff,
        ) {}
    }
}

@Composable
private fun Status(
    title: String,
    detail: String,
    icon: ImageVector? = null,
    busy: Boolean = false,
    isError: Boolean = false,
    actions: @Composable () -> Unit,
) {
    Surface(
        shape = MaterialTheme.shapes.large,
        color = if (isError) MaterialTheme.colorScheme.errorContainer else MaterialTheme.colorScheme.surfaceVariant,
        modifier = Modifier.fillMaxWidth(),
    ) {
        val onColor = if (isError) MaterialTheme.colorScheme.onErrorContainer else MaterialTheme.colorScheme.onSurface
        Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                when {
                    busy -> CircularProgressIndicator(Modifier.size(22.dp), strokeWidth = 2.5.dp)
                    icon != null -> Icon(icon, contentDescription = null, tint = onColor)
                }
                Spacer(Modifier.width(12.dp))
                Text(title, style = MaterialTheme.typography.titleMedium, color = onColor)
            }
            if (detail.isNotBlank()) {
                Text(detail, style = MaterialTheme.typography.bodyMedium, color = onColor)
            }
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) { actions() }
        }
    }
}

@Composable
private fun FoundAgentRow(agent: DiscoveredAgent, known: Boolean, onClick: () -> Unit) {
    Surface(
        onClick = onClick,
        shape = MaterialTheme.shapes.medium,
        color = MaterialTheme.colorScheme.surface,
        modifier = Modifier.fillMaxWidth().padding(top = 6.dp),
    ) {
        Row(
            Modifier.fillMaxWidth().padding(horizontal = 14.dp, vertical = 12.dp),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically,
        ) {
            Column(Modifier.weight(1f)) {
                Text(agent.displayName, style = MaterialTheme.typography.bodyLarge)
                Text(
                    "${agent.host}:${agent.port}",
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
            Text(
                if (known) "Reconnect" else "Use",
                style = MaterialTheme.typography.labelLarge,
                color = MaterialTheme.colorScheme.primary,
            )
        }
    }
}

@Composable
private fun ErrorText(text: String) {
    Text(
        text,
        color = MaterialTheme.colorScheme.error,
        style = MaterialTheme.typography.bodySmall,
        modifier = Modifier.padding(top = 6.dp),
    )
}
