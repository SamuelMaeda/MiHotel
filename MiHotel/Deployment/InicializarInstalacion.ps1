[CmdletBinding()]
param(
    [string]$Server = 'localhost',
    [ValidateRange(1, 65535)]
    [int]$Port = 3306,
    [string]$Database = 'hotel',
    [string]$ApplicationUser = 'mihotel_app',
    [string]$MySqlAdministrator = 'root',
    [Security.SecureString]$MySqlAdministratorPassword,
    [string]$HotelAdministratorName = 'Alejandra',
    [string]$HotelAdministratorEmail = 'alejandra@mihotel.local',
    [string]$HotelAdministratorPhone = '',
    [Security.SecureString]$HotelAdministratorPassword,
    [string]$DataRoot
)

. (Join-Path $PSScriptRoot 'MiHotel.Common.ps1')

if ($Database -notmatch '^[A-Za-z0-9_]+$') {
    throw 'El nombre de la base de datos solo puede contener letras, números y guion bajo.'
}
if ($ApplicationUser -notmatch '^[A-Za-z0-9_]+$') {
    throw 'El usuario de aplicación solo puede contener letras, números y guion bajo.'
}

if ($null -eq $MySqlAdministratorPassword) {
    $MySqlAdministratorPassword = Read-Host 'Contraseña del administrador de MySQL' -AsSecureString
}
if ($HotelAdministratorEmail -notmatch '^[^@\s]+@[^@\s]+\.[^@\s]+$') {
    throw 'El correo del administrador no tiene un formato válido.'
}
if (-not [string]::IsNullOrWhiteSpace($HotelAdministratorPhone) -and $HotelAdministratorPhone -notmatch '^\d{4}\s?\d{4}$') {
    throw 'El teléfono debe contener 8 dígitos.'
}
$storedPassword = 'PBKDF2-SHA256$210000$qCDv211ceGiB4uGeIvTBww==$hKaggFY4cYMMnw0M0kYGWAI4xVEQ6GlauLQC06Q5AEA='
if ($null -ne $HotelAdministratorPassword) {
    $hotelPasswordPlain = ConvertTo-MiHotelPlainText -SecureValue $HotelAdministratorPassword
    if ($hotelPasswordPlain.Length -lt 6) {
        throw 'La contraseña de MiHotel debe tener al menos 6 caracteres.'
    }
    $salt = [byte[]]::new(16)
    $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
    try { $rng.GetBytes($salt) } finally { $rng.Dispose() }
    $derive = [Security.Cryptography.Rfc2898DeriveBytes]::new(
        $hotelPasswordPlain,
        $salt,
        210000,
        [Security.Cryptography.HashAlgorithmName]::SHA256)
    try { $passwordHash = $derive.GetBytes(32) } finally { $derive.Dispose() }
    $storedPassword = 'PBKDF2-SHA256$210000$' + [Convert]::ToBase64String($salt) + '$' + [Convert]::ToBase64String($passwordHash)
}

$databaseAssets = Join-Path $PSScriptRoot 'Database\Install'
if (-not (Test-Path -LiteralPath $databaseAssets -PathType Container)) {
    $databaseAssets = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\Database\Install'))
}
$schemaPath = Join-Path $databaseAssets 'schema.sql'
$seedPath = Join-Path $databaseAssets 'seed.sql'
if (-not (Test-Path -LiteralPath $schemaPath) -or -not (Test-Path -LiteralPath $seedPath)) {
    throw 'No se encontraron schema.sql y seed.sql para inicializar MiHotel.'
}

if ([string]::IsNullOrWhiteSpace($DataRoot)) {
    $DataRoot = Get-MiHotelDataRoot
}
$DataRoot = [System.IO.Path]::GetFullPath($DataRoot)
$configDirectory = Join-Path $DataRoot 'Config'
New-Item -ItemType Directory -Path $configDirectory -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $DataRoot 'Logs') -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $DataRoot 'Backups') -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $DataRoot 'Keys') -Force | Out-Null

$mysql = Get-MiHotelMySqlTool -Name 'mysql.exe'
$mysqlAdminPassword = ConvertTo-MiHotelPlainText -SecureValue $MySqlAdministratorPassword
$randomBytes = [byte[]]::new(32)
$rng = [Security.Cryptography.RandomNumberGenerator]::Create()
try { $rng.GetBytes($randomBytes) } finally { $rng.Dispose() }
$applicationPassword = [Convert]::ToBase64String($randomBytes).TrimEnd('=').Replace('+', 'A').Replace('/', 'B')

$env:MYSQL_PWD = $mysqlAdminPassword
try {
    $tableCount = & $mysql `
        "--host=$Server" `
        "--port=$Port" `
        "--user=$MySqlAdministrator" `
        '--batch' `
        '--skip-column-names' `
        "--execute=SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = '$Database';"
    if ($LASTEXITCODE -ne 0) { throw 'No fue posible conectar con MySQL.' }
    if ([int]$tableCount -gt 0) {
        throw "La base de datos '$Database' ya contiene tablas. La inicialización fue cancelada para proteger sus datos."
    }

    $databaseSql = "CREATE DATABASE IF NOT EXISTS ``$Database`` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;"
    $databaseSql += " CREATE USER IF NOT EXISTS '$ApplicationUser'@'localhost' IDENTIFIED BY '$applicationPassword';"
    $databaseSql += " ALTER USER '$ApplicationUser'@'localhost' IDENTIFIED BY '$applicationPassword';"
    $databaseSql += " GRANT SELECT, INSERT, UPDATE, DELETE, CREATE, ALTER, INDEX, DROP, REFERENCES, EXECUTE, CREATE ROUTINE, ALTER ROUTINE, EVENT, TRIGGER ON ``$Database``.* TO '$ApplicationUser'@'localhost'; FLUSH PRIVILEGES;"
    & $mysql "--host=$Server" "--port=$Port" "--user=$MySqlAdministrator" "--execute=$databaseSql"
    if ($LASTEXITCODE -ne 0) { throw 'No fue posible crear la base de datos y su usuario restringido.' }
}
finally {
    Remove-Item Env:MYSQL_PWD -ErrorAction SilentlyContinue
    $mysqlAdminPassword = $null
}

$env:MYSQL_PWD = $applicationPassword
try {
    $applicationSettings = [pscustomobject]@{
        Server = $Server
        Port = $Port
        Database = $Database
        User = $ApplicationUser
        Password = $applicationPassword
    }
    $schemaResult = Invoke-MiHotelSqlFile -Settings $applicationSettings -SqlPath $schemaPath
    if ($schemaResult.ExitCode -ne 0) {
        throw "No fue posible crear la estructura inicial de MiHotel. $($schemaResult.StandardError)"
    }
    $seedResult = Invoke-MiHotelSqlFile -Settings $applicationSettings -SqlPath $seedPath
    if ($seedResult.ExitCode -ne 0) {
        throw "No fue posible cargar los catálogos iniciales de MiHotel. $($seedResult.StandardError)"
    }

    $nameSql = ConvertTo-MiHotelSqlLiteral -Value $HotelAdministratorName.Trim()
    $emailSql = ConvertTo-MiHotelSqlLiteral -Value $HotelAdministratorEmail.Trim().ToLowerInvariant()
    $phoneSql = if ([string]::IsNullOrWhiteSpace($HotelAdministratorPhone)) { 'NULL' } else { ConvertTo-MiHotelSqlLiteral -Value ($HotelAdministratorPhone.Replace(' ', '')) }
    $passwordSql = ConvertTo-MiHotelSqlLiteral -Value $storedPassword
    $administratorSql = "INSERT INTO usuario (id_rol, nombre_usuario, correo, telefono, clave, estado) SELECT id_rol, $nameSql, $emailSql, $phoneSql, $passwordSql, 'activo' FROM rol WHERE LOWER(nombre_rol) = 'admin' LIMIT 1;"
    & $mysql "--host=$Server" "--port=$Port" "--user=$ApplicationUser" "--database=$Database" "--execute=$administratorSql"
    if ($LASTEXITCODE -ne 0) { throw 'No fue posible crear el primer administrador de MiHotel.' }

    $receptionDayHash = ConvertTo-MiHotelSqlLiteral -Value 'PBKDF2-SHA256$210000$nl2dn4BZTF8xBPurhWDnZQ==$0Yn08D6m5xpF8j87Bp6wPRsgi1emu3LH74TwqB/FWoQ='
    $receptionNightHash = ConvertTo-MiHotelSqlLiteral -Value 'PBKDF2-SHA256$210000$qkVlkf2cuh20KU+nM/eLtg==$jborFD/lGFQyK+1pKrsrAy86MJ3q7YQwae62k3kCYBY='
    $receptionSql = "INSERT INTO usuario (id_rol, nombre_usuario, correo, telefono, clave, estado) SELECT id_rol, 'recepciondia', 'recepciondia@mihotel.local', NULL, $receptionDayHash, 'activo' FROM rol WHERE LOWER(nombre_rol) = 'recepcionista' LIMIT 1; INSERT INTO usuario (id_rol, nombre_usuario, correo, telefono, clave, estado) SELECT id_rol, 'recepcionnoche', 'recepcionnoche@mihotel.local', NULL, $receptionNightHash, 'activo' FROM rol WHERE LOWER(nombre_rol) = 'recepcionista' LIMIT 1;"
    & $mysql "--host=$Server" "--port=$Port" "--user=$ApplicationUser" "--database=$Database" "--execute=$receptionSql"
    if ($LASTEXITCODE -ne 0) { throw 'No fue posible crear los usuarios iniciales de recepción.' }

    $databaseConfig = [ordered]@{
        ConnectionStrings = [ordered]@{
            ConexionHotel = "Server=$Server;Port=$Port;Database=$Database;User ID=$ApplicationUser;Password=$applicationPassword;SslMode=Disabled;AllowPublicKeyRetrieval=True"
        }
    }
    $databaseJson = $databaseConfig | ConvertTo-Json -Depth 4
    [System.IO.File]::WriteAllText(
        (Join-Path $configDirectory 'database.json'),
        $databaseJson,
        [System.Text.UTF8Encoding]::new($false))

    $companyTemplate = Join-Path $PSScriptRoot 'Templates\config.json'
    if (Test-Path -LiteralPath $companyTemplate -PathType Leaf) {
        Copy-Item -LiteralPath $companyTemplate -Destination (Join-Path $configDirectory 'config.json') -Force
    }

    if ([Environment]::OSVersion.Platform -eq [PlatformID]::Win32NT) {
        $databaseConfigPath = Join-Path $configDirectory 'database.json'
        $currentSid = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
        & icacls.exe $databaseConfigPath `
            '/inheritance:r' `
            '/grant:r' `
            "*$currentSid`:F" `
            '*S-1-5-18:F' `
            '*S-1-5-32-544:F' | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw 'No fue posible proteger el archivo de conexión de MiHotel.'
        }
    }
}
finally {
    Remove-Item Env:MYSQL_PWD -ErrorAction SilentlyContinue
    $applicationPassword = $null
    $hotelPasswordPlain = $null
}

Write-Output "MiHotel fue inicializado correctamente en $DataRoot"
