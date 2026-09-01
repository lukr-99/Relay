<#
  publish-release.ps1 - build the agent installer and publish it as a GitHub Release.

  The agent's auto-updater reads the repo's latest *public* Release, so this is how a new version
  reaches installed copies: build the versioned installer, then create a Release (tag agent-vX.Y.Z)
  with the installer attached. The version comes from the agent csproj <Version>.

  Requires: .NET 10 SDK, Inno Setup 6, and the gh CLI authenticated (as the repo owner).
  NOTE: the auto-updater only works if the repo's Releases are public
        (make the repo public with:  gh repo edit lukr-99/Relay --visibility public).

  Usage:
    .\tools\publish-release.ps1
    .\tools\publish-release.ps1 -Notes "What changed in this release"
#>
param([string]$Notes = "")
$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $repo "agent\Relay.Agent\Relay.Agent.csproj"

[xml]$csproj = Get-Content $proj
$version = (@($csproj.Project.PropertyGroup.VersionPrefix) + @($csproj.Project.PropertyGroup.Version) |
    Where-Object { $_ } | Select-Object -First 1)
if (-not $version) { throw "No <VersionPrefix> or <Version> found in $proj" }
$tag = "agent-v$version"
Write-Host "Publishing Relay $version (tag $tag)" -ForegroundColor Cyan

# 1. Build the installer.
& (Join-Path $PSScriptRoot "build-installer.ps1")
$setup = Join-Path $repo "agent\installer\Relay-Setup-$version.exe"
if (-not (Test-Path $setup)) { throw "Installer not found at $setup" }

# 2. Create the GitHub Release with the installer attached.
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) { throw "gh CLI not found (needed to publish the release)." }
if (-not $Notes) { $Notes = "Relay agent $version." }

Write-Host "Creating GitHub Release..." -ForegroundColor Cyan
gh release create $tag $setup --repo lukr-99/Relay --title "Relay $version" --notes $Notes
if ($LASTEXITCODE -ne 0) {
    Write-Host "gh release create failed (tag may already exist). To attach the installer to an existing release:" -ForegroundColor Yellow
    Write-Host "  gh release upload $tag `"$setup`" --repo lukr-99/Relay --clobber" -ForegroundColor Yellow
    throw "release publish failed"
}
Write-Host "Published $tag with $([System.IO.Path]::GetFileName($setup))." -ForegroundColor Green

# The repo mixes agent (agent-v*) and app (app-v*) releases. The agent's updater reads
# /releases/latest, which returns the single most-recent release across ALL tags — so a newer app
# release would hide the agent one. Pin this agent release as "latest" so the agent updater finds it.
gh release edit $tag --repo lukr-99/Relay --latest | Out-Null
if ($LASTEXITCODE -eq 0) { Write-Host "Pinned $tag as the repo's latest release." -ForegroundColor Green }
