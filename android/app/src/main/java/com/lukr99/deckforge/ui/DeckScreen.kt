package com.lukr99.deckforge.ui

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.aspectRatio
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.grid.GridCells
import androidx.compose.foundation.lazy.grid.LazyVerticalGrid
import androidx.compose.foundation.lazy.grid.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Settings
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.luminance
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import com.lukr99.deckforge.net.ButtonDef
import com.lukr99.deckforge.net.Layout

@Composable
fun DeckScreen(
    layout: Layout,
    agentName: String?,
    cardMinDp: Int,
    onPress: (String) -> Unit,
    onOpenSettings: () -> Unit,
) {
    val page = layout.pages.firstOrNull { it.id == layout.activePage } ?: layout.pages.firstOrNull()
    val buttons = page?.buttons?.sortedWith(compareBy({ it.row }, { it.col })) ?: emptyList()

    Column(Modifier.fillMaxSize().padding(12.dp)) {
        Row(
            Modifier.fillMaxWidth().padding(bottom = 8.dp),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically,
        ) {
            Text(
                agentName?.let { "Connected · $it" } ?: "Connected",
                style = MaterialTheme.typography.titleMedium,
            )
            IconButton(onClick = onOpenSettings) {
                Icon(Icons.Filled.Settings, contentDescription = "Settings")
            }
        }

        // Adaptive columns keep cards near cardMinDp — more/tighter in portrait, fuller in landscape.
        LazyVerticalGrid(
            columns = GridCells.Adaptive(minSize = cardMinDp.dp),
            horizontalArrangement = Arrangement.spacedBy(10.dp),
            verticalArrangement = Arrangement.spacedBy(10.dp),
            modifier = Modifier.fillMaxSize(),
        ) {
            items(buttons, key = { it.id }) { b -> DeckButton(b, onPress) }
        }
    }
}

@Composable
private fun DeckButton(b: ButtonDef, onPress: (String) -> Unit) {
    val bg = parseColor(b.color, MaterialTheme.colorScheme.surface)
    val fg = if (bg.luminance() > 0.5f) Color(0xFF10141A) else Color.White
    Box(
        modifier = Modifier
            .fillMaxWidth()
            .aspectRatio(1f)
            .clip(RoundedCornerShape(16.dp))
            .background(bg)
            .clickable { onPress(b.id) }
            .padding(8.dp),
        contentAlignment = Alignment.Center,
    ) {
        Column(horizontalAlignment = Alignment.CenterHorizontally) {
            Icon(iconFor(b.icon), contentDescription = b.label, tint = fg)
            Text(
                b.label,
                color = fg,
                textAlign = TextAlign.Center,
                maxLines = 2,
                overflow = TextOverflow.Ellipsis,
                style = MaterialTheme.typography.labelMedium,
                modifier = Modifier.padding(top = 6.dp),
            )
        }
    }
}
