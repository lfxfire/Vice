package com.lfxfire.viceremote.data

/** Every button's command, in the format Vice's ExecuteCommand expects. */
object Commands {
    val tvPower = ViceCommand("Remote", "Power", "TV")
    val soundbarPower = ViceCommand("Remote", "Power", "Bar")
    val nightMode = ViceCommand("Remote", "NightMode")

    fun soundbarVolume(change: Int) = ViceCommand("TV Volume", if (change >= 0) "Up $change" else "Down ${-change}")
    fun wooferVolume(change: Int) = ViceCommand("Woofer Volume", if (change >= 0) "Up $change" else "Down ${-change}")

    /** mode is "Normal", "Cinema" or "True Cinema". */
    fun tvMode(mode: String) = ViceCommand("TV Mode", mode)

    fun pcVolume(change: Int) = ViceCommand("Volume Control", if (change >= 0) "Up $change" else "Down ${-change}")
    val pcMute = ViceCommand("Volume Control", "Mute")
    val mediaPrevious = ViceCommand("Media Control", "Previous")
    val mediaPlayPause = ViceCommand("Media Control", "Play/Pause")
    val mediaNext = ViceCommand("Media Control", "Next")

    fun sleepTimerAdd(minutes: Int) = ViceCommand("Sleep Timer", "Add $minutes")
    val sleepTimerStart = ViceCommand("Sleep Timer", "Start")
    val sleepTimerStop = ViceCommand("Sleep Timer", "Stop")
    val sleepTimerClear = ViceCommand("Sleep Timer", "Set 0")

    /** Turns the TV off, then does the sleep timer's action (lock, sleep, hibernate or shut down). */
    val bedTime = ViceCommand("Remote", "Bed time")

    val lockPc = ViceCommand("Lock")
    val sleepPc = ViceCommand("Sleep")
    val hibernatePc = ViceCommand("Hibernate")
    val shutDownPc = ViceCommand("PowerOff")
}
