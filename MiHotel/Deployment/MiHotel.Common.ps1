Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-MiHotelDataRoot {
    if (-not [string]::IsNullOrWhiteSpace($env:MIHOTEL_DATA_DIR)) {
        return [System.IO.Path]::GetFullPath($env:MIHOTEL_DATA_DIR)
    }

    return Join-Path $env:ProgramData 'MiHotel'
}

function Get-MiHotelDatabaseConfigPath {
    param([string]$ConfigPath)

    if (-not [string]::IsNullOrWhiteSpace($ConfigPath)) {
        return [System.IO.Path]::GetFullPath($ConfigPath)
    }

    return Join-Path (Get-MiHotelDataRoot) 'Config\database.json'
}

function Get-MiHotelMySqlTool {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet('mysql.exe', 'mysqldump.exe')]
        [string]$Name
    )

    $fromPath = Get-Command $Name -ErrorAction SilentlyContinue
    if ($null -ne $fromPath) {
        return $fromPath.Source
    }

    $candidates = @(
        (Join-Path $env:ProgramFiles "MySQL\MySQL Server 8.4\bin\$Name"),
        (Join-Path $env:ProgramFiles "MySQL\MySQL Server 8.0\bin\$Name")
    )

    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return $candidate
        }
    }

    throw "No se encontró $Name. Instale MySQL Server o indique su carpeta bin en PATH."
}

function Get-MiHotelConnectionSettings {
    param([string]$ConfigPath)

    $resolvedConfig = Get-MiHotelDatabaseConfigPath -ConfigPath $ConfigPath
    if (-not (Test-Path -LiteralPath $resolvedConfig -PathType Leaf)) {
        throw "No se encontró la configuración de base de datos en: $resolvedConfig"
    }

    $json = Get-Content -LiteralPath $resolvedConfig -Raw | ConvertFrom-Json
    $connectionString = $json.ConnectionStrings.ConexionHotel
    if ([string]::IsNullOrWhiteSpace($connectionString)) {
        throw 'La configuración no contiene ConnectionStrings:ConexionHotel.'
    }

    $builder = [System.Data.Common.DbConnectionStringBuilder]::new()
    # PowerShell interpreta de forma ambigua la asignación directa a esta
    # propiedad; el setter explícito garantiza que se analicen todos los pares.
    $builder.set_ConnectionString([string]$connectionString)

    function Read-ConnectionValue {
        param([string[]]$Names, [string]$DefaultValue = '')
        foreach ($name in $Names) {
            if ($builder.ContainsKey($name)) { return [string]$builder[$name] }
        }
        return $DefaultValue
    }

    $server = Read-ConnectionValue -Names @('Server', 'Host', 'Data Source') -DefaultValue 'localhost'
    $port = Read-ConnectionValue -Names @('Port') -DefaultValue '3306'
    $database = Read-ConnectionValue -Names @('Database', 'Initial Catalog')
    $user = Read-ConnectionValue -Names @('User ID', 'Uid', 'User')
    $password = Read-ConnectionValue -Names @('Password', 'Pwd')

    if ([string]::IsNullOrWhiteSpace($database) -or [string]::IsNullOrWhiteSpace($user)) {
        throw 'La cadena de conexión no contiene base de datos o usuario.'
    }

    [pscustomobject]@{
        ConfigPath = $resolvedConfig
        Server = $server
        Port = $port
        Database = $database
        User = $user
        Password = $password
    }
}

function ConvertTo-MiHotelPlainText {
    param([Parameter(Mandatory = $true)][Security.SecureString]$SecureValue)

    $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($SecureValue)
    try {
        return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
    }
    finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer)
    }
}

function ConvertTo-MiHotelSqlLiteral {
    param([AllowEmptyString()][string]$Value)
    return "'" + $Value.Replace("'", "''") + "'"
}

function Invoke-MiHotelSqlFile {
    param(
        [Parameter(Mandatory = $true)]$Settings,
        [Parameter(Mandatory = $true)][string]$SqlPath
    )

    $SqlPath = [System.IO.Path]::GetFullPath($SqlPath)
    if (-not (Test-Path -LiteralPath $SqlPath -PathType Leaf)) {
        throw "No se encontró el archivo SQL: $SqlPath"
    }

    $mysql = Get-MiHotelMySqlTool -Name 'mysql.exe'
    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $mysql
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardInput = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.Arguments = "--host=$($Settings.Server) --port=$($Settings.Port) --user=$($Settings.User) --database=$($Settings.Database) --default-character-set=utf8mb4"
    $startInfo.EnvironmentVariables['MYSQL_PWD'] = [string]$Settings.Password

    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    if (-not $process.Start()) { throw 'No fue posible iniciar mysql.' }

    $standardOutput = $process.StandardOutput.ReadToEndAsync()
    $standardError = $process.StandardError.ReadToEndAsync()
    $input = [IO.File]::OpenRead($SqlPath)
    try {
        $input.CopyTo($process.StandardInput.BaseStream)
        $process.StandardInput.Close()
        $process.WaitForExit()
    }
    finally {
        $input.Dispose()
    }

    [pscustomobject]@{
        ExitCode = $process.ExitCode
        StandardOutput = $standardOutput.Result
        StandardError = $standardError.Result
    }
    $process.Dispose()
}
