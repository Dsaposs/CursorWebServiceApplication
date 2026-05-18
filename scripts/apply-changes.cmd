@echo off
setlocal
set "APPLY_CHANGES_CMD=%~f0"
powershell -NoProfile -ExecutionPolicy Bypass -Command "$ErrorActionPreference = 'Stop'; $content = Get-Content -LiteralPath $env:APPLY_CHANGES_CMD -Raw; $marker = ':PowerShell'; $index = $content.LastIndexOf($marker); if ($index -lt 0) { throw 'PowerShell payload marker was not found.' }; $script = $content.Substring($index + $marker.Length); Invoke-Expression $script"
exit /b %ERRORLEVEL%

:PowerShell
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptDirectory = Split-Path -Parent $env:APPLY_CHANGES_CMD
$root = (Resolve-Path (Join-Path $scriptDirectory '..')).Path
Set-Location $root

$apiImageName = 'ttrpg-api:local'
$uiImageName = 'ttrpg-ui:local'
$apiContainerName = 'ttrpg-api'
$uiContainerName = 'ttrpg-ui'
$networkName = 'ttrpg-network'
$volumeName = 'ttrpg-data'
$apiHostPort = 5294
$uiHostPort = 3000
$apiContainerPort = 8080
$uiContainerPort = 3000
$sqliteConnection = 'Data Source=/data/ttrpg.db'
$jwtKey = if ($env:TTRPG_JWT_KEY) {
    $env:TTRPG_JWT_KEY
}
else {
    $bytes = New-Object byte[] 48
    $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $rng.GetBytes($bytes)
        [Convert]::ToBase64String($bytes)
    }
    finally {
        $rng.Dispose()
    }
}
$seedAdminPassword = if ($env:TTRPG_SEED_ADMIN_PASSWORD) {
    $env:TTRPG_SEED_ADMIN_PASSWORD
}
else {
    'LocalAdminPassword1!'
}

function Invoke-Native {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [string[]]$Arguments
    )

    $previousErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        & $FilePath @Arguments
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }

    if ($exitCode -ne 0) {
        throw "Command failed with exit code ${exitCode}: $FilePath $($Arguments -join ' ')"
    }
}

function Test-Native {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [string[]]$Arguments
    )

    $previousErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        & $FilePath @Arguments > $null 2> $null
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }

    return $exitCode -eq 0
}

function Assert-Command {
    param([Parameter(Mandatory = $true)][string]$Name)

    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "$Name was not found."
    }
}

function Assert-RequiredFiles {
    $requiredPaths = @(
        'Dockerfile',
        'api\src\NotesApi\NotesApi.csproj',
        'ui\src\Dockerfile',
        'ui\src\package.json'
    )

    foreach ($path in $requiredPaths) {
        if (-not (Test-Path -LiteralPath (Join-Path $root $path))) {
            throw "Required path was not found: $path"
        }
    }
}

function Get-Sha256Hex {
    param([Parameter(Mandatory = $true)][string]$Value)

    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        $hashBytes = $sha256.ComputeHash([Text.Encoding]::UTF8.GetBytes($Value))
        return -join ($hashBytes | ForEach-Object { $_.ToString('x2') })
    }
    finally {
        $sha256.Dispose()
    }
}

function Get-RelativePath {
    param([Parameter(Mandatory = $true)][string]$FullPath)

    $rootPath = $root.TrimEnd('\', '/') + '\'
    $rootUri = [Uri]$rootPath
    $fileUri = [Uri]$FullPath
    return [Uri]::UnescapeDataString($rootUri.MakeRelativeUri($fileUri).ToString()).Replace('/', '\')
}

function Get-StateDirectory {
    $basePath = if ($env:LOCALAPPDATA) {
        Join-Path $env:LOCALAPPDATA 'TtrpgTable\apply-changes'
    }
    else {
        Join-Path $env:TEMP 'TtrpgTable\apply-changes'
    }

    $repoKey = Get-Sha256Hex $root.ToLowerInvariant()
    $stateDirectory = Join-Path $basePath $repoKey
    New-Item -ItemType Directory -Force -Path $stateDirectory | Out-Null
    return $stateDirectory
}

function Get-SourceHash {
    param([Parameter(Mandatory = $true)][string[]]$Paths)

    $excludedDirectories = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($directory in @('bin', 'obj', 'node_modules', '.nuxt', '.output', 'dist', 'coverage')) {
        [void]$excludedDirectories.Add($directory)
    }

    $files = foreach ($path in $Paths) {
        $fullPath = Join-Path $root $path
        if (Test-Path -LiteralPath $fullPath -PathType Leaf) {
            Get-Item -LiteralPath $fullPath
            continue
        }

        Get-ChildItem -LiteralPath $fullPath -Recurse -File | Where-Object {
            $relativePath = Get-RelativePath $_.FullName
            $parts = $relativePath -split '[\\/]'
            $fileName = $_.Name
            $includeFile = $true

            foreach ($part in $parts) {
                if ($excludedDirectories.Contains($part)) {
                    $includeFile = $false
                    break
                }
            }

            if ($fileName -like '*.db' -or $fileName -like '*.db-shm' -or $fileName -like '*.db-wal') {
                $includeFile = $false
            }

            $includeFile
        }
    }

    $lines = @('apply-changes-source-hash-v1')
    $lines += $files |
        Sort-Object FullName |
        ForEach-Object {
            $relativePath = (Get-RelativePath $_.FullName).Replace('\', '/')
            $fileHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash
            "${relativePath}`t${fileHash}"
        }

    $content = $lines -join "`n"
    return Get-Sha256Hex $content
}

function Get-SavedHash {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (Test-Path -LiteralPath $Path) {
        return (Get-Content -LiteralPath $Path -Raw).Trim()
    }

    return ''
}

function Save-Hash {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Hash
    )

    Set-Content -LiteralPath $Path -Value $Hash -NoNewline
}

function Test-DockerImageExists {
    param([Parameter(Mandatory = $true)][string]$ImageName)
    return Test-Native docker @('image', 'inspect', $ImageName)
}

function Test-DockerContainerExists {
    param([Parameter(Mandatory = $true)][string]$ContainerName)
    return Test-Native docker @('container', 'inspect', $ContainerName)
}

function Test-DockerContainerRunning {
    param([Parameter(Mandatory = $true)][string]$ContainerName)

    if (-not (Test-DockerContainerExists $ContainerName)) {
        return $false
    }

    $isRunning = (& docker inspect -f '{{.State.Running}}' $ContainerName 2>$null)
    return $LASTEXITCODE -eq 0 -and $isRunning.Trim() -eq 'true'
}

function Ensure-DockerNetwork {
    if (-not (Test-Native docker @('network', 'inspect', $networkName))) {
        Write-Host "Creating Docker network $networkName..."
        Invoke-Native docker @('network', 'create', $networkName) | Out-Null
    }
}

function Ensure-DockerVolume {
    if (-not (Test-Native docker @('volume', 'inspect', $volumeName))) {
        Write-Host "Creating Docker volume $volumeName..."
        Invoke-Native docker @('volume', 'create', $volumeName) | Out-Null
    }
}

function Assert-PortAvailable {
    param([Parameter(Mandatory = $true)][int]$Port)

    $deadline = (Get-Date).AddSeconds(15)
    do {
        $listener = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
        if (-not $listener) {
            return
        }

        Start-Sleep -Milliseconds 500
    } while ((Get-Date) -lt $deadline)

    $processes = $listener | ForEach-Object {
        $process = Get-Process -Id $_.OwningProcess -ErrorAction SilentlyContinue
        if ($process) {
            "$($process.ProcessName) ($($process.Id))"
        }
        else {
            "process $($_.OwningProcess)"
        }
    }

    if ($processes) {
        throw "Port $Port is already in use by $($processes -join ', ')."
    }
}

function Remove-ContainerIfExists {
    param([Parameter(Mandatory = $true)][string]$ContainerName)

    if (Test-DockerContainerExists $ContainerName) {
        Write-Host "Removing Docker container $ContainerName..."
        Invoke-Native docker @('rm', '-f', $ContainerName) | Out-Null
    }
}

function Start-ExistingContainer {
    param([Parameter(Mandatory = $true)][string]$ContainerName)

    Write-Host "Starting existing Docker container $ContainerName..."
    Invoke-Native docker @('start', $ContainerName) | Out-Null
}

function Build-ApiImage {
    Write-Host "Building Docker image $apiImageName..."
    Invoke-Native docker @('build', '-t', $apiImageName, '.')
}

function Build-UiImage {
    Write-Host "Building Docker image $uiImageName..."
    Invoke-Native docker @('build', '-t', $uiImageName, 'ui\src')
}

function Run-ApiContainer {
    Ensure-DockerNetwork
    Ensure-DockerVolume
    Assert-PortAvailable $apiHostPort

    Write-Host "Starting $apiContainerName at http://localhost:$apiHostPort..."
    Invoke-Native docker @(
        'run',
        '--name', $apiContainerName,
        '-d',
        '--network', $networkName,
        '-p', "${apiHostPort}:${apiContainerPort}",
        '-v', "${volumeName}:/data",
        '-e', "ASPNETCORE_URLS=http://+:$apiContainerPort",
        '-e', 'ASPNETCORE_ENVIRONMENT=Production',
        '-e', "ConnectionStrings__DefaultConnection=$sqliteConnection",
        '-e', "Jwt__Key=$jwtKey",
        '-e', "Seed__AdminPassword=$seedAdminPassword",
        '-e', "Cors__AllowedOrigins__0=http://localhost:$uiHostPort",
        $apiImageName
    ) | Out-Null
}

function Run-UiContainer {
    Ensure-DockerNetwork
    Assert-PortAvailable $uiHostPort

    Write-Host "Starting $uiContainerName at http://localhost:$uiHostPort..."
    Invoke-Native docker @(
        'run',
        '--name', $uiContainerName,
        '-d',
        '--network', $networkName,
        '-p', "${uiHostPort}:${uiContainerPort}",
        '-e', "NUXT_API_BASE_URL=http://${apiContainerName}:${apiContainerPort}",
        '-e', 'HOST=0.0.0.0',
        '-e', "PORT=$uiContainerPort",
        $uiImageName
    ) | Out-Null
}

function Apply-ServiceChanges {
    param(
        [Parameter(Mandatory = $true)][string]$ServiceName,
        [Parameter(Mandatory = $true)][string]$ImageName,
        [Parameter(Mandatory = $true)][string]$ContainerName,
        [Parameter(Mandatory = $true)][string]$CurrentHash,
        [Parameter(Mandatory = $true)][string]$SavedHashPath,
        [Parameter(Mandatory = $true)][scriptblock]$BuildImage,
        [Parameter(Mandatory = $true)][scriptblock]$RunContainer
    )

    $savedHash = Get-SavedHash $SavedHashPath
    $hasSourceChanges = $CurrentHash -ne $savedHash
    $imageExists = Test-DockerImageExists $ImageName
    $containerExists = Test-DockerContainerExists $ContainerName
    $containerRunning = Test-DockerContainerRunning $ContainerName

    if ($hasSourceChanges) {
        Write-Host "$ServiceName changes detected."
    }
    elseif (-not $imageExists) {
        Write-Host "$ServiceName image is missing."
    }

    if ($hasSourceChanges -or -not $imageExists) {
        & $BuildImage
        Remove-ContainerIfExists $ContainerName
        & $RunContainer
        Save-Hash $SavedHashPath $CurrentHash
        return
    }

    if (-not $containerExists) {
        Write-Host "$ServiceName has no container; using the existing image."
        & $RunContainer
        Save-Hash $SavedHashPath $CurrentHash
        return
    }

    if (-not $containerRunning) {
        Start-ExistingContainer $ContainerName
        Save-Hash $SavedHashPath $CurrentHash
        return
    }

    Write-Host "$ServiceName is unchanged and already running."
}

Assert-Command docker
if (-not (Test-Native docker @('info'))) {
    throw 'Docker is installed but the Docker engine is not running. Start Docker Desktop and try again.'
}

Assert-RequiredFiles

$stateDirectory = Get-StateDirectory
$apiHashPath = Join-Path $stateDirectory 'api.sha256'
$uiHashPath = Join-Path $stateDirectory 'ui.sha256'

Write-Host 'Checking source changes...'
$apiHash = Get-SourceHash @('Dockerfile', 'api\src\NotesApi')
$uiHash = Get-SourceHash @('ui\src')

Apply-ServiceChanges `
    -ServiceName 'API' `
    -ImageName $apiImageName `
    -ContainerName $apiContainerName `
    -CurrentHash $apiHash `
    -SavedHashPath $apiHashPath `
    -BuildImage ${function:Build-ApiImage} `
    -RunContainer ${function:Run-ApiContainer}

Apply-ServiceChanges `
    -ServiceName 'UI' `
    -ImageName $uiImageName `
    -ContainerName $uiContainerName `
    -CurrentHash $uiHash `
    -SavedHashPath $uiHashPath `
    -BuildImage ${function:Build-UiImage} `
    -RunContainer ${function:Run-UiContainer}

Write-Host ''
Write-Host "Done. UI: http://localhost:$uiHostPort  API: http://localhost:$apiHostPort"
