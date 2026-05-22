# Bootstrap GCP resources for GitHub Actions CI/CD (Artifact Registry + IAM).
# Requires: gcloud CLI installed and authenticated (gcloud auth login).
#
# Usage:
#   .\scripts\gcp-setup.ps1 -ProjectId keen-truth-463618-a0 -Region us-central1 -Repository ttrpg `
#     -ServiceAccount dsaposs@keen-truth-463618-a0.iam.gserviceaccount.com

param(
  [Parameter(Mandatory = $true)]
  [string]$ProjectId,

  [Parameter(Mandatory = $true)]
  [string]$Region,

  [Parameter(Mandatory = $true)]
  [string]$Repository,

  [Parameter(Mandatory = $true)]
  [string]$ServiceAccount
)

$ErrorActionPreference = "Stop"

function Resolve-Gcloud {
  $candidates = @(
    "$env:LOCALAPPDATA\Google\Cloud SDK\google-cloud-sdk\bin\gcloud.cmd",
    "$env:ProgramFiles\Google\Cloud SDK\google-cloud-sdk\bin\gcloud.cmd",
    "${env:ProgramFiles(x86)}\Google\Cloud SDK\google-cloud-sdk\bin\gcloud.cmd"
  )

  foreach ($path in $candidates) {
    if (Test-Path $path) { return $path }
  }

  throw "gcloud not found. Install: winget install Google.CloudSDK, then restart the terminal or run scripts\gcloud.cmd"
}

function Invoke-Gcloud {
  param(
    [Parameter(ValueFromRemainingArguments = $true)][string[]]$Args,
    [switch]$AllowFailure
  )
  Write-Host "> gcloud $($Args -join ' ')"
  $previous = $ErrorActionPreference
  $ErrorActionPreference = "Continue"
  try {
    & $gcloud @Args 2>&1 | Out-Host
    if ($LASTEXITCODE -ne 0 -and -not $AllowFailure) {
      throw "gcloud failed (exit $LASTEXITCODE)"
    }
    return ($LASTEXITCODE -eq 0)
  } finally {
    $ErrorActionPreference = $previous
  }
}

$gcloud = Resolve-Gcloud

Write-Host "Setting active project to $ProjectId"
Invoke-Gcloud config set project $ProjectId

Write-Host "Enabling required APIs..."
Invoke-Gcloud services enable artifactregistry.googleapis.com run.googleapis.com --project=$ProjectId

Write-Host "Ensuring Artifact Registry repository '$Repository' exists in $Region..."
if (Invoke-Gcloud artifacts repositories describe $Repository --location=$Region --project=$ProjectId -AllowFailure) {
  Write-Host "Repository already exists."
} else {
  Invoke-Gcloud artifacts repositories create $Repository `
    --repository-format=docker `
    --location=$Region `
    --project=$ProjectId `
    --description="TTRPG Table Docker images"
}

Write-Host "Granting Artifact Registry writer to $ServiceAccount..."
Invoke-Gcloud artifacts repositories add-iam-policy-binding $Repository `
  --location=$Region `
  --project=$ProjectId `
  --member="serviceAccount:$ServiceAccount" `
  --role="roles/artifactregistry.writer"

Write-Host "Granting Cloud Run deploy roles..."
Invoke-Gcloud projects add-iam-policy-binding $ProjectId `
  --member="serviceAccount:$ServiceAccount" `
  --role="roles/run.admin"

Invoke-Gcloud projects add-iam-policy-binding $ProjectId `
  --member="serviceAccount:$ServiceAccount" `
  --role="roles/iam.serviceAccountUser"

Write-Host ""
Write-Host "Done. GitHub secrets should match:"
Write-Host "  GCP_PROJECT_ID=$ProjectId"
Write-Host "  GCP_REGION=$Region"
Write-Host "  ARTIFACT_REGISTRY_REPO=$Repository"
Write-Host ""
Write-Host "Image paths:"
Write-Host ('  {0}-docker.pkg.dev/{1}/{2}/api:SHA' -f $Region, $ProjectId, $Repository)
Write-Host ('  {0}-docker.pkg.dev/{1}/{2}/ui:SHA' -f $Region, $ProjectId, $Repository)
