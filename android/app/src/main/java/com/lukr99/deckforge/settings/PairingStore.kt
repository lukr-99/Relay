package com.lukr99.deckforge.settings

import android.content.Context

/** Remembers the last-used agent connection (host / port / token). Plain prefs for Phase 0. */
class PairingStore(context: Context) {
    private val prefs = context.getSharedPreferences("deckforge.pairing", Context.MODE_PRIVATE)

    var host: String
        get() = prefs.getString("host", "") ?: ""
        set(v) = prefs.edit().putString("host", v).apply()

    var port: Int
        get() = prefs.getInt("port", 8731)
        set(v) = prefs.edit().putInt("port", v).apply()

    var token: String
        get() = prefs.getString("token", "") ?: ""
        set(v) = prefs.edit().putString("token", v).apply()

    fun save(host: String, port: Int, token: String) {
        prefs.edit()
            .putString("host", host)
            .putInt("port", port)
            .putString("token", token)
            .apply()
    }
}
