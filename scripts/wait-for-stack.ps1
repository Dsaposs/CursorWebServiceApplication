param(
  [string]$ApiUrl = $(if ($env:E2E_API_URL) { $env:E2E_API_URL } else { "http://localhost:5294" }),
  [string]$UiUrl = $(if ($env:E2E_BASE_URL) { $env:E2E_BASE_URL } else { "http://localhost:3000" }),
  [string]$MobileUrl = $(if ($env:E2E_MOBILE_BASE_URL) { $env:E2E_MOBILE_BASE_URL } else { "http://localhost:3001" }),
  [int]$TimeoutSeconds = $(if ($env:E2E_WAIT_TIMEOUT_MS) { [int]([double]$env:E2E_WAIT_TIMEOUT_MS / 1000) } else { 180 })
)

$deadline = (Get-Date).AddSeconds($TimeoutSeconds)

function Wait-ForEndpoint {
  param(
    [string]$Url,
    [string]$Label
  )

  while ((Get-Date) -lt $deadline) {
    try {
      $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 5
      if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 400) {
        Write-Host "$Label is ready at $Url"
        return
      }
      $lastError = "$Label returned $($response.StatusCode)"
    }
    catch {
      $lastError = $_.Exception.Message
    }

    Start-Sleep -Seconds 2
  }

  throw "Timed out waiting for $Label at $Url. Last error: $lastError"
}

Write-Host "Waiting for API health at $ApiUrl/health ..."
Wait-ForEndpoint -Url "$ApiUrl/health" -Label "API"

Write-Host "Waiting for UI at $UiUrl ..."
Wait-ForEndpoint -Url $UiUrl -Label "UI"

Write-Host "Waiting for Mobile at $MobileUrl ..."
Wait-ForEndpoint -Url $MobileUrl -Label "Mobile"

Write-Host "Stack is ready."
