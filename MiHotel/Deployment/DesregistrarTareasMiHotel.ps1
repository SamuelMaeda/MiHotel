[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

@('MiHotel - Iniciar aplicación', 'MiHotel - Respaldo diario') | ForEach-Object {
    if (Get-ScheduledTask -TaskName $_ -ErrorAction SilentlyContinue) {
        Unregister-ScheduledTask -TaskName $_ -Confirm:$false
    }
}

Write-Output 'Las tareas programadas de MiHotel fueron retiradas.'
