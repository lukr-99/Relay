package com.lukr99.relay.ui.theme

import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.graphics.Color

private val DarkColors = darkColorScheme(
    primary = Color(0xFF7C5CFF),
    secondary = Color(0xFF16A085),
    background = Color(0xFF0E1116),
    surface = Color(0xFF161B22),
    onBackground = Color(0xFFE6E6E6),
    onSurface = Color(0xFFE6E6E6),
)

private val LightColors = lightColorScheme(
    primary = Color(0xFF5B3FE0),
    secondary = Color(0xFF12897A),
)

@Composable
fun RelayTheme(dark: Boolean = isSystemInDarkTheme(), content: @Composable () -> Unit) {
    MaterialTheme(colorScheme = if (dark) DarkColors else LightColors, content = content)
}
