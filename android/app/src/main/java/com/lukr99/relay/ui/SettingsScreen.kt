package com.lukr99.relay.ui

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.FilterChip
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp

private data class SizeOption(val label: String, val dp: Int)

private val sizeOptions = listOf(
    SizeOption("Compact", 76),
    SizeOption("Medium", 96),
    SizeOption("Large", 124),
)

@Composable
fun SettingsScreen(
    agentName: String?,
    host: String,
    port: Int,
    cardMinDp: Int,
    onSetCardMinDp: (Int) -> Unit,
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
        SectionHeader("Card size")
        Text(
            "Smaller cards fit more per row — handy in portrait.",
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
        Spacer(Modifier.height(8.dp))
        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            sizeOptions.forEach { opt ->
                FilterChip(
                    selected = cardMinDp == opt.dp,
                    onClick = { onSetCardMinDp(opt.dp) },
                    label = { Text(opt.label) },
                )
            }
        }

        Spacer(Modifier.height(24.dp))
        SectionHeader("Coming soon")
        Text(
            "Custom layouts, button & macro editor, per-orientation grids.",
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
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
