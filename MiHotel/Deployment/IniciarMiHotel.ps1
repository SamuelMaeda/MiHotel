[CmdletBinding()]
param(
    [string]$ApplicationDirectory,
    [string]$Url = 'http://127.0.0.1:5265',
    [switch]$NoOpenBrowser
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($ApplicationDirectory)) {
    $candidate = Join-Path $PSScriptRoot '..\Aplicacion'
    if (Test-Path -LiteralPath $candidate -PathType Container) {
        $ApplicationDirectory = [System.IO.Path]::GetFullPath($candidate)
    }
    else {
        $ApplicationDirectory = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
    }
}

$executable = Join-Path $ApplicationDirectory 'MiHotel.exe'
if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
    throw "No se encontró MiHotel.exe en: $ApplicationDirectory"
}

$healthy = $false
try {
    $response = Invoke-WebRequest -Uri "$Url/estado" -UseBasicParsing -TimeoutSec 2
    $healthy = $response.StatusCode -eq 200
}
catch { }

if (-not $healthy) {
    $previousEnvironment = $env:ASPNETCORE_ENVIRONMENT
    $env:ASPNETCORE_ENVIRONMENT = 'Production'
    try {
        Start-Process -FilePath $executable -WorkingDirectory $ApplicationDirectory -WindowStyle Hidden
    }
    finally {
        if ($null -eq $previousEnvironment) {
            Remove-Item Env:ASPNETCORE_ENVIRONMENT -ErrorAction SilentlyContinue
        }
        else {
            $env:ASPNETCORE_ENVIRONMENT = $previousEnvironment
        }
    }

    for ($attempt = 0; $attempt -lt 20; $attempt++) {
        Start-Sleep -Milliseconds 500
        try {
            $response = Invoke-WebRequest -Uri "$Url/estado" -UseBasicParsing -TimeoutSec 2
            if ($response.StatusCode -eq 200) {
                $healthy = $true
                break
            }
        }
        catch { }
    }
}

if (-not $healthy) {
    throw 'MiHotel no respondió correctamente. Revise los registros en ProgramData\MiHotel\Logs.'
}

if (-not $NoOpenBrowser) {
    Start-Process $Url
}

Write-Output 'MiHotel está disponible.'
