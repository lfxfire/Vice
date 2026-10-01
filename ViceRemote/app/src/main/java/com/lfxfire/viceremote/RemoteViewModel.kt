package com.lfxfire.viceremote

import android.app.Application
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import com.lfxfire.viceremote.data.Connection
import com.lfxfire.viceremote.data.Settings
import com.lfxfire.viceremote.data.ViceClient
import com.lfxfire.viceremote.data.ViceCommand
import com.lfxfire.viceremote.data.ViceException
import com.lfxfire.viceremote.data.ViceState
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.delay
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

class RemoteViewModel(application: Application) : AndroidViewModel(application) {

    private val settings = Settings(application)

    var connection by mutableStateOf(settings.load())
        private set

    /** Last state Vice reported, or null before the first successful reply. */
    var state by mutableStateOf<ViceState?>(null)
        private set

    /** Problem from the last request, cleared by the next success. */
    var error by mutableStateOf<String?>(null)
        private set

    /** Number of commands waiting for Vice to answer. */
    var pending by mutableIntStateOf(0)
        private set

    private var client = clientFor(connection)
    private var pollJob: Job? = null

    fun saveConnection(updated: Connection) {
        settings.save(updated)
        connection = updated
        client = clientFor(updated)
        state = null
        error = null
        refresh()
    }

    fun send(command: ViceCommand) {
        viewModelScope.launch {
            pending++
            try {
                val client = client
                state = withContext(Dispatchers.IO) { client.send(command) }
                error = null
            } catch (e: ViceException) {
                error = e.message
            } finally {
                pending--
            }
        }
    }

    fun refresh() {
        if (!connection.isComplete) return

        viewModelScope.launch {
            try {
                val client = client
                state = withContext(Dispatchers.IO) { client.state() }
                error = null
            } catch (e: ViceException) {
                error = e.message
            }
        }
    }

    /** Keeps the shown state fresh while the app is on screen. */
    fun startPolling() {
        if (pollJob?.isActive == true) return

        pollJob = viewModelScope.launch {
            while (isActive) {
                refresh()
                delay(3_000)
            }
        }
    }

    fun stopPolling() {
        pollJob?.cancel()
        pollJob = null
    }

    private fun clientFor(connection: Connection) =
        ViceClient(connection.host, connection.port, connection.pairingCode)
}
