package com.lfxfire.viceremote.data

import com.sun.net.httpserver.HttpServer
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import java.net.InetSocketAddress

/** Runs ViceClient against a fake Vice that speaks the same JSON as Server/PhoneApiServer.cs. */
class ViceClientTest {

    private lateinit var server: HttpServer
    private var lastBody = ""

    private val stateJson =
        """{"tvPower":true,"barPower":false,"tvVolume":30,"tvTargetVolume":32,"wooferVolume":6,""" +
            """"wooferTargetVolume":6,"tvMode":"Cinema","tvModeChanging":false,"nightMode":true,""" +
            """"sleepTimerActive":true,"sleepTimerMinutes":45,"sleepTimerAction":"Sleep"}"""

    @Before
    fun start() {
        server = HttpServer.create(InetSocketAddress("127.0.0.1", 0), 0)
        server.createContext("/api") { exchange ->
            val (code, body) = when {
                exchange.requestHeaders.getFirst("Authorization") != "Bearer GOOD-CODE" ->
                    401 to """{"ok":false,"error":"Wrong pairing code"}"""
                exchange.requestURI.path == "/api/state" ->
                    200 to """{"ok":true,"version":"1","state":$stateJson}"""
                exchange.requestURI.path == "/api/command" -> {
                    lastBody = exchange.requestBody.bufferedReader().readText()
                    if (lastBody.contains("\"Nope\"")) 400 to """{"ok":false,"error":"Unknown command: Nope","state":$stateJson}"""
                    else 200 to """{"ok":true,"error":null,"state":$stateJson}"""
                }
                else -> 404 to """{"ok":false,"error":"Not found"}"""
            }
            val bytes = body.toByteArray()
            exchange.sendResponseHeaders(code, bytes.size.toLong())
            exchange.responseBody.use { it.write(bytes) }
        }
        server.start()
    }

    @After
    fun stop() = server.stop(0)

    private fun client(code: String = "GOOD-CODE", host: String = "127.0.0.1") =
        ViceClient(host, server.address.port, code)

    @Test
    fun readsState() {
        val state = client().state()
        assertEquals(true, state.tvPower)
        assertEquals(32, state.tvTargetVolume)
        assertEquals("Cinema", state.tvMode)
        assertEquals(45, state.sleepTimerMinutes)
        assertEquals("Sleep", state.sleepTimerAction)
    }

    @Test
    fun sendsCommandFields() {
        client().send(Commands.soundbarVolume(-5))
        assertTrue(lastBody, lastBody.contains("\"command\":\"TV Volume\""))
        assertTrue(lastBody, lastBody.contains("\"value\":\"Down 5\""))
    }

    @Test
    fun acceptsAddressWithScheme() {
        assertEquals(true, client(host = "http://127.0.0.1/").state().tvPower)
    }

    @Test
    fun wrongCodeExplainsItself() {
        val error = runCatching { client(code = "BAD").state() }.exceptionOrNull()
        assertTrue(error is ViceException)
        assertTrue(error!!.message!!.startsWith("Wrong pairing code"))
    }

    @Test
    fun rejectedCommandShowsViceError() {
        val error = runCatching { client().send(ViceCommand("Nope")) }.exceptionOrNull()
        assertEquals("Unknown command: Nope", error?.message)
    }

    @Test
    fun unreachablePcSaysSo() {
        val port = server.address.port
        server.stop(0)
        val error = runCatching { ViceClient("127.0.0.1", port, "GOOD-CODE").state() }.exceptionOrNull()
        assertTrue(error?.message.orEmpty().startsWith("Can't reach the PC"))
        server = HttpServer.create(InetSocketAddress("127.0.0.1", 0), 0).also { it.start() }
    }
}
