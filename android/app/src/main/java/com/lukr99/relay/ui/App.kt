package com.lukr99.relay.ui

import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.systemBars
import androidx.compose.material3.Scaffold
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.lukr99.relay.net.ConnState

@Composable
fun App(vm: DeckViewModel) {
    val state by vm.client.state.collectAsStateWithLifecycle()
    val layout by vm.client.layout.collectAsStateWithLifecycle()
    val agentName by vm.client.agentName.collectAsStateWithLifecycle()
    val buttonStates by vm.client.states.collectAsStateWithLifecycle()
    val buttonLevels by vm.client.levels.collectAsStateWithLifecycle()
    val sliderValues by vm.client.sliderValues.collectAsStateWithLifecycle()
    val presets by vm.client.presets.collectAsStateWithLifecycle()

    var showSettings by remember { mutableStateOf(false) }

    Scaffold(contentWindowInsets = WindowInsets.systemBars) { inner ->
        val current = layout
        Box(Modifier.padding(inner)) {
            if (state is ConnState.Connected && current != null) {
                if (showSettings) {
                    SettingsScreen(
                        agentName = agentName,
                        host = vm.savedHost,
                        port = vm.savedPort,
                        onDisconnect = { vm.disconnect(); showSettings = false },
                        onBack = { showSettings = false },
                    )
                } else {
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
                }
            } else {
                // Browse the LAN for agents only while the pairing screen is up.
                val discovered by vm.discovery.agents.collectAsStateWithLifecycle()
                DisposableEffect(Unit) {
                    vm.startDiscovery()
                    onDispose { vm.stopDiscovery() }
                }
                PairScreen(
                    state = state,
                    initialHost = vm.savedHost,
                    initialPort = vm.savedPort,
                    initialToken = vm.savedToken,
                    discovered = discovered,
                    savedAgentId = vm.savedAgentId,
                    savedToken = vm.savedToken,
                    onConnect = vm::connect,
                )
            }
        }
    }
}
