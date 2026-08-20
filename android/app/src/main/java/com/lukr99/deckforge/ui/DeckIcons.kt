package com.lukr99.deckforge.ui

import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Chat
import androidx.compose.material.icons.filled.ContentCut
import androidx.compose.material.icons.filled.EditNote
import androidx.compose.material.icons.filled.Keyboard
import androidx.compose.material.icons.filled.MicOff
import androidx.compose.material.icons.filled.OpenInBrowser
import androidx.compose.material.icons.filled.PlayArrow
import androidx.compose.material.icons.filled.SkipNext
import androidx.compose.material.icons.filled.SkipPrevious
import androidx.compose.material.icons.filled.TouchApp
import androidx.compose.material.icons.filled.VolumeOff
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector

/** Maps a layout icon name (Material Symbol style) to a bundled Compose icon. */
fun iconFor(name: String?): ImageVector = when (name) {
    "play_pause", "play_arrow" -> Icons.Filled.PlayArrow
    "skip_previous" -> Icons.Filled.SkipPrevious
    "skip_next" -> Icons.Filled.SkipNext
    "volume_off" -> Icons.Filled.VolumeOff
    "mic_off" -> Icons.Filled.MicOff
    "content_cut" -> Icons.Filled.ContentCut
    "chat" -> Icons.Filled.Chat
    "edit_note" -> Icons.Filled.EditNote
    "open_in_browser" -> Icons.Filled.OpenInBrowser
    "keyboard" -> Icons.Filled.Keyboard
    else -> Icons.Filled.TouchApp
}

/** Parses "#RRGGBB" (or "#AARRGGBB") to a Compose [Color], falling back to a neutral surface. */
fun parseColor(hex: String?, fallback: Color): Color {
    if (hex.isNullOrBlank()) return fallback
    return runCatching {
        val s = hex.removePrefix("#")
        val v = s.toLong(16)
        when (s.length) {
            6 -> Color(0xFF000000 or v)
            8 -> Color(v)
            else -> fallback
        }
    }.getOrDefault(fallback)
}
