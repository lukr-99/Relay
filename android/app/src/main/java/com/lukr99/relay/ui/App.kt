package com.lukr99.relay.ui

import android.widget.Toast
import androidx.activity.compose.BackHandler
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.systemBars
import androidx.compose.material3.Scaffold
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.saveable.rememberSaveableStateHolder
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.lukr99.relay.net.AppUpdater
import com.lukr99.relay.net.ConnState
import com.lukr99.relay.settings.PairingStore

@Composable
fun App(vm: DeckViewModel) {
    val state by vm.client.state.collectAsStateWithLifecycle()
    val layout by vm.client.layout.collectAsStateWithLifecycle()
    val agentName by vm.client.agentName.collectAsStateWithLifecycle()
    val buttonStates by vm.client.states.collectAsStateWithLifecycle()
    val buttonLevels by vm.client.levels.collectAsStateWithLifecycle()
    val sliderValues by vm.client.sliderValues.collectAsStateWithLifecycle()
    val presets by vm.client.presets.collectAsStateWithLifecycle()

    var showSettings by rememberSaveable { mutableStateOf(false) }
    // Keeps the pair screen's typed host, port and token while Settings is open.
    val screens = rememberSaveableStateHolder()

    // On launch, quietly check GitHub for a newer app APK. A toast points to Settings › Updates.
    val context = LocalContext.current
    LaunchedEffect(Unit) {
        val store = PairingStore(context)
        if (store.autoUpdate) {
            val rel = AppUpdater.check(AppUpdater.currentVersion(context))
            if (rel != null) {
                Toast.makeText(context, "Relay ${rel.version} is available. See Settings › Updates.", Toast.LENGTH_LONG).show()
            }
        }
    }

    Scaffold(contentWindowInsets = WindowInsets.systemBars) { inner ->
        val current = layout
        Box(Modifier.padding(inner)) {
            if (showSettings) {
                // Settings stands on its own: it stays open if the connection drops, and Back
                // returns to whichever screen fits the connection state (deck or pairing).
                BackHandler { showSettings = false }
                SettingsScreen(
                    state = state,
                    agentName = agentName,
                    host = vm.savedHost,
                    port = vm.savedPort,
                    onDisconnect = { vm.disconnect(); showSettings = false },
                    onBack = { showSettings = false },
                )
            } else if (state is ConnState.Connected && current != null) {
                DeckScreen(
                    layout = current,
                    agentName = agentName,
                    states = buttonStates,
                    levels = buttonLevels,
                    sliderValues = sliderValues,
                    onSlider = vm::setSlider,
                    presets = presets.names,
                    activePreset = presets.active,
                    onSelectPreset = vm::selectPreset,
                    onPress = vm::press,
                    onHoldStart = vm::holdStart,
                    onHoldEnd = vm::holdEnd,
                    onOpenSettings = { showSettings = true },
                )
            } else {
                // Browse the LAN for agents only while the pairing screen is up.
                val discovered by vm.discovery.agents.collectAsStateWithLifecycle()
                DisposableEffect(Unit) {
                    vm.startDiscovery()
                    onDispose { vm.stopDiscovery() }
                }
                screens.SaveableStateProvider("pair") {
                    PairScreen(
                        state = state,
                        initialHost = vm.savedHost,
                        initialPort = vm.savedPort,
                        initialToken = vm.savedToken,
                        discovered = discovered,
                        savedAgentId = vm.savedAgentId,
                        savedToken = vm.savedToken,
                        onConnect = vm::connect,
                        onConnectUsb = vm::connectOverUsb,
                        onOpenSettings = { showSettings = true },
                    )
                }
            }
        }
    }
}
