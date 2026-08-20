<#
  run-agent.ps1 — build and launch the Relay Windows agent.
  Requires the .NET 10 SDK. The agent shows a tray icon; right-click -> "Pairing info…"
  for the host/port/token (and a QR) to pair a phone.
#>
param([switch]$Release)
$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $repo "agent\Relay.Agent\Relay.Agent.csproj"
$config = if ($Release) { "Release" } else { "Debug" }

Write-Host "Building agent ($config)..." -ForegroundColor Cyan
dotnet build $proj -c $config --nologo
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

Write-Host "Launching agent..." -ForegroundColor Green
& (Join-Path $repo "agent\Relay.Agent\bin\$config\net10.0-windows\Relay.Agent.exe")
