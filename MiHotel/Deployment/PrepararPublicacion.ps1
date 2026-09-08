[CmdletBinding()]
param(
    [ValidateSet('win-x64', 'win-arm64')]
    [string]$Runtime = 'win-x64',
    [switch]$FrameworkDependent
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$projectFile = Join-Path $projectRoot 'MiHotel.csproj'
$outputRoot = Join-Path $projectRoot 'Publicacion\MiHotel'
$applicationOutput = Join-Path $outputRoot 'Aplicacion'
$toolsOutput = Join-Path $outputRoot 'Herramientas'

if (Test-Path -LiteralPath $outputRoot) {
    Remove-Item -LiteralPath $outputRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $applicationOutput -Force | Out-Null
New-Item -ItemType Directory -Path $toolsOutput -Force | Out-Null

$selfContained = if ($FrameworkDependent) { 'false' } else { 'true' }
dotnet publish $projectFile `
    --configuration Release `
    --runtime $Runtime `
    --self-contained $selfContained `
    --output $applicationOutput `
    -p:PublishSingleFile=false `
    -p:DebugType=None `
    -p:DebugSymbols=false

if ($LASTEXITCODE -ne 0) {
    throw "La publicación terminó con código $LASTEXITCODE."
}

# Los símbolos nativos de diagnóstico de SkiaSharp superan los 80 MB y no son
# necesarios para operar el hotel. Los registros de la aplicación se conservan.
Get-ChildItem -LiteralPath $applicationOutput -Filter '*.pdb' -Recurse -File |
    Remove-Item -Force

Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'MiHotel.Common.ps1') -Destination $toolsOutput
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'InicializarInstalacion.ps1') -Destination $toolsOutput
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'CrearRespaldo.ps1') -Destination $toolsOutput
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'RestaurarRespaldo.ps1') -Destination $toolsOutput
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'IniciarMiHotel.ps1') -Destination $toolsOutput
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'RegistrarTareasMiHotel.ps1') -Destination $toolsOutput
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'DesregistrarTareasMiHotel.ps1') -Destination $toolsOutput
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Templates') -Destination $toolsOutput -Recurse
New-Item -ItemType Directory -Path (Join-Path $toolsOutput 'Database') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot 'Database\Install') -Destination (Join-Path $toolsOutput 'Database\Install') -Recurse
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'PREPARACION_INSTALACION.md') -Destination $outputRoot

Write-Output "Publicación preparada en: $outputRoot"
