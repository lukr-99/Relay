package com.lukr99.relay.ui

import android.net.Uri
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.QrCodeScanner
import androidx.compose.material3.Button
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.unit.dp
import com.google.mlkit.vision.barcode.common.Barcode
import com.google.mlkit.vision.codescanner.GmsBarcodeScannerOptions
import com.google.mlkit.vision.codescanner.GmsBarcodeScanning
import com.lukr99.relay.net.ConnState
import com.lukr99.relay.net.DiscoveredAgent

@Composable
fun PairScreen(
    state: ConnState,
    initialHost: String,
    initialPort: Int,
    initialToken: String,
    discovered: List<DiscoveredAgent> = emptyList(),
    savedAgentId: String = "",
    savedToken: String = "",
    onConnect: (host: String, port: Int, token: String, fp: String) -> Unit,
) {
    val context = LocalContext.current
    var host by remember { mutableStateOf(initialHost) }
    var port by remember { mutableStateOf(if (initialPort > 0) initialPort.toString() else "8731") }
    var token by remember { mutableStateOf(initialToken) }
    var scanError by remember { mutableStateOf<String?>(null) }

    fun scan() {
        scanError = null
        val options = GmsBarcodeScannerOptions.Builder()
            .setBarcodeFormats(Barcode.FORMAT_QR_CODE)
            .enableAutoZoom()
            .build()
        GmsBarcodeScanning.getClient(context, options).startScan()
            .addOnSuccessListener { barcode ->
                val raw = barcode.rawValue
                val uri = raw?.let { runCatching { Uri.parse(it) }.getOrNull() }
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

    Column(
        modifier = Modifier.fillMaxSize().padding(24.dp),
        verticalArrangement = Arrangement.Center,
        horizontalAlignment = Alignment.CenterHorizontally,
    ) {
        Text("Relay", style = MaterialTheme.typography.headlineMedium)
        Spacer(Modifier.height(4.dp))
        Text("Pair with your PC", style = MaterialTheme.typography.bodyMedium)
        Spacer(Modifier.height(24.dp))

        Button(onClick = { scan() }, modifier = Modifier.fillMaxWidth()) {
            Icon(Icons.Filled.QrCodeScanner, contentDescription = null)
            Spacer(Modifier.width(8.dp))
            Text("Scan QR code")
        }
        Spacer(Modifier.height(8.dp))
        Text(
            "Open Devices on the PC and scan the code.",
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )

        if (discovered.isNotEmpty()) {
            Spacer(Modifier.height(24.dp))
            Text("Found on your network", style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant)
            Spacer(Modifier.height(8.dp))
            discovered.forEach { agent ->
                val known = agent.id.isNotBlank() && agent.id == savedAgentId && savedToken.isNotBlank()
                Surface(
                    onClick = {
                        host = agent.host
                        port = agent.port.toString()
                        if (known) onConnect(agent.host, agent.port, savedToken, agent.fp)
                    },
                    shape = MaterialTheme.shapes.medium,
                    color = MaterialTheme.colorScheme.surfaceVariant,
                    modifier = Modifier.fillMaxWidth().padding(vertical = 4.dp),
                ) {
                    Row(
                        Modifier.fillMaxWidth().padding(horizontal = 14.dp, vertical = 12.dp),
                        horizontalArrangement = Arrangement.SpaceBetween,
                        verticalAlignment = Alignment.CenterVertically,
                    ) {
                        Column {
                            Text(agent.displayName, style = MaterialTheme.typography.bodyLarge)
                            Text("${agent.host}:${agent.port}", style = MaterialTheme.typography.bodySmall,
                                color = MaterialTheme.colorScheme.onSurfaceVariant)
                        }
                        Text(
                            if (known) "Reconnect" else "Use",
                            style = MaterialTheme.typography.labelLarge,
                            color = MaterialTheme.colorScheme.primary,
                        )
                    }
                }
            }
        }

        Spacer(Modifier.height(24.dp))
        Text("or enter manually", style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant)
        Spacer(Modifier.height(12.dp))

        OutlinedTextField(
            value = host, onValueChange = { host = it },
            label = { Text("Host (PC IP)") }, singleLine = true,
            keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Uri),
            modifier = Modifier.fillMaxWidth(),
        )
        Spacer(Modifier.height(12.dp))
        OutlinedTextField(
            value = port, onValueChange = { port = it.filter(Char::isDigit) },
            label = { Text("Port") }, singleLine = true,
            keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
            modifier = Modifier.fillMaxWidth(),
        )
        Spacer(Modifier.height(12.dp))
        OutlinedTextField(
            value = token, onValueChange = { token = it },
            label = { Text("Token") }, singleLine = true,
            modifier = Modifier.fillMaxWidth(),
        )
        Spacer(Modifier.height(20.dp))
        Button(
            onClick = { onConnect(host.trim(), port.toIntOrNull() ?: 8731, token.trim(), "") },
            enabled = host.isNotBlank() && token.isNotBlank() && state !is ConnState.Connecting,
            modifier = Modifier.fillMaxWidth(),
        ) { Text(if (state is ConnState.Connecting) "Connecting…" else "Connect") }

        val err = scanError ?: (state as? ConnState.Failed)?.reason
        if (err != null) {
            Spacer(Modifier.height(16.dp))
            Text(err, color = MaterialTheme.colorScheme.error, style = MaterialTheme.typography.bodyMedium)
        }
    }
}
