package com.lfxfire.viceremote.data

import android.content.Context

/** Connection details for Vice, kept in the app's private storage. */
data class Connection(val host: String = "", val port: Int = DEFAULT_PORT, val pairingCode: String = "") {
    val isComplete get() = host.isNotBlank() && pairingCode.isNotBlank()

    companion object {
        const val DEFAULT_PORT = 8420
    }
}

class Settings(context: Context) {
    private val prefs = context.getSharedPreferences("vice", Context.MODE_PRIVATE)

    fun load() = Connection(
        host = prefs.getString("host", "").orEmpty(),
        port = prefs.getInt("port", Connection.DEFAULT_PORT),
        pairingCode = prefs.getString("pairingCode", "").orEmpty(),
    )

    fun save(connection: Connection) {
        prefs.edit()
            .putString("host", connection.host.trim())
            .putInt("port", connection.port)
            .putString("pairingCode", connection.pairingCode.trim())
            .apply()
    }
}
