# Adds Google Cloud SDK to PATH for the current PowerShell session.
# Install (once): winget install Google.CloudSDK
# Then restart the terminal, or dot-source this file: . .\scripts\use-gcloud.ps1

$candidates = @(
  "$env:ProgramFiles\Google\Cloud SDK\google-cloud-sdk\bin",
  "${env:ProgramFiles(x86)}\Google\Cloud SDK\google-cloud-sdk\bin",
  "$env:LOCALAPPDATA\Google\Cloud SDK\google-cloud-sdk\bin"
)

$bin = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $bin) {
  Write-Error "Google Cloud SDK not found. Install with: winget install Google.CloudSDK"
  exit 1
}

if ($env:PATH -notlike "*$bin*") {
  $env:PATH = "$bin;$env:PATH"
}

Write-Host "gcloud ready: $(Join-Path $bin 'gcloud.cmd')"
