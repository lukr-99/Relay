package com.lukr99.relay.ui

import androidx.compose.animation.animateColorAsState
import androidx.compose.animation.core.Animatable
import androidx.compose.animation.core.LinearEasing
import androidx.compose.animation.core.Spring
import androidx.compose.animation.core.animateFloatAsState
import androidx.compose.animation.core.spring
import androidx.compose.animation.core.tween
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.interaction.MutableInteractionSource
import androidx.compose.foundation.interaction.collectIsPressedAsState
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
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.lazy.grid.GridCells
import androidx.compose.foundation.lazy.grid.LazyVerticalGrid
import androidx.compose.foundation.lazy.grid.items
import androidx.compose.foundation.pager.HorizontalPager
import androidx.compose.foundation.pager.rememberPagerState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.DropdownMenu
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Slider
import androidx.compose.material3.SliderDefaults
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.ripple
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.ArrowDropDown
import androidx.compose.material.icons.filled.Check
import androidx.compose.material.icons.filled.Settings
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.graphics.graphicsLayer
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
import com.lukr99.relay.net.Slider as SliderDef
import kotlin.math.PI
import kotlin.math.roundToInt
import kotlin.math.sin

@Composable
fun DeckScreen(
    layout: Layout,
    agentName: String?,
    states: Map<String, Boolean>,
    levels: Map<String, Float>,
    sliderValues: Map<String, Float>,
    onSlider: (String, Float) -> Unit,
    presets: List<String>,
    activePreset: String?,
    onSelectPreset: (String) -> Unit,
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
                maxLines = 1,
                overflow = TextOverflow.Ellipsis,
                modifier = Modifier.weight(1f, fill = false),
            )
            Row(verticalAlignment = Alignment.CenterVertically) {
                if (presets.size > 1) {
                    PresetPicker(presets, activePreset, onSelectPreset)
                }
                IconButton(onClick = onOpenSettings) {
                    Icon(Icons.Filled.Settings, contentDescription = "Settings")
                }
            }
        }

        HorizontalPager(state = pagerState, modifier = Modifier.weight(1f)) { index ->
            val page = pages[index]
            val cols = (page.grid?.cols ?: layout.grid.cols).coerceAtLeast(1)
            val rows = (page.grid?.rows ?: layout.grid.rows).coerceAtLeast(1)
            DeckGrid(page, cols, rows, states, levels, onPress, onHoldStart, onHoldEnd)
        }

        if (layout.sliders.isNotEmpty()) {
            Column(Modifier.fillMaxWidth().padding(top = 10.dp)) {
                layout.sliders.forEach { s -> SliderRow(s, sliderValues[s.id], onSlider) }
            }
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

/** A labeled slider bound to a PC-side param (e.g. MicForge gain). Drags send throttled slider.set;
 *  values pushed by the agent are adopted while the user isn't dragging. */
@Composable
private fun SliderRow(s: SliderDef, liveValue: Float?, onSlider: (String, Float) -> Unit) {
    var dragging by remember { mutableStateOf(false) }
    var pos by remember(s.id) { mutableStateOf(liveValue ?: s.value) }
    var lastSent by remember { mutableStateOf(0L) }
    // Adopt a pushed value only when it actually changes (external update) and we're not mid-drag —
    // NOT on drag-end, or the knob would snap back to a stale value instead of sticking.
    LaunchedEffect(liveValue) {
        if (!dragging && liveValue != null) pos = liveValue
    }
    val accent = parseColor(s.color, MaterialTheme.colorScheme.primary)
    val steps = if (s.step > 0f && s.max > s.min) (((s.max - s.min) / s.step).roundToInt() - 1).coerceAtLeast(0) else 0
    Column(Modifier.fillMaxWidth().padding(vertical = 2.dp)) {
        Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
            Text(s.label, style = MaterialTheme.typography.bodyMedium)
            Text(
                formatValue(pos, s.step) + (s.unit ?: ""),
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
        Slider(
            value = pos.coerceIn(s.min, s.max),
            onValueChange = { v ->
                dragging = true
                pos = v
                val now = System.currentTimeMillis()
                if (now - lastSent >= 40) { lastSent = now; onSlider(s.id, v) }
            },
            onValueChangeFinished = { dragging = false; onSlider(s.id, pos) },
            valueRange = s.min..s.max,
            steps = steps,
            colors = SliderDefaults.colors(thumbColor = accent, activeTrackColor = accent),
        )
    }
}

private fun formatValue(value: Float, step: Float): String =
    if (step > 0f && step < 1f) "%.1f".format(value) else "%.0f".format(value)

// ── press effects ──────────────────────────────────────────────────────────────────────────
private fun effectDurationMs(effect: String?): Int = when (effect) {
    "pop" -> 260
    "bounce" -> 460
    "glow" -> 420
    "shake" -> 420
    "ripple" -> 460
    "flash" -> 280
    else -> 250
}

private fun effectScale(effect: String?, p: Float): Float = when (effect) {
    "pop" -> 1f + sin(p * PI).toFloat() * 0.18f
    "bounce" -> 1f + (sin(p * PI * 2) * (1f - p)).toFloat() * 0.16f
    else -> 1f
}

private fun effectTranslateX(effect: String?, p: Float): Float = when (effect) {
    "shake" -> (sin(p * PI * 5) * (1f - p)).toFloat() * 20f
    else -> 0f
}

private fun glowAlpha(p: Float): Float = sin(p * PI).toFloat()

/** Meter bar colour: green when healthy, amber as it gets hot, red near clipping. */
private fun meterColor(level: Float): Color = when {
    level >= 0.85f -> Color(0xFFE5543B)
    level >= 0.6f -> Color(0xFFE3B23C)
    else -> Color(0xFF7FB069)
}

/** Header dropdown that lists the agent's deck presets and switches the active one. */
@Composable
private fun PresetPicker(
    presets: List<String>,
    active: String?,
    onSelect: (String) -> Unit,
) {
    var expanded by remember { mutableStateOf(false) }
    Box {
        TextButton(onClick = { expanded = true }) {
            Text(
                active ?: "Preset",
                maxLines = 1,
                overflow = TextOverflow.Ellipsis,
                modifier = Modifier.widthIn(max = 140.dp),
            )
            Icon(Icons.Filled.ArrowDropDown, contentDescription = "Switch preset")
        }
        DropdownMenu(expanded = expanded, onDismissRequest = { expanded = false }) {
            presets.forEach { name ->
                DropdownMenuItem(
                    text = { Text(name) },
                    leadingIcon = {
                        if (name == active) Icon(Icons.Filled.Check, contentDescription = null)
                        else Box(Modifier.size(24.dp))
                    },
                    onClick = {
                        expanded = false
                        if (name != active) onSelect(name)
                    },
                )
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
    levels: Map<String, Float>,
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
            if (b != null) DeckButton(b, states[b.id] == true, levels[b.id], onPress, onHoldStart, onHoldEnd)
            else Box(Modifier.fillMaxWidth().aspectRatio(1f))
        }
    }
}

@Composable
private fun DeckButton(
    b: ButtonDef,
    toggledOn: Boolean,
    level: Float?,
    onPress: (String) -> Unit,
    onHoldStart: (String) -> Unit,
    onHoldEnd: (String) -> Unit,
) {
    val haptics = LocalHapticFeedback.current
    val interaction = remember { MutableInteractionSource() }
    var held by remember { mutableStateOf(false) }
    val pressed = interaction.collectIsPressedAsState().value || held

    // Smooth toggle colour crossfade + a springy press "squish" shared by taps and holds.
    val targetBg = if (toggledOn) MaterialTheme.colorScheme.primary else parseColor(b.color, MaterialTheme.colorScheme.surface)
    val bg by animateColorAsState(targetBg, animationSpec = tween(200), label = "bg")
    val fg = if (bg.luminance() > 0.5f) Color(0xFF10141A) else Color.White
    val scale by animateFloatAsState(
        if (pressed) 0.90f else 1f,
        animationSpec = spring(dampingRatio = 0.45f, stiffness = Spring.StiffnessMediumLow),
        label = "scale",
    )

    // One-shot press effect (pop / bounce / glow / shake / ripple / flash), replayed on each tap.
    var burst by remember { mutableStateOf(0) }
    val prog = remember { Animatable(1f) }   // 1f = idle
    LaunchedEffect(burst) {
        if (burst == 0) return@LaunchedEffect
        prog.snapTo(0f)
        prog.animateTo(1f, animationSpec = tween(effectDurationMs(b.effect), easing = LinearEasing))
    }
    val p = prog.value
    val fxOn = b.effect != null && p < 1f
    val fxScale = if (fxOn) effectScale(b.effect, p) else 1f
    val fxTx = if (fxOn) effectTranslateX(b.effect, p) else 0f
    fun fireEffect() { if (b.effect != null) burst++ }

    // Buttons with a hold action are push-and-hold (PTT): fire on finger-down, release on up.
    val gesture = if (b.hasHold != null) {
        Modifier.pointerInput(b.id) {
            awaitEachGesture {
                awaitFirstDown()
                held = true
                fireEffect()
                haptics.performHapticFeedback(HapticFeedbackType.LongPress)
                onHoldStart(b.id)
                waitForUpOrCancellation()
                held = false
                onHoldEnd(b.id)
            }
        }
    } else {
        Modifier.clickable(interactionSource = interaction, indication = ripple()) {
            fireEffect()
            haptics.performHapticFeedback(HapticFeedbackType.LongPress)
            onPress(b.id)
        }
    }
    Box(
        modifier = Modifier
            .fillMaxWidth()
            .aspectRatio(1f)
            .graphicsLayer { scaleX = scale * fxScale; scaleY = scale * fxScale; translationX = fxTx }
            .clip(RoundedCornerShape(16.dp))
            .background(bg)
            .then(gesture),
        contentAlignment = Alignment.Center,
    ) {
        // Live input-level meter (MicForge): a thin bar pinned to the bottom edge; width eased.
        if (level != null) {
            val animLevel by animateFloatAsState(level.coerceIn(0f, 1f), animationSpec = tween(120), label = "level")
            Box(
                Modifier
                    .align(Alignment.BottomStart)
                    .fillMaxWidth(animLevel)
                    .height(5.dp)
                    .background(meterColor(level)),
            )
        }
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
        // Press-effect overlays, drawn over the content and clipped to the button shape.
        if (fxOn) {
            when (b.effect) {
                "glow" -> Box(
                    Modifier
                        .matchParentSize()
                        .border(BorderStroke(3.dp, MaterialTheme.colorScheme.primary.copy(alpha = glowAlpha(p))), RoundedCornerShape(16.dp)),
                )
                "flash" -> Box(Modifier.matchParentSize().background(Color.White.copy(alpha = (1f - p) * 0.5f)))
                "ripple" -> Canvas(Modifier.matchParentSize()) {
                    drawCircle(
                        color = fg.copy(alpha = (1f - p) * 0.6f),
                        radius = p * size.maxDimension * 0.6f,
                        center = center,
                        style = Stroke(width = 4.dp.toPx()),
                    )
                }
            }
        }
    }
}
