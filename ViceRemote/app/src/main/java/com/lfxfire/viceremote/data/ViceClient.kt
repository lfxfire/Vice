package com.lfxfire.viceremote.data

import org.json.JSONObject
import java.io.IOException
import java.net.HttpURLConnection
import java.net.SocketTimeoutException
import java.net.URL

/** What Vice on the PC believes the TV, soundbar and sleep timer are doing. */
data class ViceState(
    val tvPower: Boolean = false,
    val barPower: Boolean = false,
    val tvVolume: Int = 0,
    val tvTargetVolume: Int = 0,
    val wooferVolume: Int = 0,
    val wooferTargetVolume: Int = 0,
    val tvMode: String = "Normal",
    val tvModeChanging: Boolean = false,
    val nightMode: Boolean = false,
    val sleepTimerActive: Boolean = false,
    val sleepTimerMinutes: Int = 0,
    val sleepTimerAction: String = "Lock",
) {
    companion object {
        fun fromJson(json: JSONObject) = ViceState(
            tvPower = json.optBoolean("tvPower"),
            barPower = json.optBoolean("barPower"),
            tvVolume = json.optInt("tvVolume"),
            tvTargetVolume = json.optInt("tvTargetVolume"),
            wooferVolume = json.optInt("wooferVolume"),
            wooferTargetVolume = json.optInt("wooferTargetVolume"),
            tvMode = json.optString("tvMode", "Normal"),
            tvModeChanging = json.optBoolean("tvModeChanging"),
            nightMode = json.optBoolean("nightMode"),
            sleepTimerActive = json.optBoolean("sleepTimerActive"),
            sleepTimerMinutes = json.optInt("sleepTimerMinutes"),
            sleepTimerAction = json.optString("sleepTimerAction", "Lock"),
        )
    }
}

/** A failure worth showing to the user as is. */
class ViceException(message: String) : Exception(message)

/** A command for Vice. The three fields match lines 1 to 3 of Vice's Dropbox command file. */
data class ViceCommand(val command: String, val value: String = "", val value2: String = "")

/**
 * Talks to Vice's phone API on the PC, normally over Tailscale. All calls block, so run them off
 * the main thread.
 */
class ViceClient(host: String, private val port: Int, private val pairingCode: String) {

    private val host = host.trim()
        .removePrefix("http://")
        .removePrefix("https://")
        .trimEnd('/')

    private val baseUrl get() = "http://$host:$port"

    fun state(): ViceState = ViceState.fromJson(request("GET", "/api/state", null).getJSONObject("state"))

    fun send(command: ViceCommand): ViceState {
        val body = JSONObject()
            .put("command", command.command)
            .put("value", command.value)
            .put("value2", command.value2)

        return ViceState.fromJson(request("POST", "/api/command", body).getJSONObject("state"))
    }

    private fun request(method: String, path: String, body: JSONObject?): JSONObject {
        if (host.isEmpty()) throw ViceException("Add your PC's address in Settings")

        val connection = try {
            URL(baseUrl + path).openConnection() as HttpURLConnection
        } catch (e: Exception) {
            throw ViceException("That PC address doesn't look right")
        }

        try {
            connection.requestMethod = method
            connection.connectTimeout = 5_000
            // Some commands, like changing the TV mode, take a few seconds on the PC
            connection.readTimeout = 20_000
            connection.setRequestProperty("Authorization", "Bearer $pairingCode")
            connection.setRequestProperty("Accept", "application/json")

            if (body != null) {
                connection.doOutput = true
                connection.setRequestProperty("Content-Type", "application/json; charset=utf-8")
                connection.outputStream.use { it.write(body.toString().toByteArray(Charsets.UTF_8)) }
            }

            val code = connection.responseCode
            val stream = if (code < 400) connection.inputStream else connection.errorStream
            val text = stream?.bufferedReader(Charsets.UTF_8)?.use { it.readText() }.orEmpty()
            val json = runCatching { JSONObject(text) }.getOrNull()

            if (code == 401) throw ViceException("Wrong pairing code. Check it in Vice under Phone > Show pairing details")
            if (code !in 200..299) throw ViceException(json?.errorText() ?: "Vice replied with error $code")

            return json ?: throw ViceException("Something answered on that port, but it isn't Vice")
        } catch (e: ViceException) {
            throw e
        } catch (e: SocketTimeoutException) {
            throw ViceException("The PC didn't answer in time. Is it asleep, or is Tailscale off?")
        } catch (e: IOException) {
            throw ViceException("Can't reach the PC. Check Tailscale is on for both devices and Vice is running")
        } finally {
            connection.disconnect()
        }
    }

    private fun JSONObject.errorText(): String? =
        if (has("error") && !isNull("error")) getString("error") else null
}
