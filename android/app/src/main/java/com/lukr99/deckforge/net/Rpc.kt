package com.lukr99.deckforge.net

import kotlinx.serialization.SerialName
import kotlinx.serialization.Serializable
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.add
import kotlinx.serialization.json.buildJsonObject
import kotlinx.serialization.json.put
import kotlinx.serialization.json.putJsonObject

/** Shared JSON config — lenient so the agent can add fields without breaking the phone. */
val DeckJson: Json = Json {
    ignoreUnknownKeys = true
    isLenient = true
    encodeDefaults = true
}

// ── Layout DTOs (subset the phone actually renders; actions stay on the PC) ────────────────
@Serializable
data class Layout(
    val version: Int = 1,
    val grid: Grid = Grid(),
    val activePage: String? = null,
    val pages: List<Page> = emptyList(),
)

@Serializable
data class Grid(val cols: Int = 4, val rows: Int = 3)

@Serializable
data class Page(
    val id: String = "",
    val name: String = "",
    val grid: Grid? = null,
    val buttons: List<ButtonDef> = emptyList(),
)

@Serializable
data class ButtonDef(
    val id: String = "",
    val row: Int = 0,
    val col: Int = 0,
    val label: String = "",
    val icon: String? = null,
    val color: String? = null,
    @SerialName("holdAction") val hasHold: JsonObject? = null,
)

// ── JSON-RPC 2.0 message builders ─────────────────────────────────────────────────────────
object Rpc {
    fun hello(id: Int, deviceName: String, model: String): String =
        DeckJson.encodeToString(JsonObject.serializer(), buildJsonObject {
            put("jsonrpc", "2.0"); put("id", id); put("method", "session.hello")
            putJsonObject("params") {
                putJsonObject("device") {
                    put("id", model.ifBlank { "android" }); put("name", deviceName); put("model", model)
                }
                put("appVersion", "0.1.0")
            }
        })

    fun getLayout(id: Int): String =
        DeckJson.encodeToString(JsonObject.serializer(), buildJsonObject {
            put("jsonrpc", "2.0"); put("id", id); put("method", "deck.getLayout")
            putJsonObject("params") {}
        })

    fun press(buttonId: String): String =
        DeckJson.encodeToString(JsonObject.serializer(), buildJsonObject {
            put("jsonrpc", "2.0"); put("method", "button.press")
            putJsonObject("params") { put("id", buttonId) }
        })

    fun hold(buttonId: String, phase: String): String =
        DeckJson.encodeToString(JsonObject.serializer(), buildJsonObject {
            put("jsonrpc", "2.0"); put("method", "button.hold")
            putJsonObject("params") { put("id", buttonId); put("phase", phase) }
        })
}
