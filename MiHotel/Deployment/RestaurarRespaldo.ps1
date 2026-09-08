[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$BackupPath,
    [string]$ConfigPath,
    [switch]$ConfirmarRestauracion
)

. (Join-Path $PSScriptRoot 'MiHotel.Common.ps1')

if (-not $ConfirmarRestauracion) {
    throw 'La restauración reemplaza la información actual. Ejecútela con -ConfirmarRestauracion.'
}

$BackupPath = [System.IO.Path]::GetFullPath($BackupPath)
if (-not (Test-Path -LiteralPath $BackupPath -PathType Leaf)) {
    throw "No se encontró el respaldo: $BackupPath"
}

$manifestPath = [System.IO.Path]::ChangeExtension($BackupPath, '.json')
if (Test-Path -LiteralPath $manifestPath -PathType Leaf) {
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    $actualHash = (Get-FileHash -LiteralPath $BackupPath -Algorithm SHA256).Hash
    if ($actualHash -ne $manifest.sha256) {
        throw 'El respaldo no superó la validación de integridad SHA-256.'
    }
}

# Antes de sustituir datos se conserva automáticamente el estado anterior.
& (Join-Path $PSScriptRoot 'CrearRespaldo.ps1') -ConfigPath $ConfigPath | Out-Null

$settings = Get-MiHotelConnectionSettings -ConfigPath $ConfigPath
$result = Invoke-MiHotelSqlFile -Settings $settings -SqlPath $BackupPath
if ($result.ExitCode -ne 0) {
    throw "No fue posible restaurar el respaldo. $($result.StandardError)"
}

Write-Output 'Respaldo restaurado correctamente.'
