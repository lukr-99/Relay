package com.lukr99.relay

import android.content.Intent
import android.net.Uri
import android.os.Bundle
import android.view.WindowManager
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.lifecycle.viewmodel.compose.viewModel
import com.lukr99.relay.ui.App
import com.lukr99.relay.ui.DeckViewModel
import com.lukr99.relay.ui.theme.RelayTheme

/** Single-activity Compose host. Pair screen until connected, then the deck grid.
 *  Also handles `relay://pair?host&port&token` deep links (scan the QR with any camera). */
class MainActivity : ComponentActivity() {
    private val pairUri = mutableStateOf<Uri?>(null)

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        // A control surface shouldn't sleep mid-session.
        window.addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON)
        pairUri.value = intent?.takeIf { it.action == Intent.ACTION_VIEW }?.data

        setContent {
            RelayTheme {
                val vm: DeckViewModel = viewModel()
                val uri by pairUri
                LaunchedEffect(uri) {
                    val u = uri ?: return@LaunchedEffect
                    val host = u.getQueryParameter("host")
                    val port = u.getQueryParameter("port")?.toIntOrNull() ?: 8731
                    val token = u.getQueryParameter("token")
                    val fp = u.getQueryParameter("fp") ?: ""
                    if (!host.isNullOrBlank() && !token.isNullOrBlank()) vm.connect(host, port, token, fp)
                    pairUri.value = null
                }
                App(vm)
            }
        }
    }

    override fun onNewIntent(intent: Intent) {
        super.onNewIntent(intent)
        setIntent(intent)
        if (intent.action == Intent.ACTION_VIEW) pairUri.value = intent.data
    }
}
