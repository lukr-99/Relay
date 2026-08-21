<#
  build-apk.ps1 - build the versioned, signed Android release APK.

  Runs assembleRelease (signed from android\keystore.properties if present, else the debug key),
  then copies the output to android\dist\Relay-<versionName>.apk.

  Requires JDK 17+ (a JDK 21 is auto-detected below if JAVA_HOME points elsewhere) and the Android
  SDK (ANDROID_HOME set, or android\local.properties present).

  Usage:
    .\tools\build-apk.ps1
    .\tools\build-apk.ps1 -Install     # adb install -r the built APK afterwards
#>
param([switch]$Install)
$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot
$android = Join-Path $repo "android"
$gradlew = Join-Path $android "gradlew.bat"

# AGP 8.5 needs JDK 17+. If JAVA_HOME points at an older JDK, prefer a 17+ install if we can find one.
function Test-JavaOk($javaHome) {
    if (-not $javaHome) { return $false }
    if (-not (Test-Path (Join-Path $javaHome "bin\java.exe"))) { return $false }
    # Read the JDK's "release" file (JAVA_VERSION="21.0.12") rather than running java -version,
    # whose stderr output trips PowerShell's native-command error handling under -ErrorAction Stop.
    $releaseFile = Join-Path $javaHome "release"
    if (Test-Path $releaseFile) {
        $line = Get-Content $releaseFile | Where-Object { $_ -match '^JAVA_VERSION="(\d+)' } | Select-Object -First 1
        if ($line -match '^JAVA_VERSION="(\d+)') { return [int]$Matches[1] -ge 17 }
    }
    return $false
}
if (-not (Test-JavaOk $env:JAVA_HOME)) {
    $candidate = Get-ChildItem "C:\Program Files\Microsoft","C:\Program Files\Eclipse Adoptium","C:\Program Files\Java" -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match 'jdk-(1[7-9]|2\d)' } | Sort-Object Name -Descending | Select-Object -First 1
    if ($candidate) { $env:JAVA_HOME = $candidate.FullName; Write-Host "Using JDK at $($env:JAVA_HOME)" -ForegroundColor Yellow }
    else { throw "No JDK 17+ found (JAVA_HOME points at an older JDK). Install a JDK 17+ or set JAVA_HOME." }
}

# ---- version from the module build.gradle.kts (versionName) ----
$gradleFile = Join-Path $android "app\build.gradle.kts"
$versionName = (Select-String -Path $gradleFile -Pattern 'versionName\s*=\s*"([^"]+)"').Matches[0].Groups[1].Value
if (-not $versionName) { throw "Could not read versionName from $gradleFile" }
Write-Host "Relay app version $versionName" -ForegroundColor Cyan

if (-not (Test-Path (Join-Path $android "keystore.properties"))) {
    Write-Host "note: android\keystore.properties not found - the APK will be debug-signed (not for distribution)." -ForegroundColor Yellow
}

Write-Host "Building (assembleRelease)..." -ForegroundColor Cyan
& $gradlew -p $android assembleRelease --console=plain
if ($LASTEXITCODE -ne 0) { throw "Gradle build failed" }

$apk = Join-Path $android "app\build\outputs\apk\release\app-release.apk"
if (-not (Test-Path $apk)) { throw "APK not found at $apk" }

$dist = Join-Path $android "dist"
New-Item -ItemType Directory -Force -Path $dist | Out-Null
$out = Join-Path $dist "Relay-$versionName.apk"
Copy-Item $apk $out -Force
Write-Host "Built $out" -ForegroundColor Green

if ($Install) {
    $adb = if ($env:ANDROID_HOME) { Join-Path $env:ANDROID_HOME "platform-tools\adb.exe" } else { "adb" }
    & $adb install -r $out
}
