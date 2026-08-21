package com.lukr99.relay.ui

import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Bolt
import androidx.compose.material.icons.filled.Chat
import androidx.compose.material.icons.filled.ContentCut
import androidx.compose.material.icons.filled.EditNote
import androidx.compose.material.icons.filled.Folder
import androidx.compose.material.icons.filled.Home
import androidx.compose.material.icons.filled.Keyboard
import androidx.compose.material.icons.filled.Lock
import androidx.compose.material.icons.filled.Mic
import androidx.compose.material.icons.filled.MicOff
import androidx.compose.material.icons.filled.OpenInBrowser
import androidx.compose.material.icons.filled.Pause
import androidx.compose.material.icons.filled.PhotoCamera
import androidx.compose.material.icons.filled.PlayArrow
import androidx.compose.material.icons.filled.PowerSettingsNew
import androidx.compose.material.icons.filled.Refresh
import androidx.compose.material.icons.filled.Settings
import androidx.compose.material.icons.filled.SkipNext
import androidx.compose.material.icons.filled.SkipPrevious
import androidx.compose.material.icons.filled.Star
import androidx.compose.material.icons.filled.Stop
import androidx.compose.material.icons.filled.Terminal
import androidx.compose.material.icons.filled.TouchApp
import androidx.compose.material.icons.filled.Videocam
import androidx.compose.material.icons.filled.VolumeDown
import androidx.compose.material.icons.filled.VolumeOff
import androidx.compose.material.icons.filled.VolumeUp
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector

/** Maps a layout icon name to a bundled Compose icon. Keep in sync with the agent's IconCatalog. */
fun iconFor(name: String?): ImageVector = when (name) {
    "play_pause", "play_arrow" -> Icons.Filled.PlayArrow
    "pause" -> Icons.Filled.Pause
    "stop" -> Icons.Filled.Stop
    "skip_previous" -> Icons.Filled.SkipPrevious
    "skip_next" -> Icons.Filled.SkipNext
    "volume_up" -> Icons.Filled.VolumeUp
    "volume_down" -> Icons.Filled.VolumeDown
    "volume_off" -> Icons.Filled.VolumeOff
    "mic" -> Icons.Filled.Mic
    "mic_off" -> Icons.Filled.MicOff
    "content_cut" -> Icons.Filled.ContentCut
    "photo_camera" -> Icons.Filled.PhotoCamera
    "videocam" -> Icons.Filled.Videocam
    "chat" -> Icons.Filled.Chat
    "edit_note" -> Icons.Filled.EditNote
    "keyboard" -> Icons.Filled.Keyboard
    "open_in_browser" -> Icons.Filled.OpenInBrowser
    "folder" -> Icons.Filled.Folder
    "terminal" -> Icons.Filled.Terminal
    "refresh" -> Icons.Filled.Refresh
    "lock" -> Icons.Filled.Lock
    "home" -> Icons.Filled.Home
    "settings" -> Icons.Filled.Settings
    "star" -> Icons.Filled.Star
    "bolt" -> Icons.Filled.Bolt
    "power" -> Icons.Filled.PowerSettingsNew
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
