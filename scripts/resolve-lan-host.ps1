# Detects the host machine's LAN IPv4 address and writes LAN_HOST to the repo .env file.
# Used by start-app.cmd so docker compose can expose services to other devices on the network.

param(
    [switch]$PrintUrls
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$envFile = Join-Path $repoRoot '.env'

function Get-LanHostAddress {
    $addresses = @(Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
        Where-Object {
            $_.IPAddress -notlike '127.*' -and
            $_.IPAddress -notlike '169.254.*' -and
            $_.PrefixOrigin -ne 'WellKnown'
        } |
        Sort-Object InterfaceMetric, SkipAsSource)

    foreach ($address in $addresses) {
        $ip = $address.IPAddress
        if ($ip -match '^(10\.|192\.168\.|172\.(1[6-9]|2[0-9]|3[0-1])\.)') {
            return $ip
        }
    }

    return ($addresses | Select-Object -First 1).IPAddress
}

function Set-EnvLanHost {
    param(
        [string]$Path,
        [string]$LanHost
    )

    $lines = if (Test-Path $Path) {
        Get-Content -Path $Path
    } else {
        @()
    }

    $updated = $false
    $newLines = foreach ($line in $lines) {
        if ($line -match '^\s*LAN_HOST=') {
            $updated = $true
            "LAN_HOST=$LanHost"
        } else {
            $line
        }
    }

    if (-not $updated) {
        if ($newLines.Count -gt 0 -and $newLines[-1] -ne '') {
            $newLines += ''
        }

        $newLines += '# Host LAN IP for other devices on your network (auto-detected by resolve-lan-host.ps1)'
        $newLines += "LAN_HOST=$LanHost"
    }

    Set-Content -Path $Path -Value $newLines -Encoding utf8
}

$lanHost = Get-LanHostAddress
if ([string]::IsNullOrWhiteSpace($lanHost)) {
    Write-Warning 'Could not detect a LAN IPv4 address; falling back to 127.0.0.1.'
    $lanHost = '127.0.0.1'
}

if (-not (Test-Path $envFile)) {
    Write-Error ".env file not found at $envFile. Copy .env.example to .env first."
    exit 1
}

Set-EnvLanHost -Path $envFile -LanHost $lanHost

Write-Host "LAN_HOST set to $lanHost"

if ($PrintUrls) {
    Write-Host ''
    Write-Host '  Other devices on your network can use:'
    Write-Host "    UI:     http://${lanHost}:3000"
    Write-Host "    Mobile: http://${lanHost}:3001"
    Write-Host "    API:    http://${lanHost}:5294"
    Write-Host ''
    Write-Host '  If connections fail, allow inbound TCP 3000, 3001, and 5294 in Windows Firewall.'
}

Write-Output $lanHost
