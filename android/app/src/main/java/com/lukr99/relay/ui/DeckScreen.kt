package com.lukr99.relay.ui

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.gestures.awaitEachGesture
import androidx.compose.foundation.gestures.awaitFirstDown
import androidx.compose.foundation.gestures.waitForUpOrCancellation
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.BoxWithConstraints
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.aspectRatio
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.lazy.grid.GridCells
import androidx.compose.foundation.lazy.grid.LazyVerticalGrid
import androidx.compose.foundation.lazy.grid.items
import androidx.compose.foundation.pager.HorizontalPager
import androidx.compose.foundation.pager.rememberPagerState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Settings
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.luminance
import androidx.compose.ui.hapticfeedback.HapticFeedbackType
import androidx.compose.ui.platform.LocalHapticFeedback
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.lukr99.relay.net.ButtonDef
import com.lukr99.relay.net.Layout
import com.lukr99.relay.net.Page

@Composable
fun DeckScreen(
    layout: Layout,
    agentName: String?,
    states: Map<String, Boolean>,
    onPress: (String) -> Unit,
    onHoldStart: (String) -> Unit,
    onHoldEnd: (String) -> Unit,
    onOpenSettings: () -> Unit,
) {
    val pages = layout.pages.ifEmpty { listOf(Page(id = "p-main", name = "Main")) }

    Column(Modifier.fillMaxSize().padding(12.dp)) {
        val startPage = pages.indexOfFirst { it.id == layout.activePage }.coerceAtLeast(0)
        val pagerState = rememberPagerState(initialPage = startPage, pageCount = { pages.size })

        Row(
            Modifier.fillMaxWidth().padding(bottom = 8.dp),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically,
        ) {
            val page = pages.getOrNull(pagerState.currentPage)
            Text(
                buildString {
                    append(agentName?.let { "Connected · $it" } ?: "Connected")
                    if (pages.size > 1 && page != null) append("   ·   ${page.name}")
                },
                style = MaterialTheme.typography.titleMedium,
            )
            IconButton(onClick = onOpenSettings) {
                Icon(Icons.Filled.Settings, contentDescription = "Settings")
            }
        }

        HorizontalPager(state = pagerState, modifier = Modifier.weight(1f)) { index ->
            val page = pages[index]
            val cols = (page.grid?.cols ?: layout.grid.cols).coerceAtLeast(1)
            val rows = (page.grid?.rows ?: layout.grid.rows).coerceAtLeast(1)
            DeckGrid(page, cols, rows, states, onPress, onHoldStart, onHoldEnd)
        }

        if (pages.size > 1) {
            Row(
                Modifier.fillMaxWidth().padding(top = 10.dp),
                horizontalArrangement = Arrangement.Center,
            ) {
                repeat(pages.size) { i ->
                    val on = i == pagerState.currentPage
                    Box(
                        Modifier
                            .padding(horizontal = 4.dp)
                            .size(if (on) 9.dp else 7.dp)
                            .clip(CircleShape)
                            .background(
                                if (on) MaterialTheme.colorScheme.primary
                                else MaterialTheme.colorScheme.onSurface.copy(alpha = 0.3f)
                            )
                    )
                }
            }
        }
    }
}

@Composable
private fun DeckGrid(
    page: Page,
    cols: Int,
    rows: Int,
    states: Map<String, Boolean>,
    onPress: (String) -> Unit,
    onHoldStart: (String) -> Unit,
    onHoldEnd: (String) -> Unit,
) {
    // Fixed cols/rows from the layout, buttons placed at their exact (row, col) — matches the editor.
    LazyVerticalGrid(
        columns = GridCells.Fixed(cols),
        horizontalArrangement = Arrangement.spacedBy(10.dp),
        verticalArrangement = Arrangement.spacedBy(10.dp),
        modifier = Modifier.fillMaxSize(),
    ) {
        items(cols * rows) { index ->
            val r = index / cols
            val c = index % cols
            val b = page.buttons.firstOrNull { it.row == r && it.col == c }
            if (b != null) DeckButton(b, states[b.id] == true, onPress, onHoldStart, onHoldEnd)
            else Box(Modifier.fillMaxWidth().aspectRatio(1f))
        }
    }
}

@Composable
private fun DeckButton(
    b: ButtonDef,
    toggledOn: Boolean,
    onPress: (String) -> Unit,
    onHoldStart: (String) -> Unit,
    onHoldEnd: (String) -> Unit,
) {
    val bg = if (toggledOn) MaterialTheme.colorScheme.primary else parseColor(b.color, MaterialTheme.colorScheme.surface)
    val fg = if (bg.luminance() > 0.5f) Color(0xFF10141A) else Color.White
    val haptics = LocalHapticFeedback.current
    // Buttons with a hold action are push-and-hold (PTT): fire on finger-down, release on up.
    val gesture = if (b.hasHold != null) {
        Modifier.pointerInput(b.id) {
            awaitEachGesture {
                awaitFirstDown()
                haptics.performHapticFeedback(HapticFeedbackType.LongPress)
                onHoldStart(b.id)
                waitForUpOrCancellation()
                onHoldEnd(b.id)
            }
        }
    } else {
        Modifier.clickable {
            haptics.performHapticFeedback(HapticFeedbackType.LongPress)
            onPress(b.id)
        }
    }
    Box(
        modifier = Modifier
            .fillMaxWidth()
            .aspectRatio(1f)
            .clip(RoundedCornerShape(16.dp))
            .background(bg)
            .then(gesture),
        contentAlignment = Alignment.Center,
    ) {
        // Scale the icon + label to the card size so dense grids stay readable.
        BoxWithConstraints(contentAlignment = Alignment.Center) {
            val w = maxWidth
            val iconSize = (w * 0.34f).coerceIn(14.dp, 40.dp)
            val fontSize = (w.value * 0.13f).coerceIn(8f, 14f)
            Column(
                horizontalAlignment = Alignment.CenterHorizontally,
                modifier = Modifier.padding(4.dp),
            ) {
                Icon(iconFor(b.icon), contentDescription = b.label, tint = fg, modifier = Modifier.size(iconSize))
                if (b.label.isNotBlank() && w >= 46.dp) {
                    Text(
                        b.label,
                        color = fg,
                        textAlign = TextAlign.Center,
                        maxLines = if (w >= 92.dp) 2 else 1,
                        overflow = TextOverflow.Ellipsis,
                        fontSize = fontSize.sp,
                        lineHeight = (fontSize + 2f).sp,
                        modifier = Modifier.padding(top = 4.dp),
                    )
                }
            }
        }
    }
}
