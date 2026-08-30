package com.lukr99.relay.ui

import android.content.Intent
import android.net.Uri
import android.provider.Settings
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
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
import com.lukr99.relay.settings.PairingStore
import kotlinx.coroutines.launch

@Composable
fun SettingsScreen(
    agentName: String?,
    host: String,
    port: Int,
    onDisconnect: () -> Unit,
    onBack: () -> Unit,
) {
    Column(Modifier.fillMaxSize().padding(16.dp)) {
        Row(verticalAlignment = Alignment.CenterVertically, modifier = Modifier.padding(bottom = 12.dp)) {
            IconButton(onClick = onBack) {
                Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Back")
            }
            Text("Settings", style = MaterialTheme.typography.headlineSmall)
        }

        SectionHeader("Connection")
        Text(agentName?.let { "Agent: $it" } ?: "Agent: —", style = MaterialTheme.typography.bodyLarge)
        Text("$host:$port", style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurfaceVariant)
        Spacer(Modifier.height(10.dp))
        Button(
            onClick = onDisconnect,
            colors = ButtonDefaults.buttonColors(containerColor = MaterialTheme.colorScheme.errorContainer),
        ) { Text("Disconnect", color = MaterialTheme.colorScheme.onErrorContainer) }

        Spacer(Modifier.height(24.dp))
        SectionHeader("Deck")
        Text(
            "The deck — grid size, buttons, pages and actions — is edited in the Relay app on your PC. " +
                "Changes appear here instantly.",
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )

        Spacer(Modifier.height(24.dp))
        UpdatesSection()
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
        busy = true; status = "Checking…"; available = null
        scope.launch {
            val rel = AppUpdater.check(current)
            available = rel
            status = if (rel == null) "You're up to date (v$current)." else "Update available: v${rel.version}."
            busy = false
        }
    }

    // Auto-check when Settings opens, if enabled.
    LaunchedEffect(Unit) { if (store.autoUpdate) runCheck() }

    SectionHeader("Updates")
    Text(status, style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurfaceVariant)
    Spacer(Modifier.height(10.dp))
    Row(verticalAlignment = Alignment.CenterVertically) {
        Button(onClick = { runCheck() }, enabled = !busy) { Text("Check for updates") }
        val rel = available
        if (rel != null) {
            Spacer(Modifier.width(8.dp))
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
                    busy = true; status = "Downloading v${rel.version}…"
                    scope.launch {
                        try {
                            val apk = AppUpdater.download(context, rel)
                            AppUpdater.installApk(context, apk)
                            status = "Opening the installer…"
                        } catch (e: Exception) {
                            status = "Download failed: ${e.message}"
                        } finally {
                            busy = false
                        }
                    }
                },
            ) { Text("Download & install") }
        }
    }
    Spacer(Modifier.height(10.dp))
    Row(verticalAlignment = Alignment.CenterVertically) {
        Switch(checked = auto, onCheckedChange = { auto = it; store.autoUpdate = it })
        Spacer(Modifier.width(8.dp))
        Text("Check for updates on launch", style = MaterialTheme.typography.bodyMedium)
    }
}

@Composable
private fun SectionHeader(text: String) {
    Text(
        text,
        style = MaterialTheme.typography.titleSmall,
        fontWeight = FontWeight.SemiBold,
        color = MaterialTheme.colorScheme.primary,
        modifier = Modifier.padding(bottom = 4.dp),
    )
}
