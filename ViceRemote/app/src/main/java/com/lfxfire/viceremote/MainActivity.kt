package com.lfxfire.viceremote

import android.os.Build
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.BackHandler
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.activity.viewModels
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.dynamicDarkColorScheme
import androidx.compose.material3.dynamicLightColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.platform.LocalContext
import com.lfxfire.viceremote.ui.RemoteScreen
import com.lfxfire.viceremote.ui.SettingsScreen

class MainActivity : ComponentActivity() {

    private val viewModel: RemoteViewModel by viewModels()

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()

        setContent {
            ViceTheme {
                var showSettings by rememberSaveable { mutableStateOf(!viewModel.connection.isComplete) }

                if (showSettings) {
                    BackHandler(enabled = viewModel.connection.isComplete) { showSettings = false }

                    SettingsScreen(
                        connection = viewModel.connection,
                        canGoBack = viewModel.connection.isComplete,
                        onBack = { showSettings = false },
                        onSave = {
                            viewModel.saveConnection(it)
                            showSettings = false
                        },
                    )
                } else {
                    RemoteScreen(viewModel = viewModel, onOpenSettings = { showSettings = true })
                }
            }
        }
    }

    override fun onResume() {
        super.onResume()
        viewModel.startPolling()
    }

    override fun onPause() {
        viewModel.stopPolling()
        super.onPause()
    }
}

@Composable
private fun ViceTheme(content: @Composable () -> Unit) {
    val dark = isSystemInDarkTheme()
    val colors = when {
        Build.VERSION.SDK_INT >= Build.VERSION_CODES.S ->
            if (dark) dynamicDarkColorScheme(LocalContext.current) else dynamicLightColorScheme(LocalContext.current)
        dark -> darkColorScheme()
        else -> lightColorScheme()
    }

    MaterialTheme(colorScheme = colors, content = content)
}
