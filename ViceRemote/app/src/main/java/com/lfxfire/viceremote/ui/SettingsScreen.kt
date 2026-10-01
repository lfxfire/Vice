package com.lfxfire.viceremote.ui

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material3.Button
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.input.KeyboardCapitalization
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.unit.dp
import com.lfxfire.viceremote.data.Connection

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun SettingsScreen(
    connection: Connection,
    canGoBack: Boolean,
    onBack: () -> Unit,
    onSave: (Connection) -> Unit,
) {
    var host by rememberSaveable { mutableStateOf(connection.host) }
    var port by rememberSaveable { mutableStateOf(connection.port.toString()) }
    var code by rememberSaveable { mutableStateOf(connection.pairingCode) }

    val portNumber = port.toIntOrNull()?.takeIf { it in 1..65535 }
    val canSave = host.isNotBlank() && code.isNotBlank() && portNumber != null

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("Connect to your PC") },
                navigationIcon = {
                    if (canGoBack) {
                        IconButton(onClick = onBack) {
                            Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Back")
                        }
                    }
                },
            )
        },
    ) { padding ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(padding)
                .verticalScroll(rememberScrollState())
                .padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp),
        ) {
            Text(
                "Install Tailscale on this phone and your PC, and sign in to the same account on both. " +
                    "Then in Vice on the PC open Phone > Show pairing details and copy the details below.",
                style = MaterialTheme.typography.bodyMedium,
            )

            OutlinedTextField(
                value = host,
                onValueChange = { host = it },
                label = { Text("PC address") },
                supportingText = { Text("The PC's name in Tailscale, like my-pc, or its 100.x.y.z address") },
                singleLine = true,
                keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Uri),
                modifier = Modifier.fillMaxWidth(),
            )

            OutlinedTextField(
                value = port,
                onValueChange = { port = it.filter(Char::isDigit).take(5) },
                label = { Text("Port") },
                isError = portNumber == null,
                singleLine = true,
                keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                modifier = Modifier.fillMaxWidth(),
            )

            OutlinedTextField(
                value = code,
                onValueChange = { code = it.uppercase() },
                label = { Text("Pairing code") },
                supportingText = { Text("Looks like ABCD-EFGH-JKMN-PQRS-TUVW") },
                singleLine = true,
                keyboardOptions = KeyboardOptions(
                    capitalization = KeyboardCapitalization.Characters,
                    autoCorrectEnabled = false,
                    keyboardType = KeyboardType.Ascii,
                ),
                modifier = Modifier.fillMaxWidth(),
            )

            Button(
                onClick = { onSave(Connection(host.trim(), portNumber ?: Connection.DEFAULT_PORT, code.trim())) },
                enabled = canSave,
                modifier = Modifier.fillMaxWidth(),
            ) {
                Text("Save and connect")
            }
        }
    }
}
