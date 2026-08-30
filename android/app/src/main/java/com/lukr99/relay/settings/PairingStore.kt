package com.lukr99.relay.settings

import android.content.Context

/** Remembers the last-used agent connection (host / port / token). Plain prefs for Phase 0. */
class PairingStore(context: Context) {
    private val prefs = context.getSharedPreferences("relay.pairing", Context.MODE_PRIVATE)

    var host: String
        get() = prefs.getString("host", "") ?: ""
        set(v) = prefs.edit().putString("host", v).apply()

    var port: Int
        get() = prefs.getInt("port", 8731)
        set(v) = prefs.edit().putInt("port", v).apply()

    var token: String
        get() = prefs.getString("token", "") ?: ""
        set(v) = prefs.edit().putString("token", v).apply()

    /** Pinned SHA-256 fingerprint of the agent's cert (from the QR, or learned on first connect). */
    var fp: String
        get() = prefs.getString("fp", "") ?: ""
        set(v) = prefs.edit().putString("fp", v).apply()

    /** The paired agent's stable id (from session.hello). Lets NSD re-find it after its IP changes. */
    var agentId: String
        get() = prefs.getString("agentId", "") ?: ""
        set(v) = prefs.edit().putString("agentId", v).apply()

    /** Minimum card edge in dp — drives the adaptive grid (smaller = more/tighter cards). */
    var cardMinDp: Int
        get() = prefs.getInt("cardMinDp", 96)
        set(v) = prefs.edit().putInt("cardMinDp", v).apply()

    /** Whether to check GitHub for a newer app APK on launch (the in-app updater). */
    var autoUpdate: Boolean
        get() = prefs.getBoolean("autoUpdate", true)
        set(v) = prefs.edit().putBoolean("autoUpdate", v).apply()

    fun save(host: String, port: Int, token: String, fp: String) {
        prefs.edit()
            .putString("host", host)
            .putInt("port", port)
            .putString("token", token)
            .putString("fp", fp)
            .apply()
    }
}
