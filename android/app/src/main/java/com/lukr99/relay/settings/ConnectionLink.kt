package com.lukr99.relay.settings

/** How the phone reaches the PC. Auto uses the cable when USB tethering is on, else Wi-Fi. */
enum class ConnectionLink(val label: String, val hint: String) {
    Auto("Auto", "Cable when USB tethering is on, otherwise Wi-Fi."),
    WiFi("Wi-Fi", "Always over the network, even with a cable plugged in."),
    Cable("Cable", "Always over USB. Plug in the cable and turn on USB tethering.");

    companion object {
        fun parse(value: String?): ConnectionLink = entries.firstOrNull { it.name == value } ?: Auto
    }
}

/** The link the current connection actually uses. */
enum class ActiveLink(val label: String) { WiFi("Wi-Fi"), Cable("Cable") }
