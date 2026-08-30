package com.lukr99.relay.net

import android.content.Context
import android.content.Intent
import androidx.core.content.FileProvider
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.booleanOrNull
import kotlinx.serialization.json.contentOrNull
import kotlinx.serialization.json.jsonArray
import kotlinx.serialization.json.jsonObject
import kotlinx.serialization.json.jsonPrimitive
import okhttp3.OkHttpClient
import okhttp3.Request
import java.io.File

/** A newer app release found on GitHub: its numeric version, tag, .apk download URL and notes. */
data class AppRelease(val version: String, val tag: String, val apkUrl: String, val notes: String)

/**
 * In-app updater for the phone, mirroring the desktop agent's GitHub-Releases updater. The repo
 * publishes both agent (`agent-v*`) and app (`app-v*`) releases, so this lists releases and picks the
 * newest **app-v*** one that ships an `.apk` — `/releases/latest` alone can't tell them apart.
 *
 * Android can't silently self-install, so [installApk] hands the downloaded APK to the system
 * installer (the user confirms). The download only installs cleanly when it is signed with the same
 * key as the running app — the release build must use the same signing key as what's installed.
 */
object AppUpdater {
    private const val RELEASES = "https://api.github.com/repos/lukr-99/Relay/releases"
    private const val TAG_PREFIX = "app-v"
    private const val UA = "relay-android"

    private val http = OkHttpClient()
    private val json = Json { ignoreUnknownKeys = true }

    /** The running app's version name, e.g. "0.4.3". */
    fun currentVersion(context: Context): String =
        runCatching {
            @Suppress("DEPRECATION")
            context.packageManager.getPackageInfo(context.packageName, 0).versionName ?: "0.0.0"
        }.getOrDefault("0.0.0")

    /** The newest `app-v*` release strictly newer than [current] that has an `.apk`, or null. */
    suspend fun check(current: String): AppRelease? = withContext(Dispatchers.IO) {
        runCatching {
            val req = Request.Builder().url(RELEASES)
                .header("Accept", "application/vnd.github+json")
                .header("User-Agent", UA)
                .build()
            http.newCall(req).execute().use { resp ->
                if (!resp.isSuccessful) return@use null
                val body = resp.body?.string() ?: return@use null
                var best: AppRelease? = null
                for (el in json.parseToJsonElement(body).jsonArray) {
                    val o = el.jsonObject
                    if (o["draft"]?.jsonPrimitive?.booleanOrNull == true) continue
                    if (o["prerelease"]?.jsonPrimitive?.booleanOrNull == true) continue
                    val tag = o["tag_name"]?.jsonPrimitive?.contentOrNull ?: continue
                    if (!tag.startsWith(TAG_PREFIX)) continue
                    val ver = parseVersion(tag) ?: continue
                    if (!isNewer(ver, current)) continue
                    val apkUrl = o["assets"]?.jsonArray
                        ?.firstOrNull { it.jsonObject["name"]?.jsonPrimitive?.contentOrNull?.endsWith(".apk", true) == true }
                        ?.jsonObject?.get("browser_download_url")?.jsonPrimitive?.contentOrNull ?: continue
                    val notes = o["body"]?.jsonPrimitive?.contentOrNull ?: ""
                    if (best == null || isNewer(ver, best!!.version)) best = AppRelease(ver, tag, apkUrl, notes)
                }
                best
            }
        }.getOrNull()
    }

    /** Download the release APK to the app cache and return the file. */
    suspend fun download(context: Context, release: AppRelease): File = withContext(Dispatchers.IO) {
        val req = Request.Builder().url(release.apkUrl).header("User-Agent", UA).build()
        http.newCall(req).execute().use { resp ->
            if (!resp.isSuccessful) throw java.io.IOException("Download failed (HTTP ${resp.code})")
            val file = File(context.cacheDir, "relay-${release.version}.apk")
            val stream = resp.body?.byteStream() ?: throw java.io.IOException("Empty download")
            stream.use { input -> file.outputStream().use { input.copyTo(it) } }
            file
        }
    }

    /** Hand the APK to the system package installer (the user confirms the install). */
    fun installApk(context: Context, apk: File) {
        val uri = FileProvider.getUriForFile(context, "${context.packageName}.fileprovider", apk)
        val intent = Intent(Intent.ACTION_VIEW).apply {
            setDataAndType(uri, "application/vnd.android.package-archive")
            addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION or Intent.FLAG_ACTIVITY_NEW_TASK)
        }
        context.startActivity(intent)
    }

    /** Whether the app is allowed to launch installs ("install unknown apps" granted). */
    fun canInstall(context: Context): Boolean =
        runCatching { context.packageManager.canRequestPackageInstalls() }.getOrDefault(false)

    // "app-v1.2.3" / "v1.2" / "1.2.3" -> "1.2.3"; null if there's no version in the string.
    private fun parseVersion(s: String): String? {
        val m = Regex("""(\d+)\.(\d+)(?:\.(\d+))?""").find(s) ?: return null
        val patch = m.groupValues[3].ifEmpty { "0" }
        return "${m.groupValues[1]}.${m.groupValues[2]}.$patch"
    }

    private fun isNewer(candidate: String, current: String): Boolean {
        val c = parts(candidate); val u = parts(current)
        for (i in 0 until 3) if (c[i] != u[i]) return c[i] > u[i]
        return false
    }

    private fun parts(v: String): IntArray {
        val p = parseVersion(v)?.split(".").orEmpty()
        return IntArray(3) { p.getOrNull(it)?.toIntOrNull() ?: 0 }
    }
}
