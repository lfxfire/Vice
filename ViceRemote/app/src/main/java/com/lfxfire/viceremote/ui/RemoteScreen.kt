package com.lfxfire.viceremote.ui

import androidx.compose.foundation.gestures.detectTapGestures
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ColumnScope
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.RowScope
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.VolumeDown
import androidx.compose.material.icons.automirrored.filled.VolumeOff
import androidx.compose.material.icons.automirrored.filled.VolumeUp
import androidx.compose.material.icons.filled.Bedtime
import androidx.compose.material.icons.filled.Lock
import androidx.compose.material.icons.filled.NightsStay
import androidx.compose.material.icons.filled.PlayArrow
import androidx.compose.material.icons.filled.PowerSettingsNew
import androidx.compose.material.icons.filled.Settings
import androidx.compose.material.icons.filled.SkipNext
import androidx.compose.material.icons.filled.SkipPrevious
import androidx.compose.material.icons.filled.Speaker
import androidx.compose.material.icons.filled.Tv
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.FilledTonalButton
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SegmentedButton
import androidx.compose.material3.SegmentedButtonDefaults
import androidx.compose.material3.SingleChoiceSegmentedButtonRow
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.rememberUpdatedState
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.hapticfeedback.HapticFeedbackType
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.platform.LocalHapticFeedback
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import com.lfxfire.viceremote.RemoteViewModel
import com.lfxfire.viceremote.data.Commands
import com.lfxfire.viceremote.data.ViceCommand
import com.lfxfire.viceremote.data.ViceState
import kotlinx.coroutines.delay
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch

/** A command that needs an "are you sure" first. */
private data class Confirm(val title: String, val text: String, val command: ViceCommand)

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun RemoteScreen(viewModel: RemoteViewModel, onOpenSettings: () -> Unit) {
    val state = viewModel.state
    val haptics = LocalHapticFeedback.current
    var confirm by remember { mutableStateOf<Confirm?>(null) }

    val send: (ViceCommand) -> Unit = {
        haptics.performHapticFeedback(HapticFeedbackType.TextHandleMove)
        viewModel.send(it)
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("Vice Remote") },
                actions = {
                    IconButton(onClick = onOpenSettings) {
                        Icon(Icons.Filled.Settings, contentDescription = "Connection settings")
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
                .padding(horizontal = 16.dp, vertical = 8.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp),
        ) {
            ConnectionBanner(error = viewModel.error, connected = state != null, busy = viewModel.pending > 0)

            TvAndSoundbar(state, send)
            PcMedia(send)
            SleepTimer(state, send)

            Section("PC") {
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    BigButton("Lock", Icons.Filled.Lock, Modifier.weight(1f)) { send(Commands.lockPc) }
                    BigButton("Sleep", Icons.Filled.Bedtime, Modifier.weight(1f)) {
                        confirm = Confirm("Put the PC to sleep?", "Vice can't wake it again from your phone.", Commands.sleepPc)
                    }
                }
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    BigButton("Bed time", Icons.Filled.NightsStay, Modifier.weight(1f)) {
                        val action = state?.sleepTimerAction ?: "Lock"
                        confirm = Confirm("Bed time?", "Turns the TV off, then the PC will: $action.", Commands.bedTime)
                    }
                    BigButton("Shut down", Icons.Filled.PowerSettingsNew, Modifier.weight(1f)) {
                        confirm = Confirm("Shut down the PC?", "It shuts down in 2 seconds. Unsaved work will be lost.", Commands.shutDownPc)
                    }
                }
            }

            Spacer(Modifier.height(16.dp))
        }
    }

    confirm?.let { pending ->
        AlertDialog(
            onDismissRequest = { confirm = null },
            title = { Text(pending.title) },
            text = { Text(pending.text) },
            confirmButton = {
                TextButton(onClick = {
                    send(pending.command)
                    confirm = null
                }) { Text("Do it") }
            },
            dismissButton = { TextButton(onClick = { confirm = null }) { Text("Cancel") } },
        )
    }
}

@Composable
private fun ConnectionBanner(error: String?, connected: Boolean, busy: Boolean) {
    when {
        error != null -> Card(
            colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.errorContainer),
            modifier = Modifier.fillMaxWidth(),
        ) {
            Text(error, modifier = Modifier.padding(12.dp), color = MaterialTheme.colorScheme.onErrorContainer)
        }
        !connected -> Text("Connecting to your PC…", style = MaterialTheme.typography.bodyMedium)
    }

    // Reserves the space so the layout doesn't jump while a command is sending
    Box(Modifier.height(4.dp).fillMaxWidth()) {
        if (busy) LinearProgressIndicator(Modifier.fillMaxWidth())
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun TvAndSoundbar(state: ViceState?, send: (ViceCommand) -> Unit) {
    Section("TV and soundbar") {
        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            ToggleButton("TV", Icons.Filled.Tv, on = state?.tvPower, Modifier.weight(1f)) { send(Commands.tvPower) }
            ToggleButton("Soundbar", Icons.Filled.Speaker, on = state?.barPower, Modifier.weight(1f)) { send(Commands.soundbarPower) }
        }

        VolumeRow(
            label = "Volume",
            current = state?.tvVolume,
            target = state?.tvTargetVolume,
            steps = listOf(-5, -1, 1, 5),
            onStep = { send(Commands.soundbarVolume(it)) },
        )

        VolumeRow(
            label = "Woofer",
            current = state?.wooferVolume,
            target = state?.wooferTargetVolume,
            steps = listOf(-2, 2),
            onStep = { send(Commands.wooferVolume(it)) },
        )

        ToggleButton("Night mode", Icons.Filled.NightsStay, on = state?.nightMode, Modifier.fillMaxWidth()) {
            send(Commands.nightMode)
        }

        Text("Picture", style = MaterialTheme.typography.labelLarge)
        val modes = listOf("Normal", "Cinema", "True Cinema")
        SingleChoiceSegmentedButtonRow(Modifier.fillMaxWidth()) {
            modes.forEachIndexed { index, mode ->
                SegmentedButton(
                    selected = state?.tvMode == mode,
                    onClick = { if (state?.tvMode != mode) send(Commands.tvMode(mode)) },
                    enabled = state?.tvModeChanging != true,
                    shape = SegmentedButtonDefaults.itemShape(index, modes.size),
                ) { Text(mode) }
            }
        }
    }
}

@Composable
private fun PcMedia(send: (ViceCommand) -> Unit) {
    Section("PC sound") {
        Row(horizontalArrangement = Arrangement.SpaceEvenly, modifier = Modifier.fillMaxWidth()) {
            RoundIconButton(Icons.Filled.SkipPrevious, "Previous track") { send(Commands.mediaPrevious) }
            RoundIconButton(Icons.Filled.PlayArrow, "Play or pause", large = true) { send(Commands.mediaPlayPause) }
            RoundIconButton(Icons.Filled.SkipNext, "Next track") { send(Commands.mediaNext) }
        }
        Row(horizontalArrangement = Arrangement.SpaceEvenly, modifier = Modifier.fillMaxWidth()) {
            RoundIconButton(Icons.AutoMirrored.Filled.VolumeOff, "Mute PC") { send(Commands.pcMute) }
            RoundIconButton(Icons.AutoMirrored.Filled.VolumeDown, "PC volume down") { send(Commands.pcVolume(-10)) }
            RoundIconButton(Icons.AutoMirrored.Filled.VolumeUp, "PC volume up") { send(Commands.pcVolume(10)) }
        }
    }
}

@Composable
private fun SleepTimer(state: ViceState?, send: (ViceCommand) -> Unit) {
    Section("Sleep timer") {
        val minutes = state?.sleepTimerMinutes ?: 0
        val active = state?.sleepTimerActive == true
        val action = state?.sleepTimerAction ?: "Lock"

        Text(
            when {
                active -> "$minutes min left, then $action"
                minutes > 0 -> "$minutes min set, then $action. Not started"
                else -> "Off"
            },
            style = MaterialTheme.typography.bodyLarge,
        )

        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            OutlinedButton(onClick = { send(Commands.sleepTimerAdd(5)) }, Modifier.weight(1f)) { Text("+5") }
            OutlinedButton(onClick = { send(Commands.sleepTimerAdd(15)) }, Modifier.weight(1f)) { Text("+15") }
            OutlinedButton(onClick = { send(Commands.sleepTimerAdd(30)) }, Modifier.weight(1f)) { Text("+30") }
        }
        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            FilledTonalButton(
                onClick = { send(if (active) Commands.sleepTimerStop else Commands.sleepTimerStart) },
                enabled = active || minutes > 0,
                modifier = Modifier.weight(1f),
            ) { Text(if (active) "Stop" else "Start") }
            OutlinedButton(
                onClick = { send(Commands.sleepTimerClear) },
                enabled = !active && minutes > 0,
                modifier = Modifier.weight(1f),
            ) { Text("Clear") }
        }
    }
}

@Composable
private fun Section(title: String, content: @Composable ColumnScope.() -> Unit) {
    Card(modifier = Modifier.fillMaxWidth()) {
        Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
            Text(title, style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.SemiBold)
            content()
        }
    }
}

@Composable
private fun VolumeRow(label: String, current: Int?, target: Int?, steps: List<Int>, onStep: (Int) -> Unit) {
    Column(verticalArrangement = Arrangement.spacedBy(6.dp)) {
        val reading = when {
            current == null -> "–"
            target != null && target != current -> "$current → $target"
            else -> "$current"
        }
        Row(verticalAlignment = Alignment.CenterVertically) {
            Text(label, style = MaterialTheme.typography.labelLarge, modifier = Modifier.weight(1f))
            Text(reading, style = MaterialTheme.typography.titleMedium)
        }
        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            steps.forEach { step ->
                RepeatingButton(
                    label = if (step > 0) "+$step" else "$step",
                    onRepeat = { onStep(step) },
                    modifier = Modifier.weight(1f),
                )
            }
        }
    }
}

@Composable
private fun RowScope.BigButton(label: String, icon: ImageVector, modifier: Modifier, onClick: () -> Unit) {
    FilledTonalButton(onClick = onClick, modifier = modifier.heightIn(min = 56.dp)) {
        Icon(icon, contentDescription = null)
        Spacer(Modifier.width(8.dp))
        Text(label)
    }
}

/** Button that shows whether something is on. on is null while the state is unknown. */
@Composable
private fun ToggleButton(label: String, icon: ImageVector, on: Boolean?, modifier: Modifier, onClick: () -> Unit) {
    val colors = MaterialTheme.colorScheme
    Surface(
        onClick = onClick,
        shape = MaterialTheme.shapes.large,
        color = if (on == true) colors.primary else colors.surfaceVariant,
        contentColor = if (on == true) colors.onPrimary else colors.onSurfaceVariant,
        modifier = modifier.heightIn(min = 64.dp),
    ) {
        Row(
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.Center,
            modifier = Modifier.padding(12.dp),
        ) {
            Icon(icon, contentDescription = null)
            Spacer(Modifier.width(8.dp))
            Column {
                Text(label, fontWeight = FontWeight.SemiBold)
                Text(
                    when (on) {
                        true -> "On"
                        false -> "Off"
                        null -> "–"
                    },
                    style = MaterialTheme.typography.bodySmall,
                )
            }
        }
    }
}

@Composable
private fun RoundIconButton(icon: ImageVector, description: String, large: Boolean = false, onClick: () -> Unit) {
    val size = if (large) 72.dp else 56.dp
    Surface(
        onClick = onClick,
        shape = CircleShape,
        color = MaterialTheme.colorScheme.secondaryContainer,
        modifier = Modifier.size(size),
    ) {
        Box(contentAlignment = Alignment.Center) {
            Icon(icon, contentDescription = description, modifier = Modifier.size(size / 2))
        }
    }
}

/** Sends once on tap, then keeps sending while held, for nudging volume. */
@Composable
private fun RepeatingButton(label: String, onRepeat: () -> Unit, modifier: Modifier) {
    val scope = rememberCoroutineScope()
    val currentOnRepeat by rememberUpdatedState(onRepeat)

    Surface(
        shape = MaterialTheme.shapes.medium,
        color = MaterialTheme.colorScheme.secondaryContainer,
        modifier = modifier
            .height(48.dp)
            .pointerInput(Unit) {
                detectTapGestures(onPress = {
                    val job = scope.launch {
                        currentOnRepeat()
                        delay(450)
                        while (isActive) {
                            currentOnRepeat()
                            delay(200)
                        }
                    }
                    tryAwaitRelease()
                    job.cancel()
                })
            },
    ) {
        Box(contentAlignment = Alignment.Center) {
            Text(label, style = MaterialTheme.typography.titleMedium)
        }
    }
}
