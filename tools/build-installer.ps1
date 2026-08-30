<#
  build-installer.ps1 - build the versioned Windows agent installer.

  Publishes the agent self-contained (bundles the .NET 10 runtime, so end users need nothing
  installed), then compiles agent\installer\Relay.iss with Inno Setup into
  agent\installer\Relay-Setup-<version>.exe. The version comes from the csproj <Version>.

  Requires: .NET 10 SDK, Inno Setup 6 (ISCC.exe). Run from anywhere.

  Usage:
    .\tools\build-installer.ps1
    .\tools\build-installer.ps1 -Run     # launch the installer after building
#>
param([switch]$Run)
$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $repo "agent\Relay.Agent\Relay.Agent.csproj"
$publish = Join-Path $repo "agent\publish"
$iss = Join-Path $repo "agent\installer\Relay.iss"

# ---- version from the csproj <VersionPrefix> (falls back to <Version>) ----
[xml]$csproj = Get-Content $proj
$version = (@($csproj.Project.PropertyGroup.VersionPrefix) + @($csproj.Project.PropertyGroup.Version) |
    Where-Object { $_ } | Select-Object -First 1)
if (-not $version) { throw "No <VersionPrefix> or <Version> found in $proj" }
Write-Host "Relay agent version $version" -ForegroundColor Cyan

# ---- stop a running agent (it locks its output files) ----
# An agent launched elevated can't be stopped from a non-elevated shell; warn and continue rather
# than aborting (publish targets agent\publish, not the installed copy, so the build still succeeds).
Get-Process Relay.Agent -ErrorAction SilentlyContinue | ForEach-Object {
    Write-Host "Stopping running Relay.Agent (pid $($_.Id))..." -ForegroundColor Yellow
    $_ | Stop-Process -Force -ErrorAction SilentlyContinue
    if (-not $?) { Write-Host "  could not stop pid $($_.Id) (elevated?) — continuing." -ForegroundColor Yellow }
}

# ---- self-contained publish ----
Write-Host "Publishing self-contained (win-x64)..." -ForegroundColor Cyan
if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }
dotnet publish $proj -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=false -o $publish --nologo
if ($LASTEXITCODE -ne 0) { throw "Publish failed" }

# ---- locate ISCC (Inno Setup) ----
$iscc = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw "ISCC.exe (Inno Setup 6) not found. Install Inno Setup 6." }

# ---- compile the installer ----
Write-Host "Compiling installer..." -ForegroundColor Cyan
& $iscc "/DMyAppVersion=$version" "/DPublishDir=$publish" $iss
if ($LASTEXITCODE -ne 0) { throw "ISCC failed" }

$setup = Join-Path $repo "agent\installer\Relay-Setup-$version.exe"
Write-Host "Built $setup" -ForegroundColor Green

if ($Run) { Start-Process $setup }
