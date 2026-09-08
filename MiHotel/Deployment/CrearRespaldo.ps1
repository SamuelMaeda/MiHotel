[CmdletBinding()]
param(
    [string]$ConfigPath,
    [string]$OutputDirectory,
    [switch]$IncluirConfiguracion,
    [ValidateRange(1, 3650)]
    [int]$DiasConservacion = 90
)

. (Join-Path $PSScriptRoot 'MiHotel.Common.ps1')

$settings = Get-MiHotelConnectionSettings -ConfigPath $ConfigPath
$dataRoot = Get-MiHotelDataRoot
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $dataRoot 'Backups'
}
$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$sqlPath = Join-Path $OutputDirectory "MiHotel-$timestamp.sql"
$manifestPath = Join-Path $OutputDirectory "MiHotel-$timestamp.json"
$dump = Get-MiHotelMySqlTool -Name 'mysqldump.exe'

$env:MYSQL_PWD = $settings.Password
try {
    & $dump `
        "--host=$($settings.Server)" `
        "--port=$($settings.Port)" `
        "--user=$($settings.User)" `
        '--default-character-set=utf8mb4' `
        '--single-transaction' `
        '--quick' `
        '--skip-add-locks' `
        '--routines' `
        '--triggers' `
        '--events' `
        '--hex-blob' `
        '--no-tablespaces' `
        '--set-gtid-purged=OFF' `
        "--result-file=$sqlPath" `
        $settings.Database

    if ($LASTEXITCODE -ne 0) {
        if (Test-Path -LiteralPath $sqlPath) { Remove-Item -LiteralPath $sqlPath -Force }
        throw "No fue posible crear el respaldo. mysqldump terminó con código $LASTEXITCODE."
    }
}
finally {
    Remove-Item Env:MYSQL_PWD -ErrorAction SilentlyContinue
}

$hash = (Get-FileHash -LiteralPath $sqlPath -Algorithm SHA256).Hash
$manifest = [ordered]@{
    producto = 'MiHotel'
    versionRespaldo = 1
    fecha = (Get-Date).ToString('o')
    baseDatos = $settings.Database
    archivo = [System.IO.Path]::GetFileName($sqlPath)
    sha256 = $hash
}
$manifest | ConvertTo-Json | Set-Content -LiteralPath $manifestPath -Encoding utf8

if ($IncluirConfiguracion) {
    $configCopy = Join-Path $OutputDirectory "MiHotel-$timestamp-config.json"
    Copy-Item -LiteralPath $settings.ConfigPath -Destination $configCopy
}

# Los respaldos diarios más antiguos que el periodo configurado se eliminan
# solamente dentro del directorio de respaldos validado y usando los nombres
# generados por MiHotel. No se recorren otras carpetas.
$limiteConservacion = (Get-Date).AddDays(-$DiasConservacion)
Get-ChildItem -LiteralPath $OutputDirectory -File |
    Where-Object {
        $_.LastWriteTime -lt $limiteConservacion -and
        $_.Name -match '^MiHotel-\d{8}-\d{6}(-config)?\.(sql|json)$'
    } |
    Remove-Item -Force

Write-Output $sqlPath
Write-Output $manifestPath
