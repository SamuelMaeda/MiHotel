[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$InstallationRoot,
    [datetime]$BackupTime = '23:00'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$InstallationRoot = [System.IO.Path]::GetFullPath($InstallationRoot)
$toolsDirectory = Join-Path $InstallationRoot 'Herramientas'
$applicationDirectory = Join-Path $InstallationRoot 'Aplicacion'
$launcher = Join-Path $toolsDirectory 'IniciarMiHotel.ps1'
$backup = Join-Path $toolsDirectory 'CrearRespaldo.ps1'

if (-not (Test-Path -LiteralPath $launcher -PathType Leaf) -or
    -not (Test-Path -LiteralPath $backup -PathType Leaf) -or
    -not (Test-Path -LiteralPath (Join-Path $applicationDirectory 'MiHotel.exe') -PathType Leaf)) {
    throw 'La carpeta indicada no contiene una publicación completa de MiHotel.'
}

$powerShell = (Get-Command powershell.exe).Source
$launcherArguments = "-NoProfile -ExecutionPolicy Bypass -File `"$launcher`" -ApplicationDirectory `"$applicationDirectory`" -NoOpenBrowser"
$backupArguments = "-NoProfile -ExecutionPolicy Bypass -File `"$backup`""

$launchAction = New-ScheduledTaskAction -Execute $powerShell -Argument $launcherArguments
$launchTrigger = New-ScheduledTaskTrigger -AtLogOn
$launchSettings = New-ScheduledTaskSettingsSet -StartWhenAvailable -ExecutionTimeLimit (New-TimeSpan -Minutes 5)
Register-ScheduledTask `
    -TaskName 'MiHotel - Iniciar aplicación' `
    -Description 'Inicia MiHotel en segundo plano al comenzar la sesión de Windows.' `
    -Action $launchAction `
    -Trigger $launchTrigger `
    -Settings $launchSettings `
    -Force | Out-Null

$backupAction = New-ScheduledTaskAction -Execute $powerShell -Argument $backupArguments
$backupTrigger = New-ScheduledTaskTrigger -Daily -At $BackupTime
$backupSettings = New-ScheduledTaskSettingsSet -StartWhenAvailable -ExecutionTimeLimit (New-TimeSpan -Hours 2)
Register-ScheduledTask `
    -TaskName 'MiHotel - Respaldo diario' `
    -Description 'Crea diariamente una copia íntegra de la base de datos de MiHotel.' `
    -Action $backupAction `
    -Trigger $backupTrigger `
    -Settings $backupSettings `
    -Force | Out-Null

Write-Output 'Inicio automático y respaldo diario registrados correctamente.'
