package com.lukr99.deckforge

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.lifecycle.viewmodel.compose.viewModel
import com.lukr99.deckforge.ui.App
import com.lukr99.deckforge.ui.DeckViewModel
import com.lukr99.deckforge.ui.theme.DeckForgeTheme

/** Single-activity Compose host. Pair screen until connected, then the deck grid. */
class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        setContent {
            DeckForgeTheme {
                val vm: DeckViewModel = viewModel()
                App(vm)
            }
        }
    }
}
