package com.lukr99.deckforge.ui

import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.systemBars
import androidx.compose.material3.Scaffold
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Modifier
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.lukr99.deckforge.net.ConnState

@Composable
fun App(vm: DeckViewModel) {
    val state by vm.client.state.collectAsStateWithLifecycle()
    val layout by vm.client.layout.collectAsStateWithLifecycle()
    val agentName by vm.client.agentName.collectAsStateWithLifecycle()

    Scaffold(contentWindowInsets = WindowInsets.systemBars) { inner ->
        val current = layout
        if (state is ConnState.Connected && current != null) {
            Box(Modifier.padding(inner)) {
                DeckScreen(
                    layout = current,
                    agentName = agentName,
                    onPress = vm::press,
                    onDisconnect = vm::disconnect,
                )
            }
        } else {
            Box(Modifier.padding(inner)) {
                PairScreen(
                    state = state,
                    initialHost = vm.savedHost,
                    initialPort = vm.savedPort,
                    initialToken = vm.savedToken,
                    onConnect = vm::connect,
                )
            }
        }
    }
}
