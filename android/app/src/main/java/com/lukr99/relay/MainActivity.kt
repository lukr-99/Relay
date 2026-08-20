package com.lukr99.relay

import android.os.Bundle
import android.view.WindowManager
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.lifecycle.viewmodel.compose.viewModel
import com.lukr99.relay.ui.App
import com.lukr99.relay.ui.DeckViewModel
import com.lukr99.relay.ui.theme.RelayTheme

/** Single-activity Compose host. Pair screen until connected, then the deck grid. */
class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        // A control surface shouldn't sleep mid-session.
        window.addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON)
        setContent {
            RelayTheme {
                val vm: DeckViewModel = viewModel()
                App(vm)
            }
        }
    }
}
