<#
  build-and-install.ps1 — build the DeckForge Android app and install it on a USB-tethered phone
  (USB debugging authorized). Requires JDK 17+ and the Android SDK (ANDROID_HOME set or
  android/local.properties present). The Gradle wrapper fetches Gradle itself.

  Usage:
    .\tools\build-and-install.ps1            # assembleDebug + install -r
    .\tools\build-and-install.ps1 -Launch    # also launch the app after install
#>
param([switch]$Launch)
$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot
$android = Join-Path $repo "android"
$gradlew = Join-Path $android "gradlew.bat"
$adb = if ($env:ANDROID_HOME) { Join-Path $env:ANDROID_HOME "platform-tools\adb.exe" } else { "adb" }

Write-Host "Building (assembleDebug)..." -ForegroundColor Cyan
& $gradlew -p $android assembleDebug --console=plain
if ($LASTEXITCODE -ne 0) { throw "Gradle build failed" }

$apk = Join-Path $android "app\build\outputs\apk\debug\app-debug.apk"
if (-not (Test-Path $apk)) { throw "APK not found at $apk" }

Write-Host "Installing $apk ..." -ForegroundColor Cyan
& $adb install -r $apk
if ($LASTEXITCODE -ne 0) { throw "Install failed" }
Write-Host "Installed." -ForegroundColor Green

if ($Launch) {
    & $adb shell monkey -p com.lukr99.deckforge -c android.intent.category.LAUNCHER 1 | Out-Null
    Write-Host "Launched com.lukr99.deckforge." -ForegroundColor Green
}
