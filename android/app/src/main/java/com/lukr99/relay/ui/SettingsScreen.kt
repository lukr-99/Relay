package com.lukr99.relay.ui

import android.content.Intent
import android.net.Uri
import android.provider.Settings
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ColumnScope
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.RowScope
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.text.selection.SelectionContainer
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Surface
import androidx.compose.material3.Switch
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import com.lukr99.relay.net.AppRelease
import com.lukr99.relay.net.AppUpdater
import com.lukr99.relay.net.ConnState
import com.lukr99.relay.settings.ActiveLink
import com.lukr99.relay.settings.ConnectionLink
import com.lukr99.relay.settings.PairingStore
import kotlinx.coroutines.launch

private const val REPO_URL = "https://github.com/lukr-99/Relay"
private const val RELEASES_URL = "$REPO_URL/releases"

/**
 * The settings page. It works with or without a connection: the Connection card shows the live
 * agent when connected and a "Not connected" state otherwise, while Updates and About never need
 * an agent. Three sections, so no jump chips (the settings-pages rule adds them from 4).
 */
@Composable
fun SettingsScreen(
    state: ConnState,
    agentName: String?,
    host: String,
    port: Int,
    link: ConnectionLink,
    activeLink: ActiveLink?,
    onLink: (ConnectionLink) -> Unit,
    onDisconnect: () -> Unit,
    onBack: () -> Unit,
) {
    Column(Modifier.fillMaxSize()) {
        Row(
            verticalAlignment = Alignment.CenterVertically,
            modifier = Modifier.padding(start = 4.dp, top = 8.dp, end = 16.dp, bottom = 4.dp),
        ) {
            IconButton(onClick = onBack) {
                Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Back")
            }
            Text("Settings", style = MaterialTheme.typography.headlineSmall)
        }

        Column(
            Modifier
                .fillMaxSize()
                .verticalScroll(rememberScrollState())
                .padding(horizontal = 16.dp, vertical = 8.dp),
            horizontalAlignment = Alignment.CenterHorizontally,
        ) {
            Column(
                Modifier.widthIn(max = 760.dp).fillMaxWidth(),
                verticalArrangement = Arrangement.spacedBy(12.dp),
            ) {
                ConnectionSection(state, agentName, host, port, link, activeLink, onLink, onDisconnect)
                UpdatesSection()
                AboutSection()
            }
        }
    }
}

@Composable
private fun ConnectionSection(
    state: ConnState,
    agentName: String?,
    host: String,
    port: Int,
    link: ConnectionLink,
    activeLink: ActiveLink?,
    onLink: (ConnectionLink) -> Unit,
    onDisconnect: () -> Unit,
) {
    val connected = state is ConnState.Connected
    SettingsCard("Connection", "The PC this phone controls.") {
        InfoRow(
            "Status",
            when (state) {
                ConnState.Connected -> "Connected over ${activeLink?.label ?: "Wi-Fi"}"
                ConnState.Connecting -> "Connecting..."
                is ConnState.Retrying -> "Retrying. ${state.reason}"
                is ConnState.Failed -> state.reason
                ConnState.Disconnected -> "Not connected"
            },
        )
        RowDivider()
        Column(Modifier.fillMaxWidth().padding(vertical = 12.dp)) {
            Text("Connect via", style = MaterialTheme.typography.bodyLarge, modifier = Modifier.padding(bottom = 8.dp))
            LinkPicker(link, onLink)
        }
        if (connected) {
            RowDivider()
            InfoRow("Agent", agentName ?: "Unknown")
            RowDivider()
            InfoRow("Network address", "$host:$port")
            RowDivider()
            InfoRow(
                "Deck",
                "Edit the grid, buttons, pages and actions in the Relay app on your PC. Changes show up here at once.",
            )
            RowDivider()
            SettingsRow {
                Button(
                    onClick = onDisconnect,
                    colors = ButtonDefaults.buttonColors(containerColor = MaterialTheme.colorScheme.errorContainer),
                ) { Text("Disconnect", color = MaterialTheme.colorScheme.onErrorContainer) }
            }
        } else {
            RowDivider()
            InfoRow(
                "Pairing",
                if (host.isNotBlank()) "Last PC: $host:$port. Go back to pair or reconnect."
                else "Go back to pair with your PC.",
            )
        }
    }
}

/** Checks GitHub for a newer app APK (manual button + on-launch auto-check) and installs it. */
@Composable
private fun UpdatesSection() {
    val context = LocalContext.current
    val scope = rememberCoroutineScope()
    val store = remember { PairingStore(context) }
    val current = remember { AppUpdater.currentVersion(context) }

    var status by remember { mutableStateOf("You're on v$current.") }
    var available by remember { mutableStateOf<AppRelease?>(null) }
    var busy by remember { mutableStateOf(false) }
    var auto by remember { mutableStateOf(store.autoUpdate) }

    fun runCheck() {
        if (busy) return
        busy = true; status = "Checking..."; available = null
        scope.launch {
            val rel = AppUpdater.check(current)
            available = rel
            status = if (rel == null) "You're up to date (v$current)." else "Update available: v${rel.version}."
            busy = false
        }
    }

    // Auto-check when Settings opens, if enabled.
    LaunchedEffect(Unit) { if (store.autoUpdate) runCheck() }

    SettingsCard("Updates", "New versions of the phone app from GitHub.") {
        SettingsRow {
            Column(Modifier.weight(1f)) {
                Text("Version", style = MaterialTheme.typography.bodyLarge)
                Text(status, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
            }
            OutlinedButton(onClick = { runCheck() }, enabled = !busy) {
                if (busy) {
                    CircularProgressIndicator(Modifier.size(16.dp), strokeWidth = 2.dp)
                    Spacer(Modifier.width(8.dp))
                }
                Text("Check now")
            }
        }
        val rel = available
        if (rel != null) {
            RowDivider()
            SettingsRow {
                Text("v${rel.version} is ready", style = MaterialTheme.typography.bodyLarge, modifier = Modifier.weight(1f))
                Button(
                    enabled = !busy,
                    onClick = {
                        if (!AppUpdater.canInstall(context)) {
                            // Route the user to allow "install unknown apps", then they retry.
                            context.startActivity(
                                Intent(
                                    Settings.ACTION_MANAGE_UNKNOWN_APP_SOURCES,
                                    Uri.parse("package:${context.packageName}"),
                                ),
                            )
                            return@Button
                        }
                        busy = true; status = "Downloading v${rel.version}..."
                        scope.launch {
                            try {
                                val apk = AppUpdater.download(context, rel)
                                AppUpdater.installApk(context, apk)
                                status = "Opening the installer..."
                            } catch (e: Exception) {
                                status = "Download failed: ${e.message}"
                            } finally {
                                busy = false
                            }
                        }
                    },
                ) { Text("Install") }
            }
        }
        RowDivider()
        SettingsRow {
            Column(Modifier.weight(1f)) {
                Text("Check on launch", style = MaterialTheme.typography.bodyLarge)
                Text(
                    "Look for a new version each time the app opens.",
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
            Switch(checked = auto, onCheckedChange = { auto = it; store.autoUpdate = it })
        }
        RowDivider()
        LinkRow("Release notes", "What changed in each version.", RELEASES_URL)
    }
}

@Composable
private fun AboutSection() {
    val context = LocalContext.current
    val version = remember { AppUpdater.currentVersion(context) }
    SettingsCard("About", "Relay turns this phone into a control deck for your PC.") {
        InfoRow("App version", "v$version")
        RowDivider()
        LinkRow("Project page", "Source code and the PC app.", REPO_URL)
    }
}
